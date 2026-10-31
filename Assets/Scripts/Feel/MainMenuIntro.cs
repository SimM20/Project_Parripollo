using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Intro del menú principal, cada vez que se abre la escena: amanece en la ruta y después entra
/// el menú. El fondo arranca de noche y recorre la madrugada hasta la hora fija del menú con
/// <see cref="DayCycleBackground.SetScriptedHour"/>; cuando el sol asoma, un halo crece desde él y
/// se apaga. Recién ahí cae el título, se escribe el subtítulo y suben los botones.
///
/// Todo sale de una sola función del tiempo transcurrido (<see cref="Evaluate"/>), así que saltar
/// es llevar ese tiempo al final. Se salta con cualquier tecla, click o A / B / Start. Mientras
/// dura, el menú no recibe clicks ni Submit; después de un salto se habilita un instante más tarde
/// para que la misma tecla no apriete JUGAR. Tiempo sin escalar.
///
/// Los botones no se animan uno por uno en escala ni en posición: <see cref="RetroMenuButton"/>
/// escribe su escala cada frame y el VerticalLayoutGroup su posición. Sube el contenedor y cada
/// botón entra por alfa.
/// </summary>
[DisallowMultipleComponent]
public class MainMenuIntro : MonoBehaviour
{
    // Los primeros frames de la escena traen el pico de carga: el delta se topea para que ese salto
    // no se coma media intro (igual que SceneFadeIn).
    private const float MaxFrameStep = 1f / 30f;
    private const int AllCharacters = 99999;

    [Header("Escena")]
    [Tooltip("CanvasGroup del menú completo: bloquea clicks y Submit mientras dura la intro.")]
    [SerializeField] private CanvasGroup menuGroup;
    [SerializeField] private DayCycleBackground background;
    [Tooltip("Sol del fondo: el halo lo sigue.")]
    [SerializeField] private Transform sun;
    [Tooltip("Halo que crece desde el sol. Vacío = sin halo.")]
    [SerializeField] private SpriteRenderer halo;
    [Tooltip("Raíz del título: se le suma un CanvasGroup para el fundido.")]
    [SerializeField] private RectTransform title;
    [Tooltip("Capas del título de adelante hacia atrás (Text, Shadow): cada una cae un poco después que la anterior.")]
    [SerializeField] private RectTransform[] titleLayers;
    [Tooltip("Textos del subtítulo (Text y Shadow): se escriben letra por letra.")]
    [SerializeField] private TMP_Text[] subtitleTexts;
    [Tooltip("Contenedor de los botones: sube entero; sus hijos entran por alfa, de arriba a abajo.")]
    [SerializeField] private RectTransform buttons;
    [Tooltip("Número de versión: aparece con el último botón. Vacío = no se toca.")]
    [SerializeField] private RectTransform version;

    [Header("Amanecer")]
    [Range(0f, 24f)]
    [SerializeField] private float fromHour = 5f;
    [Tooltip("Hora en la que queda el menú. Tiene que coincidir con fallbackHour del fondo.")]
    [Range(0f, 24f)]
    [SerializeField] private float toHour = 6.5f;
    [SerializeField] private float hourStart = 0.3f;
    [SerializeField] [Min(0.01f)] private float hourSeconds = 2.4f;

    [Header("Halo del sol")]
    [SerializeField] private float haloStart = 1.8f;
    [SerializeField] [Min(0.01f)] private float haloSeconds = 1.2f;
    [Tooltip("Tinte del halo; el alfa es el del pico.")]
    [SerializeField] private Color haloColor = new Color(1f, 0.78f, 0.5f, 0.6f);
    [Tooltip("Escala de arranque y final. El sprite ya viene grande: hasta 1 su píxel no supera al del arte.")]
    [SerializeField] private float haloFromScale = 0.25f;
    [SerializeField] private float haloToScale = 1f;

    [Header("Título")]
    [SerializeField] private float titleStart = 2.1f;
    [SerializeField] [Min(0.01f)] private float titleSeconds = 0.45f;
    [Tooltip("Desde cuántos px (de referencia, 1920×1080) por encima cae.")]
    [SerializeField] private float titleDrop = 140f;
    [Tooltip("Rebote al caer (ver FeelEase.OutBack).")]
    [SerializeField] [Range(0f, 3f)] private float titleOvershoot = 1.6f;
    [Tooltip("Retraso de cada capa respecto de la anterior: la sombra llega después del texto.")]
    [SerializeField] private float layerLag = 0.05f;

    [Header("Subtítulo")]
    [SerializeField] private float subtitleStart = 2.4f;
    [SerializeField] [Min(0.01f)] private float subtitleSeconds = 0.45f;

    [Header("Botones")]
    [SerializeField] private float buttonsStart = 2.6f;
    [SerializeField] [Min(0.01f)] private float buttonsSeconds = 0.5f;
    [Tooltip("Desde cuántos px (de referencia) por debajo sube el bloque.")]
    [SerializeField] private float buttonsRise = 120f;
    [SerializeField] private float buttonStagger = 0.08f;
    [SerializeField] [Min(0.01f)] private float buttonFadeSeconds = 0.2f;

    [Header("Estilo")]
    [Tooltip("Paso en px de referencia al que se redondean los desplazamientos de la UI. 4 = el píxel del arte. 0 = liso.")]
    [SerializeField] [Min(0f)] private float pixelStep = 4f;
    [Tooltip("Segundos entre saltar la intro y que el menú responda.")]
    [SerializeField] [Min(0f)] private float skipUnlockDelay = 0.15f;

    private CanvasGroup titleGroup;
    private CanvasGroup versionGroup;
    private CanvasGroup[] buttonGroups;
    private Vector2[] titleLayerBase;
    private Vector2 buttonsBase;
    private float total;
    private float elapsed;
    private float unlockAt;

