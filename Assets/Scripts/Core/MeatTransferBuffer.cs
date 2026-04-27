using System.Collections.Generic;
using UnityEngine;

public class MeatTransferBuffer : MonoBehaviour
{
    [Header("Systems")]
    [SerializeField] private GrillSystem grillSystem;
    [SerializeField] private BuildStationSystem buildStationSystem;

    [Header("Anchors")]
    [SerializeField] private Transform toGrillAnchor;
    [SerializeField] private Transform meatHolderAnchor;
    [SerializeField] private Transform toBuildAnchor;
    [SerializeField] private Transform buildMeatHolderAnchor;

    [Header("Drop Areas")]
    [SerializeField] private SpriteRenderer toBuildDropArea;

    [Header("Visual")]
    [SerializeField] private GameObject visualPrefab;
    [Min(0.01f)]
    [SerializeField] private float stackSpacing = 0.2f;
    [SerializeField] private Vector3 stackDirection = Vector3.up;
    [SerializeField] private int toGrillSortingBase = 100;
    [SerializeField] private int meatHolderSortingBase = 200;
    [SerializeField] private int toBuildSortingBase = 300;
    [SerializeField] private int buildMeatHolderSortingBase = 400;

    [Header("Visual Size")]
    [SerializeField] private Vector3 fixedWorldScale = Vector3.one;

    private System.Type meatHolderDraggableType;

    private class BufferedMeatData
    {
        public MeatCutSO cut;
        public float sideACookTime;
        public float sideBCookTime;
        public bool isSideA;
        public MeatStates state;
        public bool isGridRotated;

        public static BufferedMeatData FromCut(MeatCutSO sourceCut)
        {
            if (sourceCut == null) return null;
            return new BufferedMeatData
            {
                cut = sourceCut,
                sideACookTime = 0f,
                sideBCookTime = 0f,
                isSideA = true,
                state = MeatStates.Crudo,
                isGridRotated = false
            };
        }

        public static BufferedMeatData FromMeat(Meat meat)
        {
            if (meat == null || meat.cut == null) return null;
            return new BufferedMeatData
            {
                cut = meat.cut,
                sideACookTime = Mathf.Max(0f, meat.sideACookTime),
                sideBCookTime = Mathf.Max(0f, meat.sideBCookTime),
                isSideA = meat.IsSideAActive,
                state = meat.state,
                isGridRotated = meat.IsGridRotated
            };
        }

        public void ApplyTo(Meat meat, bool rotateFootprint)
        {
            if (meat == null || cut == null) return;
            meat.sideACookTime = Mathf.Max(0f, sideACookTime);
            meat.sideBCookTime = Mathf.Max(0f, sideBCookTime);
            meat.isSideA = isSideA;
            meat.state = state;
            meat.SetGridRotation(rotateFootprint);
            meat.SetCut(cut);
        }
    }

    private readonly List<BufferedMeatData> toGrillCuts = new List<BufferedMeatData>();
    private readonly List<BufferedMeatData> meatHolderCuts = new List<BufferedMeatData>();
    private readonly List<BufferedMeatData> toBuildCuts = new List<BufferedMeatData>();
    private readonly List<BufferedMeatData> buildMeatHolderCuts = new List<BufferedMeatData>();
    private readonly List<Vector3> toGrillLocalPositions = new List<Vector3>();
    private readonly List<Vector3> meatHolderLocalPositions = new List<Vector3>();
    private readonly List<Vector3> toBuildLocalPositions = new List<Vector3>();
    private readonly List<Vector3> buildMeatHolderLocalPositions = new List<Vector3>();

    private readonly List<GameObject> toGrillVisuals = new List<GameObject>();
    private readonly List<GameObject> meatHolderVisuals = new List<GameObject>();
    private readonly List<GameObject> toBuildVisuals = new List<GameObject>();
    private readonly List<GameObject> buildMeatHolderVisuals = new List<GameObject>();
    private readonly List<GridSlot> meatHolderHoverSlots = new List<GridSlot>();

    void Start()
    {
        meatHolderDraggableType = ResolveType("MeatHolderDraggableMeat");
        RefreshVisuals();
    }

    public void EnqueueToGrill(MeatCutSO cut)
    {
        BufferedMeatData entry = BufferedMeatData.FromCut(cut);
        if (entry == null) return;

        toGrillCuts.Add(entry);
        toGrillLocalPositions.Add(GetStackLocalPosition(toGrillLocalPositions.Count));
        RebuildStack(toGrillCuts, toGrillVisuals, toGrillAnchor, toGrillSortingBase, false, toGrillLocalPositions);
    }

