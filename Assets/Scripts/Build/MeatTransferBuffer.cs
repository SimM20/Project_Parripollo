using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Carne del plato: cortes ya montados en el armado (BuildStationSystem). Entran arrastrando
/// carne desde la parrilla a la zona del plato, y la única salida (además de la entrega) es
/// arrastrarlos de vuelta a la parrilla, conservando la cocción. También dibuja el preview de
/// slots de la parrilla mientras se arrastra un corte (desde el plato o desde el stock).
/// </summary>
public class MeatTransferBuffer : MonoBehaviour
{
    [Header("Systems")]
    [SerializeField] private GrillSystem grillSystem;
    [SerializeField] private BuildStationSystem buildStationSystem;

    [Header("Visual")]
    [SerializeField] private GameObject visualPrefab;
    [Tooltip("Orden de dibujo de la carne sobre el plato. Debe quedar por debajo de los visuales de acompañamientos/toppings de BuildFoodDropZone.")]
    [SerializeField] private int plateMeatSortingBase = 400;

    [Header("Variant Sprite (Plate)")]
    [SerializeField] private Vector3 variantSpriteScale = new Vector3(0.12f, 0.12f, 0.12f);
    [SerializeField] private Vector3 variantSpriteRotation = new Vector3(0f, 0f, 90f);

    [Header("Visual Size")]
    [SerializeField] private Vector3 fixedWorldScale = Vector3.one;

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

    private readonly List<GameObject> plateMeatVisuals = new List<GameObject>();
    private readonly List<BufferedMeatData> plateMeatCuts = new List<BufferedMeatData>();
    private readonly List<GridSlot> meatHolderHoverSlots = new List<GridSlot>();

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
        // El visual queda donde se soltó: la carne es libre dentro del plato.
        if (!BuildFoodDropZone.TryAcceptMeatAt(dropWorldPoint, entry.cut, entry.state, entry.SideAState, entry.SideBState))
            return false;

        MeatCutSO cut = meat.cut;
        meat.ReleaseOccupiedSlots();
        Destroy(meat.gameObject);

        // Sin undo: la carne se devuelve a mano, arrastrándola a la parrilla.
        GameObject visual = AdoptVisualIntoPlate(entry, dropWorldPoint);

        Debug.Log("[Plato] Corte montado desde la parrilla: " + (cut != null ? cut.cutName : "Sin corte")
                  + " | A: " + entry.SideAState + " | B: " + entry.SideBState);

