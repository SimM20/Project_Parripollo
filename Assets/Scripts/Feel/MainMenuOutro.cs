using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MenuSequence;

/// <summary>
/// Salida del menú principal al apretar SALIR: la jornada se cierra. Es la intro al revés y
/// después la noche: se van los botones, se borra el subtítulo, sube y desaparece el título, y el
/// día entero pasa en time-lapse con <see cref="DayCycleBackground.SetScriptedHour"/> (el sol cruza
/// el cielo y se pone por la izquierda con un destello) hasta que es de noche. Termina con un
/// fundido a negro y recién ahí avisa (<see cref="Play"/>) para cerrar el juego.
///
/// Arranca apagado; <see cref="MainMenuPanel"/> lo prende desde el botón. Mientras dura, el menú no
/// recibe clicks ni Submit. Cualquier tecla, click o A / B / Start lo corta y sale en el acto
/// (quien quiere irse no espera), salvo en los primeros instantes, para que la misma pulsación que
/// apretó SALIR no lo saltee. Mismo estilo que <see cref="MainMenuIntro"/>: todo es función del
/// tiempo transcurrido, desplazamientos de UI en la grilla del arte, tiempo sin escalar.
/// </summary>
[DisallowMultipleComponent]
public class MainMenuOutro : MonoBehaviour
{
    [Header("Escena")]
    [Tooltip("CanvasGroup del menú completo: se bloquea apenas arranca la salida.")]
    [SerializeField] private CanvasGroup menuGroup;
    [SerializeField] private DayCycleBackground background;
    [Tooltip("Sol del fondo: el halo lo sigue.")]
    [SerializeField] private Transform sun;
    [Tooltip("Halo del sol (el mismo de la intro). Vacío = sin destello al ponerse.")]
    [SerializeField] private SpriteRenderer halo;
    [Tooltip("Raíz del título: se le suma un CanvasGroup para el fundido.")]
    [SerializeField] private RectTransform title;
    [Tooltip("Capas del título de adelante hacia atrás (Text, Shadow): cada una sale un poco después que la anterior.")]
    [SerializeField] private RectTransform[] titleLayers;
    [Tooltip("Textos del subtítulo (Text y Shadow): se borran letra por letra desde el final.")]
    [SerializeField] private TMP_Text[] subtitleTexts;
    [Tooltip("Contenedor de los botones: baja entero; sus hijos se apagan de arriba a abajo.")]
    [SerializeField] private RectTransform buttons;
    [Tooltip("Número de versión: se apaga con el primer botón. Vacío = no se toca.")]
    [SerializeField] private RectTransform version;

    [Header("Menú")]
    [SerializeField] private float buttonsStart = 0f;
    [SerializeField] [Min(0f)] private float buttonsSeconds = 0.4f;
    [Tooltip("Cuántos px (de referencia, 1920×1080) baja el bloque.")]
    [SerializeField] private float buttonsSink = 120f;
    [SerializeField] private float buttonStagger = 0.06f;
    [SerializeField] [Min(0f)] private float buttonFadeSeconds = 0.18f;
    [SerializeField] private float subtitleStart = 0.05f;
    [SerializeField] [Min(0f)] private float subtitleSeconds = 0.35f;
    [SerializeField] private float titleStart = 0.15f;
    [SerializeField] [Min(0f)] private float titleSeconds = 0.4f;
    [Tooltip("Cuántos px (de referencia) sube el título al irse.")]
    [SerializeField] private float titleRise = 140f;
    [Tooltip("Retraso de cada capa respecto de la anterior: la sombra se va después del texto.")]
    [SerializeField] private float layerLag = 0.05f;

    [Header("Fin del día")]
    [Tooltip("Hora a la que llega. Desde la hora que muestra el fondo al apretar SALIR.")]
    [Range(0f, 24f)]
    [SerializeField] private float toHour = 21f;
    [SerializeField] private float hourStart = 0.4f;
    [SerializeField] [Min(0f)] private float hourSeconds = 2.6f;

    [Header("Destello al ponerse el sol")]
    [Tooltip("El sol toca los árboles hacia t ≈ 2,4 s: el destello tiene que apagarse antes de que sea de noche.")]
    [SerializeField] private float haloStart = 1.95f;
    [SerializeField] [Min(0f)] private float haloSeconds = 0.7f;
    [SerializeField] private Color haloColor = new Color(1f, 0.6f, 0.35f, 0.6f);
    [SerializeField] private float haloFromScale = 0.3f;
    [SerializeField] private float haloToScale = 0.9f;

    [Header("Fundido final")]
    [SerializeField] private float fadeStart = 2.8f;
    [SerializeField] [Min(0f)] private float fadeSeconds = 0.8f;
    [Tooltip("Segundos en negro antes de cerrar.")]
    [SerializeField] [Min(0f)] private float fadeHoldSeconds = 0.15f;
    [Tooltip("Mismo color que el velo de SceneFadeIn.")]
    [SerializeField] private Color fadeColor = new Color(0.06f, 0.04f, 0.03f, 1f);

