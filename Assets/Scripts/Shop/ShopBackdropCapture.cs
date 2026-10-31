using UnityEngine;

/// <summary>
/// Marca el fondo de la shop como consumidor de la captura de la cámara. Mientras haya al menos uno
/// activo, <see cref="ShopBackdropCaptureFeature"/> copia lo que dibuja la cámara del mundo a la
/// textura global <c>_ShopBackdropTex</c>, que lee el shader de dithering (SH_ShopDither).
///
/// La copia se hace dentro del render de la cámara, así que solo trae el mundo: un canvas en
/// Screen Space - Overlay se dibuja después y nunca entra en la textura. Por eso el dithering queda
/// en el fondo y la UI de la shop (hija de este objeto, con sus propios materiales) no se toca. Con
/// el canvas en Screen Space - Camera la UI sí entraría en la captura, y se avisa por consola.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class ShopBackdropCapture : MonoBehaviour
{
    private static int activeCount;

    /// <summary>True mientras haya un fondo de shop habilitado que necesite la captura.</summary>
    public static bool IsActive => activeCount > 0;

    private void OnEnable()
    {
        activeCount++;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            Debug.LogWarning("[ShopBackdropCapture] El canvas de '" + name + "' no está en Screen Space - Overlay: la UI entraría en la captura del fondo.", this);
    }

    private void OnDisable()
    {
        activeCount = Mathf.Max(0, activeCount - 1);
    }
}
