using System.Collections.Generic;
using UnityEngine;

public class GridSlot : MonoBehaviour
{
    public ItemType acceptsType = ItemType.Meat;
    public GameObject currentItem;

    [Header("Coal Stacking")]
    public List<Coal> stackedCoals = new List<Coal>();
    private const int MAX_COAL = 3;

    [Header("Heat Values")]
    public float internalHeat;
    public float totalHeatReceived;

    [Header("Hover Preview")]
    [Tooltip("Sprite del slot: da el tamaño de la celda y la sorting layer del preview de colocación.")]
    [SerializeField] private SpriteRenderer hoverRenderer;

    [Header("Grid Position")]
    public int gridX;
    public int gridY;

    // Preview de colocación: config global (la fija GrillSystem). Cada slot cuelga un "PlacementPreview":
    // recuadro redondeado de relleno tenue y borde fino. Va en 9-slice con la escala del slot anulada,
    // así el borde y las esquinas miden lo mismo en X e Y aunque el slot esté aplastado.
    private static Color previewValidColor = new Color(0.9f, 1f, 0.6f, 0.85f);
    private static Color previewInvalidColor = new Color(1f, 0.36f, 0.2f, 0.9f);
    private static float previewInset = 0.88f;
    private static float previewFadeTime = 0.08f;
    private static float previewPulse = 0.12f;
    private static int previewSortingOrder = 0;
    private static Sprite previewSprite;

    private SpriteRenderer placementPreview;
    private Vector2 previewCellSize;
    private Color previewColor;
    private float previewAlpha;
    private bool previewActive;

    // Mapa de calor: config global (la fija GrillSystem desde su inspector). Cada slot de
    // carne cuelga un "HeatGlow": un degradado radial sin bordes, debajo de las barras, que
    // hereda la escala aplastada del slot y por eso sigue la perspectiva de la parrilla.
    // Un rectángulo (el sprite del slot) delataba el escorzo; un resplandor no tiene forma.
    private static bool heatGlowEnabled;
    private static Color heatGlowLowColor = new Color(0.85f, 0.15f, 0.05f);
    private static Color heatGlowHighColor = new Color(1f, 0.7f, 0.2f);
    private static float heatGlowMaxAlpha = 0.75f;
    private static float heatGlowFullHeat = 6f;
    private static float heatGlowScale = 1.6f;
    private static float heatGlowFlicker = 0.12f;
    private static int heatGlowSortingOrder = -1;
    private static Sprite heatGlowSprite;

    private SpriteRenderer heatGlow;
    private float heatGlowPhase;

    public bool IsOccupied => currentItem != null;
    public Meat currentMeat => currentItem != null ? currentItem.GetComponent<Meat>() : null;

    public static void ConfigureHeatGlow(
        bool enabled, Color lowColor, Color highColor, float maxAlpha,
        float fullHeat, float scale, float flicker, int sortingOrder)
    {
        heatGlowEnabled = enabled;
        heatGlowLowColor = lowColor;
        heatGlowHighColor = highColor;
        heatGlowMaxAlpha = Mathf.Clamp01(maxAlpha);
        heatGlowFullHeat = Mathf.Max(0.1f, fullHeat);
        heatGlowScale = Mathf.Max(0.1f, scale);
        heatGlowFlicker = Mathf.Clamp01(flicker);
        heatGlowSortingOrder = sortingOrder;
    }

    public static void ConfigurePlacementPreview(
        Color validColor, Color invalidColor, float inset, float fadeTime, float pulse, int sortingOrder)
    {
        previewValidColor = validColor;
        previewInvalidColor = invalidColor;
        previewInset = Mathf.Clamp(inset, 0.1f, 1f);
        previewFadeTime = Mathf.Max(0f, fadeTime);
        previewPulse = Mathf.Clamp01(pulse);
        previewSortingOrder = sortingOrder;
    }

