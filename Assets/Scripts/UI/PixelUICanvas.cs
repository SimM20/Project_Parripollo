using UnityEngine;

/// <summary>
/// Va en cada canvas raíz de UI de pantalla. Los canvases son <c>Screen Space - Camera</c> (ver
/// <see cref="PixelGrid"/>) y necesitan la cámara de la escena; un prefab no puede guardarla y un
/// canvas que sobrevive al cambio de escena (pausa, DDOL) se queda con la cámara destruida. Si le
/// falta, toma <c>Camera.main</c>. Sin cámara, Unity lo dibuja como overlay: se ve, pero fuera de la grilla.
/// Ojo: pisa el sorting layer con <see cref="PixelGrid.UiSortingLayer"/>.
/// </summary>
[RequireComponent(typeof(Canvas))]
[DisallowMultipleComponent]
public class PixelUICanvas : MonoBehaviour
{
    private Canvas canvas;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        EnsureCamera();
    }

    private void OnEnable() => EnsureCamera();

    private void LateUpdate() => EnsureCamera();

    // Sin cámara, Canvas.renderMode devuelve ScreenSpaceOverlay aunque esté guardado como Camera:
    // no se puede usar para saber si hace falta. Este componente solo va en canvases de pantalla.
    private void EnsureCamera()
    {
        if (canvas == null || !canvas.isRootCanvas || canvas.worldCamera != null)
            return;

        Camera main = Camera.main;
        if (main != null)
            PixelGrid.ConfigureScreenCanvas(canvas, main);
    }
}
