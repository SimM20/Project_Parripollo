using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lo que se ve del puntero mientras se juega con gamepad (el cursor del sistema se oculta):
///  • Un recuadro de esquinas alrededor del elemento seleccionado con la navegación
///    (amarillo = algo para agarrar o clickear, verde = lugar donde soltar lo agarrado).
///  • La flecha, solo cuando se mueve el cursor libre (stick derecho) o no hay nada seleccionado.
/// Va en el prefab de <see cref="InputManager"/>: arma su propio Canvas overlay por encima
/// de todo, así funciona igual en todas las escenas y con el juego en pausa.
/// Sin sprite asignado, genera la flecha en runtime.
/// </summary>
[RequireComponent(typeof(InputManager))]
public class GamepadCursorView : MonoBehaviour
{
    private const int CanvasSortingOrder = 32000;
    private const float ReferenceScreenHeight = 1080f;

    [Tooltip("Sprite del cursor. Vacío = flecha generada en runtime. El pivot del sprite es la punta.")]
    [SerializeField] private Sprite cursorSprite;
    [Tooltip("Tamaño en píxeles a 1080p; escala con la altura de la pantalla.")]
    [SerializeField] private float size = 40f;
    [Tooltip("Escala mientras se mantiene apretado el botón principal (agarrando algo).")]
    [SerializeField] [Range(0.5f, 1f)] private float pressedScale = 0.85f;
    [SerializeField] private Color tint = Color.white;

    [Header("Recuadro de selección")]
    [SerializeField] private Color focusColor = new Color(1f, 0.8f, 0.2f, 1f);
    [SerializeField] private Color dropColor = new Color(0.55f, 1f, 0.55f, 1f);
    [Tooltip("Grosor de las esquinas en píxeles a 1080p.")]
    [SerializeField] private float frameThickness = 5f;
    [Tooltip("Largo máximo de cada brazo de esquina en píxeles a 1080p.")]
    [SerializeField] private float cornerLength = 26f;
    [Tooltip("Separación entre el elemento y el recuadro, en píxeles a 1080p.")]
    [SerializeField] private float framePadding = 6f;
    [Tooltip("Amplitud del latido del recuadro, en píxeles a 1080p.")]
    [SerializeField] private float pulseAmplitude = 3f;
    [SerializeField] private float pulseSpeed = 6f;

    private RectTransform cursorRect;
    private Image cursorImage;
    // 4 esquinas × 2 brazos: [esquina * 2] horizontal, [esquina * 2 + 1] vertical.
    private readonly Image[] frameBars = new Image[8];
    private readonly Vector2[] frameCorners = new Vector2[4];

    private void Start()
    {
        BuildCanvas();
        Refresh();
    }

    private void LateUpdate() => Refresh();

    private void Refresh()
    {
        if (cursorRect == null) return;

        bool pad = InputManager.UsingGamepad;
        bool focused = pad && InputManager.HasNavFocus;
        bool arrow = pad && (InputManager.FreeCursorActive || (!focused && !InputManager.PrimaryHeld));

        SetEnabled(cursorImage, arrow);
        for (int i = 0; i < frameBars.Length; i++)
            SetEnabled(frameBars[i], focused);

        float unit = Screen.height / ReferenceScreenHeight;

        if (arrow)
        {
            float pixels = size * unit;
            float scale = InputManager.PrimaryHeld ? pressedScale : 1f;
            cursorRect.sizeDelta = new Vector2(pixels, pixels) * scale;
            cursorRect.anchoredPosition = InputManager.PointerPosition;
        }

        if (focused)
            LayoutFrame(InputManager.NavFocus, unit);
    }

    private void LayoutFrame(NavTarget target, float unit)
    {
        float pulse = (Mathf.Sin(Time.unscaledTime * pulseSpeed) * 0.5f + 0.5f) * pulseAmplitude * unit;
        Rect r = target.rect;
        float pad = framePadding * unit + pulse;
        r = Rect.MinMaxRect(r.xMin - pad, r.yMin - pad, r.xMax + pad, r.yMax + pad);

        float thickness = Mathf.Max(2f, frameThickness * unit);
        float arm = Mathf.Min(cornerLength * unit, Mathf.Min(r.width, r.height) * 0.45f);
        arm = Mathf.Max(arm, thickness * 2f);
        Color color = target.isDropTarget ? dropColor : focusColor;

        frameCorners[0] = new Vector2(r.xMin, r.yMin);
        frameCorners[1] = new Vector2(r.xMax, r.yMin);
        frameCorners[2] = new Vector2(r.xMin, r.yMax);
        frameCorners[3] = new Vector2(r.xMax, r.yMax);
        for (int c = 0; c < 4; c++)
        {
            // Cada brazo sale de la esquina hacia adentro del recuadro.
            float dx = c % 2 == 0 ? 1f : -1f;
            float dy = c < 2 ? 1f : -1f;
            Vector2 corner = frameCorners[c];

            PlaceBar(frameBars[c * 2], corner, new Vector2(arm * dx, thickness * dy), color);
            PlaceBar(frameBars[c * 2 + 1], corner, new Vector2(thickness * dx, arm * dy), color);
        }
    }

