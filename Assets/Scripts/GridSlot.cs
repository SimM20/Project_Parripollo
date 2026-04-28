using System.Collections.Generic;
using UnityEngine;

public class GridSlot : MonoBehaviour
{
    public ItemType acceptsType = ItemType.Meat;
    public GameObject currentItem;

    [Header("Hover Preview")]
    [SerializeField] private SpriteRenderer hoverRenderer;
    [SerializeField] private Color validHoverColor = new Color(0.35f, 1f, 0.35f, 0.6f);
    [SerializeField] private Color invalidHoverColor = new Color(1f, 0.35f, 0.35f, 0.6f);

    private Color baseHoverColor = Color.white;
    private bool baseHoverColorCached;

    public bool IsOccupied => currentItem != null;
    public Meat currentMeat => currentItem != null ? currentItem.GetComponent<Meat>() : null;

    void Awake() => EnsureHoverRenderer();

    void Update()
    {
        if (acceptsType != ItemType.Meat || currentItem == null)
            return;

        if (currentItem.TryGetComponent<Meat>(out Meat meat))
            meat.Cook(1f);

        if (currentItem.TryGetComponent<Coal>(out Coal coal))
            coal.Burn();
    }

    public bool CanPlaceItem(ItemType incomingType)
    {
        return CanPlaceItem(incomingType, null);
    }

    public bool CanPlaceItem(ItemType incomingType, GameObject incomingItem)
    {
        if (incomingType != acceptsType)
            return false;

        return currentItem == null || currentItem == incomingItem;
    }

    public void ClearSlot()
    {
        if (currentItem != null && currentItem.TryGetComponent<Meat>(out Meat currentMeatRef))
            currentMeatRef.UnregisterOccupiedSlot(this);

        currentItem = null;
    }

    public void PlaceItem(GameObject item)
    {
        if (item == null)
            return;

        if (currentItem != null && currentItem != item)
            ClearSlot();

        currentItem = item;
        //item.transform.position = transform.position;

        if (item.TryGetComponent<Meat>(out Meat meat))
            meat.RegisterOccupiedSlot(this);
    }

    public void PlaceMeat(Meat meat)
    {
        if (meat == null)
            return;

        PlaceItem(meat.gameObject);
    }

    public void SetHoverPreview(bool isActive, bool isValid)
    {
        EnsureHoverRenderer();
        if (hoverRenderer == null)
            return;

        hoverRenderer.color = isActive ? (isValid ? validHoverColor : invalidHoverColor) : baseHoverColor;
    }

    public void ClearHoverPreview() => SetHoverPreview(false, true);

