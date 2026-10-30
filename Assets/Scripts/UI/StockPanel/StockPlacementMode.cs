using UnityEngine;

/// <summary>
/// Stock "en la mano": un click en una celda del panel de stock la selecciona, el puntero pasa a
/// mostrar la pieza y un click sobre la parrilla de su capa la pone (descuenta del cooler y
/// spawnea). Se suelta con otro click en la celda, click derecho / X-□, al clickear cualquier otra
/// cosa interactiva (que igual hace lo suyo: el click no se pierde), al cambiar de capa, al
/// quedarse sin stock, al cerrar el panel o al pausar.
///
///  • Carbón: queda en la mano y cada click pone uno más (apila hasta 3).
///  • Carne: en la mano va **una sola unidad** del corte. Al ponerla la mano queda vacía; para otra
///    hay que volver a la celda. R / B rota el footprint mientras está en la mano.
///
/// Mientras está activo, las piezas ya puestas no se dejan agarrar (<see cref="Coal"/>,
/// <see cref="Meat"/>): clickearlas pone la de la mano (apila el carbón; la carne va al bloque
/// libre más cercano). El componente queda apagado mientras no hay nada en la mano (sin Update ocioso).
/// </summary>
public class StockPlacementMode : MonoBehaviour
{
    [Header("Ghost")]
    [SerializeField] private int ghostSortingOrder = 6000;
    [Tooltip("Multiplicador sobre la escala del prefab que se va a spawnear. 1 = mismo tamaño que en la parrilla.")]
    [SerializeField] private Vector3 ghostWorldScale = Vector3.one;
    [Tooltip("Segundos de la aparición de la pieza en el puntero al seleccionarla.")]
    [SerializeField] [Min(0.01f)] private float popSeconds = 0.12f;
    [Tooltip("Segundos de la vuelta a la celda al soltarla.")]
    [SerializeField] [Min(0.01f)] private float returnSeconds = 0.16f;
    [Tooltip("Giro del corte en la mano cuando su footprint va rotado (R / B).")]
    [SerializeField] private float rotatedAngleZ = 90f;

    [Header("Idle")]
    [Tooltip("Vaivén vertical mientras está en la mano (unidades de mundo).")]
    [SerializeField] [Min(0f)] private float bobAmplitude = 0.025f;
    [SerializeField] [Min(0f)] private float bobSpeed = 5f;
    [Tooltip("Balanceo en grados mientras está en la mano.")]
    [SerializeField] [Min(0f)] private float swayDegrees = 5f;

    [Header("Place / Refuse")]
    [Tooltip("Aplastado del carbón del puntero al poner uno (0.2 = 20 %).")]
    [SerializeField] [Min(0f)] private float stampAmount = 0.22f;
    [SerializeField] [Min(0.01f)] private float stampSeconds = 0.14f;
    [Tooltip("Sacudida cuando el click no puede poner nada (unidades de mundo).")]
    [SerializeField] [Min(0f)] private float shakeAmplitude = 0.06f;
    [SerializeField] [Min(0.01f)] private float shakeSeconds = 0.22f;
    [SerializeField] private Color refuseTint = new Color(1f, 0.45f, 0.35f, 1f);
    [Tooltip("Onda en el hueco donde cae cada carbón.")]
    [SerializeField] private bool landingEcho = true;

    [Header("Preview")]
    [Tooltip("Cada cuánto se rehace el preview aunque el puntero no se mueva (cenizas que se limpian, carbones que se apagan).")]
    [SerializeField] [Min(0.02f)] private float previewRefreshSeconds = 0.2f;

    /// <summary>Hay algo del stock en la mano. Lo consultan <see cref="Coal"/> y <see cref="Meat"/> (no se agarran).</summary>
    public static bool IsActive => armed != null;

    /// <summary>Hay carbón del stock en la mano.</summary>
    public static bool IsCoalActive => armed != null && armed.item is CoalSO;

    /// <summary>Hay una unidad de carne del stock en la mano.</summary>
    public static bool IsMeatActive => armed != null && armed.item is MeatCutSO;

    /// <summary>El corte en la mano, o null. Lo usa la navegación del gamepad para medir los bloques.</summary>
    public static MeatCutSO HeldCut => armed != null ? armed.item as MeatCutSO : null;

