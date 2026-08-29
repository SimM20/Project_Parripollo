using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Panel deslizante de items de armado (panes, acompañamientos y frascos de salsa)
/// dentro de la vista Parrilla. Reemplaza al FoodItemsContainer de la BuildView.
///
/// A diferencia del StockPanel, no es data-driven: hospeda los GameObjects reales
/// de los items, que conservan su propia lógica de arrastre y vertido
/// (<see cref="BuildDraggableFoodItem"/> y <see cref="ToppingDraggable"/>).
/// Solo aporta el deslizamiento, el layout en grilla y la cancelación de arrastres al cerrar.
/// </summary>
public class ToppingsPanelController : SlidingPanel
{
    [Header("Items")]
    [Tooltip("Contenedor de los items arrastrables. Sus hijos con SpriteRenderer se acomodan en grilla.")]
    [SerializeField] private Transform itemsParent;
    [Tooltip("Si está activo, el panel reacomoda los items en grilla al arrancar. Apagado respeta las posiciones puestas a mano.")]
    [SerializeField] private bool autoLayoutItems = true;

    [Header("Grid Layout")]
    [Tooltip("Orden de dibujo de los items. Debe superar al del fondo del panel o quedan tapados.")]
    [SerializeField] private int itemSortingOrder = 100;
    [SerializeField] [Min(1)] private int columns = 2;
    [SerializeField] private Vector2 cellSpacing = new Vector2(1.15f, 1.15f);
    [SerializeField] private Vector2 firstCellLocalOffset = new Vector2(-0.6f, 0.9f);

    /// <summary>Instancia activa del panel de items de armado.</summary>
    public static ToppingsPanelController Instance { get; private set; }

    private readonly List<Transform> layoutItems = new List<Transform>();

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
    }

    // ── Hooks de SlidingPanel ───────────────────────────────────────────────

    protected override void OnPanelStarted()
    {
        if (autoLayoutItems)
            LayoutItems();
    }

    /// <summary>
    /// Al cerrarse, devuelve a su sitio cualquier item que se haya quedado a medio arrastrar.
    /// Los draggables sueltan solos en OnMouseUp, así que esto cubre el cierre por cambio de
    /// vista o por desactivación del panel mientras el botón sigue apretado.
    /// </summary>
    protected override void OnPanelClosing()
    {
        if (itemsParent == null)
            return;

        // ToppingDraggable y BuildDraggableFoodItem restauran su transform en OnMouseUp;
        // forzar la desactivación del objeto dispara su propio OnDisable/OnMouseUp pendiente.
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

        layoutItems.Clear();
        for (int i = 0; i < itemsParent.childCount; i++)
        {
            Transform child = itemsParent.GetChild(i);
            if (child != null && child.GetComponent<SpriteRenderer>() != null)
                layoutItems.Add(child);
        }

        int safeColumns = Mathf.Max(1, columns);

        for (int i = 0; i < layoutItems.Count; i++)
        {
            int row = i / safeColumns;
            int col = i % safeColumns;

            layoutItems[i].localPosition = new Vector3(
                firstCellLocalOffset.x + col * cellSpacing.x,
                firstCellLocalOffset.y - row * cellSpacing.y,
                0f);

            // El fondo del panel se dibuja por encima de los sprites de escena: sin esto los items quedan tapados.
            SpriteRenderer renderer = layoutItems[i].GetComponent<SpriteRenderer>();
            if (renderer != null)
                renderer.sortingOrder = itemSortingOrder;
        }
    }

    void OnValidate()
    {
        if (!Application.isPlaying && autoLayoutItems && itemsParent != null)
            LayoutItems();
    }
}
