using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Panel deslizante de stock dentro de GrillView. Construye una celda por variedad
/// (con contador numérico) y permite arrastrar directamente a la parrilla.
/// Vive dentro del prefab de GrillView: el singleton destruye el componente duplicado,
/// nunca el GameObject.
///
/// El deslizamiento, el gateo por vista y la cancelación de drop sobre el panel viven
/// en <see cref="SlidingPanel"/>; acá queda solo la lógica de stock y celdas.
/// </summary>
public class StockPanelController : SlidingPanel
{
    [Header("Slots")]
    [SerializeField] private Transform slotsParent;
    [SerializeField] private StockPanelSlot slotPrefab;

    [Header("Data")]
    [SerializeField] private FoodCatalogSO catalog;
    [SerializeField] private CoalSO coalData;

    [Header("Systems")]
    [SerializeField] private GrillSystem grillSystem;
    [SerializeField] private MeatTransferBuffer meatTransferBuffer;
    [SerializeField] private CoalTransferBuffer coalTransferBuffer;

    [Header("Drop Gating")]
    [Tooltip("Área válida de drop. Solo se aplica si requireDropAreaHit está activo.")]
    [SerializeField] private Collider2D dropArea;
    [Tooltip("Si está activo, el drop debe caer dentro de dropArea. Apagado = paridad con MeatHolderDraggableMeat.")]
    [SerializeField] private bool requireDropAreaHit = false;

    [Header("Grid Layout")]
    [SerializeField] [Min(1)] private int columns = 4;
    [SerializeField] private Vector2 cellSpacing = new Vector2(1.2f, 1.4f);
    [SerializeField] private Vector2 firstCellLocalOffset = Vector2.zero;
    [SerializeField] private int slotSortingOrder = 100;

    /// <summary>Instancia activa del panel de stock.</summary>
    public static StockPanelController Instance { get; private set; }

    /// <summary>Parrilla destino de los drops. Puede ser null si no se cableó.</summary>
    public GrillSystem Grill => grillSystem;

    /// <summary>Buffer de carne usado solo para el preview de hover sobre la grilla.</summary>
    public MeatTransferBuffer MeatBuffer => meatTransferBuffer;

    /// <summary>Buffer de carbón usado solo para el preview de hover sobre la grilla.</summary>
    public CoalTransferBuffer CoalBuffer => coalTransferBuffer;

    private readonly List<StockPanelSlot> pooledSlots = new List<StockPanelSlot>();
    private readonly List<ItemDataSO> orderedItems = new List<ItemDataSO>();
    private readonly List<MeatCutSO> extraCuts = new List<MeatCutSO>();

