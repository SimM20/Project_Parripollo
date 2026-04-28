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


    public static bool TryFindContiguousPlacement(IList<GridSlot> allSlots, Vector2Int requiredSize, Vector3 worldPoint, ItemType incomingType, GameObject incomingItem, out List<GridSlot> placementSlots)
    {
        placementSlots = null;
        if (allSlots == null || allSlots.Count == 0) return false;
        int width = Mathf.Max(1, requiredSize.x);
        int height = Mathf.Max(1, requiredSize.y);
        List<GridSlot> validSlots = new List<GridSlot>();
        List<float> allX = new List<float>();
        List<float> allY = new List<float>();
        foreach (var s in allSlots) { if (s != null) { validSlots.Add(s); allX.Add(s.transform.position.x); allY.Add(s.transform.position.y); } }
        if (validSlots.Count == 0) return false;
        List<float> columns = BuildAxisCenters(allX);
        List<float> rows = BuildAxisCenters(allY);
        Dictionary<Vector2Int, GridSlot> slotByCell = new Dictionary<Vector2Int, GridSlot>();
        foreach (var s in validSlots)
        {
            Vector2Int key = new Vector2Int(GetNearestIndex(columns, s.transform.position.x), GetNearestIndex(rows, s.transform.position.y));
            if (!slotByCell.ContainsKey(key)) slotByCell.Add(key, s);
        }
        float bestDist = float.MaxValue;
        List<GridSlot> bestBlock = null;
        for (int c = 0; c <= columns.Count - width; c++)
        {
            for (int r = 0; r <= rows.Count - height; r++)
            {
                List<GridSlot> cand = new List<GridSlot>();
                bool ok = true;
                for (int x = 0; x < width && ok; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (!slotByCell.TryGetValue(new Vector2Int(c + x, r + y), out GridSlot s) || !s.CanPlaceItem(incomingType, incomingItem)) { ok = false; break; }
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

    private static int GetNearestIndex(List<float> axis, float val)
    {
        int best = 0; float d = float.MaxValue;
        for (int i = 0; i < axis.Count; i++) { float cur = Mathf.Abs(axis[i] - val); if (cur < d) { d = cur; best = i; } }
        return best;
    }

    private static List<float> BuildAxisCenters(List<float> raw)
    {
        List<float> res = new List<float>();
        if (raw.Count == 0) return res;
        List<float> sorted = new List<float>(raw); sorted.Sort();
        float tol = 0.1f; float sum = sorted[0]; int count = 1;
        for (int i = 1; i < sorted.Count; i++)
        {
            if (Mathf.Abs(sorted[i] - (sum / count)) <= tol) { sum += sorted[i]; count++; }
            else { res.Add(sum / count); sum = sorted[i]; count = 1; }
        }
        res.Add(sum / count); return res;
    }

    private void EnsureHoverRenderer()
    {
        if (hoverRenderer == null) hoverRenderer = GetComponent<SpriteRenderer>();
        if (hoverRenderer != null && !baseHoverColorCached) { baseHoverColor = hoverRenderer.color; baseHoverColorCached = true; }
    }
}