    /// <summary>El corte en la mano va rotado (R / B).</summary>
    public static bool HeldCutRotated => armed != null && armed.isGridRotated;

    /// <summary>Este modo tiene algo seleccionado.</summary>
    public bool IsArmed => armed == this;

    /// <summary>Celda de la que salió lo que está en la mano.</summary>
    public StockPanelSlot SourceCell => IsArmed ? sourceCell : null;

    private static StockPlacementMode armed;

    private StockPanelController owner;
    private ItemDataSO item;
    private StockPanelSlot sourceCell;
    private ItemType itemType;
    private bool isGridRotated;
    private float planeZ;
    private int armedFrame;
    private bool wasOwnLayer;

    private SpriteRenderer ghostRenderer;
    private Vector3 ghostBaseScale = Vector3.one;
    private float popT = 1f;
    private float stampT = 1f;
    private float shakeT = 1f;

    private bool previewShown;
    private Vector3 lastPreviewPoint;
    private float nextPreviewTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => armed = null;

    void Awake()
    {
        // Prendido solo con algo en la mano.
        enabled = false;
    }

    void OnDisable()
    {
        if (IsArmed)
            Release(false);
    }

    public void Bind(StockPanelController panel) => owner = panel;

    void Update()
    {
        if (!IsArmed)
        {
            enabled = false;
            return;
        }

        if (!StillArmed())
            return;

        // Nota: R también dispara GameManager.CleanAshes; el doble binding ya existía con el arrastre.
        if (item is MeatCutSO && InputManager.WasPressed(GameAction.Rotate))
            ToggleGridRotation();

        Vector3 point = GetPointerWorldPosition();
        AnimateGhost(point, Time.unscaledDeltaTime);
        UpdatePreview(point);

        // El click que lo seleccionó no pone nada.
        if (Time.frameCount == armedFrame)
            return;

        if (InputManager.SecondaryPressed)
        {
            Disarm(true);
            return;
        }

        if (InputManager.PrimaryPressed)
            HandleClick(point);
    }

    // ── Seleccionar / soltar ────────────────────────────────────────────────

    /// <summary>
    /// Pone en la mano el ítem de la celda (carbón o una unidad de carne). La celda ya validó
    /// stock, panel y tutorial. Si había otra cosa en la mano, vuelve a su celda.
    /// </summary>
    public void Arm(ItemDataSO stockItem, StockPanelSlot cell)
    {
        if (!(stockItem is CoalSO || stockItem is MeatCutSO) || cell == null || owner == null)
            return;

        if (armed != null)
            armed.Disarm(true);

        item = stockItem;
        itemType = stockItem is CoalSO ? ItemType.Coal : ItemType.Meat;
        sourceCell = cell;
        isGridRotated = false;
        planeZ = cell.transform.position.z;
        armedFrame = Time.frameCount;
        armed = this;
        enabled = true;
        GamePause.OnPaused += DisarmInstant;

        // Seleccionar la pieza ya muestra su capa, por el mismo camino que el botón (tutorial, ícono, eco).
        GrillLayerToggle.GrillLayer layer = itemType == ItemType.Coal ? GrillLayerToggle.GrillLayer.Coal : GrillLayerToggle.GrillLayer.Meat;
        GrillLayerToggle toggle = GrillLayerToggle.Current;
        if (toggle != null && toggle.CurrentLayer != layer)
            toggle.Toggle();
        wasOwnLayer = GrillLayerToggle.IsItemTypeAllowed(itemType);

        ShowGhost(GetPointerWorldPosition());
        InputManager.SetPointerIconHidden(true);
        GamepadHaptics.Play(HapticEvent.PieceGrabbed);

        if (item is MeatCutSO cut)
            TutorialSignals.Raise(TutorialSignal.PieceGrabbed, cut: cut, target: ghostRenderer.transform);
    }

    /// <summary>Suelta lo que hay en la mano sin tocar el stock. Con <paramref name="animateReturn"/> se lo ve volver a la celda.</summary>
    public void Disarm(bool animateReturn)
    {
        if (!IsArmed)
            return;

        Release(animateReturn);
        enabled = false;
    }

    private void DisarmInstant() => Disarm(false);

