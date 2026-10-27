using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Entrada y salida del menú de pausa: el oscurecido y la viñeta aparecen y se van con fundido,
/// y la ventana usa la entrada/salida de su <see cref="UIPopIn"/>. Va en la raíz del canvas de pausa.
///
/// <see cref="Show"/> prende el canvas; <see cref="Hide"/> lo deja salir y lo apaga al terminar.
/// Durante la salida el canvas ya no bloquea raycasts, así que el juego (que se despausa en el
/// acto) responde mientras el menú se va. Volver a pausar a mitad de la salida retoma desde
/// donde estaba. Tiempo sin escalar: el fundido de entrada corre con timeScale = 0.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class PauseMenuTransition : MonoBehaviour
{
    [SerializeField] private Image dim;
    [SerializeField] [Range(0f, 1f)] private float dimAlpha = 0.65f;
    [SerializeField] private Image vignette;
    [SerializeField] [Range(0f, 1f)] private float vignetteAlpha = 1f;
    [Tooltip("Ventana con los botones: entra y sale con su UIPopIn.")]
    [SerializeField] private UIPopIn window;

    [SerializeField] [Min(0.01f)] private float fadeInSeconds = 0.2f;
    [SerializeField] [Min(0.01f)] private float fadeOutSeconds = 0.14f;

    private CanvasGroup group;
    private float progress;
    private bool hiding;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        ApplyProgress();
    }

    public void Show()
    {
        bool wasHiding = hiding && gameObject.activeSelf;
        hiding = false;
        group.blocksRaycasts = true;
        gameObject.SetActive(true);

        // Si ya estaba prendido saliendo, OnEnable no corre: la ventana vuelve a entrar a mano.
        if (wasHiding && window != null && window.isActiveAndEnabled)
            window.PlayIn();
    }

    public void Hide()
    {
        if (!gameObject.activeSelf || hiding) return;

        hiding = true;
        // Solo raycasts: apagar interactable dispara el tinte de deshabilitado de cada botón y
        // parpadea durante el fundido. Los Submit de teclado los ignora PauseMenuHandler.
        group.blocksRaycasts = false;

        if (window != null && window.isActiveAndEnabled)
            window.PlayOut(null);
    }

    private void OnEnable()
    {
        if (!hiding) progress = 0f;
        ApplyProgress();
    }

    private void Update()
    {
        float step = Time.unscaledDeltaTime / (hiding ? fadeOutSeconds : fadeInSeconds);
        progress = Mathf.Clamp01(progress + (hiding ? -step : step));
        ApplyProgress();

        if (hiding && progress <= 0f)
        {
            hiding = false;
            gameObject.SetActive(false);
        }
    }

    private void ApplyProgress()
    {
        // Una sola curva para ida y vuelta: invertir a mitad de camino no salta.
        float eased = Mathf.SmoothStep(0f, 1f, progress);
        SetAlpha(dim, dimAlpha * eased);
        SetAlpha(vignette, vignetteAlpha * eased);
    }

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null) return;
        Color c = graphic.color;
        c.a = alpha;
        graphic.color = c;
    }
}