    void Awake()
    {
        EnsureHoverRenderer();
        heatGlowPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    // Después de GrillSystem.Update (propagación).
    void LateUpdate()
    {
        UpdatePlacementPreview();

        if (acceptsType != ItemType.Meat) return;

        bool show = heatGlowEnabled && GrillLayerToggle.IsItemTypeAllowed(ItemType.Meat);
        if (!show)
        {
            if (heatGlow != null && heatGlow.enabled) heatGlow.enabled = false;
            return;
        }

        if (heatGlow == null) CreateHeatGlow();
        if (heatGlow == null) return;

        float k = Mathf.Clamp01(totalHeatReceived / heatGlowFullHeat);
        if (k <= 0.001f)
        {
            if (heatGlow.enabled) heatGlow.enabled = false;
            return;
        }

        if (!heatGlow.enabled) heatGlow.enabled = true;

        // Titilado de brasa: tres senos de frecuencias no múltiplos entre sí. Una sola onda
        // se lee como un pulso mecánico; sumadas nunca repiten el mismo ciclo. La fase por
        // slot evita que toda la parrilla respire al unísono.
        float t = Time.time;
        float wave = 0.55f * Mathf.Sin(t * 6.1f + heatGlowPhase)
                   + 0.30f * Mathf.Sin(t * 11.7f + heatGlowPhase * 1.7f)
                   + 0.15f * Mathf.Sin(t * 19.3f + heatGlowPhase * 2.3f);   // queda en [-1, 1]

        float flicker = 1f - heatGlowFlicker * (0.5f + 0.5f * wave);

        // La brasa también se aviva y se apaga de tamaño, no solo de brillo.
        float breath = 1f + heatGlowFlicker * 0.35f * wave;

        // Al titilar sube un poco de temperatura: el pico tira hacia el color caliente.
        float temp = Mathf.Clamp01(k + heatGlowFlicker * 0.5f * wave);

        Color c = Color.Lerp(heatGlowLowColor, heatGlowHighColor, temp);
        c.a = k * heatGlowMaxAlpha * flicker;
        heatGlow.color = c;
        heatGlow.sortingOrder = heatGlowSortingOrder;
        heatGlow.transform.localScale = Vector3.one * (heatGlowScale * breath);
    }

    private void CreateHeatGlow()
    {
        if (heatGlowSprite == null) heatGlowSprite = MakeRadialGlowSprite(128);

        var go = new GameObject("HeatGlow");
        go.transform.SetParent(transform, false);   // hereda la escala aplastada del slot
        go.transform.localPosition = Vector3.zero;

        heatGlow = go.AddComponent<SpriteRenderer>();
        heatGlow.sprite = heatGlowSprite;
        heatGlow.sortingLayerID = hoverRenderer != null ? hoverRenderer.sortingLayerID : 0;
        heatGlow.sortingOrder = heatGlowSortingOrder;
        heatGlow.color = Color.clear;
    }

    /// <summary>
    /// Brasa radial: núcleo caliente concentrado sobre un halo ancho de caída suave.
    /// El perfil gaussiano deja una cola larga para que los slots vecinos se fundan en un
    /// campo continuo en vez de leerse como puntos naranjas separados. El RGB va de blanco
    /// en el centro a un naranja profundo en el borde, así el calor tiene temperatura
    /// espacial además de la que aporta el color según el calor del slot.
    /// </summary>
    private static Sprite MakeRadialGlowSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        var pixels = new Color32[size * size];
        float r = size * 0.5f;

        const float HaloFalloff = 4.0f;    // menor = halo más ancho
        const float CoreFalloff = 16f;     // mayor = núcleo más chico
        float haloEdge = Mathf.Exp(-HaloFalloff);   // valor en d=1, para normalizar a 0

        Color edgeTint = new Color(1f, 0.62f, 0.30f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - r) / r;
                float dy = (y + 0.5f - r) / r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                float halo = (Mathf.Exp(-HaloFalloff * d * d) - haloEdge) / (1f - haloEdge);
                halo = Mathf.Max(0f, halo);
                float core = Mathf.Exp(-CoreFalloff * d * d);

                float a = Mathf.Clamp01(0.80f * halo + 0.30f * core);
                a *= 1f - Mathf.Clamp01((d - 0.85f) / 0.15f);   // mata cualquier resto en el borde

                Color rgb = Color.Lerp(Color.white, edgeTint, Mathf.Clamp01(d * 1.25f));

                pixels[y * size + x] = new Color32(
                    (byte)(255f * rgb.r), (byte)(255f * rgb.g), (byte)(255f * rgb.b), (byte)(255f * a));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        // pixelsPerUnit = size → el sprite mide 1x1 unidades: la escala local es el tamaño real.
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private void CreatePlacementPreview()
    {
        if (previewSprite == null) previewSprite = MakePlacementPreviewSprite();

        var go = new GameObject("PlacementPreview");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        // Se anula la escala del slot: el 9-slice trabaja en unidades de mundo y no se deforma.
        Vector3 ls = transform.lossyScale;
        float sx = Mathf.Max(0.0001f, Mathf.Abs(ls.x));
        float sy = Mathf.Max(0.0001f, Mathf.Abs(ls.y));
        go.transform.localScale = new Vector3(1f / sx, 1f / sy, 1f);

        Vector2 spriteSize = hoverRenderer != null && hoverRenderer.sprite != null
            ? (Vector2)hoverRenderer.sprite.bounds.size
            : Vector2.one;
        previewCellSize = new Vector2(spriteSize.x * sx, spriteSize.y * sy);

        placementPreview = go.AddComponent<SpriteRenderer>();
        placementPreview.sprite = previewSprite;
        placementPreview.drawMode = SpriteDrawMode.Sliced;
        placementPreview.sortingLayerID = hoverRenderer != null ? hoverRenderer.sortingLayerID : 0;
        placementPreview.sortingOrder = previewSortingOrder;
        placementPreview.color = Color.clear;
        placementPreview.enabled = false;
    }

    // Fundido corto al aparecer/irse: al moverse de bloque el preview se cruza en vez de saltar.
    private void UpdatePlacementPreview()
    {
        if (placementPreview == null) return;
        if (!previewActive && previewAlpha <= 0f) return;

        float target = previewActive ? 1f : 0f;
        previewAlpha = previewFadeTime > 0f
            ? Mathf.MoveTowards(previewAlpha, target, Time.unscaledDeltaTime / previewFadeTime)
            : target;

        bool visible = previewAlpha > 0f;
        if (placementPreview.enabled != visible) placementPreview.enabled = visible;
        if (!visible) return;

        // Respira apenas: se lee vivo sin llegar a parpadear.
        float breath = 1f - previewPulse * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f));

