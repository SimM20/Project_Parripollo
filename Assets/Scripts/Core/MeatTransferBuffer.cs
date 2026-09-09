using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Staging de cortes fuera de la parrilla, dentro de la vista Parrilla.
///
/// Dos destinos:
///   Plato    — cortes ya montados en el armado (BuildStationSystem). Entran arrastrando
///              carne desde la parrilla a la zona del plato, o desde la bandeja.
///   Bandeja  — cortes que salieron del plato (undo o devolución por arrastre). Se pueden
///              volver a llevar al plato o de vuelta a la parrilla, conservando la cocción.
///
/// La cola ToGrill/MeatHolder es legado de la Cooler View deprecada: ya no la alimenta nadie.
/// </summary>
public class MeatTransferBuffer : MonoBehaviour
{
    [Header("Systems")]
    [SerializeField] private GrillSystem grillSystem;
    [SerializeField] private BuildStationSystem buildStationSystem;

    [Header("Anchors")]
    [SerializeField] private Transform toGrillAnchor;
    [SerializeField] private Transform meatHolderAnchor;
    [Tooltip("Bandeja de cortes devueltos del plato. Vive dentro de GrillView.")]
    [SerializeField] private Transform trayAnchor;

    [Header("Visual")]
    [SerializeField] private GameObject visualPrefab;
    [Min(0.01f)]
    [SerializeField] private float stackSpacing = 0.2f;
    [SerializeField] private Vector3 stackDirection = Vector3.up;
    [SerializeField] private int toGrillSortingBase = 100;
    [SerializeField] private int meatHolderSortingBase = 200;
    [SerializeField] private int traySortingBase = 400;
    [Tooltip("Orden de dibujo de la carne sobre el plato. Debe quedar por debajo de los visuales de acompañamientos/toppings de BuildFoodDropZone.")]
    [SerializeField] private int plateMeatSortingBase = 400;

    [Header("Tray Layout")]
    [Min(0.01f)]
    [SerializeField] private float trayWorldSpacing = 1.5f;
    [SerializeField] private Vector3 trayWorldDirection = Vector3.right;

    [Header("Variant Sprite (Plate)")]
    [SerializeField] private Vector3 variantSpriteScale = new Vector3(0.12f, 0.12f, 0.12f);
    [SerializeField] private Vector3 variantSpriteRotation = new Vector3(0f, 0f, 90f);

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

