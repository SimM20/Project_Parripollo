using TMPro;
using UnityEngine;

/// <summary>
/// Celda del panel de stock: un ícono + un contador numérico por variedad.
/// Carne: es dueña de todo el arrastre (WorldPointerDispatcher nunca transfiere Drag/Up a otro
/// collider): crea un ghost visual, muestra el preview sobre la grilla y al soltar descuenta del
/// cooler y spawnea en la parrilla, con rollback si el spawn falla.
/// Carbón: no se arrastra. La celda es un interruptor del <see cref="CoalPlacementMode"/> (click =
/// carbón en la mano, otro click = soltarlo) y late mientras está seleccionado.
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
    [Tooltip("Color del contador cuando quedan pocas unidades (y al menos una).")]
    [SerializeField] private Color lowCountColor = new Color(1f, 0.74f, 0.32f, 1f);
    [Tooltip("Con esta cantidad o menos el contador toma lowCountColor. 0 = nunca.")]
    [Min(0)] [SerializeField] private int lowCountThreshold = 3;

    [Header("Feedback")]
    [Tooltip("Escala del icono con el puntero encima. Solo el icono: la celda y su collider no cambian.")]
    [SerializeField] private float iconHoverScale = 1.12f;
    [SerializeField] [Min(0.01f)] private float iconScaleSmoothTime = 0.06f;
    [Tooltip("Alfa del icono mientras se arrastra una unidad desde esta celda.")]
    [Range(0f, 1f)] [SerializeField] private float iconDraggingAlpha = 0.45f;
    [Tooltip("Segundos de la aparicion del fantasma al agarrar.")]
    [SerializeField] [Min(0.01f)] private float ghostPopSeconds = 0.12f;
    [Tooltip("Segundos de la vuelta del fantasma a la celda cuando se suelta donde no entra.")]
    [SerializeField] [Min(0.01f)] private float ghostReturnSeconds = 0.16f;
    [Tooltip("Golpe del contador cuando cambia la cantidad (0.25 = 25 %).")]
    [SerializeField] private float countPunch = 0.25f;
    [Tooltip("Latido del icono de carbón mientras está en la mano (0.06 = 6 %), sobre iconHoverScale.")]
    [SerializeField] private float iconArmedPulse = 0.06f;
    [SerializeField] [Min(0f)] private float iconArmedPulseSpeed = 7f;

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

    /// <summary>El corte que se está arrastrando desde esta celda va rotado (R / B).</summary>
    public bool IsDragRotated => isGridRotated;

    /// <summary>Icono de la celda (destino de la vuelta del carbón al soltarlo).</summary>
    public SpriteRenderer IconRenderer => iconRenderer;

    private bool IsCoalArmedHere => item is CoalSO && owner != null && owner.CoalPlacement != null && owner.CoalPlacement.IsArmed;

    private ItemDataSO item;
    private StockPanelController owner;
    private bool isInteractable;

    private ItemDataSO draggingItem;
    private GameObject ghost;
    private SpriteRenderer ghostRenderer;
    private bool isDragging;
    private bool isGridRotated;

    // Presentacion: el icono es un hijo sin collider, se puede escalar sin tocar el area de la celda.
    private Vector3 iconBaseScale = Vector3.one;
    private float iconScale = 1f;
    private float iconScaleVelocity;
    private bool pointerOver;
    private Vector3 ghostBaseScale = Vector3.one;
    private float ghostPopT = 1f;
    private ItemDataSO lastBoundItem;
    private int lastBoundCount = -1;

    void Awake()
    {
        if (iconRenderer == null)
            iconRenderer = GetComponent<SpriteRenderer>();

        if (iconRenderer != null)
            iconBaseScale = iconRenderer.transform.localScale;

        EnsureCollider2D();
    }

    void Update()
    {
        UpdateIconFeedback();
        UpdateGhostPop();

        if (!isDragging || draggingItem == null)
            return;

        // Nota: R también dispara GameManager.CleanAshes en la vista de parrilla.
        // Ese doble binding ya existía con los arrastres del MeatHolder; se replica tal cual.
        if (!(draggingItem is MeatCutSO))
            return;

        if (InputManager.WasPressed(GameAction.Rotate))
            ToggleGridRotation();
    }

    void OnDisable()
    {
        pointerOver = false;
        ResetIconScale();

        if (!isDragging)
            return;

        CancelDrag();
    }

    // ── Feedback de la celda (solo visual) ──────────────────────────────────

    void OnWorldPointerEnter() => pointerOver = true;

    void OnWorldPointerExit() => pointerOver = false;

    /// <summary>
    /// El icono crece con el puntero encima (si hay algo para agarrar) y vuelve al soltar; con el
    /// carbón en la mano late. Tiempo sin escalar: es respuesta al puntero. El collider es de la
    /// celda y no se toca.
    /// </summary>
    private void UpdateIconFeedback()
    {
        if (iconRenderer == null) return;

        float target = IsCoalArmedHere
            ? iconHoverScale + Mathf.Sin(Time.unscaledTime * iconArmedPulseSpeed) * iconArmedPulse
            : pointerOver && isInteractable && !isDragging ? iconHoverScale : 1f;
        if (Mathf.Abs(iconScale - target) < 0.0005f && Mathf.Abs(iconScaleVelocity) < 0.0005f)
            return;

        iconScale = Mathf.SmoothDamp(iconScale, target, ref iconScaleVelocity, iconScaleSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        iconRenderer.transform.localScale = iconBaseScale * iconScale;
    }

    private void ResetIconScale()
    {
        iconScale = 1f;
        iconScaleVelocity = 0f;
        if (iconRenderer != null)
            iconRenderer.transform.localScale = iconBaseScale;
    }

    /// <summary>El fantasma aparece creciendo desde la celda en vez de saltar a tamaño completo.</summary>
    private void UpdateGhostPop()
    {
        if (ghost == null || ghostPopT >= 1f) return;

        ghostPopT = Mathf.Min(1f, ghostPopT + Time.unscaledDeltaTime / ghostPopSeconds);
        ghost.transform.localScale = ghostBaseScale * Mathf.LerpUnclamped(0.6f, 1f, FeelEase.OutBack(ghostPopT, 1.6f));
    }

    /// <summary>Icono atenuado mientras una unidad de esta celda va en la mano; normal al terminar.</summary>
    private void SetIconDragging(bool dragging)
    {
        if (iconRenderer == null) return;

        Color c = isInteractable ? availableColor : emptyColor;
        if (dragging) c.a *= iconDraggingAlpha;
        iconRenderer.color = c;
    }

    /// <summary>El drop no sirvió: el fantasma vuelve volando a la celda y se apaga (el stock no se tocó).</summary>
    private void PlayGhostReturn()
    {
        if (ghostRenderer == null || iconRenderer == null) return;

        SpriteGhost.Spawn(ghostRenderer, SpriteGhost.Motion.Into(iconRenderer.transform.position, ghostReturnSeconds, 0.6f, false));
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
            SetIconDragging(isDragging);
            FitIconToSlot();
        }

        if (countLabel != null)
        {
            countLabel.text = count.ToString();
            bool low = isInteractable && lowCountThreshold > 0 && count <= lowCountThreshold;
            countLabel.color = !isInteractable ? emptyColor : low ? lowCountColor : availableColor;

            // Golpe del contador solo si cambió la cantidad de la misma variedad (no al reordenar
            // las celdas del pool). Es un texto sin collider.
            if (boundItem != null && boundItem == lastBoundItem && lastBoundCount >= 0 && count != lastBoundCount)
                ScalePunch.Punch(countLabel.transform, countPunch, 0.2f, true);
        }

        lastBoundItem = boundItem;
        lastBoundCount = count;
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
        iconBaseScale = new Vector3(factor, factor, 1f);
        iconRenderer.transform.localScale = iconBaseScale * iconScale;
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

    void OnWorldPointerDown()
    {
        CoalPlacementMode placement = owner != null ? owner.CoalPlacement : null;

        // Carbón: la celda es un interruptor. Con el carbón en la mano, este click lo suelta.
        if (item is CoalSO && placement != null && placement.IsArmed)
        {
            placement.Disarm(true);
            return;
        }

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

        if (item is CoalSO coal)
        {
            if (placement != null)
                placement.Arm(coal, this);
            return;
        }

        // Agarrar carne suelta el carbón que estuviera en la mano; el arrastre sigue igual.
        if (placement != null)
            placement.Disarm(true);

        // Y muestra la capa de carne, por el mismo camino que el botón (tutorial, ícono, eco).
        GrillLayerToggle toggle = GrillLayerToggle.Current;
        if (toggle != null && toggle.CurrentLayer != GrillLayerToggle.GrillLayer.Meat)
            toggle.Toggle();

        draggingItem = item;
        isDragging = true;
        GamePause.OnPaused += CancelDrag;
        isGridRotated = false;

        Vector3 mouseWorld = GetMouseWorldPosition();
        CreateGhost(mouseWorld);
        SetIconDragging(true);

        owner.NotifyDragStarted(this);
        UpdateHoverPreview(mouseWorld);
        GamepadHaptics.Play(HapticEvent.PieceGrabbed);

        if (draggingItem is MeatCutSO cut)
            TutorialSignals.Raise(TutorialSignal.PieceGrabbed, cut: cut, target: ghost.transform);
    }

    void OnWorldPointerDrag()
    {
        if (!isDragging || draggingItem == null)
            return;

        Vector3 mouseWorld = GetMouseWorldPosition();

        if (ghost != null)
            ghost.transform.position = mouseWorld;

        UpdateHoverPreview(mouseWorld);
    }

    void OnWorldPointerUp()
    {
        if (!isDragging || draggingItem == null)
            return;

        Vector3 dropWorldPoint = GetMouseWorldPosition();
        ItemDataSO droppedItem = draggingItem;
        bool rotated = isGridRotated;

        isDragging = false;
        GamePause.OnPaused -= CancelDrag;
        ClearHoverPreview();
        SetIconDragging(false);

        // Los gates se evalúan ANTES del TryTake: un drop rechazado nunca toca el stock.
        bool placed = IsDropAccepted(dropWorldPoint) && TryPlaceOnGrill(droppedItem, dropWorldPoint, rotated);

        // Colocado: la pieza real sigue desde donde estaba el fantasma (asentado propio).
        // Rechazado: el fantasma vuelve a la celda, así se lee que no se usó nada del stock.
        if (!placed)
        {
            PlayGhostReturn();

            // Soltar sobre el propio panel es cancelar, no un error: ahí no vibra.
            if (owner != null && !owner.IsPointOverPanel(dropWorldPoint))
                GamepadHaptics.Play(HapticEvent.ActionRefused);
        }

        DestroyGhost();

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
        SetIconDragging(false);
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

    /// <summary>Descuenta y spawnea en la parrilla. Devuelve true solo si la pieza quedó colocada.</summary>
    private bool TryPlaceOnGrill(ItemDataSO droppedItem, Vector3 dropWorldPoint, bool rotateFootprint)
    {
        if (droppedItem == null || owner == null)
            return false;

        GrillSystem grill = owner.Grill;
        if (grill == null)
        {
            Debug.LogWarning("[StockPanelSlot] No hay GrillSystem cableado en StockPanelController: se cancela el drop.");
            return false;
        }

        CoolerSystem cooler = CoolerSystem.Instance;
        if (cooler == null)
            return false;

        // Retiro atómico: primero se descuenta, y si el spawn falla se devuelve.
        if (!cooler.TryTake(droppedItem, 1))
            return false;

        if (droppedItem is MeatCutSO cut)
        {
            if (!grill.TrySpawnMeatAtPoint(cut, dropWorldPoint, out Meat spawnedMeat, rotateFootprint))
            {
                cooler.Add(droppedItem, 1);
                return false;
            }

            // Solo visual: el corte se ve llegar desde donde estaba el fantasma.
            spawnedMeat.PlayDropSettle(ghost != null ? ghost.transform.position : dropWorldPoint);

            TutorialSignals.Raise(TutorialSignal.MeatDraggedToGrill, cut: cut, meat: spawnedMeat);
            TutorialSignals.Raise(TutorialSignal.MeatPlacedOnGrill, cut: cut, meat: spawnedMeat);
            return true;
        }

        // Tipo desconocido (el carbón no se arrastra: va por CoalPlacementMode): se devuelve el stock retirado.
        cooler.Add(droppedItem, 1);
        return false;
    }

    /// <summary>Los cortes de footprint cuadrado no se rotan: quedarian identicos.</summary>
    private bool CanRotate => draggingItem is MeatCutSO cut && cut.CanRotate;

    private void ToggleGridRotation()
    {
        if (!CanRotate) return;

        isGridRotated = !isGridRotated;
        ApplyGhostRotation();
        UpdateHoverPreview(GetMouseWorldPosition());
        GamepadHaptics.Play(HapticEvent.PieceRotated);
        TutorialSignals.Raise(TutorialSignal.PieceRotated, cut: draggingItem as MeatCutSO);
    }

    // ── Preview de hover sobre la grilla ────────────────────────────────────

    private void UpdateHoverPreview(Vector3 worldPoint)
    {
        if (owner == null || draggingItem == null)
            return;

        // Con la capa equivocada el buffer pinta el bloque en rojo: no se puede soltar ahí.
        if (draggingItem is MeatCutSO cut)
        {
            MeatTransferBuffer meatBuffer = owner.MeatBuffer;
            if (meatBuffer != null)
                meatBuffer.UpdateMeatHolderHover(cut, worldPoint, isGridRotated);
        }
    }

    private void ClearHoverPreview()
    {
        if (owner == null)
            return;

        MeatTransferBuffer meatBuffer = owner.MeatBuffer;
        if (meatBuffer != null)
            meatBuffer.ClearMeatHolderHover();
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
        ghostBaseScale = ResolveGhostScale(draggingItem);
        ghostPopT = 0f;
        ghost.transform.localScale = ghostBaseScale * 0.6f;
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

        Vector3 screenPoint = InputManager.PointerPosition;
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
