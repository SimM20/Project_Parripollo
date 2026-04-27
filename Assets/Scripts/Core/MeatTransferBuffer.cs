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

    [Header("Build Meat Holder Layout")]
    [Min(1)]
    [SerializeField] private int maxBuildMeatHolder = 2;
    [Min(0.01f)]
    [SerializeField] private float buildMeatHolderWorldSpacing = 1.5f;
    [SerializeField] private Vector3 buildMeatHolderWorldDirection = Vector3.right;

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
            if (sourceCut == null)
                return null;

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
            if (meat == null || meat.cut == null)
                return null;

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
            if (meat == null || cut == null)
                return;

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
    private readonly List<GameObject> plateMeatVisuals = new List<GameObject>();
    private readonly List<GridSlot> meatHolderHoverSlots = new List<GridSlot>();

    void Start()
    {
        meatHolderDraggableType = ResolveType("MeatHolderDraggableMeat");
        RefreshVisuals();
    }

    public void EnqueueToGrill(MeatCutSO cut)
    {
        BufferedMeatData entry = BufferedMeatData.FromCut(cut);
        if (entry == null)
            return;

        toGrillCuts.Add(entry);
        toGrillLocalPositions.Add(GetStackLocalPosition(toGrillLocalPositions.Count));
        RebuildStack(toGrillCuts, toGrillVisuals, toGrillAnchor, toGrillSortingBase, false, toGrillLocalPositions);
    }

    public bool EnqueueToGrillAtPoint(MeatCutSO cut, Vector3 worldPoint)
    {
        BufferedMeatData entry = BufferedMeatData.FromCut(cut);
        if (entry == null)
            return false;

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
        if (entry == null || entry.cut == null)
            return false;

        Transform anchor = ResolveToBuildAnchor();
        if (anchor == null)
            return false;

        toBuildCuts.Add(entry);
        toBuildLocalPositions.Add(WorldToAnchorLocalPosition(worldPoint, anchor));
        RebuildStack(toBuildCuts, toBuildVisuals, anchor, toBuildSortingBase, false, toBuildLocalPositions);
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

    public void ClearBuildMeatHolder()
    {
        buildMeatHolderCuts.Clear();
        buildMeatHolderLocalPositions.Clear();
        RefreshVisuals();
    }

    public void ConsumeBuildMeatEntry(int entryId, GameObject go)
    {
        if (entryId >= 0 && entryId < buildMeatHolderCuts.Count)
            buildMeatHolderCuts.RemoveAt(entryId);

        if (entryId >= 0 && entryId < buildMeatHolderLocalPositions.Count)
            buildMeatHolderLocalPositions.RemoveAt(entryId);

        buildMeatHolderVisuals.Remove(go);

        if (go != null)
        {
            go.transform.SetParent(null, true);
            plateMeatVisuals.Add(go);

            BuildMeatHolderDraggableMeat drag = go.GetComponent<BuildMeatHolderDraggableMeat>();
            if (drag != null)
                Destroy(drag);
        }

        RefreshVisuals();
    }

    public void ClearPlateMeatVisuals()
    {
        for (int i = 0; i < plateMeatVisuals.Count; i++)
        {
            if (plateMeatVisuals[i] != null)
                Destroy(plateMeatVisuals[i]);
        }

        plateMeatVisuals.Clear();
    }

    public void SetPlateMeatVisualsVisible(bool visible)
    {
        for (int i = 0; i < plateMeatVisuals.Count; i++)
        {
            if (plateMeatVisuals[i] != null)
                plateMeatVisuals[i].SetActive(visible);
        }
    }

    public void MoveToBuildMeatHolder()
    {
        if (toBuildCuts.Count == 0)
            return;

        int startIndex = buildMeatHolderCuts.Count;
        int slots = Mathf.Max(0, maxBuildMeatHolder - startIndex);
        int toAdd = Mathf.Min(toBuildCuts.Count, slots);

        for (int i = 0; i < toAdd; i++)
        {
            buildMeatHolderCuts.Add(toBuildCuts[i]);
            buildMeatHolderLocalPositions.Add(GetBuildMeatHolderLocalPosition(startIndex + i));
        }

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
        EnsureBuildMeatHolderDrags();
    }

    public bool TryQueueFromGrillToBuild(Meat meat, Vector3 dropWorldPoint)
    {
        if (meat == null || meat.cut == null)
            return false;

        if (!IsOverToBuild(dropWorldPoint))
            return false;

        BufferedMeatData entry = BufferedMeatData.FromMeat(meat);
        if (!EnqueueToBuildAtPoint(entry, dropWorldPoint))
            return false;

        MeatCutSO cut = meat.cut;
        meat.ReleaseOccupiedSlots();
        Destroy(meat.gameObject);

        string cutName = cut != null ? cut.cutName : "Sin corte";
        Debug.Log("Mandaste a Build desde la parrilla: " + cutName);
        return true;
    }

    public bool TryDropFromMeatHolder(MeatCutSO cut, Vector3 dropWorldPoint)
    {
        return TryDropFromMeatHolder(cut, dropWorldPoint, false);
    }

    public bool TryDropFromMeatHolder(MeatCutSO cut, Vector3 dropWorldPoint, bool rotateFootprint)
    {
        if (cut == null)
            return false;

        return TryDropFromMeatHolderById(FindMeatHolderIndexByCut(cut), dropWorldPoint, rotateFootprint);
    }

    public bool TryDropFromMeatHolderById(int entryId, Vector3 dropWorldPoint)
    {
        return TryDropFromMeatHolderById(entryId, dropWorldPoint, false);
    }

    public bool TryDropFromMeatHolderById(int entryId, Vector3 dropWorldPoint, bool rotateFootprint)
    {
        if (grillSystem == null)
            return false;

        if (entryId < 0 || entryId >= meatHolderCuts.Count)
            return false;

        BufferedMeatData entry = meatHolderCuts[entryId];
        if (entry == null || entry.cut == null)
            return false;

        if (!grillSystem.TrySpawnMeatAtPoint(entry.cut, dropWorldPoint, out Meat spawnedMeat, rotateFootprint))
            return false;

        entry.ApplyTo(spawnedMeat, rotateFootprint);
        meatHolderCuts.RemoveAt(entryId);

        if (entryId >= 0 && entryId < meatHolderLocalPositions.Count)
            meatHolderLocalPositions.RemoveAt(entryId);

        RefreshVisuals();

        string cutName = entry.cut != null ? entry.cut.cutName : "Sin corte";
        Debug.Log("Mandaste a la parrilla desde MeatHolder: " + cutName + " | Estado: " + entry.state);
        return true;
    }

    private int FindMeatHolderIndexByCut(MeatCutSO cut)
    {
        if (cut == null)
            return -1;

        for (int i = 0; i < meatHolderCuts.Count; i++)
        {
            BufferedMeatData entry = meatHolderCuts[i];
            if (entry != null && entry.cut == cut)
                return i;
        }

        return -1;
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

    private bool IsOverToBuild(Vector3 worldPoint)
    {
        SpriteRenderer dropArea = ResolveToBuildDropArea();
        if (dropArea == null)
            return false;

        Vector3 point = worldPoint;
        point.z = dropArea.bounds.center.z;
        return dropArea.bounds.Contains(point);
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

    private SpriteRenderer ResolveToBuildDropArea()
    {
        if (toBuildDropArea != null)
            return toBuildDropArea;

        toBuildDropArea = FindSpriteRendererByNameUnderRoot("ToBuild", "GrillView");
        return toBuildDropArea;
    }

    private Transform ResolveToBuildAnchor()
    {
        if (toBuildAnchor != null)
            return toBuildAnchor;

        SpriteRenderer dropArea = ResolveToBuildDropArea();
        if (dropArea != null)
            toBuildAnchor = dropArea.transform;

        if (toBuildAnchor == null)
            toBuildAnchor = FindTransformByNameUnderRoot("ToBuild", "GrillView");

        return toBuildAnchor;
    }

    private Transform ResolveBuildMeatHolderAnchor()
    {
        if (buildMeatHolderAnchor != null)
            return buildMeatHolderAnchor;

        buildMeatHolderAnchor = FindTransformByNameUnderRoot("MeatHolderSlot", "BuildView");
        if (buildMeatHolderAnchor == null)
            buildMeatHolderAnchor = FindTransformByNameUnderRoot("MeatHolder", "BuildView");

        return buildMeatHolderAnchor;
    }

    private static Transform FindTransformByNameUnderRoot(string targetName, string rootName)
    {
        if (string.IsNullOrEmpty(targetName))
            return null;

        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform candidate = allTransforms[i];
            if (candidate == null || candidate.name != targetName)
                continue;

            if (!IsUnderRoot(candidate, rootName))
                continue;

            return candidate;
        }

        return null;
    }

    private static SpriteRenderer FindSpriteRendererByNameUnderRoot(string targetName, string rootName)
    {
        if (string.IsNullOrEmpty(targetName))
            return null;

        SpriteRenderer[] allRenderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allRenderers.Length; i++)
        {
            SpriteRenderer candidate = allRenderers[i];
            if (candidate == null || candidate.gameObject.name != targetName)
                continue;

            if (!IsUnderRoot(candidate.transform, rootName))
                continue;

            return candidate;
        }

        return null;
    }

    private static bool IsUnderRoot(Transform target, string rootName)
    {
        if (target == null)
            return false;

        if (string.IsNullOrEmpty(rootName))
            return true;

        Transform current = target;
        while (current != null)
        {
            if (current.name == rootName)
                return true;

            current = current.parent;
        }

        return false;
    }

    private void RebuildStack(List<BufferedMeatData> sourceEntries, List<GameObject> visuals, Transform anchor, int sortingBase, bool enableDragToGrill, List<Vector3> localPositions)
    {
        if (anchor == null || visualPrefab == null)
        {
            ClearVisualList(visuals);
            return;
        }

        SyncPositionList(localPositions, sourceEntries.Count);

        while (visuals.Count < sourceEntries.Count)
        {
            GameObject go = Instantiate(visualPrefab, anchor);
            visuals.Add(go);
        }

        while (visuals.Count > sourceEntries.Count)
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
            BufferedMeatData entry = sourceEntries[i];
            MeatCutSO cut = entry != null ? entry.cut : null;

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
                EnsureMeatHolderDrag(go, entry, i);
        }
    }

    private Vector3 GetStackLocalPosition(int index)
    {
        Vector3 direction = stackDirection.sqrMagnitude > 0f ? stackDirection.normalized : Vector3.up;
        return direction * stackSpacing * Mathf.Max(0, index);
    }

    private Vector3 GetBuildMeatHolderLocalPosition(int index)
    {
        Vector3 dir = buildMeatHolderWorldDirection.sqrMagnitude > 0f
            ? buildMeatHolderWorldDirection.normalized
            : Vector3.right;

        Vector3 worldOffset = dir * buildMeatHolderWorldSpacing * Mathf.Max(0, index);

        Transform anchor = ResolveBuildMeatHolderAnchor();
        if (anchor != null)
        {
            Vector3 local = anchor.InverseTransformVector(worldOffset);
            local.z = 0f;
            return local;
        }

        return worldOffset;
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

    private void EnsureBuildMeatHolderDrags()
    {
        for (int i = 0; i < buildMeatHolderVisuals.Count; i++)
        {
            GameObject go = buildMeatHolderVisuals[i];
            if (go == null || i >= buildMeatHolderCuts.Count)
                continue;

            BufferedMeatData entry = buildMeatHolderCuts[i];
            if (entry == null || entry.cut == null)
                continue;

            BuildMeatHolderDraggableMeat drag = go.GetComponent<BuildMeatHolderDraggableMeat>();
            if (drag == null)
                drag = go.AddComponent<BuildMeatHolderDraggableMeat>();

            drag.Setup(entry.cut, this, i);
        }
    }

    private void EnsureMeatHolderDrag(GameObject go, BufferedMeatData entry, int entryId)
    {
        if (go == null || entry == null || entry.cut == null || meatHolderDraggableType == null)
            return;

        Component drag = go.GetComponent(meatHolderDraggableType);
        if (drag == null)
            drag = go.AddComponent(meatHolderDraggableType);

        go.SendMessage("SetCut", entry.cut, SendMessageOptions.DontRequireReceiver);
        go.SendMessage("SetTransferBuffer", this, SendMessageOptions.DontRequireReceiver);
        go.SendMessage("SetTransferEntryId", entryId, SendMessageOptions.DontRequireReceiver);
        go.SendMessage("SetInitialGridRotation", entry.isGridRotated, SendMessageOptions.DontRequireReceiver);
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
