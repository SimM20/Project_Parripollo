using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Panel deslizante de stock dentro de GrillView. Construye una celda por variedad
/// (con contador numérico) y permite arrastrar directamente a la parrilla.
/// Vive dentro del prefab de GrillView: el singleton destruye el componente duplicado,
/// nunca el GameObject.
/// </summary>
public class StockPanelController : MonoBehaviour
{
    [Header("Panel References")]
    [SerializeField] private Transform slidingRoot;
    [SerializeField] private Transform slotsParent;
    [SerializeField] private StockPanelSlot slotPrefab;
    [Tooltip("Fondo del panel. Un drop soltado sobre él se cancela sin tocar el stock.")]
    [SerializeField] private SpriteRenderer panelBackground;

    [Header("Data")]
    [SerializeField] private FoodCatalogSO catalog;
    [SerializeField] private CoalSO coalData;

    [Header("Systems")]
    [SerializeField] private GrillSystem grillSystem;
    [SerializeField] private ViewManager viewManager;
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

    [Header("Slide Animation")]
    [SerializeField] private float openLocalX = 0f;
    [SerializeField] private float closedLocalX = -5f;
    [SerializeField] [Min(0f)] private float slideDuration = 0.2f;

    /// <summary>Instancia activa del panel de stock.</summary>
    public static StockPanelController Instance { get; private set; }

    /// <summary>True si el panel está desplegado (o yendo hacia desplegado).</summary>
    public bool IsOpen { get; private set; }

    /// <summary>True mientras la corrutina de deslizamiento está en curso.</summary>
    public bool IsAnimating => slideRoutine != null;

    /// <summary>True solo si una celda puede iniciar un arrastre en este momento.</summary>
    public bool CanBeginDrag => IsOpen && slideRoutine == null;

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
    private Coroutine slideRoutine;
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

    void OnEnable()
    {
        BindCoolerSystem();
        BindViewManager();
    }

    void Start()
    {
        ValidateReferences();

        // ViewManager.Show() solo dispara OnViewChanged cuando cambia de vista, así que
        // el Show(Grill) inicial nunca nos llega: hay que auto-inicializarse acá.
        BindCoolerSystem();

        if (slidingRoot != null)
            slidingRoot.gameObject.SetActive(true);

        startCompleted = true;
        RefreshSlots();
        Close(true);
    }