    private void Release(bool animateReturn)
    {
        armed = null;
        GamePause.OnPaused -= DisarmInstant;
        ClearPreview();

        if (animateReturn)
            PlayReturn();

        if (ghostRenderer != null)
            ghostRenderer.gameObject.SetActive(false);

        InputManager.SetPointerIconHidden(false);
        item = null;
        sourceCell = null;
    }

    /// <summary>Se suelta solo si se terminó el stock, se cambió de capa o la celda dejó de ser la de ese ítem.</summary>
    private bool StillArmed()
    {
        if (sourceCell == null || !sourceCell.isActiveAndEnabled || sourceCell.Item != item)
        {
            Disarm(false);
            return false;
        }

        if (CoolerSystem.Instance == null || CoolerSystem.Instance.GetCount(item) <= 0)
        {
            Disarm(true);
            return false;
        }

        bool ownLayer = GrillLayerToggle.IsItemTypeAllowed(itemType);
        bool leftOwnLayer = wasOwnLayer && !ownLayer;
        wasOwnLayer = ownLayer;
        if (leftOwnLayer)
        {
            Disarm(true);
            return false;
        }

        return true;
    }

    // ── Click ───────────────────────────────────────────────────────────────

    private void HandleClick(Vector3 point)
    {
        GameObject pressed = InputManager.PressedObject;

        // Una celda del panel ya resolvió el click (soltar lo de la mano o tomar otra cosa).
        if (pressed != null && pressed.TryGetComponent(out StockPanelSlot _))
            return;

        if (owner.IsPointOverPanel(point))
            return;

        if (IsGrillPiece(pressed) && IsOverOwnGrid(point))
        {
            if (item is CoalSO)
                TryPlaceCoal(point);
            else
                TryPlaceMeat(point);
            return;
        }

        // Otra cosa interactiva: ya hizo lo suyo (el Down llegó antes que este Update) y la pieza se suelta.
        if (pressed != null)
            Disarm(true);
    }

    /// <summary>Lo que hay sobre la parrilla de la capa: nada, un hueco o una pieza ya puesta (no se agarra).</summary>
    private bool IsGrillPiece(GameObject pressed)
    {
        if (pressed == null || pressed.TryGetComponent(out GridSlot _))
            return true;

        return itemType == ItemType.Coal ? pressed.TryGetComponent(out Coal _) : pressed.TryGetComponent(out Meat _);
    }

    /// <summary>
    /// El gate de la parrilla es necesario: <c>TryFindContiguousPlacement</c> busca el hueco libre
    /// más cercano sin límite de distancia, así que sin él un click en un cliente ponía la pieza.
    /// </summary>
    private bool IsOverOwnGrid(Vector3 point)
    {
        GrillSystem grill = owner.Grill;
        return grill != null && GridSlot.IsOverSlotsOfType(grill.slots, itemType, point);
    }

    /// <summary>Descuenta y spawnea con los mismos gates que el drop del stock: un click rechazado nunca toca el stock.</summary>
    private void TryPlaceCoal(Vector3 point)
    {
        CoalSO placed = (CoalSO)item;
        GrillSystem grill = owner.Grill;
        CoolerSystem cooler = CoolerSystem.Instance;

        if (grill == null || cooler == null || !GrillLayerToggle.IsItemTypeAllowed(ItemType.Coal)
            || !TutorialManager.CheckStockDragAllowed(placed) || !cooler.TryTake(placed, 1))
        {
            Refuse();
            return;
        }

        if (!grill.TrySpawnCoalAtPoint(placed, point, out Coal spawned) || spawned == null)
        {
            cooler.Add(placed, 1);
            Refuse();
            return;
        }

        // Solo visual: el carbón baja desde el puntero hasta su hueco.
        Vector3 rest = spawned.transform.position;
        spawned.PlayDropSettle(ghostRenderer != null ? ghostRenderer.transform.position : point);
        PlayLandingEcho(spawned, rest);
        stampT = 0f;
        nextPreviewTime = 0f;

        // Las señales pueden cerrar el panel o pausar (y soltar el carbón): van con la copia local.
        TutorialSignals.Raise(TutorialSignal.CoalDraggedToGrill, coal: placed, target: spawned.transform);
        TutorialSignals.Raise(TutorialSignal.CoalPlacedOnGrill, coal: placed, target: spawned.transform);
    }