    private void Awake()
    {
        titleGroup = GetOrAddGroup(title);
        versionGroup = version != null ? GetOrAddGroup(version) : null;

        titleLayerBase = new Vector2[titleLayers.Length];
        for (int i = 0; i < titleLayers.Length; i++)
            titleLayerBase[i] = titleLayers[i].anchoredPosition;

        buttonsBase = buttons.anchoredPosition;
        buttonGroups = new CanvasGroup[buttons.childCount];
        for (int i = 0; i < buttonGroups.Length; i++)
            buttonGroups[i] = GetOrAddGroup(buttons.GetChild(i));

        total = Mathf.Max(
            hourStart + hourSeconds,
            halo != null ? haloStart + haloSeconds : 0f,
            titleStart + layerLag * Mathf.Max(titleLayers.Length - 1, 0) + titleSeconds,
            subtitleStart + subtitleSeconds,
            buttonsStart + buttonsSeconds,
            buttonsStart + buttonStagger * buttonGroups.Length + buttonFadeSeconds);

        // En Awake y no en Start: la hora tiene que estar puesta antes del Start del fondo, que
        // pinta el primer frame. Si no, se ve un frame de amanecer antes de la noche.
        Evaluate(0f);
    }

    private void Start()
    {
        // En Start: para entonces el RetroMenuButton de JUGAR ya se seleccionó en su OnEnable, que
        // con el grupo bloqueado no lo habría hecho y el menú quedaría sin cursor.
        SetMenuInteractable(false);
    }

    private void Update()
    {
        if (elapsed < total)
        {
            if (SkipPressed())
            {
                elapsed = total;
                unlockAt = Time.unscaledTime + skipUnlockDelay;
            }
            else
            {
                elapsed = Mathf.Min(elapsed + Mathf.Min(Time.unscaledDeltaTime, MaxFrameStep), total);
                unlockAt = Time.unscaledTime;
            }

            Evaluate(elapsed);
            if (elapsed < total) return;

            background.ClearScriptedHour();
        }

        if (Time.unscaledTime < unlockAt) return;

        SetMenuInteractable(true);
        enabled = false;
    }

    private void Evaluate(float t)
    {
        float hourT = Progress(t, hourStart, hourSeconds);
        background.SetScriptedHour(Mathf.Lerp(fromHour, toHour, FeelEase.InOutCubic(hourT)));

        if (halo != null)
            EvaluateHalo(Progress(t, haloStart, haloSeconds));

        // Título: aparece en el primer tramo de la caída y rebota al llegar.
        titleGroup.alpha = Progress(t, titleStart, titleSeconds * 0.3f);
        for (int i = 0; i < titleLayers.Length; i++)
        {
            float p = Progress(t, titleStart + i * layerLag, titleSeconds);
            float drop = titleDrop * (1f - FeelEase.OutBack(p, titleOvershoot));
            titleLayers[i].anchoredPosition = titleLayerBase[i] + new Vector2(0f, Snap(drop));
        }

        float subtitleT = Progress(t, subtitleStart, subtitleSeconds);
        for (int i = 0; i < subtitleTexts.Length; i++)
        {
            TMP_Text text = subtitleTexts[i];
            text.maxVisibleCharacters = subtitleT >= 1f
                ? AllCharacters
                : Mathf.CeilToInt(subtitleT * text.text.Length);
        }

        float rise = buttonsRise * (1f - FeelEase.OutCubic(Progress(t, buttonsStart, buttonsSeconds)));
        buttons.anchoredPosition = buttonsBase - new Vector2(0f, Snap(rise));
        for (int i = 0; i < buttonGroups.Length; i++)
            buttonGroups[i].alpha = Progress(t, buttonsStart + i * buttonStagger, buttonFadeSeconds);

        if (versionGroup != null)
            versionGroup.alpha = Progress(t, buttonsStart + buttonGroups.Length * buttonStagger, buttonFadeSeconds);
    }

    private void EvaluateHalo(float p)
    {
        bool visible = p > 0f && p < 1f;
        halo.enabled = visible;
        if (!visible) return;

        // Sigue al sol, que todavía está subiendo.
        halo.transform.position = sun.position;
        halo.transform.localScale = Vector3.one * Mathf.Lerp(haloFromScale, haloToScale, FeelEase.OutCubic(p));

        // Se prende rápido, se sostiene y se apaga.
        const float riseShare = 0.15f;
        float alpha = Mathf.Clamp01(p / riseShare) * (1f - FeelEase.InQuad((p - riseShare) / (1f - riseShare)));
        Color color = haloColor;
        color.a *= alpha;
        halo.color = color;
    }

    private void SetMenuInteractable(bool interactable)
    {
        menuGroup.interactable = interactable;
        menuGroup.blocksRaycasts = interactable;
    }

    private float Snap(float value) =>
        pixelStep > 0f ? Mathf.Round(value / pixelStep) * pixelStep : value;

    private static float Progress(float t, float start, float seconds) =>
        Mathf.Clamp01((t - start) / seconds);

    private static CanvasGroup GetOrAddGroup(Component target)
    {
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        return group != null ? group : target.gameObject.AddComponent<CanvasGroup>();
    }

    private static bool SkipPressed()
    {
        // El teclado se lee directo para que Enter y Espacio también salten: InputManager no los
        // expone fuera del módulo de UI.
        Keyboard keyboard = Keyboard.current;
        return (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
            || InputManager.PrimaryPressed
            || InputManager.WasPressed(GameAction.Back)
            || InputManager.WasPressed(GameAction.Pause);
    }
}
