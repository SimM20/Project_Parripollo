using System.Collections;
using UnityEngine;

/// <summary>
/// Base de los paneles deslizantes de la vista Parrilla (stock a la izquierda,
/// items de armado a la derecha). Resuelve el deslizamiento, el gateo por vista y
/// el area de cancelacion de drops; cada panel concreto define su contenido.
///
/// Los nombres de los campos serializados se mantienen identicos a los que tenia
/// StockPanelController, asi que los prefabs y overrides de escena existentes
/// siguen resolviendo sin recablear.
/// </summary>
public abstract class SlidingPanel : MonoBehaviour
{
    [Header("Panel References")]
    [SerializeField] protected Transform slidingRoot;
    [Tooltip("Fondo del panel. Un drop soltado sobre el se cancela sin tocar el estado del juego.")]
    [SerializeField] protected SpriteRenderer panelBackground;

    [Header("Systems")]
    [SerializeField] protected ViewManager viewManager;

    [Header("Slide Animation")]
    [SerializeField] private float openLocalX = 0f;
    [SerializeField] private float closedLocalX = -5f;
    [SerializeField] [Min(0f)] private float slideDuration = 0.2f;

    /// <summary>True si el panel esta desplegado (o yendo hacia desplegado).</summary>
    public bool IsOpen { get; private set; }

    /// <summary>True mientras la corrutina de deslizamiento esta en curso.</summary>
    public bool IsAnimating => slideRoutine != null;

    /// <summary>True solo si el contenido puede iniciar un arrastre en este momento.</summary>
    public bool CanBeginDrag => IsOpen && slideRoutine == null;

    private Coroutine slideRoutine;

    // ── Ciclo de vida ───────────────────────────────────────────────────────

    protected virtual void OnEnable()
    {
        BindViewManager();
    }

    protected virtual void Start()
    {
        ValidateReferences();

        // ViewManager.Show() solo dispara OnViewChanged cuando cambia de vista, asi que
        // el Show(Grill) inicial nunca nos llega: hay que auto-inicializarse aca.
        if (slidingRoot != null)
            slidingRoot.gameObject.SetActive(true);

        OnPanelStarted();
        Close(true);
    }

    protected virtual void OnDisable()
    {
        if (viewManager != null)
            viewManager.OnViewChanged -= HandleViewChanged;

        OnPanelClosing();

        if (slideRoutine != null)
        {
            StopCoroutine(slideRoutine);
            slideRoutine = null;
        }
    }

    // ── Hooks para los paneles concretos ────────────────────────────────────

    /// <summary>Se llama al final del Start, antes del cierre inicial. Ideal para poblar el contenido.</summary>
    protected virtual void OnPanelStarted() { }

    /// <summary>Se llama al entrar a la vista Parrilla, antes de cerrar el panel. Ideal para repintar.</summary>
    protected virtual void OnEnteredGrillView() { }

    /// <summary>Se llama antes de cada cierre y al desactivarse. Debe cancelar cualquier arrastre en curso.</summary>
    protected virtual void OnPanelClosing() { }

    /// <summary>Se llama despues de iniciar una apertura aceptada.</summary>
    protected virtual void OnPanelOpened() { }

    /// <summary>Gate de apertura. Devolver false rechaza el Open() (lo usa el tutorial).</summary>
    protected virtual bool CanOpen() => true;

    /// <summary>Validacion de referencias de inspector. El hijo debe llamar a base.ValidateReferences().</summary>
    protected virtual void ValidateReferences()
    {
        if (slidingRoot == null)
            Debug.LogWarning("[" + GetType().Name + "] Falta la referencia 'slidingRoot'. El panel no puede deslizarse.");

        if (viewManager == null)
            Debug.LogWarning("[" + GetType().Name + "] Falta la referencia 'viewManager'. Usa SetViewManager() desde afuera del prefab.");

        if (panelBackground == null)
            Debug.LogWarning("[" + GetType().Name + "] Falta la referencia 'panelBackground'. Soltar un item sobre el panel no cancelara el arrastre.");
    }

    // ── Referencias externas ────────────────────────────────────────────────