    public static bool TryFindContiguousPlacement(
        IList<GridSlot> allSlots,
        Vector2Int requiredSize,
        Vector3 worldPoint,
        ItemType incomingType,
        GameObject incomingItem,
        out List<GridSlot> placementSlots)
    {
        placementSlots = null;

        if (allSlots == null || allSlots.Count == 0)
            return false;

        int width = Mathf.Max(1, requiredSize.x);
        int height = Mathf.Max(1, requiredSize.y);

        List<GridSlot> validSlots = new List<GridSlot>();
        List<float> allX = new List<float>();
        List<float> allY = new List<float>();

        for (int i = 0; i < allSlots.Count; i++)
        {
            GridSlot slot = allSlots[i];
            if (slot == null)
                continue;

            validSlots.Add(slot);
            allX.Add(slot.transform.position.x);
            allY.Add(slot.transform.position.y);
        }

        if (validSlots.Count == 0)
            return false;

        List<float> columns = BuildAxisCenters(allX);
        List<float> rows = BuildAxisCenters(allY);

        if (columns.Count == 0 || rows.Count == 0)
            return false;

        Dictionary<Vector2Int, GridSlot> slotByCell = new Dictionary<Vector2Int, GridSlot>();

        for (int i = 0; i < validSlots.Count; i++)
        {
            GridSlot slot = validSlots[i];
            int col = GetNearestIndex(columns, slot.transform.position.x);
            int row = GetNearestIndex(rows, slot.transform.position.y);

            Vector2Int key = new Vector2Int(col, row);
            if (!slotByCell.ContainsKey(key))
                slotByCell.Add(key, slot);
        }

        float bestDistance = float.MaxValue;
        List<GridSlot> bestBlock = null;

        for (int colStart = 0; colStart <= columns.Count - width; colStart++)
        {
            for (int rowStart = 0; rowStart <= rows.Count - height; rowStart++)
            {
                List<GridSlot> candidate = new List<GridSlot>(width * height);
                bool validBlock = true;

                for (int x = 0; x < width && validBlock; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        Vector2Int cell = new Vector2Int(colStart + x, rowStart + y);
                        if (!slotByCell.TryGetValue(cell, out GridSlot slot))
                        {
                            validBlock = false;
                            break;
                        }

                        if (!slot.CanPlaceItem(incomingType, incomingItem))
                        {
                            validBlock = false;
                            break;
                        }

                        candidate.Add(slot);
                    }
                }

                if (!validBlock)
                    continue;

                Vector3 center = GetCenter(candidate);
                float sqrDistance = (new Vector2(center.x, center.y) - new Vector2(worldPoint.x, worldPoint.y)).sqrMagnitude;

                if (sqrDistance < bestDistance)
                {
                    bestDistance = sqrDistance;
                    bestBlock = candidate;
                }
            }
        }

        if (bestBlock == null)
            return false;

        placementSlots = bestBlock;
        return true;
    }

    private static Vector3 GetCenter(List<GridSlot> slots)
    {
        if (slots == null || slots.Count == 0)
            return Vector3.zero;

        Vector3 sum = Vector3.zero;
        for (int i = 0; i < slots.Count; i++)
            sum += slots[i].transform.position;

        return sum / slots.Count;
    }

    private static int GetNearestIndex(List<float> axis, float value)
    {
        int bestIndex = 0;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < axis.Count; i++)
        {
            float distance = Mathf.Abs(axis[i] - value);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static List<float> BuildAxisCenters(List<float> rawValues)
    {
        List<float> result = new List<float>();
        if (rawValues == null || rawValues.Count == 0)
            return result;

        List<float> sorted = new List<float>(rawValues);
        sorted.Sort();

        float tolerance = EstimateAxisTolerance(sorted);

        float bucketSum = sorted[0];
        int bucketCount = 1;
        float bucketCenter = sorted[0];

        for (int i = 1; i < sorted.Count; i++)
        {
            float value = sorted[i];
            if (Mathf.Abs(value - bucketCenter) <= tolerance)
            {
                bucketSum += value;
                bucketCount++;
                bucketCenter = bucketSum / bucketCount;
            }
            else
            {
                result.Add(bucketCenter);
                bucketSum = value;
                bucketCount = 1;
                bucketCenter = value;
            }
        }

        result.Add(bucketCenter);
        return result;
    }

    private static float EstimateAxisTolerance(List<float> sorted)
    {
        float minPositiveDelta = float.MaxValue;

        for (int i = 1; i < sorted.Count; i++)
        {
            float delta = sorted[i] - sorted[i - 1];
            if (delta > 0.0001f && delta < minPositiveDelta)
                minPositiveDelta = delta;
        }

        if (minPositiveDelta < float.MaxValue)
            return Mathf.Max(0.001f, minPositiveDelta * 0.35f);

        return 0.05f;
    }

    private void EnsureHoverRenderer()
    {
        if (hoverRenderer == null)
            hoverRenderer = GetComponent<SpriteRenderer>();

        if (hoverRenderer != null && !baseHoverColorCached)
        {
            baseHoverColor = hoverRenderer.color;
            baseHoverColorCached = true;
        }
    }

    void OnValidate() => EnsureHoverRenderer();
}