    private static void PlaceBar(Image bar, Vector2 corner, Vector2 extent, Color color)
    {
        RectTransform rt = bar.rectTransform;
        rt.anchoredPosition = new Vector2(Mathf.Min(corner.x, corner.x + extent.x), Mathf.Min(corner.y, corner.y + extent.y));
        rt.sizeDelta = new Vector2(Mathf.Abs(extent.x), Mathf.Abs(extent.y));
        bar.color = color;
    }

    private static void SetEnabled(Image image, bool enabled)
    {
        if (image != null && image.enabled != enabled)
            image.enabled = enabled;
    }

    private void BuildCanvas()
    {
        GameObject canvasGo = new GameObject("GamepadCursorCanvas", typeof(RectTransform), typeof(Canvas));
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortingOrder;
        canvas.overrideSorting = true;

        for (int i = 0; i < frameBars.Length; i++)
        {
            GameObject barGo = new GameObject("FocusFrame" + i, typeof(RectTransform), typeof(Image));
            barGo.transform.SetParent(canvasGo.transform, false);
            RectTransform barRect = barGo.GetComponent<RectTransform>();
            barRect.anchorMin = Vector2.zero;
            barRect.anchorMax = Vector2.zero;
            barRect.pivot = Vector2.zero;
            frameBars[i] = barGo.GetComponent<Image>();
            frameBars[i].raycastTarget = false;
            frameBars[i].enabled = false;
        }

        GameObject cursorGo = new GameObject("Cursor", typeof(RectTransform), typeof(Image));
        cursorGo.transform.SetParent(canvasGo.transform, false);

        Sprite sprite = cursorSprite != null ? cursorSprite : CreateArrowSprite();

        cursorRect = cursorGo.GetComponent<RectTransform>();
        cursorRect.anchorMin = Vector2.zero;
        cursorRect.anchorMax = Vector2.zero;
        // La punta del sprite cae exactamente en la posición del puntero.
        cursorRect.pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);

        cursorImage = cursorGo.GetComponent<Image>();
        cursorImage.sprite = sprite;
        cursorImage.color = tint;
        cursorImage.raycastTarget = false;
        cursorImage.preserveAspect = true;
    }

    /// <summary>Flecha clásica (relleno blanco, borde oscuro) rasterizada con antialias.</summary>
    private static Sprite CreateArrowSprite()
    {
        const int Res = 64;
        const int Samples = 4;
        const float Outline = 3.2f;

        // Contorno en coordenadas del lienzo, y hacia abajo desde la punta (2,2).
        Vector2[] poly =
        {
            new Vector2(4, 3), new Vector2(4, 50), new Vector2(15, 39), new Vector2(23, 57),
            new Vector2(31, 53), new Vector2(23, 36), new Vector2(38, 36),
        };

        Color fill = Color.white;
        Color border = new Color(0.1f, 0.08f, 0.07f, 1f);

        Texture2D tex = new Texture2D(Res, Res, TextureFormat.RGBA32, false)
        {
            name = "GamepadCursorArrow",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        Color[] pixels = new Color[Res * Res];
        for (int y = 0; y < Res; y++)
        {
            for (int x = 0; x < Res; x++)
            {
                float fillHits = 0f, borderHits = 0f;
                for (int sy = 0; sy < Samples; sy++)
                {
                    for (int sx = 0; sx < Samples; sx++)
                    {
                        Vector2 p = new Vector2(x + (sx + 0.5f) / Samples, y + (sy + 0.5f) / Samples);
                        bool inside = Contains(poly, p);
                        float edge = DistanceToEdges(poly, p);
                        if (inside && edge > Outline * 0.5f) fillHits++;
                        else if (inside || edge <= Outline * 0.5f) borderHits++;
                    }
                }

                float total = Samples * Samples;
                float a = (fillHits + borderHits) / total;
                Color c = a > 0f ? Color.Lerp(border, fill, fillHits / (fillHits + borderHits)) : Color.clear;
                c.a = a;
                // La textura va con y hacia arriba: el polígono está dibujado con y hacia abajo.
                pixels[(Res - 1 - y) * Res + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, true);

        // Pivot en la punta de la flecha.
        Vector2 pivot = new Vector2(4f / Res, 1f - 3f / Res);
        return Sprite.Create(tex, new Rect(0, 0, Res, Res), pivot, 100f);
    }

    private static bool Contains(Vector2[] poly, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].y > p.y) != (poly[j].y > p.y)
                && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        }
        return inside;
    }

    private static float DistanceToEdges(Vector2[] poly, Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            Vector2 a = poly[j], b = poly[i];
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
        }
        return best;
    }
}
