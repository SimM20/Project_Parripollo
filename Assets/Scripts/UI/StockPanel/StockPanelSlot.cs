using TMPro;
using UnityEngine;

/// <summary>
/// Celda del panel de stock: un ícono + un contador numérico por variedad.
/// Es dueña de todo el arrastre (Unity nunca transfiere OnMouseDrag/OnMouseUp a otro collider):
/// crea un ghost visual, muestra el preview sobre la grilla y al soltar descuenta del cooler
/// y spawnea en la parrilla, con rollback si el spawn falla.
/// </summary>
public class StockPanelSlot : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private TextMeshPro countLabel;
    [Tooltip("Lado mayor del icono en unidades locales de la celda. Los sprites de cortes tienen tamanos distintos y sin esto desbordan el slot.")]
    [SerializeField] [Min(0.01f)] private float iconLocalSize = 0.62f;

    [Header("Colors")]
    [SerializeField] private Color availableColor = Color.white;
    [SerializeField] private Color emptyColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    [Header("Drag Ghost")]
    [SerializeField] private int ghostSortingOrder = 6000;
    [Tooltip("Multiplicador sobre la escala del prefab real que se va a spawnear. 1 = mismo tamano que en la parrilla.")]
    [SerializeField] private Vector3 ghostWorldScale = Vector3.one;
    [SerializeField] private bool rotateGhostVisual = true;
    [SerializeField] private float rotatedPreviewAngleZ = 90f;

    /// <summary>Ítem actualmente bindeado en esta celda.</summary>
    public ItemDataSO Item => item;

    /// <summary>True si la celda puede iniciar un arrastre (hay stock y está habilitada).</summary>
    public bool IsInteractable => isInteractable;

    private ItemDataSO item;
    private StockPanelController owner;
    private bool isInteractable;

    private ItemDataSO draggingItem;
    private GameObject ghost;
    private SpriteRenderer ghostRenderer;
    private bool isDragging;
    private bool isGridRotated;

    void Awake()
    {
        if (iconRenderer == null)
            iconRenderer = GetComponent<SpriteRenderer>();

        EnsureCollider2D();
    }

    void Update()
    {
        if (!isDragging || draggingItem == null)
            return;

        // Nota: R también dispara GameManager.CleanAshes en la vista de parrilla.
        // Ese doble binding ya existía con los arrastres del MeatHolder; se replica tal cual.
        if (!(draggingItem is MeatCutSO))
            return;

        if (Input.GetKeyDown(KeyCode.R))
            ToggleGridRotation();
    }

    void OnDisable()
    {
        if (!isDragging)
            return;

        CancelDrag();
    }

    // ── Binding ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Configura la celda con un ítem, su cantidad y el controlador dueño.
    /// El carbón en 0 queda grisado y no arrastrable; el resto queda habilitado.
    /// </summary>
    public void Bind(ItemDataSO boundItem, int count, StockPanelController boundOwner)
    {
        item = boundItem;
        owner = boundOwner;
        isInteractable = boundItem != null && count > 0;

        if (iconRenderer != null)
        {
            iconRenderer.sprite = ResolveIcon(boundItem);
            iconRenderer.color = isInteractable ? availableColor : emptyColor;
            FitIconToSlot();
        }

        if (countLabel != null)
        {
            countLabel.text = count.ToString();
            countLabel.color = isInteractable ? availableColor : emptyColor;
        }
    }

    /// <summary>
    /// Escala el ícono para que su lado mayor mida <see cref="iconLocalSize"/>, de modo que
    /// cortes con sprites de distinto tamaño se vean uniformes dentro de la celda.
    /// </summary>
    private void FitIconToSlot()
    {
        if (iconRenderer == null || iconRenderer.sprite == null)
            return;

        Vector2 spriteSize = iconRenderer.sprite.bounds.size;
        float largestSide = Mathf.Max(spriteSize.x, spriteSize.y);
        if (largestSide <= 0.0001f)
            return;

        float factor = iconLocalSize / largestSide;
        iconRenderer.transform.localScale = new Vector3(factor, factor, 1f);
    }

    /// <summary>Coloca el ícono y el contador por encima del fondo del panel.</summary>
    public void SetSortingOrder(int order)
    {
        if (iconRenderer != null)
            iconRenderer.sortingOrder = order;

        if (countLabel != null)
            countLabel.sortingOrder = order + 1;
    }

    // ── Arrastre ────────────────────────────────────────────────────────────

    void OnMouseDown()
    {
        if (!isInteractable || item == null || owner == null)
            return;

        if (!owner.CanBeginDrag)
            return;

        if (CoolerSystem.Instance == null || CoolerSystem.Instance.GetCount(item) <= 0)
            return;

        if (!TutorialManager.CheckStockDragAllowed(item))
        {
            Debug.Log($"[StockPanelSlot] Arrastre de {item.itemName} bloqueado por el tutorial.");
            return;
        }

        draggingItem = item;
        isDragging = true;
        GamePause.OnPaused += CancelDrag;
        isGridRotated = false;

        Vector3 mouseWorld = GetMouseWorldPosition();
        CreateGhost(mouseWorld);

        owner.NotifyDragStarted(this);
        UpdateHoverPreview(mouseWorld);
    }

    void OnMouseDrag()
    {
        if (!isDragging || draggingItem == null)
            return;

        Vector3 mouseWorld = GetMouseWorldPosition();

        if (ghost != null)
            ghost.transform.position = mouseWorld;

        UpdateHoverPreview(mouseWorld);
    }

    void OnMouseUp()
    {
        if (!isDragging || draggingItem == null)
            return;

        Vector3 dropWorldPoint = GetMouseWorldPosition();
        ItemDataSO droppedItem = draggingItem;
        bool rotated = isGridRotated;

        isDragging = false;
        GamePause.OnPaused -= CancelDrag;
        ClearHoverPreview();
        DestroyGhost();

        // Los gates se evalúan ANTES del TryTake: un drop rechazado nunca toca el stock.
        if (IsDropAccepted(dropWorldPoint))
            TryPlaceOnGrill(droppedItem, dropWorldPoint, rotated);

        draggingItem = null;

        if (owner != null)
            owner.NotifyDragEnded(this);
    }

    /// <summary>
    /// Cancela el arrastre en curso sin tocar el stock (el TryTake solo ocurre al soltar).
    /// Lo llama el controlador al cerrar el panel o cambiar de vista.
    /// </summary>
    public void CancelDrag()
    {
        if (!isDragging && ghost == null)
            return;

        isDragging = false;
        GamePause.OnPaused -= CancelDrag;
        ClearHoverPreview();
        DestroyGhost();
        draggingItem = null;

        if (owner != null)
            owner.NotifyDragEnded(this);
    }

    /// <summary>
    /// Decide si el punto de suelte puede colocar el ítem. Soltar sobre el panel siempre
    /// cancela; el área de parrilla solo filtra si está configurada como obligatoria.
    /// </summary>
    private bool IsDropAccepted(Vector3 dropWorldPoint)
    {
        if (owner == null)
            return false;

        if (owner.IsPointOverPanel(dropWorldPoint))
            return false;

        return owner.IsPointInDropArea(dropWorldPoint);
    }

    private void TryPlaceOnGrill(ItemDataSO droppedItem, Vector3 dropWorldPoint, bool rotateFootprint)
    {
        if (droppedItem == null || owner == null)
            return;

        GrillSystem grill = owner.Grill;
        if (grill == null)
        {
            Debug.LogWarning("[StockPanelSlot] No hay GrillSystem cableado en StockPanelController: se cancela el drop.");
            return;
        }

        CoolerSystem cooler = CoolerSystem.Instance;
        if (cooler == null)
            return;

        // Retiro atómico: primero se descuenta, y si el spawn falla se devuelve.
        if (!cooler.TryTake(droppedItem, 1))
            return;

        if (droppedItem is MeatCutSO cut)
        {
            if (!grill.TrySpawnMeatAtPoint(cut, dropWorldPoint, rotateFootprint))
            {
                cooler.Add(droppedItem, 1);
                return;
            }

            TutorialManager.NotifyMeatDraggedToGrill(cut);
            TutorialManager.NotifyMeatPlacedOnGrill(cut);
            return;
        }

        if (droppedItem is CoalSO coal)
        {
            if (!grill.TrySpawnCoalAtPoint(coal, dropWorldPoint, out Coal spawnedCoal) || spawnedCoal == null)
            {
                cooler.Add(droppedItem, 1);
                return;
            }

            TutorialManager.NotifyCoalDraggedToGrill(coal);
            TutorialManager.NotifyCoalPlacedOnGrill(coal);
            return;
        }

        // Tipo desconocido: se devuelve el stock retirado.
        cooler.Add(droppedItem, 1);
    }

    private void ToggleGridRotation()
    {
        isGridRotated = !isGridRotated;
        ApplyGhostRotation();
        UpdateHoverPreview(GetMouseWorldPosition());
    }

    // ── Preview de hover sobre la grilla ────────────────────────────────────

    private void UpdateHoverPreview(Vector3 worldPoint)
    {
        if (owner == null || draggingItem == null)
            return;

        if (!GrillLayerToggle.IsItemTypeAllowed(draggingItem.category))
        {
            ClearHoverPreview();
            return;
        }

        if (draggingItem is MeatCutSO cut)
        {
            MeatTransferBuffer meatBuffer = owner.MeatBuffer;
            if (meatBuffer != null)
                meatBuffer.UpdateMeatHolderHover(cut, worldPoint, isGridRotated);

            return;
        }

        if (draggingItem is CoalSO coal)
        {
            CoalTransferBuffer coalBuffer = owner.CoalBuffer;
            if (coalBuffer != null)
                coalBuffer.UpdateCoalHolderHover(coal, worldPoint);
        }
    }

    private void ClearHoverPreview()
    {
        if (owner == null)
            return;

        MeatTransferBuffer meatBuffer = owner.MeatBuffer;
        if (meatBuffer != null)
            meatBuffer.ClearMeatHolderHover();

        CoalTransferBuffer coalBuffer = owner.CoalBuffer;
        if (coalBuffer != null)
            coalBuffer.ClearCoalHolderHover();
    }

    // ── Ghost ───────────────────────────────────────────────────────────────

    private void CreateGhost(Vector3 worldPoint)
    {
        DestroyGhost();

        ghost = new GameObject("StockPanelDragGhost");
        ghostRenderer = ghost.AddComponent<SpriteRenderer>();
        ghostRenderer.sprite = ResolveIcon(draggingItem);
        ghostRenderer.color = availableColor;
        ghostRenderer.sortingOrder = ghostSortingOrder;

        if (iconRenderer != null)
            ghostRenderer.sortingLayerID = iconRenderer.sortingLayerID;

        ghost.transform.position = worldPoint;
        ghost.transform.localScale = ResolveGhostScale(draggingItem);
        ApplyGhostRotation();
    }

    /// <summary>
    /// Toma la escala del prefab que GrillSystem va a instanciar (Coal.prefab 0.35, Meat1.prefab ~0.5)
    /// y le aplica ghostWorldScale como multiplicador, para que el fantasma se vea del mismo tamano
    /// que el objeto una vez colocado. Sin esto el sprite crudo se arrastra a escala 1 y queda enorme,
    /// sobre todo el carbon.
    /// </summary>
    private Vector3 ResolveGhostScale(ItemDataSO ghostItem)
    {
        GameObject reference = null;

        CoalSO coal = ghostItem as CoalSO;
        if (coal != null)
            reference = coal.coalPrefab;

        GrillSystem grill = owner != null ? owner.Grill : null;
        if (reference == null && grill != null)
            reference = coal != null ? grill.coalPrefab : grill.meatPrefab;

        Vector3 prefabScale = reference != null ? reference.transform.localScale : Vector3.one;

        return new Vector3(
            prefabScale.x * ghostWorldScale.x,
            prefabScale.y * ghostWorldScale.y,
            1f);
    }

    private void ApplyGhostRotation()
    {
        if (ghost == null || !rotateGhostVisual)
            return;

        ghost.transform.localEulerAngles = new Vector3(0f, 0f, isGridRotated ? rotatedPreviewAngleZ : 0f);
    }

    private void DestroyGhost()
    {
        if (ghost != null)
            Destroy(ghost);

        ghost = null;
        ghostRenderer = null;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static Sprite ResolveIcon(ItemDataSO source)
    {
        if (source is MeatCutSO cut)
            return cut.GetDefaultSprite();

        if (source is CoalSO coal)
            return coal.coalSprite;

        return null;
    }

    private Vector3 GetMouseWorldPosition()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return transform.position;

        Vector3 screenPoint = Input.mousePosition;
        screenPoint.z = Mathf.Abs(transform.position.z - cam.transform.position.z);

        Vector3 world = cam.ScreenToWorldPoint(screenPoint);
        world.z = transform.position.z;
        return world;
    }

    private void EnsureCollider2D()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
            return;

        box = gameObject.AddComponent<BoxCollider2D>();
        box.size = Vector2.one;
    }
}