    public bool EnqueueToGrillAtPoint(MeatCutSO cut, Vector3 worldPoint)
    {
        BufferedMeatData entry = BufferedMeatData.FromCut(cut);
        if (entry == null) return false;

        toGrillCuts.Add(entry);
        toGrillLocalPositions.Add(WorldToAnchorLocalPosition(worldPoint, toGrillAnchor));
        RebuildStack(toGrillCuts, toGrillVisuals, toGrillAnchor, toGrillSortingBase, false, toGrillLocalPositions);
        return true;
    }

    public bool EnqueueToBuildAtPoint(MeatCutSO cut, Vector3 worldPoint)
    {
        return EnqueueToBuildAtPoint(BufferedMeatData.FromCut(cut), worldPoint);
    }

    private bool EnqueueToBuildAtPoint(BufferedMeatData entry, Vector3 worldPoint)
    {
        if (entry == null || entry.cut == null) return false;

        Transform anchor = ResolveToBuildAnchor();
        if (anchor == null) return false;

        toBuildCuts.Add(entry);
        toBuildLocalPositions.Add(WorldToAnchorLocalPosition(worldPoint, anchor));
        RebuildStack(toBuildCuts, toBuildVisuals, anchor, toBuildSortingBase, false, toBuildLocalPositions);
        return true;
    }

    public void MoveToMeatHolder()
    {
        if (toGrillCuts.Count == 0) return;
        meatHolderCuts.AddRange(toGrillCuts);
        meatHolderLocalPositions.AddRange(toGrillLocalPositions);
        toGrillCuts.Clear();
        toGrillLocalPositions.Clear();
        RefreshVisuals();
    }

    public void ClearBuildMeatHolder()
    {
        buildMeatHolderCuts.Clear();
        buildMeatHolderLocalPositions.Clear();
        RefreshVisuals();
    }

    public void MoveToBuildMeatHolder()
    {
        if (toBuildCuts.Count == 0) return;
        if (buildStationSystem != null)
        {
            buildStationSystem.ClearAssembly();
            foreach (var entry in toBuildCuts)
            {
                if (entry?.cut != null)
                    buildStationSystem.AddCut(entry.cut);
            }
        }

        buildMeatHolderCuts.AddRange(toBuildCuts);
        buildMeatHolderLocalPositions.AddRange(toBuildLocalPositions);
        toBuildCuts.Clear();
        toBuildLocalPositions.Clear();
        RefreshVisuals();
    }

    public void RefreshVisuals()
    {
        RebuildStack(toGrillCuts, toGrillVisuals, toGrillAnchor, toGrillSortingBase, false, toGrillLocalPositions);
        RebuildStack(meatHolderCuts, meatHolderVisuals, meatHolderAnchor, meatHolderSortingBase, true, meatHolderLocalPositions);
        RebuildStack(toBuildCuts, toBuildVisuals, ResolveToBuildAnchor(), toBuildSortingBase, false, toBuildLocalPositions);
        RebuildStack(buildMeatHolderCuts, buildMeatHolderVisuals, ResolveBuildMeatHolderAnchor(), buildMeatHolderSortingBase, false, buildMeatHolderLocalPositions);
    }

    public bool TryQueueFromGrillToBuild(Meat meat, Vector3 dropWorldPoint)
    {
        if (meat == null || meat.cut == null) return false;
        if (!IsOverToBuild(dropWorldPoint)) return false;

        BufferedMeatData entry = BufferedMeatData.FromMeat(meat);
        if (!EnqueueToBuildAtPoint(entry, dropWorldPoint)) return false;

        MeatCutSO cut = meat.cut;
        meat.ReleaseOccupiedSlots();
        Destroy(meat.gameObject);
        Debug.Log("Mandaste a Build desde la parrilla: " + (cut != null ? cut.cutName : "Sin corte"));
        return true;
    }

    public bool TryDropFromMeatHolder(MeatCutSO cut, Vector3 dropWorldPoint) => TryDropFromMeatHolder(cut, dropWorldPoint, false);

    public bool TryDropFromMeatHolder(MeatCutSO cut, Vector3 dropWorldPoint, bool rotateFootprint)
    {
        if (cut == null) return false;
        return TryDropFromMeatHolderById(FindMeatHolderIndexByCut(cut), dropWorldPoint, rotateFootprint);
    }