    void OnDisable()
    {
        if (coolerSystem != null)
            coolerSystem.OnInventoryChanged -= RefreshSlots;

        if (viewManager != null)
            viewManager.OnViewChanged -= HandleViewChanged;

        CancelActiveDrag();

        if (slideRoutine != null)
        {
            StopCoroutine(slideRoutine);
            slideRoutine = null;
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ── Referencias externas ────────────────────────────────────────────────

    /// <summary>
    /// Asigna el ViewManager desde afuera del prefab (vive fuera de GrillView).
    /// Rebinde la suscripción al evento de cambio de vista.
    /// </summary>
    public void SetViewManager(ViewManager manager)
    {
        if (viewManager == manager)
            return;

        if (viewManager != null)
            viewManager.OnViewChanged -= HandleViewChanged;

        viewManager = manager;

        if (isActiveAndEnabled)
            BindViewManager();
    }

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

    private void BindViewManager()
    {
        if (viewManager == null)
            return;

        viewManager.OnViewChanged -= HandleViewChanged;
        viewManager.OnViewChanged += HandleViewChanged;
    }

    private void ValidateReferences()
    {
        if (slidingRoot == null)
            Debug.LogWarning("[StockPanelController] Falta la referencia 'slidingRoot'. El panel no puede deslizarse.");

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

        if (viewManager == null)
            Debug.LogWarning("[StockPanelController] Falta la referencia 'viewManager'. Usá SetViewManager() desde afuera del prefab.");

        if (meatTransferBuffer == null)
            Debug.LogWarning("[StockPanelController] Falta la referencia 'meatTransferBuffer'. No habrá preview de hover para carne.");

        if (coalTransferBuffer == null)
            Debug.LogWarning("[StockPanelController] Falta la referencia 'coalTransferBuffer'. No habrá preview de hover para carbón.");

        if (panelBackground == null)
            Debug.LogWarning("[StockPanelController] Falta la referencia 'panelBackground'. Soltar un ítem sobre el panel lo colocará igual en la parrilla.");

        if (requireDropAreaHit && dropArea == null)
            Debug.LogWarning("[StockPanelController] 'requireDropAreaHit' está activo pero falta la referencia 'dropArea'. El gate queda desactivado.");
    }

    private void HandleViewChanged(ViewType view)
    {
        if (view == ViewType.Grill)
        {
            if (slidingRoot != null)
                slidingRoot.gameObject.SetActive(true);

            RefreshSlots();
            Close(true);
            return;
        }

        CancelActiveDrag();

        if (slidingRoot != null)
            slidingRoot.gameObject.SetActive(false);
    }

    // ── Gates de drop ───────────────────────────────────────────────────────

    /// <summary>
    /// True si el punto cae dentro del fondo del panel. El panel tapa el borde izquierdo
    /// de la pantalla, así que soltar ahí debe cancelar el arrastre en vez de colocar
    /// el ítem en la parrilla. Sin panelBackground asignado no filtra nada.
    /// </summary>
    public bool IsPointOverPanel(Vector3 worldPoint)
    {
        if (panelBackground == null)
            return false;

        Vector3 point = worldPoint;
        point.z = panelBackground.bounds.center.z;

        return panelBackground.bounds.Contains(point);
    }

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

    // ── Apertura / cierre ───────────────────────────────────────────────────

    /// <summary>Alterna entre abierto y cerrado. Lo llama la pestaña lateral.</summary>
    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    /// <summary>Despliega el panel deslizándolo hasta openLocalX.</summary>
    public void Open()
    {
        IsOpen = true;
        StartSlide(openLocalX, false);
    }

    /// <summary>
    /// Repliega el panel hasta closedLocalX. Con instant en true salta la animación.
    /// Cancela cualquier arrastre en curso.
    /// </summary>
    public void Close(bool instant = false)
    {
        CancelActiveDrag();
        IsOpen = false;
        StartSlide(closedLocalX, instant);
    }

    private void StartSlide(float targetX, bool instant)
    {
        if (slidingRoot == null)
            return;

        if (slideRoutine != null)
        {
            StopCoroutine(slideRoutine);
            slideRoutine = null;
        }

        Vector3 current = slidingRoot.localPosition;

        if (instant || slideDuration <= 0f || !isActiveAndEnabled || !slidingRoot.gameObject.activeInHierarchy)
        {
            current.x = targetX;
            slidingRoot.localPosition = current;
            return;
        }

        // Escala la duración por el tramo que falta, para que una interrupción no
        // vuelva a tardar el tiempo completo.
        float span = Mathf.Abs(openLocalX - closedLocalX);
        float remaining = span > 0.0001f ? Mathf.Abs(targetX - current.x) / span : 0f;
        float duration = slideDuration * Mathf.Clamp01(remaining);

        if (duration <= 0.0001f)
        {
            current.x = targetX;
            slidingRoot.localPosition = current;
            return;
        }

        slideRoutine = StartCoroutine(SlideRoutine(targetX, duration));
    }

    private IEnumerator SlideRoutine(float targetX, float duration)
    {
        float startX = slidingRoot.localPosition.x;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            Vector3 position = slidingRoot.localPosition;
            position.x = Mathf.Lerp(startX, targetX, k);
            slidingRoot.localPosition = position;

            yield return null;
        }

        Vector3 finalPosition = slidingRoot.localPosition;
        finalPosition.x = targetX;
        slidingRoot.localPosition = finalPosition;

        slideRoutine = null;
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
