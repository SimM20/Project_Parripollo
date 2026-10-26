using UnityEngine;

/// <summary>
/// Carbón "en la mano": un click en la celda de carbón del panel de stock lo selecciona, el
/// puntero pasa a mostrar el carbón y cada click sobre la parrilla de carbón pone uno (descuenta
/// del cooler y spawnea por el mismo camino que tenía el arrastre). Se suelta con otro click en
/// la celda, click derecho / X-□, al clickear cualquier otra cosa interactiva (que igual hace lo
/// suyo: el click no se pierde), al volver a la capa de carne, al quedarse sin stock, al cerrar
/// el panel o al pausar.
///
/// La carne sigue con el arrastre de siempre (<see cref="StockPanelSlot"/>). Mientras está
/// activo, los carbones ya puestos no se dejan agarrar (<see cref="Coal"/>): clickearlos apila
/// otro encima. El componente queda apagado mientras no hay carbón en la mano (sin Update ocioso).
/// </summary>
public class CoalPlacementMode : MonoBehaviour
{
    [Header("Ghost")]
    [SerializeField] private int ghostSortingOrder = 6000;
    [Tooltip("Multiplicador sobre la escala del prefab de carbón. 1 = mismo tamaño que en la parrilla.")]
    [SerializeField] private Vector3 ghostWorldScale = Vector3.one;
    [Tooltip("Segundos de la aparición del carbón en el puntero al seleccionarlo.")]
    [SerializeField] [Min(0.01f)] private float popSeconds = 0.12f;
    [Tooltip("Segundos de la vuelta a la celda al soltarlo.")]
    [SerializeField] [Min(0.01f)] private float returnSeconds = 0.16f;

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

    /// <summary>Hay un carbón en la mano. Lo consultan <see cref="Coal"/> (no se agarra) y la navegación del gamepad.</summary>
    public static bool IsActive => armed != null;

    /// <summary>Este modo tiene el carbón seleccionado.</summary>
    public bool IsArmed => armed == this;

    private static CoalPlacementMode armed;

    private StockPanelController owner;
    private CoalSO coal;
    private StockPanelSlot sourceCell;
    private float planeZ;
    private int armedFrame;
    private bool wasCoalLayer;

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
        // Prendido solo con el carbón en la mano.
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

        Vector3 point = GetPointerWorldPosition();
        AnimateGhost(point, Time.unscaledDeltaTime);
        UpdatePreview(point);

        // El click que lo seleccionó no pone carbón.
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

    /// <summary>Pone el carbón en la mano. La celda ya validó stock, panel y tutorial.</summary>
    public void Arm(CoalSO coalData, StockPanelSlot cell)
    {
        if (coalData == null || cell == null || owner == null)
            return;

        if (armed != null && armed != this)
            armed.Disarm(false);

        coal = coalData;
        sourceCell = cell;
        planeZ = cell.transform.position.z;
        armedFrame = Time.frameCount;
        armed = this;
        enabled = true;
        GamePause.OnPaused += DisarmInstant;

        // Seleccionar el carbón ya muestra su capa, por el mismo camino que el botón (tutorial, ícono, eco).
        GrillLayerToggle toggle = GrillLayerToggle.Current;
        if (toggle != null && toggle.CurrentLayer != GrillLayerToggle.GrillLayer.Coal)
            toggle.Toggle();
        wasCoalLayer = GrillLayerToggle.IsItemTypeAllowed(ItemType.Coal);

        ShowGhost(GetPointerWorldPosition());
        InputManager.SetPointerIconHidden(true);
        GamepadHaptics.Play(HapticEvent.PieceGrabbed);
    }