        Color c = previewColor;
        c.a *= previewAlpha * breath;
        placementPreview.color = c;
        placementPreview.sortingOrder = previewSortingOrder;
    }

    /// <summary>
    /// Recuadro redondeado blanco (el color lo pone el renderer): borde fino con antialias, un
    /// brillo interno que se apaga hacia adentro y un relleno tenue. Todo lo que varía cae dentro
    /// del borde del 9-slice, así el centro estirado queda uniforme.
    /// </summary>
    private static Sprite MakePlacementPreviewSprite()
    {
        const int Size = 32;
        const float Border = 12f;
        const float Radius = 8f;
        const float Rim = 1.5f;
        const float GlowWidth = 6f;
        const float FillAlpha = 0.16f;
        const float GlowAlpha = 0.24f;

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        var pixels = new Color32[Size * Size];
        float half = Size * 0.5f;
        float inner = half - 1f - Radius;   // 1px de margen para el antialias del borde exterior

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                // Distancia con signo al rectángulo redondeado: depth > 0 adentro.
                float qx = Mathf.Abs(x + 0.5f - half) - inner;
                float qy = Mathf.Abs(y + 0.5f - half) - inner;
                float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                float depth = Radius - outside - Mathf.Min(Mathf.Max(qx, qy), 0f);

                float coverage = Mathf.Clamp01(depth + 0.5f);
                float rim = Mathf.Clamp01(Rim + 0.5f - depth);
                float glow = Mathf.Clamp01(1f - (depth - Rim) / GlowWidth);
                glow *= glow;

                float a = coverage * Mathf.Max(rim, FillAlpha + GlowAlpha * glow);
                pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(255f * a));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        // 100 ppu ≈ 1 texel por pixel de pantalla a 1080p: el borde mide ~1.5px.
        return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(Border, Border, Border, Border));
    }

    void Update()
    {
        if (stackedCoals != null)
        {
            for (int i = 0; i < stackedCoals.Count; i++)
            {
                Coal coal = stackedCoals[i];
                if (coal != null)
                    coal.Burn();
            }
        }

        CalculateInternalHeat();

        if (acceptsType == ItemType.Meat && currentItem != null)
        {
            if (currentItem.TryGetComponent<Meat>(out Meat meat))
                meat.Cook(totalHeatReceived);
        }
    }
    public void SetGridPos(int x, int y)
    {
        gridX = x;
        gridY = y;
    }

    private void CalculateInternalHeat()
    {
        internalHeat = 0f;
        for (int i = 0; i < stackedCoals.Count; i++)
        {
            float power = stackedCoals[i].GetCurrentHeatOutput();
            if (i == 0) internalHeat += power;
            else if (i == 1) internalHeat += power * 0.307f;
            else if (i == 2) internalHeat += power * 0.153f;
        }
    }

    public void PlaceItem(GameObject item)
    {
        if (item == null) return;

        if (item.TryGetComponent<Coal>(out Coal newCoal))
        {
            if (stackedCoals.Count < MAX_COAL)
            {
                stackedCoals.Add(newCoal);
                newCoal.RegisterOccupiedSlot(this);
            }
            return;
        }

        if (currentItem != null && currentItem != item) ClearSlot();
        currentItem = item;
        if (item.TryGetComponent<Meat>(out Meat meat)) meat.RegisterOccupiedSlot(this);
    }

    public void RemoveCoal(Coal coal) => stackedCoals.Remove(coal);

    public bool CanPlaceItem(ItemType incomingType, GameObject incomingItem)
        => GrillLayerToggle.IsItemTypeAllowed(incomingType) && Fits(incomingType, incomingItem);

    // Hay lugar para la pieza, sin mirar la capa activa de la parrilla.
    private bool Fits(ItemType incomingType, GameObject incomingItem)
    {
        if (incomingType != acceptsType) return false;
        if (incomingType == ItemType.Coal) return stackedCoals.Count < MAX_COAL;
        return currentItem == null || currentItem == incomingItem;
    }

    public void ResetReceivedHeat() => totalHeatReceived = internalHeat;

    public void AddExternalHeat(float amount) => totalHeatReceived = Mathf.Min(10f, totalHeatReceived + amount);

    public void ClearSlot()
    {
        if (currentItem != null && currentItem.TryGetComponent<Meat>(out Meat currentMeatRef))
            currentMeatRef.UnregisterOccupiedSlot(this);
        currentItem = null;
    }

    public void PlaceMeat(Meat meat) { if (meat != null) PlaceItem(meat.gameObject); }

    public void SetHoverPreview(bool isActive, bool isValid)
    {
        previewActive = isActive;
        if (!isActive) return;   // al apagarse conserva el color mientras se desvanece

        if (placementPreview == null) CreatePlacementPreview();
        previewColor = isValid ? previewValidColor : previewInvalidColor;
        placementPreview.size = previewCellSize * previewInset;
    }

    public void ClearHoverPreview() => SetHoverPreview(false, true);


    public const float AXIS_TOLERANCE = 0.1f;

    // Rows are grouped by world Y, but the column index comes from the left to right order
    // inside each row. Rows can have their own spacing and centering, so world X is never
    // comparable between different rows.
    public static List<List<GridSlot>> BuildLogicalRows(IList<GridSlot> slots)
    {
        List<List<GridSlot>> rows = new List<List<GridSlot>>();
        if (slots == null) return rows;

        List<GridSlot> sorted = new List<GridSlot>();
        foreach (var s in slots) { if (s != null && s.gameObject.activeInHierarchy) sorted.Add(s); }
        if (sorted.Count == 0) return rows;

        sorted.Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y));

        List<GridSlot> currentRow = null;
        float lastY = 0f;
        foreach (var s in sorted)
        {
            float y = s.transform.position.y;
            if (currentRow == null || Mathf.Abs(y - lastY) > AXIS_TOLERANCE)
            {
                currentRow = new List<GridSlot>();
                rows.Add(currentRow);
            }
            currentRow.Add(s);
            lastY = y;
        }

        foreach (var row in rows) row.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        return rows;
    }

    // Each type gets its own column numbering so the nth coal slot of a row always shares
    // a cell with the nth meat slot of that same row, no matter how the two grids are spaced.
    public static void AssignGridCoordinates(IList<GridSlot> slots)
    {
        List<List<GridSlot>> rows = BuildLogicalRows(slots);
        for (int r = 0; r < rows.Count; r++)
        {
            Dictionary<ItemType, int> nextColumn = new Dictionary<ItemType, int>();
            foreach (var s in rows[r])
            {
                nextColumn.TryGetValue(s.acceptsType, out int column);
                s.SetGridPos(column, r);
                nextColumn[s.acceptsType] = column + 1;
            }
        }
    }

    public static bool TryFindContiguousPlacement(IList<GridSlot> allSlots, Vector2Int requiredSize, Vector3 worldPoint, ItemType incomingType, GameObject incomingItem, out List<GridSlot> placementSlots)
        => TryFindNearestPlacement(allSlots, requiredSize, worldPoint, incomingType, incomingItem, false, out placementSlots);

    /// <summary>
    /// Bloque a pintar en el preview de colocación; <paramref name="isValid"/> dice si la pieza se
    /// puede soltar ahí. Con la capa equivocada (carne en modo carbón o al revés) y el puntero sobre
    /// la parrilla, devuelve el bloque donde caería en su capa marcado inválido: el rojo avisa que
    /// así no se puede poner.
    /// </summary>
    public static bool TryFindPreviewPlacement(IList<GridSlot> allSlots, Vector2Int requiredSize, Vector3 worldPoint, ItemType incomingType, GameObject incomingItem, out List<GridSlot> placementSlots, out bool isValid)
    {
        isValid = TryFindContiguousPlacement(allSlots, requiredSize, worldPoint, incomingType, incomingItem, out placementSlots);
        if (isValid || GrillLayerToggle.IsItemTypeAllowed(incomingType)) return isValid;
        if (!IsOverSlotsOfType(allSlots, incomingType, worldPoint)) return false;

        return TryFindNearestPlacement(allSlots, requiredSize, worldPoint, incomingType, incomingItem, true, out placementSlots);
    }

    // Área que cubren los slots de ese tipo. Usa el sprite del slot y no el collider: la capa
    // inactiva tiene los colliders apagados.
    private static bool IsOverSlotsOfType(IList<GridSlot> allSlots, ItemType type, Vector3 worldPoint)
    {
        if (allSlots == null) return false;

        bool any = false;
        Bounds area = default;
        for (int i = 0; i < allSlots.Count; i++)
        {
            GridSlot s = allSlots[i];
            if (s == null || s.acceptsType != type || !s.gameObject.activeInHierarchy) continue;

            Bounds b = s.hoverRenderer != null ? s.hoverRenderer.bounds : new Bounds(s.transform.position, s.transform.lossyScale);
            if (any) area.Encapsulate(b);
            else { area = b; any = true; }
        }

        worldPoint.z = area.center.z;
        return any && area.Contains(worldPoint);
    }

    private static bool TryFindNearestPlacement(IList<GridSlot> allSlots, Vector2Int requiredSize, Vector3 worldPoint, ItemType incomingType, GameObject incomingItem, bool ignoreLayer, out List<GridSlot> placementSlots)
    {
        placementSlots = null;
        List<List<GridSlot>> blocks = new List<List<GridSlot>>();
        CollectContiguousPlacements(allSlots, requiredSize, incomingType, incomingItem, ignoreLayer, blocks);

        float bestDist = float.MaxValue;
        List<GridSlot> bestBlock = null;
        foreach (List<GridSlot> cand in blocks)
        {
            float d = (new Vector2(GetCenter(cand).x, GetCenter(cand).y) - new Vector2(worldPoint.x, worldPoint.y)).sqrMagnitude;
            if (d < bestDist) { bestDist = d; bestBlock = cand; }
        }
        if (bestBlock == null) return false;
        placementSlots = bestBlock; return true;
    }

    /// <summary>
    /// Todos los bloques de <paramref name="requiredSize"/> slots contiguos donde entra la pieza
    /// (mismas reglas que <see cref="TryFindContiguousPlacement"/>, que elige el más cercano de
    /// estos). La navegación con gamepad los recorre como destinos de la carne.
    /// </summary>
    public static void CollectContiguousPlacements(IList<GridSlot> allSlots, Vector2Int requiredSize, ItemType incomingType, GameObject incomingItem, List<List<GridSlot>> into)
        => CollectContiguousPlacements(allSlots, requiredSize, incomingType, incomingItem, false, into);

    private static void CollectContiguousPlacements(IList<GridSlot> allSlots, Vector2Int requiredSize, ItemType incomingType, GameObject incomingItem, bool ignoreLayer, List<List<GridSlot>> into)
    {
        if (allSlots == null || allSlots.Count == 0) return;
        int width = Mathf.Max(1, requiredSize.x);
        int height = Mathf.Max(1, requiredSize.y);
        List<GridSlot> validSlots = new List<GridSlot>();
        foreach (var s in allSlots) { if (s != null && s.acceptsType == incomingType) validSlots.Add(s); }
        if (validSlots.Count == 0) return;
        List<List<GridSlot>> rows = BuildLogicalRows(validSlots);
        if (rows.Count < height) return;
        for (int r = 0; r + height <= rows.Count; r++)
        {
            int usableColumns = int.MaxValue;
            for (int y = 0; y < height; y++) usableColumns = Mathf.Min(usableColumns, rows[r + y].Count);

            for (int c = 0; c + width <= usableColumns; c++)
            {
                List<GridSlot> cand = new List<GridSlot>(width * height);
                bool ok = true;
                for (int y = 0; y < height && ok; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        GridSlot s = rows[r + y][c + x];
                        bool fits = ignoreLayer ? s.Fits(incomingType, incomingItem) : s.CanPlaceItem(incomingType, incomingItem);
                        if (!fits) { ok = false; break; }
                        cand.Add(s);
                    }
                }
                if (ok) into.Add(cand);
            }
        }
    }

    /// <summary>Centro de un bloque de slots: donde queda la pieza al colocarla.</summary>
    public static Vector3 GetBlockCenter(List<GridSlot> slots) => GetCenter(slots);

    private static Vector3 GetCenter(List<GridSlot> slots)
    {
        Vector3 sum = Vector3.zero;
        foreach (var s in slots) sum += s.transform.position;
        return sum / slots.Count;
    }

    private void EnsureHoverRenderer()
    {
        if (hoverRenderer == null) hoverRenderer = GetComponent<SpriteRenderer>();
    }
}