        public MeatStates SideAState => cut != null ? cut.GetStateForHeat(sideACookTime) : MeatStates.Crudo;
        public MeatStates SideBState => cut != null ? cut.GetStateForHeat(sideBCookTime) : MeatStates.Crudo;

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
            meat.SetGridRotation(rotateFootprint);
            meat.SetCut(cut);
            meat.RefreshState();
        }
    }

    private readonly List<BufferedMeatData> toGrillCuts = new List<BufferedMeatData>();
    private readonly List<BufferedMeatData> meatHolderCuts = new List<BufferedMeatData>();
    private readonly List<BufferedMeatData> trayCuts = new List<BufferedMeatData>();
    private readonly List<Vector3> toGrillLocalPositions = new List<Vector3>();
    private readonly List<Vector3> meatHolderLocalPositions = new List<Vector3>();
    private readonly List<Vector3> trayLocalPositions = new List<Vector3>();

    private readonly List<GameObject> toGrillVisuals = new List<GameObject>();
    private readonly List<GameObject> meatHolderVisuals = new List<GameObject>();
    private readonly List<GameObject> trayVisuals = new List<GameObject>();
    private readonly List<GameObject> plateMeatVisuals = new List<GameObject>();
    private readonly List<BufferedMeatData> plateMeatCuts = new List<BufferedMeatData>();
    private readonly List<GridSlot> meatHolderHoverSlots = new List<GridSlot>();

    void Start()
    {
        meatHolderDraggableType = ResolveType("MeatHolderDraggableMeat");
        RefreshVisuals();
    }

    // ── Cola legado ToGrill → MeatHolder (Cooler View deprecada) ────────────

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

    public void MoveToMeatHolder()
    {
        if (toGrillCuts.Count > 0)
        {
            meatHolderCuts.AddRange(toGrillCuts);
            meatHolderLocalPositions.AddRange(toGrillLocalPositions);
            toGrillCuts.Clear();
            toGrillLocalPositions.Clear();
        }

        RefreshVisuals();
    }

    // ── Parrilla → Plato ────────────────────────────────────────────────────

    /// <summary>
    /// Monta directamente en el plato un corte arrastrado desde la parrilla.
    /// Devuelve false si el punto no cae sobre la zona del plato (el corte queda intacto).
    /// Conserva los tiempos de cocción de ambas caras.
    /// </summary>
    public bool TryPlateMeatFromGrill(Meat meat, Vector3 dropWorldPoint)
    {
        if (meat == null || meat.cut == null)
            return false;

        BufferedMeatData entry = BufferedMeatData.FromMeat(meat);
        if (entry == null)
            return false;

        // TryAcceptMeatAt solo muta el armado si el punto cae dentro de la zona del plato.
        if (!BuildFoodDropZone.TryAcceptMeatAt(dropWorldPoint, entry.cut, entry.state, entry.SideAState, entry.SideBState))
            return false;

        MeatCutSO cut = meat.cut;
        meat.ReleaseOccupiedSlots();
        Destroy(meat.gameObject);

        GameObject visual = AdoptVisualIntoPlate(entry, null, dropWorldPoint);
        BuildUndoHistory.Instance?.Push(new AddMeatUndoAction(this, visual));

        Debug.Log("[Plato] Corte montado desde la parrilla: " + (cut != null ? cut.cutName : "Sin corte")
                  + " | A: " + entry.SideAState + " | B: " + entry.SideBState);

        TutorialManager.NotifyMeatDraggedToBuild(cut);
        TutorialManager.NotifyMeatPlacedOnBuildZone(cut);
        return true;
    }

    // ── Bandeja → Plato / Parrilla ──────────────────────────────────────────

    /// <summary>Monta en el plato un corte que estaba en la bandeja. False si el punto no cae en la zona del plato.</summary>
    public bool TryPlateFromTrayById(int entryId, Vector3 dropWorldPoint)
    {
        if (entryId < 0 || entryId >= trayCuts.Count)
            return false;

        BufferedMeatData entry = trayCuts[entryId];
        if (entry == null || entry.cut == null)
            return false;

        if (!BuildFoodDropZone.TryAcceptMeatAt(dropWorldPoint, entry.cut, entry.state, entry.SideAState, entry.SideBState))
            return false;

        GameObject visual = entryId < trayVisuals.Count ? trayVisuals[entryId] : null;
        RemoveTrayEntryAt(entryId, false);

        GameObject plated = AdoptVisualIntoPlate(entry, visual, dropWorldPoint);
        BuildUndoHistory.Instance?.Push(new AddMeatUndoAction(this, plated));

        RefreshVisuals();

        Debug.Log("[Plato] Corte montado desde la bandeja: " + entry.cut.cutName + " | Estado: " + entry.state);
        TutorialManager.NotifyMeatPlacedOnBuildZone(entry.cut);
        return true;
    }

    /// <summary>Devuelve a la parrilla un corte de la bandeja, restaurando sus tiempos de cocción.</summary>
    public bool TryDropFromTrayById(int entryId, Vector3 dropWorldPoint, bool rotateFootprint)
    {
        if (grillSystem == null)
            return false;

        if (entryId < 0 || entryId >= trayCuts.Count)
            return false;

        BufferedMeatData entry = trayCuts[entryId];
        if (entry == null || entry.cut == null)
            return false;

        if (!grillSystem.TrySpawnMeatAtPoint(entry.cut, dropWorldPoint, out Meat spawnedMeat, rotateFootprint))
            return false;

        entry.ApplyTo(spawnedMeat, rotateFootprint);
        RemoveTrayEntryAt(entryId, true);

        RefreshVisuals();

        Debug.Log("[Bandeja] Corte devuelto a la parrilla: " + entry.cut.cutName + " | Estado: " + entry.state);
        TutorialManager.NotifyMeatPlacedOnGrill(entry.cut);
        return true;
    }

    /// <summary>
    /// Retorna la carne del plato a la bandeja, restaurando sus tiempos de cocción originales.
    /// Usado tanto por arrastre directo (drop sobre la bandeja) como por el Rollback/Undo.
    /// </summary>
    public bool TryReturnPlateMeatToTray(GameObject plateVisual = null)
    {
        if (!TryResolvePlateEntry(plateVisual, out int index, out BufferedMeatData returnedData))
            return false;

        RemovePlateEntryAt(index);

        int newIndex = trayCuts.Count;
        trayCuts.Add(returnedData);
        trayLocalPositions.Add(GetTrayLocalPosition(newIndex));

        RefreshVisuals();

        string cutName = returnedData.cut != null ? returnedData.cut.cutName : "Sin corte";
        Debug.Log("[Bandeja] Carne devuelta desde el plato: " + cutName + " | Estado: " + returnedData.state);
        return true;
    }

    /// <summary>
    /// Devuelve a la parrilla la carne del plato, restaurando tiempos de coccion y rotacion.
    /// False si el punto no cae en un hueco libre de la grilla: el corte se queda en el plato.
    /// </summary>
    public bool TryReturnPlateMeatToGrill(GameObject plateVisual, Vector3 dropWorldPoint)
    {
        if (grillSystem == null)
            return false;

        if (!TryResolvePlateEntry(plateVisual, out int index, out BufferedMeatData returnedData))
            return false;

        if (returnedData.cut == null)
            return false;

        bool rotateFootprint = returnedData.isGridRotated;
        if (!grillSystem.TrySpawnMeatAtPoint(returnedData.cut, dropWorldPoint, out Meat spawnedMeat, rotateFootprint))
            return false;

        returnedData.ApplyTo(spawnedMeat, rotateFootprint);
        RemovePlateEntryAt(index);

        RefreshVisuals();

        Debug.Log("[Plato] Carne devuelta a la parrilla: " + returnedData.cut.cutName + " | Estado: " + returnedData.state);
        TutorialManager.NotifyMeatPlacedOnGrill(returnedData.cut);
        return true;
    }

    /// <summary>
    /// Resuelve que entrada del plato corresponde a un visual (o la ultima si no se pasa ninguno),
    /// sin mutar nada. Cae al armado cuando no hay visual (falta 'visualPrefab').
    /// </summary>
    private bool TryResolvePlateEntry(GameObject plateVisual, out int index, out BufferedMeatData entry)
    {
        index = -1;
        entry = null;

        if (plateMeatVisuals.Count == 0 && plateMeatCuts.Count == 0)
            return false;

        if (plateVisual != null)
            index = plateMeatVisuals.IndexOf(plateVisual);

        if (index < 0)
            index = plateMeatVisuals.Count - 1;

        if (index < 0 && plateMeatCuts.Count > 0)
            index = plateMeatCuts.Count - 1;

        if (index < 0)
            return false;

        if (index < plateMeatCuts.Count)
        {
            entry = plateMeatCuts[index];
            return entry != null;
        }

        BuildStationSystem station = ResolveBuildStation();
        if (station == null || index >= station.AssembledCuts.Count)
            return false;

        MeatStates state = index < station.AssembledCutStates.Count ? station.AssembledCutStates[index] : MeatStates.Crudo;
        entry = BufferedMeatData.FromCut(station.AssembledCuts[index]);
        if (entry == null)
            return false;

        entry.state = state;
        return true;
    }

    /// <summary>Saca del plato la entrada en 'index': datos, visual y corte del armado.</summary>
    private void RemovePlateEntryAt(int index)
    {
        if (index < 0)
            return;

        if (index < plateMeatCuts.Count)
            plateMeatCuts.RemoveAt(index);

        if (index < plateMeatVisuals.Count)
        {
            GameObject visual = plateMeatVisuals[index];
            plateMeatVisuals.RemoveAt(index);
            if (visual != null)
                Destroy(visual);
        }

        ResolveBuildStation()?.RemoveCutAt(index);
    }

    private void RemoveTrayEntryAt(int entryId, bool destroyVisual)
    {
        if (entryId < 0 || entryId >= trayCuts.Count)
            return;

        trayCuts.RemoveAt(entryId);

        if (entryId < trayLocalPositions.Count)
            trayLocalPositions.RemoveAt(entryId);

        if (entryId >= trayVisuals.Count)
            return;

        GameObject visual = trayVisuals[entryId];
        trayVisuals.RemoveAt(entryId);

        if (destroyVisual && visual != null)
            Destroy(visual);
    }

    /// <summary>
    /// Convierte un visual (nuevo o reciclado de la bandeja) en un visual de plato:
    /// lo saca del anchor, lo deja en mundo, le pone el sprite del estado y lo hace
    /// arrastrable para la entrega.
    /// </summary>
    private GameObject AdoptVisualIntoPlate(BufferedMeatData entry, GameObject visual, Vector3 worldPoint)
    {
        if (entry == null || entry.cut == null)
            return null;

        if (visual == null)
        {
            if (visualPrefab == null)
            {
                Debug.LogWarning("[MeatTransferBuffer] Falta 'visualPrefab': el corte entra al armado pero sin visual en el plato.");
                plateMeatCuts.Add(entry);
                return null;
            }

            visual = Instantiate(visualPrefab);
        }

        visual.transform.SetParent(null, true);
        visual.transform.position = worldPoint;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = fixedWorldScale;

        SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sprite = entry.cut.GetSpriteForState(entry.state, entry.isSideA);
            renderer.flipX = !entry.isSideA;
            renderer.sortingOrder = plateMeatSortingBase + plateMeatVisuals.Count;
        }

        // Los draggables de staging no tienen sentido una vez montado en el plato.
        ToBuildDraggableMeat trayDrag = visual.GetComponent<ToBuildDraggableMeat>();
        if (trayDrag != null)
            Destroy(trayDrag);

        if (visual.GetComponent<PlateDeliveryDraggable>() == null)
            visual.AddComponent<PlateDeliveryDraggable>();

        plateMeatCuts.Add(entry);
        plateMeatVisuals.Add(visual);
        return visual;
    }

    // ── Visuales del plato ──────────────────────────────────────────────────

    /// <summary>
    /// Elimina el visual del plato en 'index' (alineado con el orden en que se montaron los cortes).
    /// Usado por el descarte contextual de quemados. Best-effort: ignora índices fuera de rango.
    /// </summary>
    public void RemovePlateMeatVisualAt(int index)
    {
        if (index >= 0 && index < plateMeatCuts.Count)
            plateMeatCuts.RemoveAt(index);

        if (index < 0 || index >= plateMeatVisuals.Count)
            return;

        if (plateMeatVisuals[index] != null)
            Destroy(plateMeatVisuals[index]);

        plateMeatVisuals.RemoveAt(index);
    }

    public void ClearPlateMeatVisuals()
    {
        for (int i = 0; i < plateMeatVisuals.Count; i++)
        {
            if (plateMeatVisuals[i] != null)
                Destroy(plateMeatVisuals[i]);
        }

        plateMeatVisuals.Clear();
        plateMeatCuts.Clear();
    }

    public void UpdatePlateMeatSprite(Sprite sprite)
    {
        if (sprite == null || plateMeatVisuals.Count == 0)
            return;

        GameObject go = plateMeatVisuals[plateMeatVisuals.Count - 1];
        if (go == null)
            return;

        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.sprite = sprite;

        go.transform.localScale = variantSpriteScale;
        go.transform.localEulerAngles = variantSpriteRotation;

        go.GetComponent<PlateDeliveryDraggable>()?.RefreshCollider();
    }

    /// <summary>
    /// Captura el estado visual del último visual de carne del plato (sprite, escala, rotación)
    /// antes de que un pan lo transforme. Usado por el undo. Devuelve false si no hay visual.
    /// </summary>
    public bool TryCaptureLastPlateMeatVisual(out GameObject visual, out Sprite sprite, out Vector3 scale, out Vector3 euler)
    {
        visual = null;
        sprite = null;
        scale = Vector3.one;
        euler = Vector3.zero;

        if (plateMeatVisuals.Count == 0)
            return false;

        GameObject go = plateMeatVisuals[plateMeatVisuals.Count - 1];
        if (go == null)
            return false;

        visual = go;
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        sprite = sr != null ? sr.sprite : null;
        scale = go.transform.localScale;
        euler = go.transform.localEulerAngles;
        return true;
    }

    /// <summary>Restaura sprite, escala y rotación de un visual de carne del plato. Usado por el undo. Tolera visual destruido.</summary>
    public void RestorePlateMeatVisual(GameObject visual, Sprite sprite, Vector3 scale, Vector3 euler)
    {
        if (visual == null)
            return;

        SpriteRenderer sr = visual.GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.sprite = sprite;

        visual.transform.localScale = scale;
        visual.transform.localEulerAngles = euler;

        visual.GetComponent<PlateDeliveryDraggable>()?.RefreshCollider();
    }

    public void SetPlateMeatVisualsVisible(bool visible)
    {
        for (int i = 0; i < plateMeatVisuals.Count; i++)
        {
            if (plateMeatVisuals[i] != null)
                plateMeatVisuals[i].SetActive(visible);
        }
    }

    // ── Reconstrucción de visuales ──────────────────────────────────────────

    public void RefreshVisuals()
    {
        RebuildStack(toGrillCuts, toGrillVisuals, toGrillAnchor, toGrillSortingBase, false, toGrillLocalPositions);
        RebuildStack(meatHolderCuts, meatHolderVisuals, meatHolderAnchor, meatHolderSortingBase, true, meatHolderLocalPositions);
        RebuildStack(trayCuts, trayVisuals, ResolveTrayAnchor(), traySortingBase, false, trayLocalPositions);
        EnsureTrayDrags();
    }

    // ── Parrilla ← MeatHolder (legado) ──────────────────────────────────────

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
        TutorialManager.NotifyMeatPlacedOnGrill(entry.cut);
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

    // ── Preview de hover sobre la grilla ────────────────────────────────────

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

    // ── Bandeja: anclas y consultas ─────────────────────────────────────────

    /// <summary>
    /// True si el punto cae sobre la bandeja. Lo usa el arrastre del plato para devolver un corte.
    ///
    /// Mide SOLO el subarbol del anchor: el padre de 'MeatTray' es el root de GrillView, asi que
    /// medirlo tragaba media parrilla y la carne devuelta terminaba en la bandeja. Si la bandeja
    /// esta desactivada en la escena no reclama ningun punto.
    /// </summary>
    public bool IsOverMeatTray(Vector3 worldPoint)
    {
        Transform anchor = ResolveTrayAnchor();
        if (anchor == null || !anchor.gameObject.activeInHierarchy)
            return false;

        Collider2D[] colliders = anchor.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].enabled && colliders[i].OverlapPoint(new Vector2(worldPoint.x, worldPoint.y)))
                return true;
        }

        SpriteRenderer[] renderers = anchor.GetComponentsInChildren<SpriteRenderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || renderers[i].sprite == null)
                continue;

            Vector3 point = worldPoint;
            point.z = renderers[i].bounds.center.z;
            if (renderers[i].bounds.Contains(point))
                return true;
        }

        return false;
    }

    private Transform ResolveTrayAnchor()
    {
        if (trayAnchor != null)
            return trayAnchor;

        trayAnchor = FindTransformByNameUnderRoot("MeatTray", "GrillView");
        return trayAnchor;
    }

    private BuildStationSystem ResolveBuildStation()
    {
        if (buildStationSystem == null)
            buildStationSystem = Object.FindAnyObjectByType<BuildStationSystem>();
        return buildStationSystem;
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

    // ── Layout de pilas ─────────────────────────────────────────────────────

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
                renderer.sprite = cut.GetSpriteForState(entry.state, entry.isSideA);
                renderer.flipX = !entry.isSideA;
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

    private Vector3 GetTrayLocalPosition(int index)
    {
        Vector3 dir = trayWorldDirection.sqrMagnitude > 0f
            ? trayWorldDirection.normalized
            : Vector3.right;

        Vector3 worldOffset = dir * trayWorldSpacing * Mathf.Max(0, index);

        Transform anchor = ResolveTrayAnchor();
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

    private void EnsureTrayDrags()
    {
        for (int i = 0; i < trayVisuals.Count; i++)
        {
            GameObject go = trayVisuals[i];
            if (go == null || i >= trayCuts.Count)
                continue;

            BufferedMeatData entry = trayCuts[i];
            if (entry == null || entry.cut == null)
                continue;

            ToBuildDraggableMeat drag = go.GetComponent<ToBuildDraggableMeat>();
            if (drag == null)
                drag = go.AddComponent<ToBuildDraggableMeat>();

            drag.Setup(entry.cut, this, i, entry.isGridRotated);
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
