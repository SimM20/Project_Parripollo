using UnityEngine;

/// <summary>
/// Nivel de salsa de un frasco dibujado sobre su casilla del panel de toppings: 5 bloques
/// apilados que se apagan de arriba hacia abajo a medida que se gasta. El color (verde,
/// amarillo, rojo) lo decide el frasco con <see cref="ToppingDraggable.GetSauceLevelTier"/>.
///
/// Se crea en runtime como hijo de la casilla (<see cref="Attach"/>) y solo se redibuja cuando
/// cambia la cantidad de bloques o el color. El sprite cubre la casilla entera (25x25 px a
/// 25 PPU, el tamaño de HUD Casilla en escena) y deja transparentes los remaches del marco.
/// </summary>
public class SauceLevelIndicator : MonoBehaviour
{
    private const int CellPixels = 25;
    private const int Segments = 5;
    private const int SegmentHeight = 3;
    private const int SegmentGap = 1;
    private const int SegmentBottom = 2;
    private const int SegmentLeft = 3;
    private const int SegmentRight = 21;

    private static readonly Vector2Int[] RivetPixels =
    {
        new Vector2Int(3, 3), new Vector2Int(21, 3), new Vector2Int(3, 20), new Vector2Int(21, 20)
    };

    // Rampas de cada bloque encendido: brillo, claro, base, base, sombra. Índice 0 verde, 1 amarillo, 2 rojo.
    // Apagadas (saturación x0.75, valor x0.85) respecto de la vieja barra para no gritar sobre las botellas.
    private static readonly Color32[][] FillRamps =
    {
        new[] { new Color32(0xC9, 0xD3, 0xA5, 0xFF), new Color32(0x99, 0xBB, 0x60, 0xFF), new Color32(0x6B, 0x98, 0x4B, 0xFF), new Color32(0x6B, 0x98, 0x4B, 0xFF), new Color32(0x42, 0x68, 0x35, 0xFF) },
        new[] { new Color32(0xD9, 0xD2, 0xAF, 0xFF), new Color32(0xD9, 0xC1, 0x65, 0xFF), new Color32(0xCC, 0xA2, 0x4E, 0xFF), new Color32(0xCC, 0xA2, 0x4E, 0xFF), new Color32(0x99, 0x6E, 0x38, 0xFF) },
        new[] { new Color32(0xD9, 0xB2, 0xA5, 0xFF), new Color32(0xCE, 0x6D, 0x5B, 0xFF), new Color32(0xAA, 0x49, 0x48, 0xFF), new Color32(0xAA, 0x49, 0x48, 0xFF), new Color32(0x72, 0x2E, 0x33, 0xFF) },
    };

    private static readonly Color32 Track = new Color32(0x2B, 0x14, 0x16, 0xFF);
    private static readonly Color32 TrackShadow = new Color32(0x1A, 0x0B, 0x0C, 0xFF);

    // [color, bloques encendidos]. Con 0 encendidos el color no importa: se usa siempre la fila 0.
    private static readonly Sprite[,] CachedSprites = new Sprite[3, Segments + 1];

    private ToppingDraggable jar;
    private SpriteRenderer spriteRenderer;
    private int shownLit = -1;
    private int shownTier = -1;

    /// <summary>
    /// Crea (una sola vez) el indicador sobre la casilla y lo engancha al frasco. La casilla baja
    /// su orden de dibujo si hace falta para que el indicador quede entre ella y el frasco.
    /// </summary>
    public static SauceLevelIndicator Attach(SpriteRenderer slot, ToppingDraggable jar, int sortingOrder)
    {
        SauceLevelIndicator indicator = slot.GetComponentInChildren<SauceLevelIndicator>(true);
        if (indicator == null)
        {
            var go = new GameObject("SauceLevel");
            go.transform.SetParent(slot.transform, false);
            indicator = go.AddComponent<SauceLevelIndicator>();
            indicator.spriteRenderer = go.AddComponent<SpriteRenderer>();
        }

        Vector2 size = slot.drawMode == SpriteDrawMode.Simple && slot.sprite != null
            ? (Vector2)slot.sprite.bounds.size
            : slot.size;
        indicator.transform.localScale = new Vector3(size.x, size.y, 1f);

        indicator.spriteRenderer.sortingLayerID = slot.sortingLayerID;
        indicator.spriteRenderer.sortingOrder = sortingOrder;
        slot.sortingOrder = Mathf.Min(slot.sortingOrder, sortingOrder - 1);

        indicator.Bind(jar);
        return indicator;
    }

    void OnDestroy() => Bind(null);

    private void Bind(ToppingDraggable newJar)
    {
        if (jar != null)
            jar.SauceLevelChanged -= Refresh;

        jar = newJar;

        if (jar == null)
            return;

        jar.SauceLevelChanged += Refresh;
        shownLit = -1;
        Refresh();
    }

    private void Refresh()
    {
        float ratio = jar.GetSauceRatio();
        int lit = ratio > 0f ? Mathf.Clamp(Mathf.CeilToInt(ratio * Segments - 0.0001f), 1, Segments) : 0;
        int tier = lit > 0 ? jar.GetSauceLevelTier() : 0;

        if (lit == shownLit && tier == shownTier)
            return;

        shownLit = lit;
        shownTier = tier;
        spriteRenderer.sprite = GetSprite(tier, lit);
    }

    private static Sprite GetSprite(int tier, int lit)
    {
        Sprite cached = CachedSprites[tier, lit];
        if (cached != null)
            return cached;

        Color32[] ramp = FillRamps[tier];
        var pixels = new Color32[CellPixels * CellPixels];

        for (int i = 0; i < Segments; i++)
        {
            int bottom = SegmentBottom + i * (SegmentHeight + SegmentGap);
            bool on = i < lit;

            for (int k = 0; k < SegmentHeight; k++)
            for (int x = SegmentLeft; x <= SegmentRight; x++)
            {
                Color32 c;
                if (on)
                {
                    if (k == SegmentHeight - 1 || (x == SegmentLeft && k > 0)) c = ramp[1];
                    else if (k == 0) c = ramp[4];
                    else c = ramp[2];
                }
                else
                {
                    c = k == SegmentHeight - 1 ? TrackShadow : Track;
                }

                pixels[(bottom + k) * CellPixels + x] = c;
            }
        }

        // Los remaches del marco quedan a la vista.
        foreach (Vector2Int p in RivetPixels)
            pixels[p.y * CellPixels + p.x] = default;

        var tex = new Texture2D(CellPixels, CellPixels, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "SauceLevel_" + tier + "_" + lit
        };
        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        cached = Sprite.Create(tex, new Rect(0, 0, CellPixels, CellPixels), new Vector2(0.5f, 0.5f), CellPixels, 0, SpriteMeshType.FullRect);
        cached.name = tex.name;
        CachedSprites[tier, lit] = cached;
        return cached;
    }
}
