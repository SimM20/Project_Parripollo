using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fuente de verdad de la grilla de pixel art: el juego se dibuja a 480×270 (una render texture
/// que la <c>PixelPerfectCamera</c> de URP estira a la pantalla) y 1 px del arte = 1/25 u del mundo.
///
/// La UI de pantalla va en el mismo lienzo: canvases <c>Screen Space - Camera</c> con la cámara de la
/// escena, <c>CanvasScaler</c> a 480×270, así 1 unidad de UI = 1 px de la render texture. Con
/// <c>referencePixelsPerUnit = 25</c>, un sprite importado a PPU 25 se ve a 1 px por px en la UI.
///
/// Texto: Tiny5 (<c>Fonts/Tiny5-Regular Pixel.asset</c>, bitmap a 8 px, filtro Point) a ×1 (em 8 px) o,
/// los títulos, a ×2 (16 px). Otros tamaños deforman las letras.
/// </summary>
public static class PixelGrid
{
    /// <summary>Resolución interna: ancho en px del lienzo del juego.</summary>
    public const int ReferenceWidth = 480;
    /// <summary>Resolución interna: alto en px del lienzo del juego.</summary>
    public const int ReferenceHeight = 270;
    /// <summary>Píxeles del arte por unidad del mundo (cámara: 270 / 25 / 2 = 5,4 de tamaño ortográfico).</summary>
    public const int AssetsPPU = 25;

    /// <summary>Sorting layer de la UI de pantalla: por encima de todo lo del mundo, como el overlay de antes.</summary>
    public const string UiSortingLayer = "UI";
    /// <summary>Distancia del canvas a la cámara. Cualquier valor entre near y far sirve: ordena el sorting layer.</summary>
    public const float UiPlaneDistance = 1f;

    /// <summary>Em de Tiny5 en px a ×1: mayúsculas de 5 px, minúsculas de 4, renglón de 9.</summary>
    public const int FontPixels = 8;
    /// <summary>Desde este em (px) un texto es título y va a ×2; por debajo, ×1.</summary>
    public const float LargeTextPixels = 12f;

    // TextMeshPro de mundo (no UGUI) dibuja el em a fontSize × 0,1 unidades.
    private const float WorldTextUnitsPerFontSize = 0.1f;

    public static Vector2 ReferenceResolution => new Vector2(ReferenceWidth, ReferenceHeight);

    /// <summary>Múltiplo de la fuente (1 o 2) para un texto que hoy mide <paramref name="emPixels"/>.</summary>
    public static int FontScaleFor(float emPixels) => emPixels >= LargeTextPixels ? 2 : 1;

    /// <summary>
    /// Cuántos px de la render texture mide el em del texto. <paramref name="worldScale"/> pisa la escala
    /// del objeto (para textos que se arman antes de que su raíz tenga la escala final); 0 = la actual.
    /// </summary>
    public static float EmPixels(TMP_Text text, float worldScale = 0f)
    {
        if (text is TextMeshProUGUI ui)
        {
            Canvas canvas = ui.canvas != null ? ui.canvas.rootCanvas : null;
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
                return text.fontSize;   // 1 unidad de UI = 1 px (CanvasScaler a 480×270)
        }

        float scale = worldScale > 0f ? worldScale : Mathf.Abs(text.transform.lossyScale.y);
        float units = text is TextMeshProUGUI ? text.fontSize * scale : text.fontSize * WorldTextUnitsPerFontSize * scale;
        return units * AssetsPPU;
    }

    /// <summary>fontSize que da un em de <paramref name="fontScale"/> × <see cref="FontPixels"/> px.</summary>
    public static float FontSizeFor(TMP_Text text, int fontScale, float worldScale = 0f)
    {
        float current = EmPixels(text, worldScale);
        float target = fontScale * FontPixels;
        return current > 0f ? text.fontSize * target / current : text.fontSize;
    }

    /// <summary>
    /// Deja un texto en la grilla: tamaño fijo a ×1 o ×2 (según lo que medía) y sin autosize, que
    /// elige tamaños intermedios. La fuente sale de TMP Settings (Tiny5) si el texto no tiene otra.
    /// </summary>
    public static void SnapFont(TMP_Text text, float worldScale = 0f)
    {
        if (text == null)
            return;

        int fontScale = FontScaleFor(EmPixels(text, worldScale));
        text.enableAutoSizing = false;
        text.fontSize = FontSizeFor(text, fontScale, worldScale);
    }

    /// <summary>
    /// Deja un canvas raíz como UI de pantalla en la grilla: dibujado por la cámara (entra en la render
    /// texture de 480×270) y escalado a la resolución de referencia.
    /// </summary>
    public static void ConfigureScreenCanvas(Canvas canvas, Camera camera = null)
    {
        if (canvas == null)
            return;

        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera != null ? camera : Camera.main;
        canvas.planeDistance = UiPlaneDistance;
        canvas.sortingLayerName = UiSortingLayer;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
            ConfigureScaler(scaler);
    }

    public static void ConfigureScaler(CanvasScaler scaler)
    {
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = AssetsPPU;
    }
}