    public bool TryDropFromMeatHolderById(int entryId, Vector3 dropWorldPoint) => TryDropFromMeatHolderById(entryId, dropWorldPoint, false);

    public bool TryDropFromMeatHolderById(int entryId, Vector3 dropWorldPoint, bool rotateFootprint)
    {
        if (grillSystem == null || entryId < 0 || entryId >= meatHolderCuts.Count) return false;

        BufferedMeatData entry = meatHolderCuts[entryId];
        if (entry == null || entry.cut == null) return false;

        if (!grillSystem.TrySpawnMeatAtPoint(entry.cut, dropWorldPoint, out Meat spawnedMeat, rotateFootprint))
            return false;

        entry.ApplyTo(spawnedMeat, rotateFootprint);
        meatHolderCuts.RemoveAt(entryId);
        if (entryId < meatHolderLocalPositions.Count) meatHolderLocalPositions.RemoveAt(entryId);

        RefreshVisuals();
        Debug.Log("Mandaste a la parrilla desde MeatHolder: " + (entry.cut != null ? entry.cut.cutName : "Sin corte"));
        return true;
    }

    private int FindMeatHolderIndexByCut(MeatCutSO cut)
    {
        if (cut == null) return -1;
        for (int i = 0; i < meatHolderCuts.Count; i++)
            if (meatHolderCuts[i]?.cut == cut) return i;
        return -1;
    }

