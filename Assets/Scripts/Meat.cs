using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class Meat : Item
{
    [Header("Data")]
    [FormerlySerializedAs("meatData")]
    [SerializeField] public MeatCutSO cut;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Visual Effects Prefabs")]
    [SerializeField] private GameObject heatPrefab;
    [SerializeField] private GameObject smokePrefab;
    [SerializeField] private Vector3 heatOffset = Vector3.zero;
    [Tooltip("Corrimiento del humo respecto del centro del sprite. El area de emision ya cubre la pieza entera.")]
    [SerializeField] private Vector3 smokeOffset = new Vector3(0f, 0.05f, -0.05f);
    [Tooltip("Orden de dibujo del humo relativo al sprite de la carne.")]
    [SerializeField] private int smokeSortingOrderOffset = 4;

    [Header("Aviso de quemado")]
    [Tooltip("Cartel '!' que aparece sobre la pieza cuando se esta por quemar y el puntero no esta encima. Vacio = sin aviso.")]
    [SerializeField] private MeatBurnAlert burnAlertPrefab;

    private GameObject heatInstance;
    private GameObject smokeInstance;
    private BurnSmoke smoke;
    private MeatBurnAlert burnAlert;
    private Transform fxRoot;
    private bool isPointerOver;

    [Header("Drag Float")]
    [Tooltip("Cuanto se despega la pieza del puntero mientras se la arrastra (unidades de mundo).")]
    [SerializeField] private float dragLiftHeight = 0.05f;
    [Tooltip("Amplitud de la oscilacion mientras flota.")]
    [SerializeField] private float dragBobAmplitude = 0.022f;
    [Tooltip("Oscilaciones por segundo mientras flota.")]
    [SerializeField] private float dragBobFrequency = 1.2f;
    [Tooltip("Segundos que tarda en despegar al agarrarla.")]
    [SerializeField] private float dragEaseTime = 0.18f;

    private float floatWeight;
    private float floatTime;
    private Vector3 floatOffset;
    private Vector3 lastFloatedPosition;
    private Vector2 baseColliderOffset;

    [Header("Flip Puff")]
    [SerializeField] private FlipPuff flipPuff;
    [Tooltip("Altura del puff respecto de la base del sprite (contacto con la parrilla).")]
    [SerializeField] private float flipPuffContactOffsetY = 0.04f;

    [Header("Sound")]
    [SerializeField] protected AudioClip hardSound;
    [SerializeField] protected AudioClip softSound;

    [Header("Flip Animation")]
    [SerializeField] private float flipDuration = 0.45f;
    [SerializeField] private float flipLiftHeight = 0.6f;
    [SerializeField] private int flipSortingOrderBoost = 50;
    [SerializeField] private AudioClip flipSound;

    [Header("State Change Feedback")]
    [Tooltip("Cuanto crece la pieza en el golpe al pasar al punto siguiente (0.07 = 7%).")]
    [SerializeField] private float statePulseScale = 0.07f;
    [Tooltip("Multiplicador del golpe al quemarse.")]
    [SerializeField] private float burnedPulseMultiplier = 1.8f;
    [Tooltip("Segundos del golpe.")]
    [Min(0.01f)] [SerializeField] private float statePulseDuration = 0.28f;

    private float statePulseT = 1f;
    private float statePulseAmount;

    private bool isFlipping = false;
    private Coroutine flipCoroutine;
    private Vector3 baseLocalScale = Vector3.one;
    private int baseSortingOrder = 0;

    [Header("Grid Rotation")]
    [SerializeField] private bool rotatePreviewVisual = true;
    [SerializeField] private float rotatedPreviewAngleZ = 90f;

    [Header("Cooking Speed")]
    [Tooltip("Aceleración por estado: cada estado que alcanza una cara suma este porcentaje a su velocidad de cocción. " +
             "0.05 = Crudo x1, Jugoso x1.05, Hecho x1.10, Bien Hecho x1.15, Pasado x1.20.")]
    [Min(0f)]
    [SerializeField] private float cookSpeedBonusPerState = 0.05f;

    [Tooltip("Fracción del calor que le llega a la cara de arriba (la que no está apoyada), antes del frenado por estado. " +
             "0 = no se cocina. Con 0.3, al dar vuelta un corte en Bien Hecho la otra cara está por llegar a Jugoso o recién llegó.")]
    [Range(0f, 1f)]
    [SerializeField] private float idleSideHeatFactor = 0.3f;

    [Tooltip("Frenado por estado de la cara de arriba, el efecto opuesto a cookSpeedBonusPerState: cada estado que alcanzó " +
             "le resta este porcentaje a su velocidad. 0.2 = Crudo x1, Jugoso x0.8, Hecho x0.6, Bien Hecho x0.4, Pasado x0.2. " +
             "Así la cara que ya se cocinó casi no se mueve al darla vuelta. Desde 0.25, Pasado queda frenado del todo.")]
    [Min(0f)]
    [SerializeField] private float idleSideSlowdownPerState = 0.2f;

    [Header("Cooking State (Heat Units)")]
    public float sideACookTime = 0f;
    public float sideBCookTime = 0f;
    public bool isSideA = true;
    public MeatStates state = MeatStates.Crudo;

    private readonly List<GridSlot> occupiedSlots = new List<GridSlot>();
    private int lastCookFrame = -1;
    private bool isGridRotated;
    private Collider2D ownCollider;

    public float SideAProgress01 => HeatScale > 0f ? Mathf.Clamp(sideACookTime, 0f, BurnThreshold) / HeatScale : 0f;
    public float SideBProgress01 => HeatScale > 0f ? Mathf.Clamp(sideBCookTime, 0f, BurnThreshold) / HeatScale : 0f;
    public float CookedPercent01 => Mathf.Clamp01((sideACookTime + sideBCookTime) / Mathf.Max(0.0001f, HeatScale * 2f));
    public float HeatScale => cut != null ? cut.GetHeatScale() : 10000f;
    public float BurnThreshold => cut != null ? cut.GetBurnThreshold() : 10000f;
    public MeatStates SideAState => cut != null ? cut.GetStateForHeat(sideACookTime) : MeatStates.Crudo;
    public MeatStates SideBState => cut != null ? cut.GetStateForHeat(sideBCookTime) : MeatStates.Crudo;
    public MeatStates ActiveSideState => isSideA ? SideAState : SideBState;
    public float ActiveSideProgress01 => isSideA ? SideAProgress01 : SideBProgress01;
    public bool IsAnySideBurned => SideAState == MeatStates.Quemado || SideBState == MeatStates.Quemado;
    public bool IsOnGrill => occupiedSlots.Count > 0 && !isHeldByMouse;
    public bool IsSideAActive => isSideA;
    public bool IsGridRotated => isGridRotated;
    public bool IsFlipping => isFlipping;
    public bool IsCooking => IsCurrentlyCooking();

    /// <summary>
    /// El puntero (mouse o cursor del gamepad) esta sobre la pieza: entre el Enter y el Exit del
    /// <see cref="WorldPointerDispatcher"/>, los mismos que abren y cierran la burbuja de estado.
    /// </summary>
    public bool IsPointerOver => isPointerOver;

    /// <summary>
    /// El sprite de la pieza se esta dibujando. GrillSystem y ViewManager apagan los renderers de las
    /// piezas para ocultarlas; el aviso de quemado, que no es hijo de la pieza, lo sigue por aca.
    /// </summary>
    public bool IsVisualShown => spriteRenderer != null && spriteRenderer.enabled;

    /// <summary>
    /// Posicion de la pieza sin el flote del arrastre. La burbuja y los FX se enganchan aca
    /// para no flotar con la carne.
    /// </summary>
    public Vector3 RestPosition => transform.position - floatOffset;

    /// <summary>Que tan cerca de quemarse esta la cara apoyada: 0 cruda, 1 en el umbral de Quemado.</summary>
    private float ActiveSideBurnProgress01
    {
        get
        {
            float burnThreshold = BurnThreshold;
            float heat = isSideA ? sideACookTime : sideBCookTime;
            return burnThreshold > 0f ? Mathf.Clamp01(heat / burnThreshold) : 0f;
        }
    }

    /// <summary>
    /// Distancia en Y desde el pivote de la pieza hasta el borde de arriba de su sprite, en
    /// unidades de mundo. Usa la escala de reposo y no la actual, para que la burbuja de cocción
    /// (que se apoya acá) no salte con el estirón del flip. Contempla la rotación de la grilla.
    /// Las piezas sin arte (sprite de 1px) devuelven un mínimo para no quedar pegadas al centro.
    /// </summary>
    public float VisualTopOffset
    {
        get
        {
            const float minTop = 0.25f;
            if (spriteRenderer == null || spriteRenderer.sprite == null) return minTop;

            Vector3 restScale = (baseLocalScale.sqrMagnitude > 0.0001f) ? baseLocalScale : transform.localScale;
            if (transform.parent != null)
                restScale = Vector3.Scale(restScale, transform.parent.lossyScale);

            Bounds b = spriteRenderer.sprite.bounds;
            Quaternion rotation = transform.rotation;
            float top = float.MinValue;

            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector3(i < 2 ? b.min.x : b.max.x, (i % 2 == 0) ? b.min.y : b.max.y, 0f);
                top = Mathf.Max(top, (rotation * Vector3.Scale(corner, restScale)).y);
            }

            return Mathf.Max(top, minTop);
        }
    }

    /// <summary>
    /// Segundos que le faltan al corte para quemarse con el calor que recibe ahora: la primera de
    /// las dos caras en llegar, porque la de arriba también se cocina (más lento). Infinito si no
    /// se está cocinando o si ya tiene una cara quemada. Lo usa el aviso de quemado del gamepad
    /// (<see cref="GamepadHaptics.ReportBurnRisk"/>).
    /// </summary>
    public float SecondsToBurn
    {
        get
        {
            if (cut == null || TutorialManager.IsCookingPaused || !IsCurrentlyCooking() || IsAnySideBurned)
                return float.PositiveInfinity;

            float heat = GetTotalHeatReceived();
            return Mathf.Min(SecondsToSideHeat(true, BurnThreshold, heat), SecondsToSideHeat(false, BurnThreshold, heat));
        }
    }

    /// <summary>
    /// Segundos que le faltan a la cara apoyada para acumular <paramref name="targetHeat"/> con el
    /// calor que recibe ahora. Infinito si no se está cocinando o si ya llegó. Lo usa el cartel de
    /// quemado (<see cref="MeatBurnAlert"/>) para aparecer con anticipación.
    /// </summary>
    public float SecondsToActiveSideHeat(float targetHeat)
    {
        if (cut == null || TutorialManager.IsCookingPaused || !IsCurrentlyCooking())
            return float.PositiveInfinity;

        return SecondsToSideHeat(isSideA, targetHeat, GetTotalHeatReceived());
    }

    /// <summary>
    /// Estima con la velocidad del estado actual de la cara. Dentro de una misma banda (en Pasado,
    /// la última antes de Quemado y la única en la que avisa el gamepad) el resultado es exacto.
    /// </summary>
    private float SecondsToSideHeat(bool sideA, float targetHeat, float heatPerSecond)
    {
        float sideHeat = sideA ? sideACookTime : sideBCookTime;
        float remaining = targetHeat - sideHeat;
        float rate = heatPerSecond * GetSideCookRate(sideA, cut.GetStateForHeat(sideHeat));
        return remaining > 0f && rate > 0f ? remaining / rate : float.PositiveInfinity;
    }

    /// <summary>
    /// Qué parte del calor del corte acumula una cara según su estado. La apoyada lo recibe todo y
    /// acelera cookSpeedBonusPerState por cada estado alcanzado; la de arriba recibe
    /// idleSideHeatFactor y frena idleSideSlowdownPerState por cada estado alcanzado.
    /// </summary>
    private float GetSideCookRate(bool sideA, MeatStates sideState)
    {
        int stateIndex = (int)sideState;

        if (sideA == isSideA)
            return 1f + cookSpeedBonusPerState * stateIndex;

        return idleSideHeatFactor * Mathf.Max(0f, 1f - idleSideSlowdownPerState * stateIndex);
    }

    protected virtual void Awake()
    {
        itemType = ItemType.Meat;
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
            baseSortingOrder = spriteRenderer.sortingOrder;

        baseLocalScale = transform.localScale;
        ownCollider = GetComponent<Collider2D>();
        if (ownCollider != null)
            baseColliderOffset = ownCollider.offset;

        if (flipPuff == null)
            flipPuff = GetComponentInChildren<FlipPuff>(true);

        ApplyCutVisual();
    }

    protected override void Update()
    {
        base.Update();
        UpdateDragFloat();
        UpdateStatePulse();
        UpdateEffects();
        GamepadHaptics.ReportBurnRisk(SecondsToBurn);
    }

    // ── Flote del arrastre ───────────────────────────────────────────────────

    /// <summary>
    /// Mientras se arrastra, la pieza va despegada del puntero y oscila lento. El drag la vuelve
    /// a poner bajo el puntero cada frame y aca se le suma el flote, asi que la deteccion de slots
    /// (UpdateHoverPreview, que corre antes) y el drop usan siempre la posicion sin flote. El humo
    /// y el calor no flotan (StabilizeFxRoot) y el collider se compensa para quedar bajo el puntero.
    /// </summary>
    private void UpdateDragFloat()
    {
        bool canFloat = isHeldByMouse && !isFlipping;
        if (!canFloat)
        {
            ClearDragFloat();
            return;
        }

        floatWeight = dragEaseTime > 0f ? Mathf.MoveTowards(floatWeight, 1f, Time.deltaTime / dragEaseTime) : 1f;

        // Arranca en fase 0 (sin salto) y sube: el despegue y la oscilacion se suman.
        floatTime += Time.deltaTime;
        float bob = Mathf.Sin(floatTime * dragBobFrequency * 2f * Mathf.PI) * dragBobAmplitude;
        float lift = Mathf.SmoothStep(0f, 1f, floatWeight) * (dragLiftHeight + bob);
        SetFloatOffset(new Vector3(0f, lift, 0f));
    }

    private void SetFloatOffset(Vector3 offset)
    {
        // Si algo movio la pieza desde el ultimo frame (el drag lo hace cada frame), esa ya es
        // la posicion de reposo.
        Vector3 rest = transform.position;
        if (rest == lastFloatedPosition)
            rest -= floatOffset;

        floatOffset = offset;
        lastFloatedPosition = rest + offset;
        transform.position = lastFloatedPosition;

        if (ownCollider != null)
        {
            Vector2 localOffset = offset == Vector3.zero ? Vector2.zero : (Vector2)transform.InverseTransformVector(offset);
            ownCollider.offset = baseColliderOffset - localOffset;
        }
    }

    /// <summary>Apoya la pieza al toque: al soltarla o al cancelar el agarre, que usan su posicion.</summary>
    private void ClearDragFloat()
    {
        if (floatOffset != Vector3.zero)
            SetFloatOffset(Vector3.zero);

        floatWeight = 0f;
        floatTime = 0f;
    }

    // ── Cambio de punto ──────────────────────────────────────────────────────

    /// <summary>
    /// La cara apoyada paso al punto siguiente por la coccion: puff de humo (mas fuerte cuanto
    /// mas cocida) y un golpe de escala en la pieza. El cartel lo detecta por su cuenta.
    /// Quemado no pide puff: BurnSmoke ya larga su golpe propio al quemarse.
    /// </summary>
    private void PlayStateAdvanceFeedback(MeatStates newState)
    {
        bool burnedNow = newState == MeatStates.Quemado;

        if (smoke != null && !burnedNow)
            smoke.Puff(Mathf.InverseLerp((int)MeatStates.Jugoso, (int)MeatStates.Pasado, (int)newState));

        statePulseAmount = statePulseScale * (burnedNow ? burnedPulseMultiplier : 1f);
        if (!isFlipping && !isHeldByMouse)
            statePulseT = 0f;
    }

    /// <summary>
    /// Sube y baja la escala de reposo con un seno: termina exacto en baseLocalScale. El flip y el
    /// agarre manejan la escala por su cuenta, asi que el golpe se corta si arranca alguno.
    /// </summary>
    private void UpdateStatePulse()
    {
        if (statePulseT >= 1f) return;

        if (isFlipping)
        {
            statePulseT = 1f;   // el flip ya pisa la escala cada frame y la deja en reposo
            return;
        }

        if (isHeldByMouse)
        {
            StopStatePulse();
            return;
        }

        statePulseT = Mathf.Min(1f, statePulseT + Time.deltaTime / statePulseDuration);
        float pulse = 1f + statePulseAmount * Mathf.Sin(statePulseT * Mathf.PI);
        Vector3 baseScale = (baseLocalScale.sqrMagnitude > 0.0001f) ? baseLocalScale : Vector3.one;
        transform.localScale = new Vector3(baseScale.x * pulse, baseScale.y * pulse, baseScale.z);
    }

    /// <summary>Corta el golpe y deja la pieza en su escala de reposo.</summary>
    private void StopStatePulse()
    {
        if (statePulseT >= 1f) return;

        statePulseT = 1f;
        transform.localScale = (baseLocalScale.sqrMagnitude > 0.0001f) ? baseLocalScale : Vector3.one;
    }

    // ── Efectos ──────────────────────────────────────────────────────────────

    private void UpdateEffects()
    {
        bool showHeat = IsCurrentlyCooking();
        bool isBurned = IsAnySideBurned;

        // El aviso de quemado se crea con la primera coccion y despues decide solo cuando se ve.
        // Va suelto en la escena y no bajo el nodo FX (ver MeatBurnAlert); se destruye con la pieza.
        if (burnAlert == null && burnAlertPrefab != null && showHeat)
        {
            burnAlert = Instantiate(burnAlertPrefab);
            burnAlert.Bind(this);
        }

        if (heatInstance == null && heatPrefab == null && smokeInstance == null && smokePrefab == null)
            return;

        StabilizeFxRoot();

        if (heatInstance != null)
        {
            if (heatInstance.activeSelf != showHeat)
                heatInstance.SetActive(showHeat);
        }
        else if (heatPrefab != null && showHeat)
        {
            heatInstance = Instantiate(heatPrefab, EnsureFxRoot());
            heatInstance.transform.localPosition = heatOffset;
            heatInstance.transform.localRotation = Quaternion.identity;
            heatInstance.SetActive(true);
        }

        // Humo: acompaña toda la coccion, se espesa a medida que la cara apoyada se acerca a
        // quemarse y se dispara al quemarse (un corte quemado humea aunque no este en la parrilla).
        // Vive colgado de este corte: varios cortes => varias instancias independientes.
        bool showSmoke = showHeat || isBurned;

        if (smokeInstance == null && smokePrefab != null && showSmoke)
        {
            smokeInstance = Instantiate(smokePrefab, EnsureFxRoot());
            smokeInstance.transform.localRotation = Quaternion.identity;
            smoke = smokeInstance.GetComponent<BurnSmoke>();
            SetUpSmokeRenderers(smokeInstance);
        }

        if (smokeInstance == null) return;

        PlaceSmokeSource();

        if (smoke != null)
            smoke.SetState(showSmoke, ActiveSideBurnProgress01, isBurned);
        else if (smokeInstance.activeSelf != showSmoke)
            smokeInstance.SetActive(showSmoke);
    }

    /// <summary>
    /// El humo sale de toda la superficie de la pieza y no de un punto: le pasa el rectangulo
    /// del sprite en reposo (contempla la rotacion de la grilla, no el estiron del flip) en el
    /// espacio local del humo, y lo centra sobre el sprite.
    /// </summary>
    private void PlaceSmokeSource()
    {
        Vector3 center = Vector3.zero;
        Vector3 size = new Vector3(0.3f, 0.3f, 0f);

        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            Vector3 restScale = (baseLocalScale.sqrMagnitude > 0.0001f) ? baseLocalScale : transform.localScale;
            if (transform.parent != null)
                restScale = Vector3.Scale(restScale, transform.parent.lossyScale);

            Bounds b = spriteRenderer.sprite.bounds;
            Quaternion rotation = transform.rotation;
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, 0f);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, 0f);

            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector3(i < 2 ? b.min.x : b.max.x, (i % 2 == 0) ? b.min.y : b.max.y, 0f);
                Vector3 p = rotation * Vector3.Scale(corner, restScale);
                min = Vector3.Min(min, new Vector3(p.x, p.y, 0f));
                max = Vector3.Max(max, new Vector3(p.x, p.y, 0f));
            }

            // De mundo al espacio del humo: el nodo FX no rota, solo escala.
            Vector3 unit = smokeInstance.transform.lossyScale;
            center = new Vector3(SafeDivide(min.x + max.x, 2f * unit.x), SafeDivide(min.y + max.y, 2f * unit.y), 0f);
            size = new Vector3(SafeDivide(max.x - min.x, unit.x), SafeDivide(max.y - min.y, unit.y), 0f);
        }

        smokeInstance.transform.localPosition = smokeOffset + center;
        if (smoke != null)
            smoke.SetSourceSize(size);
    }

    private static float SafeDivide(float value, float divisor)
    {
        return Mathf.Abs(divisor) < 0.0001f ? value : value / divisor;
    }

    private Transform EnsureFxRoot()
    {
        if (fxRoot == null)
        {
            fxRoot = new GameObject("FX").transform;
            fxRoot.SetParent(transform, false);
        }
        return fxRoot;
    }

    /// <summary>
    /// El humo tiene que subir en vertical de mundo y no deformarse: la carne rota 90 grados
    /// con R y el flip le aplasta la escala en X. El nodo FX cancela ambas cosas, y tambien el
    /// flote del arrastre: humo y calor no se despegan con la pieza.
    /// </summary>
    private void StabilizeFxRoot()
    {
        if (fxRoot == null) return;

        fxRoot.position = RestPosition;
        fxRoot.rotation = Quaternion.identity;

        Vector3 baseScale = (baseLocalScale.sqrMagnitude > 0.0001f) ? baseLocalScale : Vector3.one;
        Vector3 current = transform.localScale;

        fxRoot.localScale = new Vector3(
            SafeRatio(baseScale.x, current.x),
            SafeRatio(baseScale.y, current.y),
            SafeRatio(baseScale.z, current.z));
    }

    private static float SafeRatio(float baseValue, float current)
    {
        if (Mathf.Abs(current) < 0.01f) return 1f;
        return Mathf.Clamp(baseValue / current, 0.05f, 20f);
    }

    /// <summary>
    /// El humo se dibuja por encima de su propia carne. Se usa baseSortingOrder y no el
    /// orden vivo porque durante el flip el sprite sube +flipSortingOrderBoost.
    /// </summary>
    private void SetUpSmokeRenderers(GameObject smoke)
    {
        Renderer[] renderers = smoke.GetComponentsInChildren<Renderer>(true);
        int meatLayer = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;

        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sortingLayerID = meatLayer;
            renderers[i].sortingOrder = baseSortingOrder + smokeSortingOrderOffset;
        }
    }

    /// <summary>Puff de game feel del flip: se dispara desde la base del corte, donde toca la parrilla.</summary>
    private void EmitFlipPuff()
    {
        if (flipPuff == null) return;

        Vector3 origin = transform.position;
        if (spriteRenderer != null)
        {
            Bounds b = spriteRenderer.bounds;
            origin = new Vector3(b.center.x, b.min.y + flipPuffContactOffsetY, transform.position.z);
        }

        flipPuff.Play(origin);
    }

    private bool IsCurrentlyCooking()
    {
        return IsOnGrill && GetTotalHeatReceived() > 0.01f;
    }

    /// <summary>
    /// Calor total que recibe el corte este frame: la suma de todos los slots que ocupa.
    /// Es la misma cantidad que acumula Cook(), y sirve para que audio y VFX se decidan
    /// una vez por corte y no una vez por slot.
    /// </summary>
    protected float GetTotalHeatReceived()
    {
        float total = 0f;
        for (int i = 0; i < occupiedSlots.Count; i++)
        {
            GridSlot slot = occupiedSlots[i];
            if (slot != null)
                total += slot.totalHeatReceived;
        }
        return total;
    }

    protected int OccupiedSlotCount => occupiedSlots.Count;

    public void SetCut(MeatCutSO newCut)
    {
        cut = newCut;
        if (!CanRotate && isGridRotated)
            SetGridRotation(false);
        ApplyCutVisual();
    }

    public virtual void Cook(float heatFromSlot)
    {
        if (TutorialManager.IsCookingPaused)
            return;

        if (lastCookFrame == Time.frameCount)
            return;

        lastCookFrame = Time.frameCount;

        float deltaHeat = GetTotalHeatReceived() * Time.deltaTime;

        // La cara apoyada acelera con cada estado y la de arriba frena (ver GetSideCookRate).
        // Al alcanzar el umbral de Quemado deja de acumular (clamp).
        float burnThreshold = BurnThreshold;

        bool sideAWasBurned = sideACookTime >= burnThreshold;
        bool sideBWasBurned = sideBCookTime >= burnThreshold;

        float sideAGain = deltaHeat * GetSideCookRate(true, SideAState);
        float sideBGain = deltaHeat * GetSideCookRate(false, SideBState);

        sideACookTime = Mathf.Min(sideACookTime + sideAGain, burnThreshold);
        sideBCookTime = Mathf.Min(sideBCookTime + sideBGain, burnThreshold);

        if ((!sideAWasBurned && sideACookTime >= burnThreshold) || (!sideBWasBurned && sideBCookTime >= burnThreshold))
            GamepadHaptics.Play(HapticEvent.MeatBurned);

        // El feedback sale de aca y no de RefreshState: ese tambien corre al restaurar una pieza
        // (MeatTransferBuffer) y ahi el punto "salta" sin que la carne se haya cocinado.
        MeatStates stateBefore = state;
        RefreshState();
        if (state > stateBefore)
            PlayStateAdvanceFeedback(state);
    }

    public override void OnWorldPointerUp()
    {
        // El Up no trae Drag: la pieza sigue con el flote del frame anterior. Se apoya antes de
        // decidir el destino, que se mide con la posicion bajo el puntero.
        ClearDragFloat();
        if (!EndHold()) return;

        if (TrashZone.TryConsumeAtWorldPoint(transform.position, this))
            return;

        if (TrySendToBuildBuffer(transform.position))
            return;

        // El plato admite una sola carne: si ya tiene una, este corte vuelve al lugar donde
        // estaba en vez de caer en los slots de la grilla que el plato tapa.
        if (BuildFoodDropZone.IsPlateOccupiedAt(transform.position))
        {
            transform.position = startPosition;
            RestoreHoverIfPointerOver();
            return;
        }

        Vector2Int requiredSize = GetRequiredGridSize();
        GridSlot[] allSlots = FindObjectsOfType<GridSlot>();

        if (GridSlot.TryFindContiguousPlacement(allSlots, requiredSize, transform.position, itemType, gameObject, out List<GridSlot> slotsEncontrados))
        {
            ReleaseOccupiedSlots();

            foreach (var s in slotsEncontrados)
            {
                s.PlaceMeat(this);
            }

            currentSlot = slotsEncontrados[0];

            Vector3 rawOffset = (cut != null) ? cut.visualOffset : Vector3.zero;
            Vector3 rotatedOffset = transform.rotation * rawOffset;
            transform.position = CalcCenter(slotsEncontrados) + rotatedOffset;

            RestoreHoverIfPointerOver();
            return;
        }

        transform.position = startPosition;
        RestoreHoverIfPointerOver();
    }

    /// <summary>
    /// Tras soltar la pieza, reactiva burbuja y barra si el puntero nunca salió del collider
    /// (WorldPointerDispatcher no dispara OnWorldPointerEnter de nuevo en ese caso).
    /// </summary>
    private void RestoreHoverIfPointerOver()
    {
        if (isHeldByMouse || ownCollider == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 pointerWorld = GetMouseWorldPosition();
        if (!ownCollider.OverlapPoint(pointerWorld)) return;

        ShowHover();
    }

    private void ShowHover()
    {
        if (MeatHoverBubble.Instance != null)
            MeatHoverBubble.Instance.Show(this);   // la barra de coccion va adentro de la burbuja
    }

    private bool TrySendToBuildBuffer(Vector3 dropWorldPoint)
    {
        if (!TutorialManager.CheckMeatDragToBuildAllowed())
            return false;

        MeatTransferBuffer transferBuffer = FindFirstObjectByType<MeatTransferBuffer>();
        if (transferBuffer == null) return false;
        return transferBuffer.TryPlateMeatFromGrill(this, dropWorldPoint);
    }

    protected override void HandleHeldInput()
    {
        if (InputManager.WasPressed(GameAction.Rotate))
            ToggleGridRotation();
    }

    protected override void OnPickedUp()
    {
        if (isFlipping)
            CancelFlipAnimation();

        StopStatePulse();

        if (MeatHoverBubble.Instance != null)
            MeatHoverBubble.Instance.Hide();

        ApplyGridRotationPreview();
        UpdateHoverPreview();
        TutorialSignals.Raise(TutorialSignal.PieceGrabbed, meat: this);
    }

    protected override void UpdateHoverPreview()
    {
        Vector2Int requiredSize = GetRequiredGridSize();
        GridSlot[] allSlots = FindObjectsOfType<GridSlot>();

        if (GridSlot.TryFindContiguousPlacement(allSlots, requiredSize, transform.position, itemType, gameObject, out List<GridSlot> slotsEncontrados))
        {
            SetHoverPreview(slotsEncontrados, true);
            return;
        }

        Collider2D hit = Physics2D.OverlapPoint(transform.position);
        if (hit != null && hit.TryGetComponent<GridSlot>(out GridSlot hoveredSlot))
        {
            SetHoverPreview(hoveredSlot, false);
            return;
        }

        ClearHoverPreview();
    }

    private Vector3 CalcCenter(List<GridSlot> slots)
    {
        Vector3 centro = Vector3.zero;
        foreach (var s in slots) centro += s.transform.position;
        return centro / slots.Count;
    }

    public void Flip()
    {
        if (isFlipping) return;

        if (gameObject.activeInHierarchy)
        {
            flipCoroutine = StartCoroutine(FlipRoutine());
        }
        else
        {
            isSideA = !isSideA;
            state = ActiveSideState;
            ApplyCutVisual();
            TutorialSignals.Raise(TutorialSignal.MeatFlipped, meat: this);
        }
    }

    private IEnumerator FlipRoutine()
    {
        isFlipping = true;

        Vector3 startPos = transform.position;
        Vector3 initialLocalScale = (baseLocalScale.sqrMagnitude > 0.0001f) ? baseLocalScale : transform.localScale;
        int origSortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder : baseSortingOrder;

        if (spriteRenderer != null)
            spriteRenderer.sortingOrder = origSortingOrder + flipSortingOrderBoost;

        PlayFlipSound();

        // Arranque del giro: el corte recien se despega de la parrilla.
        EmitFlipPuff();

        float elapsed = 0f;
        bool logicFlipped = false;

        while (elapsed < flipDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flipDuration);

            // Parabolic lift arc
            float lift = Mathf.Sin(t * Mathf.PI) * flipLiftHeight;
            transform.position = startPos + Vector3.up * lift;

            // Horizontal flip scale (compressing towards 0 and expanding back to full width)
            float cosVal = Mathf.Cos(t * Mathf.PI);
            float scaleX = Mathf.Abs(cosVal) * initialLocalScale.x;

            // Subtle vertical stretch in flight
            float scaleY = initialLocalScale.y * (1f + 0.12f * Mathf.Sin(t * Mathf.PI));
            transform.localScale = new Vector3(scaleX, scaleY, initialLocalScale.z);

            // Mid-air flip point (at peak of the jump / when scaleX reaches 0)
            if (!logicFlipped && t >= 0.5f)
            {
                logicFlipped = true;
                isSideA = !isSideA;
                state = ActiveSideState;
                ApplyCutVisual();
                TutorialSignals.Raise(TutorialSignal.MeatFlipped, meat: this);
            }

            yield return null;
        }

        // Safety guarantee for logical flip
        if (!logicFlipped)
        {
            isSideA = !isSideA;
            state = ActiveSideState;
            ApplyCutVisual();
            TutorialSignals.Raise(TutorialSignal.MeatFlipped, meat: this);
        }

        transform.position = startPos;
        transform.localScale = initialLocalScale;

        if (spriteRenderer != null)
            spriteRenderer.sortingOrder = origSortingOrder;

        // Landing bounce / impact juice
        yield return StartCoroutine(LandingBounce(initialLocalScale));

        isFlipping = false;
        flipCoroutine = null;
    }

    private IEnumerator LandingBounce(Vector3 targetScale)
    {
        float bounceDuration = 0.08f;
        float elapsed = 0f;
        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / bounceDuration);
            float bounceAmount = Mathf.Sin(t * Mathf.PI) * 0.12f;
            transform.localScale = new Vector3(
                targetScale.x * (1f + bounceAmount),
                targetScale.y * (1f - bounceAmount),
                targetScale.z
            );
            yield return null;
        }
        transform.localScale = targetScale;
    }

    public void CancelFlipAnimation()
    {
        if (flipCoroutine != null)
        {
            StopCoroutine(flipCoroutine);
            flipCoroutine = null;
        }

        if (isFlipping)
        {
            isFlipping = false;
            transform.localScale = (baseLocalScale.sqrMagnitude > 0.0001f) ? baseLocalScale : Vector3.one;
            if (spriteRenderer != null)
                spriteRenderer.sortingOrder = baseSortingOrder;
            ApplyCutVisual();
        }
    }

    /// <summary>
    /// Sin flipSound asignado el flip es mudo. No cae a softSound: ese es el loop de coccion
    /// (18 s) y reproducirlo como one-shot sonaba a "la carne chisporrotea aunque no haya calor",
    /// porque el sizzle de MeatInstance no controla el AudioSource base.
    /// </summary>
    private void PlayFlipSound()
    {
        if (flipSound == null) return;

        AudioSource src = GetComponent<AudioSource>();
        if (src != null)
        {
            src.PlayOneShot(flipSound);
        }
        else
        {
            AudioSource.PlayClipAtPoint(flipSound, transform.position);
        }
    }

    /// <summary>Los cortes de footprint cuadrado no se rotan: quedarian identicos.</summary>
    public bool CanRotate => cut != null && cut.CanRotate;

    public void ToggleGridRotation()
    {
        if (!CanRotate) return;

        SetGridRotation(!isGridRotated);
        if (isHeldByMouse) UpdateHoverPreview();
        TutorialSignals.Raise(TutorialSignal.PieceRotated, meat: this);
    }

    public void SetGridRotation(bool rotated)
    {
        isGridRotated = rotated && CanRotate;
        ApplyGridRotationPreview();
    }

    public void RegisterOccupiedSlot(GridSlot slot)
    {
        if (slot == null) return;
        if (!occupiedSlots.Contains(slot)) occupiedSlots.Add(slot);
        if (currentSlot == null) currentSlot = slot;
    }

    public void UnregisterOccupiedSlot(GridSlot slot)
    {
        if (slot == null) return;
        occupiedSlots.Remove(slot);
        if (currentSlot == slot)
            currentSlot = occupiedSlots.Count > 0 ? occupiedSlots[0] : null;
    }

    public void ReleaseOccupiedSlots()
    {
        for (int i = occupiedSlots.Count - 1; i >= 0; i--)
        {
            GridSlot slot = occupiedSlots[i];
            if (slot != null && slot.currentItem == gameObject)
                slot.ClearSlot();
        }
        occupiedSlots.Clear();
        currentSlot = null;
    }

    /// <summary>
    /// Deriva el estado visible desde el calor acumulado de la cara activa.
    /// Los estados por cara siempre se derivan de los floats (SideAState/SideBState).
    /// </summary>
    public void RefreshState()
    {
        MeatStates newState = ActiveSideState;

        if (newState != state)
        {
            state = newState;
            ApplyCutVisual();
            TutorialSignals.Raise(TutorialSignal.MeatStateChanged, meat: this);
        }
    }

    private Vector2Int GetRequiredGridSize()
    {
        if (cut == null) return Vector2Int.one;
        Vector2Int space = cut.GrillSpace;
        if (isGridRotated) space = new Vector2Int(space.y, space.x);
        return new Vector2Int(Mathf.Max(1, space.x), Mathf.Max(1, space.y));
    }

    private void ApplyGridRotationPreview()
    {
        if (!rotatePreviewVisual) return;
        transform.eulerAngles = new Vector3(0, 0, isGridRotated ? rotatedPreviewAngleZ : 0f);
    }

    private void ApplyCutVisual()
    {
        if (spriteRenderer == null || cut == null) return;

        Sprite targetSprite = cut.GetSpriteForState(state, isSideA);

        if (targetSprite == null)
            targetSprite = cut.GetSpriteForSide(isSideA) ?? cut.GetDefaultSprite();

        if (targetSprite != null)
            spriteRenderer.sprite = targetSprite;

        spriteRenderer.flipX = !isSideA;
    }

    void OnWorldPointerEnter()
    {
        isPointerOver = true;
        if (isHeldByMouse) return;
        ShowHover();
    }

    void OnWorldPointerExit()
    {
        isPointerOver = false;
        if (MeatHoverBubble.Instance != null)
            MeatHoverBubble.Instance.Hide();
    }

    /// <summary>La pausa corta el arrastre: la pieza vuelve a su lugar apoyada, sin el flote.</summary>
    protected override void CancelDrag()
    {
        ClearDragFloat();
        base.CancelDrag();
    }

    public override void OnDisable()
    {
        base.OnDisable();
        if (isFlipping)
            CancelFlipAnimation();

        StopStatePulse();
        ClearDragFloat();
        isPointerOver = false;   // apagada no recibe el Exit del dispatcher
    }

    void OnDestroy()
    {
        if (isFlipping)
            CancelFlipAnimation();

        ReleaseOccupiedSlots();

        if (burnAlert != null)
            Destroy(burnAlert.gameObject);

        if (MeatHoverBubble.Instance != null)
            MeatHoverBubble.Instance.HideIfTarget(this);
    }

    void OnValidate()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        itemType = ItemType.Meat;
        ApplyCutVisual();
        ApplyGridRotationPreview();
    }
}