    /// <summary>Suelta el carbón sin tocar el stock. Con <paramref name="animateReturn"/> se lo ve volver a la celda.</summary>
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
        coal = null;
        sourceCell = null;
    }

    /// <summary>Se suelta solo si se terminó el stock, se volvió a la capa de carne o la celda dejó de ser la del carbón.</summary>
    private bool StillArmed()
    {
        if (sourceCell == null || !sourceCell.isActiveAndEnabled || sourceCell.Item != coal)
        {
            Disarm(false);
            return false;
        }

        if (CoolerSystem.Instance == null || CoolerSystem.Instance.GetCount(coal) <= 0)
        {
            Disarm(true);
            return false;
        }

        bool coalLayer = GrillLayerToggle.IsItemTypeAllowed(ItemType.Coal);
        bool leftCoalLayer = wasCoalLayer && !coalLayer;
        wasCoalLayer = coalLayer;
        if (leftCoalLayer)
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

        // Una celda del panel ya resolvió el click (soltar el carbón o arrastrar carne).
        if (pressed != null && pressed.TryGetComponent(out StockPanelSlot _))
            return;

        if (owner.IsPointOverPanel(point))
            return;

        if (IsGrillPiece(pressed) && IsOverCoalGrid(point))
        {
            TryPlace(point);
            return;
        }

        // Otra cosa interactiva: ya hizo lo suyo (el Down llegó antes que este Update) y el carbón se suelta.
        if (pressed != null)
            Disarm(true);
    }

    /// <summary>Lo que hay sobre la parrilla de carbón: nada, un hueco o un carbón ya puesto (no se agarra).</summary>
    private static bool IsGrillPiece(GameObject pressed)
        => pressed == null || pressed.TryGetComponent(out GridSlot _) || pressed.TryGetComponent(out Coal _);

    private bool IsOverCoalGrid(Vector3 point)
    {
        GrillSystem grill = owner.Grill;
        return grill != null && GridSlot.IsOverSlotsOfType(grill.slots, ItemType.Coal, point);
    }

    /// <summary>Descuenta y spawnea con los mismos gates que el drop del stock: un click rechazado nunca toca el stock.</summary>
    private void TryPlace(Vector3 point)
    {
        CoalSO placed = coal;
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

    private void Refuse()
    {
        shakeT = 0f;
        GamepadHaptics.Play(HapticEvent.ActionRefused);
    }

    // ── Preview de colocación ───────────────────────────────────────────────

    /// <summary>
    /// Mismo preview que tenía el arrastre (verde donde cae, rojo si no entra), solo con el puntero
    /// sobre la parrilla de carbón: afuera un click no pone nada. Se rehace al moverse el puntero o
    /// cada <see cref="previewRefreshSeconds"/>, no en todos los frames.
    /// </summary>
    private void UpdatePreview(Vector3 point)
    {
        CoalTransferBuffer buffer = owner.CoalBuffer;
        if (buffer == null)
            return;

        if (owner.IsPointOverPanel(point) || !IsOverCoalGrid(point))
        {
            ClearPreview();
            return;
        }

        float now = Time.unscaledTime;
        if (previewShown && now < nextPreviewTime && (point - lastPreviewPoint).sqrMagnitude < 0.0001f)
            return;

        buffer.UpdateCoalHolderHover(coal, point);
        previewShown = true;
        lastPreviewPoint = point;
        nextPreviewTime = now + previewRefreshSeconds;
    }

    private void ClearPreview()
    {
        if (!previewShown)
            return;

        previewShown = false;
        if (owner != null && owner.CoalBuffer != null)
            owner.CoalBuffer.ClearCoalHolderHover();
    }

    // ── Carbón del puntero ──────────────────────────────────────────────────

    private void ShowGhost(Vector3 point)
    {
        if (ghostRenderer == null)
            ghostRenderer = new GameObject("CoalPlacementGhost").AddComponent<SpriteRenderer>();

        SpriteRenderer icon = sourceCell.IconRenderer;
        ghostRenderer.sprite = coal.coalSprite;
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

    /// <summary>Escala del prefab que se va a spawnear, así el carbón del puntero mide lo mismo que el puesto.</summary>
    private Vector3 ResolveGhostScale()
    {
        GameObject reference = coal.coalPrefab;
        if (reference == null && owner.Grill != null)
            reference = owner.Grill.coalPrefab;

        Vector3 prefabScale = reference != null ? reference.transform.localScale : Vector3.one;
        return new Vector3(prefabScale.x * ghostWorldScale.x, prefabScale.y * ghostWorldScale.y, 1f);
    }

    /// <summary>
    /// El carbón sigue al puntero con vida propia: aparece con rebote, se mece, se aplasta al poner
    /// uno y se sacude (rojizo) si el click no pudo. Tiempo sin escalar: es respuesta al puntero.
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

        Transform ghost = ghostRenderer.transform;
        ghost.SetPositionAndRotation(
            point + new Vector3(shake, Mathf.Sin(time * bobSpeed) * bobAmplitude, 0f),
            Quaternion.Euler(0f, 0f, Mathf.Sin(time * bobSpeed * 0.5f) * swayDegrees));
        ghost.localScale = new Vector3(ghostBaseScale.x * pop * (1f + squash), ghostBaseScale.y * pop * (1f - squash), 1f);
        ghostRenderer.color = Color.Lerp(refuseTint, Color.white, shakeT);
    }

    /// <summary>El carbón vuelve volando a su celda y se apaga: se lee que se soltó sin gastar nada.</summary>
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