    /// <summary>
    /// Pone la única unidad de carne de la mano: descuenta, spawnea (rollback si no entra) y la mano
    /// queda vacía. Rechazado, el corte sigue en la mano y el stock no se tocó.
    /// </summary>
    private void TryPlaceMeat(Vector3 point)
    {
        MeatCutSO cut = (MeatCutSO)item;
        bool rotated = isGridRotated;
        GrillSystem grill = owner.Grill;
        CoolerSystem cooler = CoolerSystem.Instance;

        if (grill == null || cooler == null || !GrillLayerToggle.IsItemTypeAllowed(ItemType.Meat)
            || !TutorialManager.CheckStockDragAllowed(cut) || !cooler.TryTake(cut, 1))
        {
            Refuse();
            return;
        }

        if (!grill.TrySpawnMeatAtPoint(cut, point, out Meat spawned, rotated) || spawned == null)
        {
            cooler.Add(cut, 1);
            Refuse();
            return;
        }

        // Solo visual: el corte real sigue desde donde estaba el de la mano.
        spawned.PlayDropSettle(ghostRenderer != null ? ghostRenderer.transform.position : point);

        // Una sola unidad: se puso y la mano queda vacía (sin vuelta a la celda, ya está en la parrilla).
        Disarm(false);

        TutorialSignals.Raise(TutorialSignal.MeatDraggedToGrill, cut: cut, meat: spawned);
        TutorialSignals.Raise(TutorialSignal.MeatPlacedOnGrill, cut: cut, meat: spawned);
    }

    private void Refuse()
    {
        shakeT = 0f;
        GamepadHaptics.Play(HapticEvent.ActionRefused);
    }

    /// <summary>Los cortes de footprint cuadrado no se rotan: quedarían idénticos.</summary>
    private void ToggleGridRotation()
    {
        if (!(item is MeatCutSO cut) || !cut.CanRotate)
            return;

        isGridRotated = !isGridRotated;
        nextPreviewTime = 0f;
        previewShown = false;
        GamepadHaptics.Play(HapticEvent.PieceRotated);
        TutorialSignals.Raise(TutorialSignal.PieceRotated, cut: cut);
    }

    // ── Preview de colocación ───────────────────────────────────────────────

    /// <summary>
    /// Mismo preview que tenía el arrastre (verde donde cae, rojo si no entra), solo con el puntero
    /// sobre la parrilla de su capa: afuera un click no pone nada. Se rehace al moverse el puntero
    /// o cada <see cref="previewRefreshSeconds"/>, no en todos los frames.
    /// </summary>
    private void UpdatePreview(Vector3 point)
    {
        if (owner.IsPointOverPanel(point) || !IsOverOwnGrid(point))
        {
            ClearPreview();
            return;
        }

        float now = Time.unscaledTime;
        if (previewShown && now < nextPreviewTime && (point - lastPreviewPoint).sqrMagnitude < 0.0001f)
            return;

        if (item is CoalSO coal)
        {
            if (owner.CoalBuffer == null)
                return;
            owner.CoalBuffer.UpdateCoalHolderHover(coal, point);
        }
        else
        {
            if (owner.MeatBuffer == null)
                return;
            owner.MeatBuffer.UpdateMeatHolderHover((MeatCutSO)item, point, isGridRotated);
        }

        previewShown = true;
        lastPreviewPoint = point;
        nextPreviewTime = now + previewRefreshSeconds;
    }

    private void ClearPreview()
    {
        if (!previewShown)
            return;

        previewShown = false;
        if (owner == null)
            return;

        if (itemType == ItemType.Coal)
        {
            if (owner.CoalBuffer != null)
                owner.CoalBuffer.ClearCoalHolderHover();
        }
        else if (owner.MeatBuffer != null)
            owner.MeatBuffer.ClearMeatHolderHover();
    }

    // ── Pieza del puntero ───────────────────────────────────────────────────

