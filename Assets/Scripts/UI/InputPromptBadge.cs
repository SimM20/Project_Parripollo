using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ícono fijo de la tecla o el botón que acciona un elemento de la partida: Q bajo la pestaña del
/// stock, E bajo la de toppings, Espacio bajo el botón de capa. Sigue a su objetivo del mundo (la
/// pestaña se desliza con su panel) y dibuja el control en uso con el mismo
/// <see cref="InputGlyphSetSO"/> que los carteles del tutorial: cambia solo al pasar de teclado a
/// joystick, de un Xbox a un PlayStation o a un genérico, y al editar el binding en
/// <c>GameControls.inputactions</c>.
///
/// Vive en un canvas overlay sin GraphicRaycaster (prefab <c>Prefabs/UI/InputPromptBadges</c>): nunca
/// le saca un click ni un hover a nada. Se oculta en pausa, con el objetivo apagado (ViewManager apaga
/// los renderers), mientras un cartel del tutorial apunta al mismo objeto y si la acción no tiene
/// tecla en el control en uso. Late al apretar la acción.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class InputPromptBadge : MonoBehaviour
{
    /// <summary>
    /// Objetos de la partida que el ícono encuentra solo, así el prefab anda en cualquier escena sin
    /// referencias. ⚠️ Se serializa como int: los valores nuevos van siempre al final.
    /// </summary>
    public enum Anchor
    {
        /// <summary>El campo <c>target</c>.</summary>
        Custom,
        /// <summary>Pestaña del panel de stock (izquierda).</summary>
        StockTab,
        /// <summary>Pestaña del panel de panes, guarniciones y salsas (derecha).</summary>
        ToppingsTab,
        /// <summary>Botón de capa carne / carbón.</summary>
        LayerButton,
    }

    [Header("Qué muestra")]
    [SerializeField] private GameAction action;
    [SerializeField] private InputGlyphSetSO glyphs;

    [Header("Dónde")]
    [Tooltip("Objeto que se busca solo en la escena. Custom = el de 'target'.")]
    [SerializeField] private Anchor anchor = Anchor.Custom;
    [SerializeField] private Transform target;
    [Tooltip("Punto del objetivo (0..1 sobre su rectángulo en pantalla) donde se apoya el ícono. (0.5, 0) = abajo al medio.")]
    [SerializeField] private Vector2 targetPoint = new Vector2(0.5f, 0f);
    [Tooltip("Punto del ícono que se apoya ahí. (0.5, 1) = el ícono cuelga debajo.")]
    [SerializeField] private Vector2 badgePivot = new Vector2(0.5f, 1f);
    [Tooltip("Corrimiento extra, en px de la resolución de referencia.")]
    [SerializeField] private Vector2 offset = new Vector2(0f, -6f);
    [Tooltip("Nunca más cerca que esto del borde de la pantalla.")]
    [SerializeField] private float screenMargin = 6f;

    [Header("Partes")]
    [Tooltip("Dibujo entero del control (LetraQ, Xbox LB...).")]
    [SerializeField] private Image glyphImage;
    [Tooltip("Tecla o botón en blanco para los controles sin dibujo propio; el nombre va en keyLabel.")]
    [SerializeField] private Image keyCap;
    [SerializeField] private TMP_Text keyLabel;

    [Header("Medidas y animación (px de la resolución de referencia, tiempo sin escalar)")]
    [SerializeField] private float glyphHeight = 40f;
    [Tooltip("Margen a cada lado del nombre, en la tecla en blanco.")]
    [SerializeField] private float keyCapPadding = 10f;
    [SerializeField] [Range(0f, 1f)] private float visibleAlpha = 1f;
    [SerializeField] [Min(0.01f)] private float fadeSeconds = 0.15f;
    [Tooltip("Golpe de escala al apretar la acción (0.18 = 18 %). 0 = sin golpe.")]
    [SerializeField] [Min(0f)] private float pressPunch = 0.18f;
    [Tooltip("Se esconde mientras un cartel del tutorial apunta al mismo objeto: ya muestra el control.")]
    [SerializeField] private bool hideUnderTutorialHint = true;

    private const float TargetLookupInterval = 1f;
    private static readonly Vector3[] Corners = new Vector3[8];

    private RectTransform rect;
    private CanvasGroup group;
    private Canvas rootCanvas;
    private Renderer targetRenderer;
    private Collider2D targetCollider;
    private Transform boundTarget;
    private float nextLookup;
    private bool hasGlyph;

    private void Awake()
    {
        rect = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = 0f;

        Canvas canvas = GetComponentInParent<Canvas>();
        rootCanvas = canvas != null ? canvas.rootCanvas : null;

        if (glyphs == null)
            Debug.LogWarning($"[InputPromptBadge] '{name}' no tiene 'glyphs': no muestra nada.");
    }

    private void OnEnable()
    {
        Loc.OnTextsChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        Loc.OnTextsChanged -= Refresh;
        group.alpha = 0f;
    }

    // ── Contenido ───────────────────────────────────────────────────────────

    /// <summary>
    /// Ícono para el control en uso. Corre con <see cref="Loc.OnTextsChanged"/>, que también salta al
    /// cambiar de teclado a joystick y de familia de joystick (<see cref="InputPrompts"/>).
    /// </summary>
    private void Refresh()
    {
        HintGlyph glyph = glyphs != null ? glyphs.Resolve(HintPrompt.For(action)) : default;
        bool drawn = glyph.Sprite != null;
        bool keyed = !drawn && !string.IsNullOrEmpty(glyph.Label);
        hasGlyph = drawn || keyed;

        if (glyphImage != null) glyphImage.gameObject.SetActive(drawn);
        if (keyCap != null) keyCap.gameObject.SetActive(keyed);

        float width = glyphHeight;

        if (drawn && glyphImage != null)
        {
            glyphImage.sprite = glyph.Sprite;
            Rect spriteRect = glyph.Sprite.rect;
            width = spriteRect.height > 0f ? glyphHeight * spriteRect.width / spriteRect.height : glyphHeight;
        }
        else if (keyed && keyCap != null)
        {
            if (glyph.Blank != null)
                keyCap.sprite = glyph.Blank;

            if (keyLabel != null)
            {
                keyLabel.text = glyph.Label.ToUpperInvariant();
                width = Mathf.Max(glyphHeight, keyLabel.GetPreferredValues(keyLabel.text).x + keyCapPadding * 2f);
            }
        }

        rect.sizeDelta = new Vector2(width, glyphHeight);
    }

    // ── Por frame ───────────────────────────────────────────────────────────

    private void LateUpdate()
    {
        BindTarget();

        bool visible = hasGlyph
            && boundTarget != null
            && !GamePause.IsPaused
            && IsTargetShown()
            && !(hideUnderTutorialHint && TutorialHintLayer.Instance != null && TutorialHintLayer.Instance.IsPointingAt(boundTarget))
            && TryGetTargetScreenRect(out Vector2 screenMin, out Vector2 screenMax)
            && Place(screenMin, screenMax);

        float alphaTarget = visible ? visibleAlpha : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, alphaTarget, Time.unscaledDeltaTime / fadeSeconds);

        if (visible && pressPunch > 0f && InputManager.WasPressed(action))
            ScalePunch.Punch(transform, pressPunch, 0.2f, unscaledTime: true);
    }

    // ── Objetivo ────────────────────────────────────────────────────────────

    private void BindTarget()
    {
        Transform wanted = target;

        if (anchor != Anchor.Custom)
        {
            // Lo encontrado se conserva; si se destruye (cambio de escena) se vuelve a buscar, de a ratos.
            if (boundTarget != null)
                return;
            if (Time.unscaledTime < nextLookup)
                return;

            nextLookup = Time.unscaledTime + TargetLookupInterval;
            wanted = FindAnchor(anchor);
        }

        if (wanted == boundTarget)
            return;

        boundTarget = wanted;
        targetRenderer = wanted != null ? wanted.GetComponent<Renderer>() : null;
        targetCollider = wanted != null ? wanted.GetComponent<Collider2D>() : null;
    }

    private static Transform FindAnchor(Anchor id)
    {
        switch (id)
        {
            case Anchor.LayerButton:
            {
                GrillLayerToggle toggle = GrillLayerToggle.Current != null
                    ? GrillLayerToggle.Current
                    : Object.FindFirstObjectByType<GrillLayerToggle>();
                return toggle != null ? toggle.transform : null;
            }

            case Anchor.StockTab:
            case Anchor.ToppingsTab:
                foreach (StockPanelTab tab in Object.FindObjectsByType<StockPanelTab>(FindObjectsSortMode.None))
                {
                    if (id == Anchor.StockTab ? tab.Controller is StockPanelController : tab.Controller is ToppingsPanelController)
                        return tab.transform;
                }
                return null;

            default:
                return null;
        }
    }

    /// <summary>El objetivo se dibuja: ViewManager y los paneles apagan renderers, no GameObjects.</summary>
    private bool IsTargetShown()
    {
        if (!boundTarget.gameObject.activeInHierarchy)
            return false;
        if (targetRenderer != null)
            return targetRenderer.enabled;
        return targetCollider == null || targetCollider.enabled;
    }

    /// <summary>Rectángulo del objetivo en píxeles de pantalla. False si está detrás de la cámara.</summary>
    private bool TryGetTargetScreenRect(out Vector2 min, out Vector2 max)
    {
        min = max = Vector2.zero;

        Camera cam = Camera.main;
        if (cam == null)
            return false;

        Bounds bounds = targetRenderer != null ? targetRenderer.bounds
            : targetCollider != null ? targetCollider.bounds
            : new Bounds(boundTarget.position, Vector3.zero);

        // Cámara en perspectiva: se proyectan las 8 esquinas, no el centro con un tamaño.
        Vector3 c = bounds.center;
        Vector3 e = bounds.extents;
        for (int i = 0; i < Corners.Length; i++)
        {
            Corners[i] = new Vector3(
                c.x + ((i & 1) == 0 ? -e.x : e.x),
                c.y + ((i & 2) == 0 ? -e.y : e.y),
                c.z + ((i & 4) == 0 ? -e.z : e.z));

            Vector3 screen = cam.WorldToScreenPoint(Corners[i]);
            if (screen.z <= 0f)
                return false;

            min = i == 0 ? (Vector2)screen : Vector2.Min(min, screen);
            max = i == 0 ? (Vector2)screen : Vector2.Max(max, screen);
        }
        return true;
    }

    // ── Ubicación ───────────────────────────────────────────────────────────

    private bool Place(Vector2 screenMin, Vector2 screenMax)
    {
        RectTransform parent = rect.parent as RectTransform;
        if (parent == null)
            return false;

        Camera uiCamera = rootCanvas == null || rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        Vector2 screenPoint = screenMin + Vector2.Scale(screenMax - screenMin, targetPoint);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, uiCamera, out Vector2 local))
            return false;

        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = badgePivot;

        // No salirse de la pantalla: el ícono se corre hacia adentro.
        Vector2 position = local + offset;
        Vector2 size = rect.rect.size;
        Rect bounds = parent.rect;
        Vector2 lowerLeft = position - Vector2.Scale(badgePivot, size);
        position.x += ClampShift(lowerLeft.x, size.x, bounds.xMin + screenMargin, bounds.xMax - screenMargin);
        position.y += ClampShift(lowerLeft.y, size.y, bounds.yMin + screenMargin, bounds.yMax - screenMargin);

        // Las anclas del ícono están en el centro del padre.
        rect.anchoredPosition = position - bounds.center;
        return true;
    }

    /// <summary>Corrimiento para que [start, start + length] entre en [min, max]. Si no entra, se centra.</summary>
    private static float ClampShift(float start, float length, float min, float max)
    {
        if (length > max - min)
            return (min + max) * 0.5f - (start + length * 0.5f);
        if (start < min)
            return min - start;
        if (start + length > max)
            return max - (start + length);
        return 0f;
    }
}
