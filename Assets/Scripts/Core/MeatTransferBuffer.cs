using System.Collections.Generic;
using UnityEngine;

public class MeatTransferBuffer : MonoBehaviour
{
    [Header("Systems")]
    [SerializeField] private GrillSystem grillSystem;

    [Header("Anchors")]
    [SerializeField] private Transform toGrillAnchor;
    [SerializeField] private Transform meatHolderAnchor;

    [Header("Visual")]
    [SerializeField] private GameObject visualPrefab;
    [Min(0.01f)]
    [SerializeField] private float stackSpacing = 0.2f;
    [SerializeField] private Vector3 stackDirection = Vector3.up;
    [SerializeField] private int toGrillSortingBase = 100;
    [SerializeField] private int meatHolderSortingBase = 200;

    [Header("Visual Size")]
    [SerializeField] private Vector3 fixedWorldScale = Vector3.one;

    private System.Type meatHolderDraggableType;

    private readonly List<MeatCutSO> toGrillCuts = new List<MeatCutSO>();
    private readonly List<MeatCutSO> meatHolderCuts = new List<MeatCutSO>();
    private readonly List<Vector3> toGrillLocalPositions = new List<Vector3>();
    private readonly List<Vector3> meatHolderLocalPositions = new List<Vector3>();

    private readonly List<GameObject> toGrillVisuals = new List<GameObject>();
    private readonly List<GameObject> meatHolderVisuals = new List<GameObject>();
    private readonly List<GridSlot> meatHolderHoverSlots = new List<GridSlot>();

    void Start()
    {
        meatHolderDraggableType = ResolveType("MeatHolderDraggableMeat");
        RefreshVisuals();
    }

    public void EnqueueToGrill(MeatCutSO cut)
    {
        if (cut == null)
            return;

        toGrillCuts.Add(cut);
        toGrillLocalPositions.Add(GetStackLocalPosition(toGrillLocalPositions.Count));
        RebuildStack(toGrillCuts, toGrillVisuals, toGrillAnchor, toGrillSortingBase, false, toGrillLocalPositions);
    }

    public bool EnqueueToGrillAtPoint(MeatCutSO cut, Vector3 worldPoint)
    {
        if (cut == null)
            return false;

        toGrillCuts.Add(cut);
        toGrillLocalPositions.Add(WorldToAnchorLocalPosition(worldPoint, toGrillAnchor));
        RebuildStack(toGrillCuts, toGrillVisuals, toGrillAnchor, toGrillSortingBase, false, toGrillLocalPositions);
        return true;
    }

    public void MoveToMeatHolder()
    {
        if (toGrillCuts.Count == 0)
            return;

        meatHolderCuts.AddRange(toGrillCuts);
        meatHolderLocalPositions.AddRange(toGrillLocalPositions);
        toGrillCuts.Clear();
        toGrillLocalPositions.Clear();

        RefreshVisuals();
    }

    public void RefreshVisuals()
    {
        RebuildStack(toGrillCuts, toGrillVisuals, toGrillAnchor, toGrillSortingBase, false, toGrillLocalPositions);
        RebuildStack(meatHolderCuts, meatHolderVisuals, meatHolderAnchor, meatHolderSortingBase, true, meatHolderLocalPositions);
    }

    public bool TryDropFromMeatHolder(MeatCutSO cut, Vector3 dropWorldPoint)
    {
        return TryDropFromMeatHolder(cut, dropWorldPoint, false);
    }

    public bool TryDropFromMeatHolder(MeatCutSO cut, Vector3 dropWorldPoint, bool rotateFootprint)
    {
        if (cut == null || grillSystem == null)
            return false;

        if (!grillSystem.TrySpawnMeatAtPoint(cut, dropWorldPoint, rotateFootprint))
            return false;

        int index = meatHolderCuts.IndexOf(cut);
        if (index < 0)
            return false;

        meatHolderCuts.RemoveAt(index);

        if (index >= 0 && index < meatHolderLocalPositions.Count)
            meatHolderLocalPositions.RemoveAt(index);

        RefreshVisuals();

        string cutName = cut != null ? cut.cutName : "Sin corte";
        Debug.Log("Mandaste a la parrilla desde MeatHolder: " + cutName);
        return true;
    }

