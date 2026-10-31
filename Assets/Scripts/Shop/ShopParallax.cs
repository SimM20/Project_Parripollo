using UnityEngine;

/// <summary>
/// Parallax del fondo de la shop siguiendo al puntero, con dos capas separadas: la ventana del local
/// (cerca, se mueve más) y el exterior ditherado (lejos, se mueve menos). Las dos se corren al revés
/// del puntero, como si el jugador moviera la cabeza frente a la ventana.
///
/// La ventana se mueve con su RectTransform, de a pasos enteros de su pixel art para que no se
/// deforme. El exterior no se puede mover así: el shader SH_ShopDither lee la captura de la cámara
/// por posición de pantalla, así que el corrimiento le llega por la global <c>_ShopBackdropParallax</c>
/// (px a 1080p) y el shader lo encaja a celdas enteras de dither, con el patrón acompañando a la imagen.
///
/// Corre con tiempo sin escala: la shop puede abrirse con el juego en pausa.
/// </summary>
[DisallowMultipleComponent]
public class ShopParallax : MonoBehaviour
{
    private static readonly int BackdropParallaxId = Shader.PropertyToID("_ShopBackdropParallax");

    [Tooltip("Capa de la ventana (pared + marco + vidrio). Se mueve con el puntero.")]
    [SerializeField] private RectTransform windowLayer;

    [Tooltip("Corrimiento máximo de la ventana en unidades del canvas (= px a 1080p), con el puntero en el borde de la pantalla.")]
    [SerializeField] private Vector2 windowRange = new Vector2(24f, 12f);

    [Tooltip("Corrimiento máximo del exterior en px a 1080p. Menor que el de la ventana: está más lejos.")]
    [SerializeField] private Vector2 backdropRange = new Vector2(9f, 6f);

    [Tooltip("Tamaño del pixel de arte de la ventana en unidades del canvas. La ventana se mueve de a pasos enteros de este tamaño.")]
    [Min(1f)]
    [SerializeField] private float windowPixelSize = 3f;

    [Tooltip("Qué tan rápido sigue al puntero (1/s). Más alto = más pegado al mouse.")]
    [Min(0.1f)]
    [SerializeField] private float followSpeed = 6f;

    private Vector2 windowOrigin;
    private Vector2 smoothedPointer;

    private void Awake()
    {
        if (windowLayer != null)
            windowOrigin = windowLayer.anchoredPosition;
    }

    private void OnEnable()
    {
        // Arranca donde está el puntero: al abrir la shop no hay barrido inicial.
        smoothedPointer = PointerDirection();
        Apply();
    }

    private void OnDisable()
    {
        Shader.SetGlobalVector(BackdropParallaxId, Vector4.zero);
        if (windowLayer != null)
            windowLayer.anchoredPosition = windowOrigin;
    }

    private void Update()
    {
        float t = 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
        smoothedPointer = Vector2.Lerp(smoothedPointer, PointerDirection(), t);
        Apply();
    }

    private void Apply()
    {
        if (windowLayer != null)
        {
            Vector2 offset = -Vector2.Scale(smoothedPointer, windowRange);
            offset.x = Mathf.Round(offset.x / windowPixelSize) * windowPixelSize;
            offset.y = Mathf.Round(offset.y / windowPixelSize) * windowPixelSize;
            windowLayer.anchoredPosition = windowOrigin + offset;
        }

        Vector2 backdrop = -Vector2.Scale(smoothedPointer, backdropRange);
        Shader.SetGlobalVector(BackdropParallaxId, new Vector4(backdrop.x, backdrop.y, 0f, 0f));
    }

    /// <summary>Puntero relativo al centro de la pantalla, en [-1, 1] por eje. Sin InputManager queda centrado.</summary>
    private static Vector2 PointerDirection()
    {
        if (InputManager.Instance == null || Screen.width <= 0 || Screen.height <= 0)
            return Vector2.zero;

        Vector3 pointer = InputManager.PointerPosition;
        return new Vector2(
            Mathf.Clamp(pointer.x / Screen.width * 2f - 1f, -1f, 1f),
            Mathf.Clamp(pointer.y / Screen.height * 2f - 1f, -1f, 1f));
    }
}