    /// <summary>
    /// Asigna el ViewManager desde afuera del prefab (vive fuera de GrillView).
    /// Rebinde la suscripcion al evento de cambio de vista.
    /// </summary>
    public void SetViewManager(ViewManager manager)
    {
        if (viewManager == manager)
            return;

        if (viewManager != null)
            viewManager.OnViewChanged -= HandleViewChanged;

        viewManager = manager;

        if (isActiveAndEnabled)
            BindViewManager();
    }

    private void BindViewManager()
    {
        if (viewManager == null)
            return;

        viewManager.OnViewChanged -= HandleViewChanged;
        viewManager.OnViewChanged += HandleViewChanged;
    }

    private void HandleViewChanged(ViewType view)
    {
        if (view == ViewType.Grill)
        {
            if (slidingRoot != null)
                slidingRoot.gameObject.SetActive(true);

            OnEnteredGrillView();
            Close(true);
            return;
        }

        OnPanelClosing();

        // ViewManager.SetVisualVisibility apaga SpriteRenderer/Collider2D/Canvas pero no los
        // MeshRenderer de TextMeshPro: hay que desactivar el root para que el panel desaparezca.
        if (slidingRoot != null)
            slidingRoot.gameObject.SetActive(false);
    }

    // ── Gate de drop ────────────────────────────────────────────────────────

    /// <summary>
    /// True si el punto cae dentro del fondo del panel. El panel tapa un borde de la
    /// pantalla, asi que soltar ahi debe cancelar el arrastre en vez de aplicar el drop.
    /// Sin panelBackground asignado no filtra nada.
    /// </summary>
    public bool IsPointOverPanel(Vector3 worldPoint)
    {
        if (panelBackground == null)
            return false;

        Vector3 point = worldPoint;
        point.z = panelBackground.bounds.center.z;

        return panelBackground.bounds.Contains(point);
    }

    // ── Apertura / cierre ───────────────────────────────────────────────────

    /// <summary>Alterna entre abierto y cerrado. Lo llama la pestana lateral.</summary>
    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    /// <summary>Despliega el panel deslizandolo hasta openLocalX.</summary>
    public void Open()
    {
        if (!CanOpen())
        {
            Debug.Log("[" + GetType().Name + "] Apertura del panel bloqueada.");
            return;
        }

        IsOpen = true;
        StartSlide(openLocalX, false);
        OnPanelOpened();
    }

    /// <summary>
    /// Repliega el panel hasta closedLocalX. Con instant en true salta la animacion.
    /// Cancela cualquier arrastre en curso.
    /// </summary>
    public void Close(bool instant = false)
    {
        OnPanelClosing();
        IsOpen = false;
        StartSlide(closedLocalX, instant);
    }

    private void StartSlide(float targetX, bool instant)
    {
        if (slidingRoot == null)
            return;

        if (slideRoutine != null)
        {
            StopCoroutine(slideRoutine);
            slideRoutine = null;
        }

        Vector3 current = slidingRoot.localPosition;

        if (instant || slideDuration <= 0f || !isActiveAndEnabled || !slidingRoot.gameObject.activeInHierarchy)
        {
            current.x = targetX;
            slidingRoot.localPosition = current;
            return;
        }

        // Escala la duracion por el tramo que falta, para que una interrupcion no
        // vuelva a tardar el tiempo completo.
        float span = Mathf.Abs(openLocalX - closedLocalX);
        float remaining = span > 0.0001f ? Mathf.Abs(targetX - current.x) / span : 0f;
        float duration = slideDuration * Mathf.Clamp01(remaining);

        if (duration <= 0.0001f)
        {
            current.x = targetX;
            slidingRoot.localPosition = current;
            return;
        }

        slideRoutine = StartCoroutine(SlideRoutine(targetX, duration));
    }

    private IEnumerator SlideRoutine(float targetX, float duration)
    {
        float startX = slidingRoot.localPosition.x;
        float elapsed = 0f;

        // TutorialOfferController pone Time.timeScale = 0 al entrar a GameScene:
        // el deslizamiento tiene que correr en tiempo no escalado.
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            Vector3 position = slidingRoot.localPosition;
            position.x = Mathf.Lerp(startX, targetX, k);
            slidingRoot.localPosition = position;

            yield return null;
        }

        Vector3 finalPosition = slidingRoot.localPosition;
        finalPosition.x = targetX;
        slidingRoot.localPosition = finalPosition;

        slideRoutine = null;
    }
}
