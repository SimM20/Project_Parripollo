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

    public bool IsOccupied => currentItem != null;
    public Meat currentMeat => currentItem != null ? currentItem.GetComponent<Meat>() : null;

    void Awake() => EnsureHoverRenderer();

    void Update()
    {
        foreach (var coal in stackedCoals) coal.Burn();

        CalculateInternalHeat();

        if (acceptsType == ItemType.Meat && currentItem != null)
        {
            if (currentItem.TryGetComponent<Meat>(out Meat meat))
            {
                meat.Cook(totalHeatReceived);
                Debug.Log($"Cocinando {meat.name} con {totalHeatReceived} de calor");
            }
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
        if (allSlots == null || allSlots.Count == 0) return false;
        int width = Mathf.Max(1, requiredSize.x);
        int height = Mathf.Max(1, requiredSize.y);
        List<GridSlot> validSlots = new List<GridSlot>();
        foreach (var s in allSlots) { if (s != null && s.acceptsType == incomingType) validSlots.Add(s); }
        if (validSlots.Count == 0) return false;
        List<List<GridSlot>> rows = BuildLogicalRows(validSlots);
        if (rows.Count < height) return false;
        float bestDist = float.MaxValue;
        List<GridSlot> bestBlock = null;
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
                if (ok)
                {
                    float d = (new Vector2(GetCenter(cand).x, GetCenter(cand).y) - new Vector2(worldPoint.x, worldPoint.y)).sqrMagnitude;
                    if (d < bestDist) { bestDist = d; bestBlock = cand; }
                }
            }
        }
        if (bestBlock == null) return false;
        placementSlots = bestBlock; return true;
    }

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