    public void UpdateMeatHolderHover(MeatCutSO cut, Vector3 worldPoint)
    {
        UpdateMeatHolderHover(cut, worldPoint, false);
    }

    public void UpdateMeatHolderHover(MeatCutSO cut, Vector3 worldPoint, bool rotateFootprint)
    {
        if (cut == null || grillSystem == null)
        {
            ClearMeatHolderHover();
            return;
        }

        Vector2Int requiredSize = ResolveRequiredSize(cut, rotateFootprint);
        if (GridSlot.TryFindContiguousPlacement(grillSystem.slots, requiredSize, worldPoint, ItemType.Meat, null, out List<GridSlot> placementSlots))
        {
            SetMeatHolderHover(placementSlots, true);
            return;
        }

        GridSlot hoveredSlot = GetSlotAtWorldPoint(worldPoint);
        if (hoveredSlot != null)
        {
            List<GridSlot> singleSlot = new List<GridSlot>(1) { hoveredSlot };
            SetMeatHolderHover(singleSlot, false);
            return;
        }

        ClearMeatHolderHover();
    }

    public void ClearMeatHolderHover()
    {
        for (int i = 0; i < meatHolderHoverSlots.Count; i++)
        {
            GridSlot slot = meatHolderHoverSlots[i];
            if (slot != null)
                slot.ClearHoverPreview();
        }

        meatHolderHoverSlots.Clear();
    }

    private void SetMeatHolderHover(List<GridSlot> slots, bool isValid)
    {
        if (slots == null || slots.Count == 0)
        {
            ClearMeatHolderHover();
            return;
        }

        if (meatHolderHoverSlots.Count == slots.Count)
        {
            bool sameSlots = true;
            for (int i = 0; i < meatHolderHoverSlots.Count; i++)
            {
                if (meatHolderHoverSlots[i] != slots[i])
                {
                    sameSlots = false;
                    break;
                }
            }

            if (sameSlots)
            {
                for (int i = 0; i < meatHolderHoverSlots.Count; i++)
                {
                    GridSlot slot = meatHolderHoverSlots[i];
                    if (slot != null)
                        slot.SetHoverPreview(true, isValid);
                }

                return;
            }
        }

        ClearMeatHolderHover();

        for (int i = 0; i < slots.Count; i++)
        {
            GridSlot slot = slots[i];
            if (slot == null)
                continue;

            slot.SetHoverPreview(true, isValid);
            meatHolderHoverSlots.Add(slot);
        }
    }

    private GridSlot GetSlotAtWorldPoint(Vector3 worldPoint)
    {
        if (grillSystem == null || grillSystem.slots == null)
            return null;

        for (int i = 0; i < grillSystem.slots.Count; i++)
        {
            GridSlot slot = grillSystem.slots[i];
            if (slot == null)
                continue;

            Collider2D collider = slot.GetComponent<Collider2D>();
            if (collider == null)
                continue;

            Vector3 point = worldPoint;
            point.z = collider.bounds.center.z;

            if (collider.bounds.Contains(point))
                return slot;
        }

        return null;
    }

    private static Vector2Int ResolveRequiredSize(MeatCutSO cut, bool rotateFootprint)
    {
        if (cut == null)
            return Vector2Int.one;

        Vector2Int size = cut.GrillSpace;
        if (rotateFootprint)
            size = new Vector2Int(size.y, size.x);

        return new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
    }