    private CoolerSystem coolerSystem;
    private StockPanelSlot activeDragSlot;
    private bool refreshPending;
    private bool startCompleted;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        BindCoolerSystem();
    }

    protected override void OnDisable()
    {
        if (coolerSystem != null)
            coolerSystem.OnInventoryChanged -= RefreshSlots;

        base.OnDisable();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ── Hooks de SlidingPanel ───────────────────────────────────────────────

    protected override void OnPanelStarted()
    {
        BindCoolerSystem();
        startCompleted = true;
        RefreshSlots();
    }

    protected override void OnEnteredGrillView() => RefreshSlots();

    protected override void OnPanelClosing() => CancelActiveDrag();

    protected override void OnPanelOpened() => TutorialManager.NotifyStockPanelOpened();

    protected override bool CanOpen() => TutorialManager.CheckStockPanelOpenAllowed();

    protected override void ValidateReferences()
    {
        base.ValidateReferences();

        if (slotsParent == null)
            Debug.LogWarning("[StockPanelController] Falta la referencia 'slotsParent'. No se pueden crear celdas.");

        if (slotPrefab == null)
            Debug.LogWarning("[StockPanelController] Falta la referencia 'slotPrefab'. No se pueden crear celdas.");

        if (catalog == null)
            Debug.LogWarning("[StockPanelController] Falta la referencia 'catalog'. Solo se mostrarán cortes sueltos del stock.");

        if (coalData == null)
            Debug.LogWarning("[StockPanelController] Falta la referencia 'coalData'. No se mostrará la celda de carbón.");

        if (grillSystem == null)
            Debug.LogWarning("[StockPanelController] Falta la referencia 'grillSystem'. Los drops no podrán spawnear en la parrilla.");

        if (meatTransferBuffer == null)
            Debug.LogWarning("[StockPanelController] Falta la referencia 'meatTransferBuffer'. No habrá preview de hover para carne.");

        if (coalTransferBuffer == null)
            Debug.LogWarning("[StockPanelController] Falta la referencia 'coalTransferBuffer'. No habrá preview de hover para carbón.");

        if (requireDropAreaHit && dropArea == null)
            Debug.LogWarning("[StockPanelController] 'requireDropAreaHit' está activo pero falta la referencia 'dropArea'. El gate queda desactivado.");
    }

    // ── Referencias externas ────────────────────────────────────────────────

    private void BindCoolerSystem()
    {
        if (coolerSystem == null || coolerSystem != CoolerSystem.Instance)
        {
            if (coolerSystem != null)
                coolerSystem.OnInventoryChanged -= RefreshSlots;

            coolerSystem = CoolerSystem.Instance;
        }

        if (coolerSystem != null)
        {
            coolerSystem.OnInventoryChanged -= RefreshSlots;
            coolerSystem.OnInventoryChanged += RefreshSlots;
        }
    }

    // ── Gates de drop ───────────────────────────────────────────────────────

    /// <summary>
    /// True si el punto está permitido por el área de drop configurable.
    /// Con requireDropAreaHit apagado (o sin dropArea asignado) siempre devuelve true,
    /// manteniendo la paridad con los arrastres del MeatHolder.
    /// </summary>
    public bool IsPointInDropArea(Vector3 worldPoint)
    {
        if (!requireDropAreaHit || dropArea == null)
            return true;

        Vector3 point = worldPoint;
        point.z = dropArea.bounds.center.z;

        return dropArea.bounds.Contains(point);
    }

    // ── Arrastre ────────────────────────────────────────────────────────────

    /// <summary>Una celda avisa que empezó a arrastrar. Congela los refresh del panel.</summary>
    public void NotifyDragStarted(StockPanelSlot slot)
    {
        if (activeDragSlot != null && activeDragSlot != slot)
            activeDragSlot.CancelDrag();

        activeDragSlot = slot;
    }

    /// <summary>
    /// Una celda avisa que terminó (o canceló) su arrastre. Ejecuta el refresh
    /// que se hubiera diferido mientras el arrastre estaba activo.
    /// </summary>
    public void NotifyDragEnded(StockPanelSlot slot)
    {
        if (activeDragSlot == slot)
            activeDragSlot = null;

        if (activeDragSlot != null || !refreshPending)
            return;

        refreshPending = false;
        RefreshSlots();
    }

    /// <summary>Cancela el arrastre en curso, si lo hay, sin tocar el stock.</summary>
    public void CancelActiveDrag()
    {
        StockPanelSlot slot = activeDragSlot;
        if (slot == null)
            return;

        // CancelDrag() reingresa por NotifyDragEnded(); esto es la red de seguridad
        // por si la celda ya no estaba arrastrando.
        slot.CancelDrag();

        if (activeDragSlot != slot)
            return;

        activeDragSlot = null;

        if (!refreshPending)
            return;

        refreshPending = false;
        RefreshSlots();
    }

    // ── Construcción de celdas ──────────────────────────────────────────────

    /// <summary>
    /// Reconstruye el contenido de las celdas desde el stock actual.
    /// Si hay un arrastre activo difiere el refresh para no re-bindear la celda
    /// que el jugador está usando.
    /// </summary>
    public void RefreshSlots()
    {
        if (!startCompleted)
            return;

        if (activeDragSlot != null)
        {
            refreshPending = true;
            return;
        }

        if (slotsParent == null || slotPrefab == null)
            return;

        BuildOrderedItems();
        EnsureSlotCount(orderedItems.Count);

        for (int i = 0; i < pooledSlots.Count; i++)
        {
            StockPanelSlot slot = pooledSlots[i];
            if (slot == null)
                continue;

            if (i >= orderedItems.Count)
            {
                slot.gameObject.SetActive(false);
                continue;
            }

            ItemDataSO item = orderedItems[i];
            int count = coolerSystem != null ? coolerSystem.GetCount(item) : 0;

            slot.gameObject.SetActive(true);
            slot.SetSortingOrder(slotSortingOrder);
            slot.Bind(item, count, this);
            LayoutSlot(i, slot.transform);
        }
    }

    private void BuildOrderedItems()
    {
        orderedItems.Clear();

        // 1) Cortes del catálogo con stock, en el orden del catálogo.
        if (catalog != null)
        {
            IReadOnlyList<MeatCutSO> cuts = catalog.GetAllCuts();
            for (int i = 0; i < cuts.Count; i++)
            {
                MeatCutSO cut = cuts[i];
                if (cut == null)
                    continue;

                if (coolerSystem == null || coolerSystem.GetCount(cut) <= 0)
                    continue;

                if (orderedItems.Contains(cut))
                    continue;

                orderedItems.Add(cut);
            }
        }

        // 2) Cortes con stock que no están en el catálogo (ej: ChorizoTutorial),
        //    ordenados por itemName para que el orden sea determinístico.
        extraCuts.Clear();

        if (coolerSystem != null)
        {
            foreach (KeyValuePair<ItemDataSO, int> entry in coolerSystem.EnumerateStock())
            {
                if (entry.Value <= 0)
                    continue;

                MeatCutSO cut = entry.Key as MeatCutSO;
                if (cut == null)
                    continue;

                if (orderedItems.Contains(cut) || extraCuts.Contains(cut))
                    continue;

                extraCuts.Add(cut);
            }

            extraCuts.Sort(CompareByItemName);

            for (int i = 0; i < extraCuts.Count; i++)
                orderedItems.Add(extraCuts[i]);
        }

        // 3) El carbón va siempre último y siempre presente, incluso en 0.
        if (coalData != null)
            orderedItems.Add(coalData);
    }

    private static int CompareByItemName(MeatCutSO a, MeatCutSO b)
    {
        string nameA = a != null && a.itemName != null ? a.itemName : string.Empty;
        string nameB = b != null && b.itemName != null ? b.itemName : string.Empty;
        return string.Compare(nameA, nameB, StringComparison.Ordinal);
    }

    private void EnsureSlotCount(int target)
    {
        while (pooledSlots.Count < target)
        {
            StockPanelSlot slot = Instantiate(slotPrefab, slotsParent);
            slot.gameObject.SetActive(false);
            pooledSlots.Add(slot);
        }
    }

    private void LayoutSlot(int index, Transform slotTransform)
    {
        if (slotTransform == null)
            return;

        int safeColumns = Mathf.Max(1, columns);
        int row = index / safeColumns;
        int col = index % safeColumns;

        slotTransform.localPosition = new Vector3(
            firstCellLocalOffset.x + col * cellSpacing.x,
            firstCellLocalOffset.y - row * cellSpacing.y,
            0f);
        slotTransform.localRotation = Quaternion.identity;
    }
}
