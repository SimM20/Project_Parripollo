using System.Collections.Generic;
using UnityEngine;

public class CoalTransferBuffer : MonoBehaviour
{
    [Header("Systems")]
    [SerializeField] private GrillSystem grillSystem;

    [Header("Anchors")]
    [SerializeField] private Transform toGrillAnchor;
    [SerializeField] private Transform coalHolderAnchor;

    [Header("Visual")]
    [SerializeField] private GameObject visualPrefab;
    [Min(0.01f)]
    [SerializeField] private float stackSpacing = 0.15f;
    [SerializeField] private Vector3 stackDirection = Vector3.up;
    [SerializeField] private int toGrillSortingBase = 100;
    [SerializeField] private int coalHolderSortingBase = 200;

    [Header("Visual Size")]
    [SerializeField] private Vector3 fixedWorldScale = Vector3.one;

    private System.Type coalHolderDraggableType;

    private class BufferedCoalData
    {
        public CoalSO coalType;
        public float burnTime;
        public CoalStates state;

        public static BufferedCoalData FromSO(CoalSO so)
        {
            if (so == null) return null;
            return new BufferedCoalData { coalType = so, burnTime = 0f, state = CoalStates.Apagado };
        }

        public static BufferedCoalData FromCoal(Coal coal)
        {
            if (coal == null || coal.coalData == null) return null;
            return new BufferedCoalData
            {
                coalType = coal.coalData,
                burnTime = coal.currentBurnTime,
                state = coal.state
            };
        }

        public void ApplyTo(Coal coal)
        {
            if (coal == null || coalType == null) return;
            coal.coalData = coalType;
            coal.currentBurnTime = burnTime;
            coal.state = state;
        }
    }

    private readonly List<BufferedCoalData> toGrillCoals = new List<BufferedCoalData>();
    private readonly List<BufferedCoalData> coalHolderCoals = new List<BufferedCoalData>();
    private readonly List<Vector3> toGrillLocalPositions = new List<Vector3>();
    private readonly List<Vector3> coalHolderLocalPositions = new List<Vector3>();
    private readonly List<GameObject> toGrillVisuals = new List<GameObject>();
    private readonly List<GameObject> coalHolderVisuals = new List<GameObject>();
    private readonly List<GridSlot> coalHolderHoverSlots = new List<GridSlot>();

    void Start()
    {
        coalHolderDraggableType = ResolveType("CoalHolderDraggableCoal");
        RefreshVisuals();
    }

    public void EnqueueToGrill(CoalSO coalSO)
    {
        BufferedCoalData entry = BufferedCoalData.FromSO(coalSO);
        if (entry == null) return;

        toGrillCoals.Add(entry);
        toGrillLocalPositions.Add(GetStackLocalPosition(toGrillLocalPositions.Count));
        RebuildStack(toGrillCoals, toGrillVisuals, toGrillAnchor, toGrillSortingBase, false, toGrillLocalPositions);
    }

    public void UpdateCoalHolderHover(CoalSO coal, Vector3 worldPoint)
    {
        if (coal == null || grillSystem == null)
        {
            ClearCoalHolderHover();
            return;
        }

        Vector2Int requiredSize = Vector2Int.one;

        if (GridSlot.TryFindContiguousPlacement(grillSystem.slots, requiredSize, worldPoint, ItemType.Coal, null, out List<GridSlot> placementSlots))
            SetCoalHolderHover(placementSlots, true);

        else
        {
            GridSlot hoveredSlot = GetSlotAtWorldPoint(worldPoint);
            if (hoveredSlot != null)
                SetCoalHolderHover(new List<GridSlot> { hoveredSlot }, false);

            else
                ClearCoalHolderHover();
        }
    }

    public void ClearCoalHolderHover()
    {
        foreach (var slot in coalHolderHoverSlots)
        {
            if (slot != null) slot.ClearHoverPreview();
        }
        coalHolderHoverSlots.Clear();
    }

    private void SetCoalHolderHover(List<GridSlot> slots, bool isValid)
    {
        ClearCoalHolderHover();
        foreach (var slot in slots)
        {
            if (slot != null)
            {
                slot.SetHoverPreview(true, isValid);
                coalHolderHoverSlots.Add(slot);
            }
        }
    }

    private GridSlot GetSlotAtWorldPoint(Vector3 worldPoint)
    {
        if (grillSystem == null) return null;
        foreach (var slot in grillSystem.slots)
        {
            Collider2D col = slot.GetComponent<Collider2D>();
            if (col != null && col.bounds.Contains(new Vector3(worldPoint.x, worldPoint.y, col.bounds.center.z)))
                return slot;
        }
        return null;
    }