        Transform platedMeat = visual != null ? visual.transform : null;
        TutorialSignals.Raise(TutorialSignal.MeatDraggedToBuild, cut: cut, target: platedMeat);
        TutorialSignals.Raise(TutorialSignal.MeatPlacedOnBuildZone, cut: cut, target: platedMeat);
        return true;
    }

    // ── Plato → Parrilla ────────────────────────────────────────────────────

    /// <summary>
    /// Devuelve a la parrilla la carne del plato, restaurando tiempos de coccion y rotacion.
    /// False si el punto no cae en un hueco libre de la grilla: el corte se queda en el plato.
    /// </summary>
    public bool TryReturnPlateMeatToGrill(GameObject plateVisual, Vector3 dropWorldPoint)
    {
        if (!TryResolvePlateEntry(plateVisual, out _, out BufferedMeatData data) || data == null)
            return false;

        return TryReturnPlateMeatToGrill(plateVisual, dropWorldPoint, data.isGridRotated);
    }

    /// <summary>Igual, con la rotación elegida durante el arrastre (R) en vez de la que tenía el corte.</summary>
    public bool TryReturnPlateMeatToGrill(GameObject plateVisual, Vector3 dropWorldPoint, bool rotateFootprint)
    {
        if (grillSystem == null)
            return false;

        if (!TryResolvePlateEntry(plateVisual, out int index, out BufferedMeatData returnedData))
            return false;

        if (returnedData.cut == null)
            return false;

        if (!grillSystem.TrySpawnMeatAtPoint(returnedData.cut, dropWorldPoint, out Meat spawnedMeat, rotateFootprint))
            return false;

        returnedData.ApplyTo(spawnedMeat, rotateFootprint);
        spawnedMeat.PlayDropSettle(dropWorldPoint);
        RemovePlateEntryAt(index);

        Debug.Log("[Plato] Carne devuelta a la parrilla: " + returnedData.cut.cutName + " | Estado: " + returnedData.state);
        TutorialSignals.Raise(TutorialSignal.MeatPlacedOnGrill, cut: returnedData.cut, meat: spawnedMeat);
        return true;
    }

    /// <summary>
    /// Corte y rotación de grilla de un visual del plato. Lo usa PlateDeliveryDraggable para el
    /// preview de slots mientras se arrastra solo la carne hacia la parrilla.
    /// </summary>
    public bool TryGetPlateMeatInfo(GameObject plateVisual, out MeatCutSO cut, out bool isGridRotated)
    {
        cut = null;
        isGridRotated = false;

        if (!TryResolvePlateEntry(plateVisual, out _, out BufferedMeatData entry))
            return false;

        cut = entry.cut;
        isGridRotated = entry.isGridRotated;
        return cut != null;
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

    /// <summary>
    /// Crea el visual de plato de un corte: lo deja en mundo, le pone el sprite del estado
    /// y lo hace arrastrable para la entrega.
    /// </summary>
    private GameObject AdoptVisualIntoPlate(BufferedMeatData entry, Vector3 worldPoint)
    {
        if (entry == null || entry.cut == null)
            return null;

        if (visualPrefab == null)
        {
            Debug.LogWarning("[MeatTransferBuffer] Falta 'visualPrefab': el corte entra al armado pero sin visual en el plato.");
            plateMeatCuts.Add(entry);
            return null;
        }

        GameObject visual = Instantiate(visualPrefab);
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

        if (visual.GetComponent<PlateDeliveryDraggable>() == null)
            visual.AddComponent<PlateDeliveryDraggable>();

        // Onda detras del corte recien apoyado: "quedo en el plato". Es un fantasma, el visual
        // real (y su collider de agarre) no se escala.
        SpriteGhost.Spawn(renderer, SpriteGhost.Motion.Echo());
        GamepadHaptics.Play(HapticEvent.MeatOnPlate);

        plateMeatCuts.Add(entry);
        plateMeatVisuals.Add(visual);
        return visual;
    }

    // ── Visuales del plato ──────────────────────────────────────────────────

    public void ClearPlateMeatVisuals()
    {
        for (int i = 0; i < plateMeatVisuals.Count; i++)
        {
            if (plateMeatVisuals[i] == null) continue;

            // Solo visual: el corte se desvanece donde está (en el plato al limpiarlo, sobre el
            // cliente al entregarlo). El visual real se destruye igual que siempre.
            SpriteGhost.Spawn(plateMeatVisuals[i].GetComponent<SpriteRenderer>(), SpriteGhost.Motion.Vanish(0.12f, 0.26f));
            Destroy(plateMeatVisuals[i]);
        }

        plateMeatVisuals.Clear();
        plateMeatCuts.Clear();
    }

    /// <summary>
    /// Tinte de preview por corte (alineado con el orden de montaje) mientras el plato se
    /// arrastra sobre un cliente. Lo invoca GameManager.
    /// </summary>
    public void SetPlateMeatTints(List<Color> tints)
    {
        for (int i = 0; i < plateMeatVisuals.Count; i++)
        {
            if (plateMeatVisuals[i] == null) continue;

            SpriteRenderer sr = plateMeatVisuals[i].GetComponent<SpriteRenderer>();
            if (sr == null) continue;

            sr.color = (tints != null && i < tints.Count) ? tints[i] : Color.white;
        }
    }

    public void ClearPlateMeatTints()
    {
        for (int i = 0; i < plateMeatVisuals.Count; i++)
        {
            if (plateMeatVisuals[i] == null) continue;

            SpriteRenderer sr = plateMeatVisuals[i].GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = Color.white;
        }
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

        // El pan transforma el corte en sándwich: una onda del dibujo nuevo marca el cambio.
        SpriteGhost.Spawn(sr, SpriteGhost.Motion.Echo());
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
        if (GridSlot.TryFindPreviewPlacement(grillSystem.slots, requiredSize, worldPoint, ItemType.Meat, null, out List<GridSlot> placementSlots, out bool isValid))
        {
            SetMeatHolderHover(placementSlots, isValid);
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

    private BuildStationSystem ResolveBuildStation()
    {
        if (buildStationSystem == null)
            buildStationSystem = Object.FindAnyObjectByType<BuildStationSystem>();
        return buildStationSystem;
    }

    void OnDisable()
    {
        ClearMeatHolderHover();
    }
}
