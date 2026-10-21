using UnityEngine;

/// <summary>
/// Entrada de un panel de UI cada vez que se activa: aparece con un fundido corto y crece desde
/// apenas más chico con un rebote leve. Va en la ventana del panel (no en el fondo oscuro).
///
/// No demora nada: los botones responden desde el primer frame (el CanvasGroup nunca bloquea
/// raycasts ni apaga la interacción) y cerrar sigue siendo instantáneo. Corre en tiempo sin escalar
/// porque estos paneles se abren con el juego en pausa. Al apagarse vuelve a escala y alfa base,
/// así que abrir y cerrar rápido no acumula nada.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class UIPopIn : MonoBehaviour
{
    [Tooltip("Segundos de la entrada.")]
    [SerializeField] [Min(0.01f)] private float duration = 0.18f;
    [Tooltip("Escala de arranque (1 = sin crecimiento).")]
    [SerializeField] [Range(0.5f, 1f)] private float fromScale = 0.92f;
    [Tooltip("Cuanto se pasa antes de asentarse. 0 = sin rebote.")]
    [SerializeField] [Range(0f, 3f)] private float overshoot = 1.2f;
    [Tooltip("Fundido de alfa con un CanvasGroup (se agrega solo si falta).")]
    [SerializeField] private bool fade = true;

    /// <summary>
    /// Suma la entrada a una ventana armada en escena sin ella. Si la ventana está apagada, la
    /// entrada se ve la primera vez que se prende. No hace nada si ya la tiene.
    /// </summary>
    public static void Ensure(Transform window)
    {
        if (window is RectTransform && window.GetComponent<UIPopIn>() == null)
            window.gameObject.AddComponent<UIPopIn>();
    }

    private RectTransform rect;
    private CanvasGroup group;
    private Vector3 baseScale = Vector3.one;
    private float baseAlpha = 1f;
    private float elapsed;
    private bool playing;

    private void Awake()
    {
        rect = (RectTransform)transform;
        baseScale = rect.localScale;

        if (fade)
        {
            group = GetComponent<CanvasGroup>();
            if (group == null)
                group = gameObject.AddComponent<CanvasGroup>();
            baseAlpha = group.alpha;
        }
    }

    private void OnEnable()
    {
        elapsed = 0f;
        playing = true;
        Apply(0f);
    }

    private void Update()
    {
        if (!playing) return;

        elapsed += Time.unscaledDeltaTime;
        float t = elapsed / duration;

        if (t >= 1f)
        {
            Restore();
            return;
        }

        Apply(t);
    }

    private void Apply(float t)
    {
        float scale = Mathf.LerpUnclamped(fromScale, 1f, FeelEase.OutBack(t, overshoot));
        rect.localScale = baseScale * scale;

        if (group != null)
            group.alpha = baseAlpha * FeelEase.OutCubic(t * 1.6f);
    }

    private void Restore()
    {
        playing = false;

        if (rect != null)
            rect.localScale = baseScale;

        if (group != null)
            group.alpha = baseAlpha;
    }

    private void OnDisable() => Restore();
}