    private void ShowGhost(Vector3 point)
    {
        if (ghostRenderer == null)
            ghostRenderer = new GameObject("StockPlacementGhost").AddComponent<SpriteRenderer>();

        SpriteRenderer icon = sourceCell.IconRenderer;
        ghostRenderer.sprite = item is CoalSO coal ? coal.coalSprite : ((MeatCutSO)item).GetDefaultSprite();
        ghostRenderer.sortingOrder = ghostSortingOrder;
        if (icon != null)
            ghostRenderer.sortingLayerID = icon.sortingLayerID;

        ghostBaseScale = ResolveGhostScale();
        popT = 0f;
        stampT = 1f;
        shakeT = 1f;
        previewShown = false;

        ghostRenderer.gameObject.SetActive(true);
        AnimateGhost(point, 0f);
    }

    /// <summary>Escala del prefab que se va a spawnear, así la pieza del puntero mide lo mismo que la puesta.</summary>
    private Vector3 ResolveGhostScale()
    {
        GameObject reference = item is CoalSO coal ? coal.coalPrefab : null;
        if (reference == null && owner.Grill != null)
            reference = itemType == ItemType.Coal ? owner.Grill.coalPrefab : owner.Grill.meatPrefab;

        Vector3 prefabScale = reference != null ? reference.transform.localScale : Vector3.one;
        return new Vector3(prefabScale.x * ghostWorldScale.x, prefabScale.y * ghostWorldScale.y, 1f);
    }

    /// <summary>
    /// La pieza sigue al puntero con vida propia: aparece con rebote, se mece, se aplasta al poner
    /// un carbón y se sacude (rojiza) si el click no pudo. Tiempo sin escalar: es respuesta al puntero.
    /// </summary>
    private void AnimateGhost(Vector3 point, float dt)
    {
        if (ghostRenderer == null)
            return;

        popT = Mathf.Min(1f, popT + dt / popSeconds);
        stampT = Mathf.Min(1f, stampT + dt / stampSeconds);
        shakeT = Mathf.Min(1f, shakeT + dt / shakeSeconds);

        float time = Time.unscaledTime;
        float pop = Mathf.LerpUnclamped(0.6f, 1f, FeelEase.OutBack(popT, 1.6f));
        float squash = FeelEase.Bump(stampT) * stampAmount;
        float shake = Mathf.Sin(shakeT * Mathf.PI * 6f) * shakeAmplitude * (1f - shakeT);
        float baseAngle = isGridRotated ? rotatedAngleZ : 0f;

        Transform ghost = ghostRenderer.transform;
        ghost.SetPositionAndRotation(
            point + new Vector3(shake, Mathf.Sin(time * bobSpeed) * bobAmplitude, 0f),
            Quaternion.Euler(0f, 0f, baseAngle + Mathf.Sin(time * bobSpeed * 0.5f) * swayDegrees));
        ghost.localScale = new Vector3(ghostBaseScale.x * pop * (1f + squash), ghostBaseScale.y * pop * (1f - squash), 1f);
        ghostRenderer.color = Color.Lerp(refuseTint, Color.white, shakeT);
    }

    /// <summary>La pieza vuelve volando a su celda y se apaga: se lee que se soltó sin gastar nada.</summary>
    private void PlayReturn()
    {
        if (ghostRenderer == null || sourceCell == null || sourceCell.IconRenderer == null)
            return;

        SpriteGhost.Spawn(ghostRenderer, SpriteGhost.Motion.Into(sourceCell.IconRenderer.transform.position, returnSeconds, 0.6f, false));
    }

    /// <summary>Onda en el hueco donde queda el carbón (no donde arranca el asentado).</summary>
    private void PlayLandingEcho(Coal placed, Vector3 rest)
    {
        SpriteRenderer sr = placed.VisualRenderer;
        if (!landingEcho || sr == null || sr.sprite == null)
            return;

        SpriteGhost.Spawn(sr.sprite, rest, sr.transform.rotation, sr.transform.lossyScale, sr.color,
            sr.sortingLayerID, sr.sortingOrder, SpriteGhost.Motion.Echo());
    }

    private Vector3 GetPointerWorldPosition()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return transform.position;

        Vector3 screenPoint = InputManager.PointerPosition;
        screenPoint.z = Mathf.Abs(planeZ - cam.transform.position.z);

        Vector3 world = cam.ScreenToWorldPoint(screenPoint);
        world.z = planeZ;
        return world;
    }
}
