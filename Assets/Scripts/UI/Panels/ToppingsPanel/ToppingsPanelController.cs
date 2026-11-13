using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Panel deslizante de items de armado (panes, acompañamientos y frascos de salsa)
/// dentro de la vista Parrilla. Reemplaza al FoodItemsContainer de la BuildView.
///
/// A diferencia del StockPanel, no es data-driven: hospeda los GameObjects reales
/// de los items, que conservan su propia lógica de arrastre y vertido
/// (<see cref="BuildDraggableFoodItem"/> y <see cref="ToppingDraggable"/>).
/// Aporta el deslizamiento, el layout en grilla, la cancelación de arrastres al cerrar
/// y el filtro de panes según el corte que hay en el plato.
/// </summary>
public class ToppingsPanelController : SlidingPanel
{
    [Header("Items")]
    [Tooltip("Contenedor de los items arrastrables. Sus hijos con SpriteRenderer se acomodan en grilla.")]
    [SerializeField] private Transform itemsParent;
    [Tooltip("Si está activo, el panel reacomoda los items en grilla al arrancar. Apagado respeta las posiciones puestas a mano.")]
    [SerializeField] private bool autoLayoutItems = true;
    [Tooltip("Contenedor de las casillas de fondo, hermano de itemsParent y en el mismo origen. El hijo i se alinea al item i.")]
    [SerializeField] private Transform slotsParent;

    [Header("Grid Layout")]
    [Tooltip("Orden de dibujo de los items. Debe superar al del fondo del panel o quedan tapados.")]
    [SerializeField] private int itemSortingOrder = 100;
    [SerializeField] [Min(1)] private int columns = 2;
    [SerializeField] private Vector2 cellSpacing = new Vector2(1.15f, 1.15f);
    [SerializeField] private Vector2 firstCellLocalOffset = new Vector2(-0.6f, 0.9f);

    [Header("Bread Filter")]
    [Tooltip("Armado del plato. Con un corte montado, el panel solo muestra el pan que le corresponde. Si falta, se busca en la escena.")]
    [SerializeField] private BuildStationSystem buildStation;
    [Tooltip("Segundos que tarda una casilla (item + fondo) en achicarse hasta desaparecer o en volver a aparecer.")]
    [SerializeField] [Min(0.01f)] private float cellPopSeconds = 0.12f;
    [Tooltip("Qué tan rápido se corren las casillas vecinas para cerrar el hueco. Más alto = más seco.")]
    [SerializeField] [Min(1f)] private float cellSlideSharpness = 16f;

    /// <summary>Instancia activa del panel de items de armado.</summary>
    public static ToppingsPanelController Instance { get; private set; }

    /// <summary>Una casilla del panel: el item arrastrable y su fondo, que se mueven y escalan juntos.</summary>
    private class Cell
    {
        public Transform item;
        public Transform slot;
        public BuildDraggableFoodItem food;
        public ToppingDraggable jar;
        public Vector3 itemBaseScale;
        public Vector3 slotBaseScale;
        public Vector3 current;
        public Vector3 target;
        /// <summary>0 = desaparecida, 1 = a tamaño completo.</summary>
        public float visibility = 1f;
        public bool wantVisible = true;

        /// <summary>El item se está arrastrando o volviendo a su lugar: él maneja su transform.</summary>
        public bool IsBusy => (food != null && food.IsBusy) || (jar != null && jar.IsDragging);
    }