    public void UpdateMeatHolderHover(MeatCutSO cut, Vector3 worldPoint) => UpdateMeatHolderHover(cut, worldPoint, false);

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
            SetMeatHolderHover(new List<GridSlot> { hoveredSlot }, false);
            return;
        }
        ClearMeatHolderHover();
    }

    public void ClearMeatHolderHover()
    {
        foreach (var slot in meatHolderHoverSlots)
            if (slot != null) slot.ClearHoverPreview();
        meatHolderHoverSlots.Clear();
    }

    private void SetMeatHolderHover(List<GridSlot> slots, bool isValid)
    {
        if (slots == null || slots.Count == 0) { ClearMeatHolderHover(); return; }
        ClearMeatHolderHover();
        foreach (var slot in slots)
        {
            if (slot != null)
            {
                slot.SetHoverPreview(true, isValid);
                meatHolderHoverSlots.Add(slot);
            }
        }
    }

    private GridSlot GetSlotAtWorldPoint(Vector3 worldPoint)
    {
        if (grillSystem?.slots == null) return null;
        foreach (var slot in grillSystem.slots)
        {
            Collider2D collider = slot.GetComponent<Collider2D>();
            if (collider != null && collider.bounds.Contains(new Vector3(worldPoint.x, worldPoint.y, collider.bounds.center.z)))
                return slot;
        }
        return null;
    }

    private bool IsOverToBuild(Vector3 worldPoint)
    {
        SpriteRenderer dropArea = ResolveToBuildDropArea();
        if (dropArea == null) return false;
        return dropArea.bounds.Contains(new Vector3(worldPoint.x, worldPoint.y, dropArea.bounds.center.z));
    }

    private static Vector2Int ResolveRequiredSize(MeatCutSO cut, bool rotateFootprint)
    {
        if (cut == null) return Vector2Int.one;
        Vector2Int size = cut.GrillSpace;
        if (rotateFootprint) size = new Vector2Int(size.y, size.x);
        return new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
    }

    private SpriteRenderer ResolveToBuildDropArea() => toBuildDropArea ??= FindSpriteRendererByNameUnderRoot("ToBuild", "GrillView");
    private Transform ResolveToBuildAnchor() => toBuildAnchor ??= (ResolveToBuildDropArea()?.transform ?? FindTransformByNameUnderRoot("ToBuild", "GrillView"));
    private Transform ResolveBuildMeatHolderAnchor() => buildMeatHolderAnchor ??= (FindTransformByNameUnderRoot("MeatHolderSlot", "BuildView") ?? FindTransformByNameUnderRoot("MeatHolder", "BuildView"));

    private static Transform FindTransformByNameUnderRoot(string targetName, string rootName)
    {
        if (string.IsNullOrEmpty(targetName)) return null;
        var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in allTransforms)
            if (t != null && t.name == targetName && IsUnderRoot(t, rootName)) return t;
        return null;
    }

    private static SpriteRenderer FindSpriteRendererByNameUnderRoot(string targetName, string rootName)
    {
        if (string.IsNullOrEmpty(targetName)) return null;
        var allRenderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var r in allRenderers)
            if (r != null && r.gameObject.name == targetName && IsUnderRoot(r.transform, rootName)) return r;
        return null;
    }

    private static bool IsUnderRoot(Transform target, string rootName)
    {
        if (target == null) return false;
        if (string.IsNullOrEmpty(rootName)) return true;
        Transform current = target;
        while (current != null)
        {
            if (current.name == rootName) return true;
            current = current.parent;
        }
        return false;
    }

    private void RebuildStack(List<BufferedMeatData> sourceEntries, List<GameObject> visuals, Transform anchor, int sortingBase, bool enableDragToGrill, List<Vector3> localPositions)
    {
        if (anchor == null || visualPrefab == null) { ClearVisualList(visuals); return; }
        SyncPositionList(localPositions, sourceEntries.Count);

        while (visuals.Count < sourceEntries.Count) visuals.Add(Instantiate(visualPrefab, anchor));
        while (visuals.Count > sourceEntries.Count)
        {
            int lastIndex = visuals.Count - 1;
            if (visuals[lastIndex] != null) Destroy(visuals[lastIndex]);
            visuals.RemoveAt(lastIndex);
        }

        Vector3 direction = stackDirection.sqrMagnitude > 0f ? stackDirection.normalized : Vector3.up;
        for (int i = 0; i < visuals.Count; i++)
        {
            GameObject go = visuals[i];
            BufferedMeatData entry = sourceEntries[i];
            if (go == null || entry?.cut == null) continue;

            go.transform.SetParent(anchor, false);
            ApplyFixedWorldScale(go.transform);
            go.transform.localPosition = (localPositions != null && i < localPositions.Count) ? localPositions[i] : direction * stackSpacing * i;
            go.transform.localRotation = Quaternion.identity;

            if (go.TryGetComponent<SpriteRenderer>(out var r))
            {
                r.sprite = entry.cut.GetDefaultSprite();
                r.sortingOrder = sortingBase + i;
            }
            if (enableDragToGrill) EnsureMeatHolderDrag(go, entry, i);
        }
    }

    private Vector3 GetStackLocalPosition(int index) => (stackDirection.sqrMagnitude > 0f ? stackDirection.normalized : Vector3.up) * stackSpacing * Mathf.Max(0, index);

    private void ApplyFixedWorldScale(Transform target)
    {
        if (target == null) return;
        Transform parent = target.parent;
        if (parent == null) { target.localScale = fixedWorldScale; return; }
        Vector3 ps = parent.lossyScale;
        target.localScale = new Vector3(SafeDivide(fixedWorldScale.x, ps.x), SafeDivide(fixedWorldScale.y, ps.y), SafeDivide(fixedWorldScale.z, ps.z));
    }

    private static float SafeDivide(float v, float d) => Mathf.Abs(d) > 0.0001f ? v / d : v;
    private static Vector3 WorldToAnchorLocalPosition(Vector3 wp, Transform anchor) => anchor == null ? wp : anchor.InverseTransformPoint(wp);

    private void SyncPositionList(List<Vector3> localPositions, int targetCount)
    {
        if (localPositions == null) return;
        while (localPositions.Count > targetCount) localPositions.RemoveAt(localPositions.Count - 1);
        while (localPositions.Count < targetCount) localPositions.Add(GetStackLocalPosition(localPositions.Count));
    }

    private void EnsureMeatHolderDrag(GameObject go, BufferedMeatData entry, int id)
    {
        if (go == null || entry?.cut == null || meatHolderDraggableType == null) return;
        Component drag = go.GetComponent(meatHolderDraggableType) ?? go.AddComponent(meatHolderDraggableType);
        go.SendMessage("SetCut", entry.cut, SendMessageOptions.DontRequireReceiver);
        go.SendMessage("SetTransferBuffer", this, SendMessageOptions.DontRequireReceiver);
        go.SendMessage("SetTransferEntryId", id, SendMessageOptions.DontRequireReceiver);
        go.SendMessage("SetInitialGridRotation", entry.isGridRotated, SendMessageOptions.DontRequireReceiver);
    }

    private static System.Type ResolveType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return null;
        System.Type direct = System.Type.GetType(typeName);
        if (direct != null) return direct;
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type found = assembly.GetType(typeName);
            if (found != null) return found;
        }
        return null;
    }

    private void ClearVisualList(List<GameObject> visuals)
    {
        foreach (var go in visuals) if (go != null) Destroy(go);
        visuals.Clear();
    }

    void OnDisable() => ClearMeatHolderHover();
}