    private void RebuildStack(List<MeatCutSO> sourceCuts, List<GameObject> visuals, Transform anchor, int sortingBase, bool enableDragToGrill, List<Vector3> localPositions)
    {
        if (anchor == null || visualPrefab == null)
        {
            ClearVisualList(visuals);
            return;
        }

        SyncPositionList(localPositions, sourceCuts.Count);

        while (visuals.Count < sourceCuts.Count)
        {
            GameObject go = Instantiate(visualPrefab, anchor);
            visuals.Add(go);
        }

        while (visuals.Count > sourceCuts.Count)
        {
            int lastIndex = visuals.Count - 1;
            GameObject go = visuals[lastIndex];
            visuals.RemoveAt(lastIndex);

            if (go != null)
                Destroy(go);
        }

        Vector3 direction = stackDirection.sqrMagnitude > 0f ? stackDirection.normalized : Vector3.up;

        for (int i = 0; i < visuals.Count; i++)
        {
            GameObject go = visuals[i];
            MeatCutSO cut = sourceCuts[i];

            if (go == null || cut == null)
                continue;

            if (go.transform.parent != anchor)
                go.transform.SetParent(anchor, false);

            ApplyFixedWorldScale(go.transform);

            if (localPositions != null && i < localPositions.Count)
                go.transform.localPosition = localPositions[i];
            else
                go.transform.localPosition = direction * stackSpacing * i;

            go.transform.localRotation = Quaternion.identity;

            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.sprite = cut.GetDefaultSprite();
                renderer.sortingOrder = sortingBase + i;
            }

            if (enableDragToGrill)
                EnsureMeatHolderDrag(go, cut);
        }
    }

    private Vector3 GetStackLocalPosition(int index)
    {
        Vector3 direction = stackDirection.sqrMagnitude > 0f ? stackDirection.normalized : Vector3.up;
        return direction * stackSpacing * Mathf.Max(0, index);
    }

    private void ApplyFixedWorldScale(Transform target)
    {
        if (target == null)
            return;

        Transform parent = target.parent;
        if (parent == null)
        {
            target.localScale = fixedWorldScale;
            return;
        }

        Vector3 parentScale = parent.lossyScale;
        target.localScale = new Vector3(
            SafeDivide(fixedWorldScale.x, parentScale.x),
            SafeDivide(fixedWorldScale.y, parentScale.y),
            SafeDivide(fixedWorldScale.z, parentScale.z));
    }

    private static float SafeDivide(float value, float divisor)
    {
        return Mathf.Abs(divisor) > 0.0001f ? value / divisor : value;
    }

    private static Vector3 WorldToAnchorLocalPosition(Vector3 worldPoint, Transform anchor)
    {
        if (anchor == null)
            return worldPoint;

        Vector3 localPoint = anchor.InverseTransformPoint(worldPoint);
        localPoint.z = 0f;
        return localPoint;
    }

    private void SyncPositionList(List<Vector3> localPositions, int targetCount)
    {
        if (localPositions == null)
            return;

        while (localPositions.Count > targetCount)
            localPositions.RemoveAt(localPositions.Count - 1);

        while (localPositions.Count < targetCount)
            localPositions.Add(GetStackLocalPosition(localPositions.Count));
    }

    private void EnsureMeatHolderDrag(GameObject go, MeatCutSO cut)
    {
        if (go == null || cut == null || meatHolderDraggableType == null)
            return;

        Component drag = go.GetComponent(meatHolderDraggableType);
        if (drag == null)
            drag = go.AddComponent(meatHolderDraggableType);

        go.SendMessage("SetCut", cut, SendMessageOptions.DontRequireReceiver);
        go.SendMessage("SetTransferBuffer", this, SendMessageOptions.DontRequireReceiver);
    }

    private static System.Type ResolveType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return null;

        System.Type direct = System.Type.GetType(typeName);
        if (direct != null)
            return direct;

        var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            System.Type found = assemblies[i].GetType(typeName);
            if (found != null)
                return found;
        }

        return null;
    }

    private void ClearVisualList(List<GameObject> visuals)
    {
        for (int i = 0; i < visuals.Count; i++)
        {
            if (visuals[i] != null)
                Destroy(visuals[i]);
        }

        visuals.Clear();
    }

    void OnDisable()
    {
        ClearMeatHolderHover();
    }
}