    private readonly List<Transform> layoutItems = new List<Transform>();
    private readonly List<Cell> cells = new List<Cell>();
    private bool cellsAnimating;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (buildStation != null)
            buildStation.OnAssemblyChanged -= RefreshBreadVisibility;
    }

    void Update()
    {
        if (cellsAnimating)
            TickCells(Time.unscaledDeltaTime, false);
    }

    // ── Hooks de SlidingPanel ───────────────────────────────────────────────

    protected override void OnPanelStarted()
    {
        if (autoLayoutItems)
            LayoutItems();

        InitBreadFilter();
    }

    /// <summary>
    /// Al cerrarse, devuelve a su sitio cualquier item que se haya quedado a medio arrastrar.
    /// Los draggables sueltan solos en OnWorldPointerUp, así que esto cubre el cierre por cambio de
    /// vista o por desactivación del panel mientras el botón sigue apretado.
    /// </summary>
    protected override void OnPanelClosing()
    {
        if (itemsParent == null)
            return;

        // ToppingDraggable y BuildDraggableFoodItem restauran su transform en OnWorldPointerUp;
        // forzar la desactivación del objeto dispara su propio OnDisable/OnWorldPointerUp pendiente.
        for (int i = 0; i < itemsParent.childCount; i++)
        {
            Transform child = itemsParent.GetChild(i);
            if (child == null)
                continue;

            ToppingDraggable pourable = child.GetComponent<ToppingDraggable>();
            if (pourable != null)
                pourable.CancelDrag();
        }
    }

    protected override void OnPanelOpened() => TutorialSignals.Raise(TutorialSignal.ToppingsPanelOpened);

    protected override void ValidateReferences()
    {
        base.ValidateReferences();

        if (itemsParent == null)
            Debug.LogWarning("[ToppingsPanelController] Falta la referencia 'itemsParent'. El panel no tiene items para mostrar.");
    }

    // ── Layout ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Acomoda en grilla los items arrastrables. Solo cuenta los hijos con SpriteRenderer:
    /// los transforms auxiliares (por ejemplo los SplatterSource) no ocupan celda.
    /// </summary>
    public void LayoutItems()
    {
        if (itemsParent == null)
            return;

        CollectLayoutItems();

        for (int i = 0; i < layoutItems.Count; i++)
        {
            layoutItems[i].localPosition = CellLocalPosition(i);

            if (slotsParent != null && i < slotsParent.childCount)
                slotsParent.GetChild(i).localPosition = layoutItems[i].localPosition;

            // El fondo del panel se dibuja por encima de los sprites de escena: sin esto los items quedan tapados.
            SpriteRenderer renderer = layoutItems[i].GetComponent<SpriteRenderer>();
            if (renderer != null)
                renderer.sortingOrder = itemSortingOrder;
        }
    }

    private void CollectLayoutItems()
    {
        layoutItems.Clear();
        for (int i = 0; i < itemsParent.childCount; i++)
        {
            Transform child = itemsParent.GetChild(i);
            if (child != null && child.GetComponent<SpriteRenderer>() != null)
                layoutItems.Add(child);
        }
    }

    private Vector3 CellLocalPosition(int index)
    {
        int safeColumns = Mathf.Max(1, columns);
        int row = index / safeColumns;
        int col = index % safeColumns;

        return new Vector3(
            firstCellLocalOffset.x + col * cellSpacing.x,
            firstCellLocalOffset.y - row * cellSpacing.y,
            0f);
    }

    // ── Filtro de panes ─────────────────────────────────────────────────────

    private void InitBreadFilter()
    {
        cells.Clear();
        if (itemsParent != null)
        {
            CollectLayoutItems();
            for (int i = 0; i < layoutItems.Count; i++)
            {
                Transform item = layoutItems[i];
                Transform slot = slotsParent != null && i < slotsParent.childCount ? slotsParent.GetChild(i) : null;

                cells.Add(new Cell
                {
                    item = item,
                    slot = slot,
                    food = item.GetComponent<BuildDraggableFoodItem>(),
                    jar = item.GetComponent<ToppingDraggable>(),
                    itemBaseScale = item.localScale,
                    slotBaseScale = slot != null ? slot.localScale : Vector3.one,
                    current = item.localPosition,
                    target = item.localPosition
                });
            }
        }

        if (buildStation == null)
            buildStation = FindFirstObjectByType<BuildStationSystem>(FindObjectsInactive.Include);

        if (buildStation == null)
        {
            Debug.LogWarning("[ToppingsPanelController] No hay BuildStationSystem: el panel muestra siempre todos los panes.");
            return;
        }

        buildStation.OnAssemblyChanged += RefreshBreadVisibility;
        RefreshBreadVisibility();
        TickCells(0f, true);
    }

    /// <summary>
    /// Sin corte en el plato se ven todos los panes; con un corte, solo el que le corresponde
    /// (ninguno si el corte se sirve únicamente al plato). La casilla del pan que sobra desaparece
    /// y las demás se corren para cerrar el hueco; la animación la lleva <see cref="TickCells"/>.
    /// </summary>
    private void RefreshBreadVisibility()
    {
        MeatCutSO cut = buildStation != null && buildStation.HasAnyCut ? buildStation.AssembledCuts[0] : null;

        int visibleIndex = 0;
        for (int i = 0; i < cells.Count; i++)
        {
            Cell cell = cells[i];
            if (cell.item == null)
                continue;

            BreadSO bread = cell.food != null ? cell.food.breadData : null;
            cell.wantVisible = bread == null || BuildStationSystem.IsBreadAllowedFor(cut, bread);

            // Con el layout manual cada item se queda donde lo pusieron: solo aparece o desaparece.
            if (cell.wantVisible && autoLayoutItems)
                cell.target = CellLocalPosition(visibleIndex++);
        }

        cellsAnimating = true;
    }

    /// <summary>
    /// Lleva cada casilla a su lugar y tamaño. Dentro de la misma fila se desliza; si le toca
    /// cambiar de fila, se achica donde está y reaparece en la celda nueva, en vez de cruzar el
    /// panel en diagonal por encima de las otras.
    /// </summary>
    private void TickCells(float deltaTime, bool instant)
    {
        bool pending = false;
        float slide = 1f - Mathf.Exp(-cellSlideSharpness * deltaTime);

        for (int i = 0; i < cells.Count; i++)
        {
            Cell cell = cells[i];
            if (cell.item == null)
                continue;

            // Un item en la mano (o volviendo al panel) maneja su propio transform: se lo espera.
            if (!instant && cell.IsBusy)
            {
                pending = true;
                continue;
            }

            float settled = cell.wantVisible ? 1f : 0f;
            if (instant)
            {
                cell.visibility = settled;
                cell.current = cell.target;
            }
            else
            {
                bool changesRow = Mathf.Abs(cell.current.y - cell.target.y) > 0.001f;
                float goal = changesRow ? 0f : settled;
                cell.visibility = Mathf.MoveTowards(cell.visibility, goal, deltaTime / cellPopSeconds);

                if (cell.visibility <= 0f)
                    cell.current = cell.target;
                else if ((cell.current - cell.target).sqrMagnitude < 0.000001f)
                    cell.current = cell.target;
                else
                    cell.current = Vector3.Lerp(cell.current, cell.target, slide);
            }

            ApplyCell(cell);

            if (!Mathf.Approximately(cell.visibility, settled) || cell.current != cell.target)
                pending = true;
        }

        cellsAnimating = pending;
    }

    private static void ApplyCell(Cell cell)
    {
        bool active = cell.visibility > 0f;
        float scale = FeelEase.OutCubic(cell.visibility);

        if (cell.item.gameObject.activeSelf != active)
            cell.item.gameObject.SetActive(active);
        cell.item.localPosition = cell.current;
        cell.item.localScale = cell.itemBaseScale * scale;

        if (cell.slot == null)
            return;

        if (cell.slot.gameObject.activeSelf != active)
            cell.slot.gameObject.SetActive(active);
        cell.slot.localPosition = cell.current;
        cell.slot.localScale = cell.slotBaseScale * scale;
    }

    void OnValidate()
    {
        if (!Application.isPlaying && autoLayoutItems && itemsParent != null)
            LayoutItems();
    }
}