    [Header("Estilo")]
    [Tooltip("Paso en px de referencia al que se redondean los desplazamientos de la UI. 4 = el píxel del arte. 0 = liso.")]
    [SerializeField] [Min(0f)] private float pixelStep = 4f;
    [Tooltip("Segundos desde que arranca hasta que una tecla o click lo corta.")]
    [SerializeField] [Min(0f)] private float skipArmSeconds = 0.25f;

    private Image veil;
    private CanvasGroup titleGroup;
    private CanvasGroup versionGroup;
    private CanvasGroup[] buttonGroups;
    private Vector2[] titleLayerBase;
    private Vector2 buttonsBase;
    private float fromHour;
    private float total;
    private float elapsed;
    private Action onComplete;

    /// <summary>True desde <see cref="Play"/> hasta que avisa.</summary>
    public bool IsPlaying => onComplete != null;

    private void Awake()
    {
        enabled = false;
    }

    /// <summary>
    /// Arranca la salida y llama a <paramref name="complete"/> al terminar el fundido. Si ya está
    /// corriendo, no hace nada.
    /// </summary>
    public void Play(Action complete)
    {
        if (IsPlaying) return;
        onComplete = complete ?? delegate { };

        menuGroup.interactable = false;
        menuGroup.blocksRaycasts = false;

        // Se toma todo como está ahora: es el estado final de la intro (o el que haya dejado un salto).
        titleGroup = GetOrAddGroup(title);
        versionGroup = version != null ? GetOrAddGroup(version) : null;

        titleLayerBase = new Vector2[titleLayers.Length];
        for (int i = 0; i < titleLayers.Length; i++)
            titleLayerBase[i] = titleLayers[i].anchoredPosition;

        buttonsBase = buttons.anchoredPosition;
        buttonGroups = new CanvasGroup[buttons.childCount];
        for (int i = 0; i < buttonGroups.Length; i++)
            buttonGroups[i] = GetOrAddGroup(buttons.GetChild(i));

        if (veil == null)
            veil = CreateVeil(menuGroup.GetComponentInParent<Canvas>().rootCanvas.transform, "OutroVeil");

        fromHour = background.VisualHour;
        total = fadeStart + fadeSeconds + fadeHoldSeconds;
        elapsed = 0f;
        enabled = true;
        Evaluate(0f);
    }

    private void Update()
    {
        bool skip = elapsed >= skipArmSeconds && SkipPressed();
        elapsed = skip ? total : Mathf.Min(elapsed + Mathf.Min(Time.unscaledDeltaTime, MaxFrameStep), total);
        Evaluate(elapsed);
        if (elapsed < total) return;

        enabled = false;
        Action complete = onComplete;
        onComplete = null;
        complete();
    }

    private void Evaluate(float t)
    {
        // Botones: el bloque se hunde y cada uno se apaga, de arriba a abajo.
        float sink = buttonsSink * FeelEase.InCubic(Progress(t, buttonsStart, buttonsSeconds));
        buttons.anchoredPosition = buttonsBase - new Vector2(0f, Snap(sink, pixelStep));
        for (int i = 0; i < buttonGroups.Length; i++)
            buttonGroups[i].alpha = 1f - Progress(t, buttonsStart + i * buttonStagger, buttonFadeSeconds);

        if (versionGroup != null)
            versionGroup.alpha = 1f - Progress(t, buttonsStart, buttonFadeSeconds);

        // Subtítulo: se borra desde la última letra.
        float subtitleT = Progress(t, subtitleStart, subtitleSeconds);
        for (int i = 0; i < subtitleTexts.Length; i++)
        {
            TMP_Text text = subtitleTexts[i];
            text.maxVisibleCharacters = subtitleT <= 0f
                ? AllCharacters
                : Mathf.FloorToInt((1f - subtitleT) * text.text.Length);
        }

        // Título: sube acelerando y se desvanece en el último tramo.
        titleGroup.alpha = 1f - Progress(t, titleStart + titleSeconds * 0.5f, titleSeconds * 0.5f);
        for (int i = 0; i < titleLayers.Length; i++)
        {
            float rise = titleRise * FeelEase.InCubic(Progress(t, titleStart + i * layerLag, titleSeconds));
            titleLayers[i].anchoredPosition = titleLayerBase[i] + new Vector2(0f, Snap(rise, pixelStep));
        }

        float hourT = Progress(t, hourStart, hourSeconds);
        background.SetScriptedHour(Mathf.Lerp(fromHour, toHour, FeelEase.InOutCubic(hourT)));

        if (halo != null)
            EvaluateHalo(halo, sun, haloColor, haloFromScale, haloToScale, Progress(t, haloStart, haloSeconds));

        SetVeilAlpha(veil, fadeColor, FeelEase.InOutCubic(Progress(t, fadeStart, fadeSeconds)));
    }
}
