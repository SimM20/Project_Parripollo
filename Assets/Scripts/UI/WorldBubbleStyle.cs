using TMPro;
using UnityEngine;

/// <summary>
/// Piezas compartidas por las burbujas de mundo que se arman por codigo: la de pedido del
/// cliente (<see cref="CustomerOrderBubble"/>) y la de estado de la carne
/// (<see cref="MeatHoverBubble"/>). Estan aca para que las dos se vean iguales (mismo panel
/// redondeado, mismos textos, mismo ajuste de iconos) sin copiar el codigo en cada una.
/// </summary>
public static class WorldBubbleStyle
{
    // Paleta comun de las burbujas (los mismos valores por defecto que CustomerOrderBubble y
    // MeatHoverBubble): panel crema, borde marron, textos oscuros.
    public static readonly Color PanelColor = new Color(0.97f, 0.94f, 0.86f, 0.97f);
    public static readonly Color BorderColor = new Color(0.29f, 0.19f, 0.13f, 1f);
    public static readonly Color TitleColor = new Color(0.16f, 0.12f, 0.09f, 1f);
    public static readonly Color DetailColor = new Color(0.29f, 0.23f, 0.18f, 1f);

    // Montos y resultados sobre el panel crema. Tienen que ser oscuros: el verde y el amarillo
    // claros de los carteles viejos (#4ADE80, #FACC15) casi no se leian sobre crema.
    public static readonly Color MoneyGood = new Color(0.12f, 0.49f, 0.22f, 1f);   // cobra / propina
    public static readonly Color MoneyWarn = new Color(0.66f, 0.42f, 0.02f, 1f);   // sin propina, pendiente
    public static readonly Color MoneyHalf = new Color(0.78f, 0.34f, 0.05f, 1f);   // cobra la mitad
    public static readonly Color MoneyBad = new Color(0.75f, 0.14f, 0.10f, 1f);    // rechazo, $0, strike

    public static readonly string MoneyGoodHex = "#" + ColorUtility.ToHtmlStringRGB(MoneyGood);
    public static readonly string MoneyWarnHex = "#" + ColorUtility.ToHtmlStringRGB(MoneyWarn);
    public static readonly string MoneyHalfHex = "#" + ColorUtility.ToHtmlStringRGB(MoneyHalf);
    public static readonly string MoneyBadHex = "#" + ColorUtility.ToHtmlStringRGB(MoneyBad);

    private static Sprite panelSprite;

    /// <summary>
    /// Panel redondeado 9-sliced generado por codigo, para no depender de un asset. El borde
    /// es el mismo sprite un poco mas grande por detras.
    /// </summary>
    public static Sprite PanelSprite
    {
        get
        {
            if (panelSprite == null)
                panelSprite = BuildPanelSprite();

            return panelSprite;
        }
    }

    public static SpriteRenderer CreatePanel(Transform parent, string panelName, Color color, Vector2 size)
    {
        var go = new GameObject(panelName);
        go.transform.SetParent(parent, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = PanelSprite;
        sr.color = color;
        sr.drawMode = SpriteDrawMode.Sliced;   // el panel crece sin deformar las esquinas
        sr.size = size;

        return sr;
    }

    public static TextMeshPro CreateText(Transform parent, string textName, float fontSize, FontStyles style, TextAlignmentOptions align)
    {
        var go = new GameObject(textName);
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Truncate;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 1.1f;
        tmp.fontSizeMax = fontSize;
        tmp.margin = Vector4.zero;
        tmp.raycastTarget = false;

        return tmp;
    }

    /// <summary>
    /// Escala y recentra un sprite para que entre en un cuadro de <paramref name="target"/>
    /// sin deformarse. Los sprites del proyecto tienen tamanios y pivots distintos, asi que
    /// se corrige por el centro real de los bounds.
    /// </summary>
    public static void FitSprite(Transform t, Sprite sprite, float target, Vector3 position)
    {
        if (sprite == null)
        {
            t.localPosition = position;
            return;
        }

        // Se ajusta por alto y no por el lado mayor: los iconos de salsas y guarniciones
        // vienen en lienzos de proporciones distintas (la criolla es 2048x1266 y el
        // chimichurri es cuadrado), y normalizar por el lado mayor hacia que los anchos
        // se vieran la mitad de chicos que el resto de la fila. El ancho se limita para
        // que un sprite muy apaisado no se le meta encima al de al lado.
        Vector3 size = sprite.bounds.size;
        float scale = size.y > 0.0001f ? target / size.y : 1f;

        const float maxWidthRatio = 1.3f;
        if (size.x * scale > target * maxWidthRatio)
            scale = target * maxWidthRatio / size.x;

        t.localScale = new Vector3(scale, scale, 1f);

        Vector3 center = sprite.bounds.center * scale;
        t.localPosition = position - new Vector3(center.x, center.y, 0f);
    }

    private static Sprite BuildPanelSprite()
    {
        const int size = 64;
        const int radius = 16;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int cx = (x < radius) ? radius : ((x >= size - radius) ? size - radius - 1 : x);
                int cy = (y < radius) ? radius : ((y >= size - radius) ? size - radius - 1 : y);

                float dx = x - cx;
                float dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();

        var rect = new Rect(0, 0, size, size);
        var pivot = new Vector2(0.5f, 0.5f);
        var border = new Vector4(radius, radius, radius, radius);

        return Sprite.Create(tex, rect, pivot, 100f, 0, SpriteMeshType.FullRect, border);
    }
}