    public void MoveToCoalHolder()
    {
        if (toGrillCoals.Count == 0) return;

        coalHolderCoals.AddRange(toGrillCoals);
        coalHolderLocalPositions.AddRange(toGrillLocalPositions);
        toGrillCoals.Clear();
        toGrillLocalPositions.Clear();

        RefreshVisuals();
    }

    public void RefreshVisuals()
    {
        RebuildStack(toGrillCoals, toGrillVisuals, toGrillAnchor, toGrillSortingBase, false, toGrillLocalPositions);
        RebuildStack(coalHolderCoals, coalHolderVisuals, coalHolderAnchor, coalHolderSortingBase, true, coalHolderLocalPositions);
    }

    public bool TryDropFromCoalHolderById(int entryId, Vector3 dropWorldPoint)
    {
        if (grillSystem == null || entryId < 0 || entryId >= coalHolderCoals.Count)
            return false;

        BufferedCoalData entry = coalHolderCoals[entryId];

        if (grillSystem.TrySpawnCoalAtPoint(entry.coalType, dropWorldPoint, out Coal spawnedCoal))
        {
            entry.ApplyTo(spawnedCoal);
            coalHolderCoals.RemoveAt(entryId);
            if (entryId < coalHolderLocalPositions.Count) coalHolderLocalPositions.RemoveAt(entryId);

            RefreshVisuals();
            return true;
        }
        return false;
    }

    private void RebuildStack(List<BufferedCoalData> sourceEntries, List<GameObject> visuals, Transform anchor, int sortingBase, bool enableDrag, List<Vector3> localPositions)
    {
        if (anchor == null || visualPrefab == null) { ClearVisualList(visuals); return; }

        SyncPositionList(localPositions, sourceEntries.Count);

        while (visuals.Count < sourceEntries.Count) visuals.Add(Instantiate(visualPrefab, anchor));
        while (visuals.Count > sourceEntries.Count)
        {
            int idx = visuals.Count - 1;
            if (visuals[idx] != null) Destroy(visuals[idx]);
            visuals.RemoveAt(idx);
        }

        for (int i = 0; i < visuals.Count; i++)
        {
            GameObject go = visuals[i];
            BufferedCoalData entry = sourceEntries[i];
            if (go == null || entry?.coalType == null) continue;

            go.transform.SetParent(anchor, false);
            ApplyFixedWorldScale(go.transform);
            go.transform.localPosition = localPositions[i];

            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.sprite = entry.coalType.coalSprite;
                renderer.sortingOrder = sortingBase + i;
            }

            if (enableDrag) EnsureCoalDrag(go, entry, i);
        }
    }

    private void EnsureCoalDrag(GameObject go, BufferedCoalData entry, int entryId)
    {
        if (go == null || coalHolderDraggableType == null) return;

        Component drag = go.GetComponent(coalHolderDraggableType) ?? go.AddComponent(coalHolderDraggableType);

        go.SendMessage("SetCoalData", entry.coalType, SendMessageOptions.DontRequireReceiver);
        go.SendMessage("SetTransferBuffer", this, SendMessageOptions.DontRequireReceiver);
        go.SendMessage("SetTransferEntryId", entryId, SendMessageOptions.DontRequireReceiver);
    }

    private Vector3 GetStackLocalPosition(int index) => stackDirection.normalized * stackSpacing * Mathf.Max(0, index);

    private void SyncPositionList(List<Vector3> localPositions, int targetCount)
    {
        while (localPositions.Count > targetCount) localPositions.RemoveAt(localPositions.Count - 1);
        while (localPositions.Count < targetCount) localPositions.Add(GetStackLocalPosition(localPositions.Count));
    }

    private void ApplyFixedWorldScale(Transform target)
    {
        if (target == null || target.parent == null) return;
        Vector3 pScale = target.parent.lossyScale;
        target.localScale = new Vector3(fixedWorldScale.x / pScale.x, fixedWorldScale.y / pScale.y, fixedWorldScale.z / pScale.z);
    }

    private void ClearVisualList(List<GameObject> list) { foreach (var g in list) if (g != null) Destroy(g); list.Clear(); }

    private static System.Type ResolveType(string typeName)
    {
        var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            var t = assembly.GetType(typeName);
            if (t != null) return t;
        }
        return null;
    }
}