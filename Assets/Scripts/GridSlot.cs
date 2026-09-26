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
    [SerializeField] private SpriteRenderer hoverRenderer;
    [SerializeField] private Color validHoverColor = new Color(0.35f, 1f, 0.35f, 0.6f);
    [SerializeField] private Color invalidHoverColor = new Color(1f, 0.35f, 0.35f, 0.6f);

    [Header("Grid Position")]
    public int gridX;
    public int gridY;

    private Color baseHoverColor = Color.white;
    private bool baseHoverColorCached;

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

    void Awake()
    {
        EnsureHoverRenderer();
        heatGlowPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    // Después de GrillSystem.Update (propagación).
    void LateUpdate()
    {
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
    {
        if (!GrillLayerToggle.IsItemTypeAllowed(incomingType)) return false;

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
        EnsureHoverRenderer();
        if (hoverRenderer != null)
            hoverRenderer.color = isActive ? (isValid ? validHoverColor : invalidHoverColor) : baseHoverColor;
    }

    public void ClearHoverPreview() => SetHoverPreview(false, true);

    public void SetBaseHoverColor(Color color)
    {
        baseHoverColor = color;
        baseHoverColorCached = true;
    }


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
    {
        placementSlots = null;
        List<List<GridSlot>> blocks = new List<List<GridSlot>>();
        CollectContiguousPlacements(allSlots, requiredSize, incomingType, incomingItem, blocks);

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
                        if (!s.CanPlaceItem(incomingType, incomingItem)) { ok = false; break; }
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
        if (hoverRenderer != null && !baseHoverColorCached) { baseHoverColor = hoverRenderer.color; baseHoverColorCached = true; }
    }
}