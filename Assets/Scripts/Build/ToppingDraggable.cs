using System.Collections.Generic;
using UnityEngine;

public class ToppingDraggable : MonoBehaviour
{
    [Header("Food Data")]
    [Tooltip("The ToppingSO this container represents (Chimichurri, Salsa Criolla, etc.).")]
    [SerializeField] private ToppingSO toppingData;

    public ToppingSO ToppingData => toppingData;

    [Tooltip("Frasco que no lee ni gasta el ToppingStock: arranca lleno cada vez. Para el tutorial, " +
             "que no debe tocar la salsa de la run.")]
    [SerializeField] private bool ignoreStock;

    [Header("Rotation")]
    [Tooltip("Collider2D that defines where the container starts rotating. " +
             "Create a separate GameObject with a BoxCollider2D above the food.")]
    [SerializeField] private Collider2D rotationZone;

    [Tooltip("Degrees per second the container rotates when inside the zone.")]
    [SerializeField] private float rotationSpeed = 360f;

    /// <summary>Zona donde el frasco se da vuelta y vierte (el destino al arrastrarlo).</summary>
    public Collider2D PouringZone => rotationZone;

    [Header("Pour Visual")]
    [Tooltip("Color base de la salsa (el cuerpo del chorro y del charco).")]
    [SerializeField] private Color sauceColor = new Color(0.18f, 0.54f, 0.14f, 1f);

    [Tooltip("Ancho del chorro al salir de la boca. Se afina al caer.")]
    [SerializeField] private float sauceWidth = 0.06f;

    [Tooltip("Distancia hacia abajo desde la boca del frasco donde la salsa toca la mesa. " +
             "Si ese punto cae fuera del plato, el chorro se desvía hasta el borde del plato.")]
    [SerializeField] private float sauceLength = 2f;

    [Tooltip("Optional child Transform at the tip of the container. " +
             "If null, defaults to this transform's position.")]
    [SerializeField] private Transform pourOrigin;

    [Header("Wobbly Stream")]
    [Tooltip("Number of points in the sauce stream. More = smoother curve.")]
    [SerializeField] private int streamSegments = 10;

    [Tooltip("Cuánto se arquea el chorro hacia atrás por cada unidad/s que se mueve el frasco.")]
    [SerializeField] private float streamSwayPerSpeed = 0.06f;

    [Tooltip("Arqueo máximo (unidades de mundo) a mitad del chorro, por más rápido que se mueva el frasco.")]
    [SerializeField] private float streamMaxSway = 0.25f;

    [Tooltip("Segundos que tarda el arqueo en seguir al movimiento. Más alto = más suave y perezoso.")]
    [SerializeField, Min(0.01f)] private float streamSwaySmoothTime = 0.08f;

    [Tooltip("Horizontal wobble intensity from Perlin noise.")]
    [SerializeField] private float wobbleAmplitude = 0.04f;

    [Tooltip("Speed of the Perlin noise scroll. Higher = faster wobble.")]
    [SerializeField] private float wobbleSpeed = 4f;

    [Tooltip("Velocidad inicial (unidades/s) con la que el chorro sale del frasco.")]
    [SerializeField] private float streamGrowSpeed = 3f;

    [Tooltip("Aceleración (unidades/s²) del chorro al caer: sale lento y llega al plato cada vez más rápido. " +
             "Las salpicaduras no aparecen hasta que el chorro toca el plato.")]
    [SerializeField] private float streamFallAcceleration = 14f;

    [Header("Sauce Pixel Art")]
    [Tooltip("Tamaño en mundo de un pixel de salsa (chorro, gotas y charco). 0.02 ≈ 2 pixeles de pantalla a 1080p.")]
    [SerializeField, Min(0.005f)] private float saucePixelSize = 0.02f;

    [Tooltip("Contorno: el borde de abajo y de la derecha del charco y los costados del chorro. " +
             "Alfa 0 = sale del color base.")]
    [SerializeField] private Color sauceOutlineColor = new Color(0f, 0f, 0f, 0f);

    [Tooltip("Sombra: la panza del charco (la fila sobre el contorno) y el lado derecho del chorro. Alfa 0 = automático.")]
    [SerializeField] private Color sauceShadowColor = new Color(0f, 0f, 0f, 0f);

    [Tooltip("Luz: el borde de arriba del charco y el lado izquierdo del chorro. Alfa 0 = automático.")]
    [SerializeField] private Color sauceLightColor = new Color(0f, 0f, 0f, 0f);

    [Tooltip("Brillo: los reflejos cortos de la salsa mojada. Alfa 0 = automático.")]
    [SerializeField] private Color sauceHighlightColor = new Color(0f, 0f, 0f, 0f);

    [Tooltip("Aceite o jugo que se escurre alrededor del charco, un pixel por fuera del borde. " +
             "El alfa es cuánto se ve (0 = sin halo).")]
    [SerializeField] private Color sauceHaloColor = new Color(0f, 0f, 0f, 0f);

    [Header("Sauce Chunks")]
    [Tooltip("Colores de los trocitos que bajan por el chorro y quedan en el charco (cebolla, morrón, hierbas...). " +
             "Vacío = salsa lisa.")]
    [SerializeField] private Color[] sauceChunkColors;

    [Tooltip("Trocitos por segundo que salen por el chorro.")]
    [SerializeField, Min(0f)] private float streamChunkRate = 150f;

    [Tooltip("Fracción de los pixeles del charco que tienen un trocito.")]
    [SerializeField, Range(0f, 0.5f)] private float puddleChunkDensity = 0.12f;

    [Header("Landing Marker")]
    [Tooltip("Muestra dónde va a caer la salsa: un aro sobre el plato y, antes de verter, una guía " +
             "punteada desde la boca del frasco hasta ese punto.")]
    [SerializeField] private bool showLandingMarker = true;

    [Tooltip("Color del aro y de la guía punteada.")]
    [SerializeField] private Color landingMarkerColor = Color.white;

    [Tooltip("Diámetro mínimo del aro en unidades de mundo. Mientras vierte crece con el charco.")]
    [SerializeField, Min(0.05f)] private float landingMarkerSize = 0.35f;

    [Tooltip("Ancho de la guía punteada.")]
    [SerializeField] private float previewLineWidth = 0.035f;

    [Tooltip("Rayas por unidad de mundo en la guía punteada.")]
    [SerializeField, Min(0.5f)] private float previewDashesPerUnit = 6f;

    [Tooltip("Velocidad con la que las rayas bajan hacia el aro.")]
    [SerializeField] private float previewFlowSpeed = 2f;

    [Tooltip("Sorting order del aro (arriba de las salpicaduras, debajo del chorro).")]
    [SerializeField] private int landingMarkerSortingOrder = 5600;

    [Header("Sauce on Plate")]
    [Tooltip("Segundos entre tics de la vertida: vibración del gamepad y chance de que salte una gotita.")]
    [SerializeField] private float splatterInterval = 0.05f;

    [Tooltip("Margen (mundo) que se deja entre el impacto y el borde del plato, junto con el charco.")]
    [SerializeField] private float splatterSize = 0.15f;

    [Tooltip("Radio del charco alrededor del impacto que se reserva al acercarse al borde del plato " +
             "(y el mínimo del aro). El charco dibujado crece solo, con la salsa que cae.")]
    [SerializeField] private float splatterSpread = 0.1f;

    [Tooltip("Fracción de splatterSpread con la que nace el charco cuando el chorro toca el plato.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float puddleStartSpread = 0.85f;

    [Tooltip("Gotitas que saltan de golpe cuando el chorro toca el plato.")]
    [SerializeField, Min(0)] private int landingSplashCount = 9;

    [Tooltip("Segundos vertiendo en el mismo lugar hasta que el charco reservado llega a splatterSpread.")]
    [SerializeField, Min(0.01f)] private float puddleGrowTime = 0.8f;

    [Tooltip("Cuánta salsa cae por segundo. Más = el charco crece más rápido y el cordón que queda " +
             "al mover el frasco sale más ancho.")]
    [SerializeField, Min(0f)] private float pourVolumeRate = 1400f;

    [Tooltip("Qué tan espesa es la salsa: más alto = se apila y se abre menos; más bajo = se escurre más.")]
    [SerializeField, Range(0.05f, 2f)] private float sauceThickness = 0.35f;

    [Tooltip("Segundos entre las ondas que salen del impacto mientras se vierte quieto.")]
    [SerializeField, Min(0.05f)] private float rippleInterval = 0.22f;

    [Tooltip("Aplastamiento vertical de la mesa 2.5D: el charco se abre en un óvalo, no en un círculo.")]
    [Range(0.2f, 1f)]
    [SerializeField] private float perspectiveSquash = 0.6f;

    [Tooltip("Chance por tic de splatterInterval de que salte una gotita del impacto.")]
    [Range(0f, 1f)]
    [SerializeField] private float dropletChance = 0.12f;

    [Tooltip("Hasta qué distancia (mundo) del impacto cae una gotita.")]
    [SerializeField] private float dropletDistance = 0.1f;

    [Tooltip("Sorting order del charco (arriba de la comida). Cada charco nuevo va un poco más arriba.")]
    [SerializeField] private int splatterSortingOrder = 5500;

    [Tooltip("Padre de los charcos. Null = en la raíz de la escena.")]
    [SerializeField] private Transform splatterParent;

    [Header("Squeeze")]
    [Tooltip("How much the bottle is squeezed while pouring (0 = off).")]
    [SerializeField] private float squeezeAmount = 0.06f;

    [Tooltip("Squeezes per second while pouring.")]
    [SerializeField] private float squeezeFrequency = 6f;

    [Header("Fallback Visual")]
    [Tooltip("Color used to render a square placeholder when no sprite is assigned.")]
    [SerializeField] private Color placeholderColor = new Color(0.25f, 0.6f, 0.2f, 1f);

    [Header("Sauce Bar")]
    [Tooltip("Width of the sauce bar in bar pixels (frame included).")]
    [SerializeField, Min(10)] private int barWidthPixels = 44;

    [Tooltip("Size of one bar pixel in the container's local units. 0.12 = one pixel of the bottle art (12 texels at 100 PPU).")]
    [SerializeField] private float barPixelSize = 0.12f;

    [Tooltip("Gap (world units) between the top of the container sprite (the cap) and the bar.")]
    [SerializeField] private float barGapAboveTop = 0.04f;

    [Tooltip("At or above this fill ratio the bar is green.")]
    [SerializeField, Range(0f, 1f)] private float barHighThreshold = 0.6f;

    [Tooltip("At or above this fill ratio (and below the high one) the bar is yellow; below it, red.")]
    [SerializeField, Range(0f, 1f)] private float barLowThreshold = 0.3f;

    [Tooltip("Sorting order for the sauce bar visuals.")]
    [SerializeField] private int barSortingOrder = 6100;

    [Tooltip("Scale of the sauce bar while pouring, so it reads comfortably over the plate.")]
    [SerializeField, Min(1f)] private float barPouringScale = 3f;

    [Tooltip("Seconds the bar takes to grow when pouring starts (overshoots a little before settling).")]
    [SerializeField, Min(0.01f)] private float barGrowDuration = 0.3f;

    [Tooltip("Seconds the bar takes to shrink back when pouring stops.")]
    [SerializeField, Min(0.01f)] private float barShrinkDuration = 0.25f;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private Vector3 startScale;
    private Vector2 squeezeFactor = Vector2.one;
    private int startSortingOrder;
    private SpriteRenderer selfRenderer;

    private bool isDragging;
    private bool isPouring;
    private bool toppingRegistered; 

    private Vector3[] streamPoints;
    private float streamProgress;
    private float streamFallSpeed;
    private bool streamLanded;
    private Vector3 impactPoint;
    private float puddleRadius;
    private Vector3 lastPuddleImpact;
    private float perlinSeed;
    private Vector3 lastStreamOrigin;
    private float streamSway;
    private float streamSwayVelocity;

    // El chorro (y lo que queda en el aire al cortar) y el charco de este arrastre en el plato.
    private SauceStreamFX streamFX;
    private SauceDecal currentDecal;
    private SaucePalette palette;
    private Vector3 lastPourImpact;
    private float lastRippleTime;
    private static int decalSortingCounter;

    // Por debajo de esta velocidad del impacto (unidades/s) se está vertiendo quieto: salen ondas.
    private const float StillPourSpeed = 0.5f;
    // El primer contacto con el plato deja de una la salsa de este rato de vertida.
    private const float LandingPlopSeconds = 0.12f;
    private const float ChunkShadeFactor = 0.75f;

    private SpriteRenderer landingMarker;
    private LineRenderer previewLine;
    private Vector3[] previewPoints;
    private float landingMarkerAlpha;
    private static Sprite cachedLandingRingSprite;
    private static Material cachedPreviewLineMaterial;

    private const int PreviewLineSegments = 12;
    // Cuánto del recorrido cae derecho antes de curvarse hacia el impacto (si el plato lo desvía).
    private const float StreamStraightFraction = 0.6f;

    private float lastSplatterTime;
    // Un charco por arrastre; el último del frasco es el visible (los anteriores quedan escondidos
    // adentro de él, para el undo).
    private readonly List<SauceDecal> sauceDecals = new List<SauceDecal>();

    private float currentSauceAmount;
    private bool sauceEmpty;

    private GameObject barRoot;
    private SpriteRenderer barFill;
    private float barPopScale = 1f;
    private float barPopFrom = 1f;
    private float barPopT = 1f;
    private bool barPopGrowing;
    private static Sprite cachedBarFrameSprite;
    private static Sprite[] cachedBarFillSprites;

    // Marco de la barra, en pixeles: contorno 1 + madera 2 a cada lado, sombra de 1 abajo.
    private const int BarFrameInset = 3;
    private const int BarInnerHeight = 5;
    private const int BarShadowRows = 1;
    private const int BarFrameHeight = BarShadowRows + BarFrameInset * 2 + BarInnerHeight;

    // Paleta de la madera, sacada de la barra de cocción (Barra Coccion v4).
    private static readonly Color32 BarOutline = new Color32(0x14, 0x0A, 0x0B, 0xFF);
    private static readonly Color32 BarWoodLight = new Color32(0xE3, 0xA9, 0x8F, 0xFF);
    private static readonly Color32 BarWoodMid = new Color32(0xB9, 0x71, 0x5B, 0xFF);
    private static readonly Color32 BarWoodDark = new Color32(0x8C, 0x4B, 0x3B, 0xFF);
    private static readonly Color32 BarWoodDarker = new Color32(0x63, 0x2F, 0x2B, 0xFF);
    private static readonly Color32 BarTrack = new Color32(0x2B, 0x14, 0x16, 0xFF);
    private static readonly Color32 BarTrackShadow = new Color32(0x1A, 0x0B, 0x0C, 0xFF);
    private static readonly Color32 BarDropShadow = new Color32(0x00, 0x00, 0x00, 0x5A);

    // Rampas del relleno de arriba hacia abajo: brillo, claro, base, base, sombra. Índice 0 verde, 1 amarillo, 2 rojo.
    private static readonly Color32[][] BarFillRamps =
    {
        new[] { new Color32(0xE8, 0xF8, 0xB0, 0xFF), new Color32(0xA6, 0xDC, 0x4E, 0xFF), new Color32(0x6C, 0xB3, 0x3A, 0xFF), new Color32(0x6C, 0xB3, 0x3A, 0xFF), new Color32(0x3F, 0x7A, 0x2A, 0xFF) },
        new[] { new Color32(0xFF, 0xF4, 0xBE, 0xFF), new Color32(0xFF, 0xD9, 0x4A, 0xFF), new Color32(0xF0, 0xAE, 0x2A, 0xFF), new Color32(0xF0, 0xAE, 0x2A, 0xFF), new Color32(0xB4, 0x70, 0x1C, 0xFF) },
        new[] { new Color32(0xFF, 0xC2, 0xAE, 0xFF), new Color32(0xF2, 0x5A, 0x3E, 0xFF), new Color32(0xC8, 0x30, 0x2E, 0xFF), new Color32(0xC8, 0x30, 0x2E, 0xFF), new Color32(0x86, 0x1C, 0x24, 0xFF) },
    };

    private static readonly List<ToppingDraggable> ActiveInstances = new List<ToppingDraggable>();

    void OnEnable()
    {
        if (!ActiveInstances.Contains(this))
            ActiveInstances.Add(this);
    }

    void OnDestroy()
    {
        GamePause.OnPaused -= CancelDrag;
        ActiveInstances.Remove(this);

        if (landingMarker != null)
            Destroy(landingMarker.gameObject);
        if (previewLine != null)
            Destroy(previewLine.gameObject);
        if (streamFX != null)
            Destroy(streamFX.gameObject);
    }

    void Awake()
    {
        selfRenderer = GetComponent<SpriteRenderer>();
        EnsureCollider();
        EnsurePlaceholderVisual();
        InitializeSauceAmount();
        CreateSauceBar();
        UpdateSauceBar();
    }

    // En Start y no en Awake: en la primera escena el Awake del ToppingStock puede correr después.
    void Start()
    {
        if (!UsesStock) return;

        currentSauceAmount = ToppingStock.Instance.GetSauceAmount(toppingData);
        sauceEmpty = currentSauceAmount <= 0f;
        UpdateSauceBar();
    }

    // Solo trabaja mientras la barra se agranda o se achica. Tiempo sin escala: si se pausa a
    // mitad de vertida, la barra igual vuelve a su tamaño.
    void LateUpdate()
    {
        if (barPopT >= 1f) return;

        barPopT = Mathf.Min(1f, barPopT + Time.unscaledDeltaTime / (barPopGrowing ? barGrowDuration : barShrinkDuration));
        float target = barPopGrowing ? barPouringScale : 1f;
        float eased = barPopGrowing ? FeelEase.OutBack(barPopT) : FeelEase.OutBack(barPopT, 1f);
        barPopScale = Mathf.LerpUnclamped(barPopFrom, target, eased);
        UpdateSauceBar();
    }

    /// <summary>Arranca la animación hacia grande (vertiendo) o hacia el tamaño normal, desde el tamaño actual.</summary>
    private void SetBarPop(bool grow)
    {
        if (grow == barPopGrowing) return;

        barPopGrowing = grow;
        barPopFrom = barPopScale;
        barPopT = 0f;
    }

    private bool UsesStock => !ignoreStock && toppingData != null && ToppingStock.Instance != null;

    /// <summary>Se está arrastrando el frasco (vierta o no).</summary>
    public bool IsDragging => isDragging;

    /// <summary>El frasco está dado vuelta y saliendo salsa.</summary>
    public bool IsPouring => isPouring;

    /// <summary>El frasco que se está arrastrando, o null. Solo se arrastra uno a la vez.</summary>
    public static ToppingDraggable Held
    {
        get
        {
            for (int i = 0; i < ActiveInstances.Count; i++)
            {
                if (ActiveInstances[i] != null && ActiveInstances[i].isDragging)
                    return ActiveInstances[i];
            }
            return null;
        }
    }

    /// <summary>
    /// Dónde tiene que estar el frasco para que, dado vuelta, la salsa caiga en el centro del plato.
    /// Queda dentro de la zona donde se da vuelta (si no, ahí no vierte). False sin plato o sin zona.
    /// Lo usa el tutorial para marcar el punto.
    /// </summary>
    public bool TryGetIdealPourPosition(out Vector3 position)
    {
        position = transform.position;

        BuildFoodDropZone zone = FindDropZone();
        if (zone == null || zone.ZoneCollider == null || rotationZone == null)
            return false;

        Vector3 invertedMouth = GetInvertedMouthOffset();

        Vector3 plateCenter = zone.ZoneCollider.bounds.center;
        Vector3 ideal = plateCenter + Vector3.up * sauceLength - invertedMouth;

        if (!rotationZone.OverlapPoint(ideal))
            ideal = rotationZone.ClosestPoint(ideal);

        position = new Vector3(ideal.x, ideal.y, transform.position.z);
        return true;
    }

    /// <summary>Boca del frasco respecto del pivote con el frasco derecho; dado vuelta (180°) queda espejada.</summary>
    private Vector3 GetInvertedMouthOffset()
    {
        Vector3 mouthLocal = pourOrigin != null ? transform.InverseTransformPoint(pourOrigin.position) : Vector3.zero;
        return Quaternion.Euler(0f, 0f, 180f) * Vector3.Scale(mouthLocal, transform.lossyScale);
    }

    private Vector3 GetMouthPosition() => pourOrigin != null ? pourOrigin.position : transform.position;

    private void SaveSauceToStock()
    {
        if (UsesStock)
            ToppingStock.Instance.SetSauceAmount(toppingData, currentSauceAmount);
    }

    void OnWorldPointerDown()
    {
        if (sauceEmpty && UsesStock)
            DeliveryFeedbackText.Instance?.Show(Loc.Format("sauce.empty", toppingData.toppingName));

        isDragging = true;
        GamePause.OnPaused += CancelDrag;
        isPouring = false;
        toppingRegistered = false;
        // Cada arrastre tiene su charco (el undo lo saca entero).
        currentDecal = null;

        startPosition = transform.position;
        startRotation = transform.rotation;
        startScale = transform.localScale;
        squeezeFactor = Vector2.one;
        GamepadHaptics.Play(HapticEvent.PieceGrabbed);

        if (selfRenderer != null)
        {
            startSortingOrder = selfRenderer.sortingOrder;
            selfRenderer.sortingOrder = 6000;
        }
    }

    void OnWorldPointerDrag() => DragTo(GetMouseWorldPos());

    /// <summary>Un frame de arrastre con el frasco en esa posición de mundo (la del puntero).</summary>
    private void DragTo(Vector3 worldPosition)
    {
        if (!isDragging) return;

        transform.position = worldPosition;

        bool insideZone = IsInsideRotationZone();

        float currentZ = NormalizeAngle(transform.eulerAngles.z);

        if (insideZone)
        {
            float newZ = Mathf.MoveTowardsAngle(currentZ, 180f, rotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f, newZ);
        }
        else
        {
            float newZ = Mathf.MoveTowardsAngle(currentZ, 0f, rotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f, newZ);

            if (isPouring)
                StopPouring();
        }

        float absAngle = Mathf.Abs(NormalizeAngle(transform.eulerAngles.z));
        bool fullyInverted = absAngle >= 175f;

        if (fullyInverted && insideZone && !isPouring && !sauceEmpty)
        {
            StartPouring();
        }

        if (isPouring)
        {
            DrainSauce();

            if (isPouring && streamPoints != null)
                UpdateSauceThread();

            // El charco recién empieza cuando el chorro toca el plato, no apenas se da vuelta el frasco.
            if (isPouring && streamLanded)
            {
                UpdatePuddle();

                float impactSpeed = Vector2.Distance(impactPoint, lastPourImpact) / Mathf.Max(Time.deltaTime, 0.0001f);
                PourIntoDecal();

                // Quieto, la salsa que cae hace ondas en el charco; moviéndose deja el cordón.
                if (impactSpeed < StillPourSpeed && Time.time - lastRippleTime >= rippleInterval)
                {
                    lastRippleTime = Time.time;
                    if (currentDecal != null)
                        currentDecal.AddRipple(impactPoint);
                }

                if (Time.time - lastSplatterTime >= splatterInterval)
                {
                    if (Random.value < dropletChance)
                        SpawnDroplet(impactPoint);
                    lastSplatterTime = Time.time;
                    GamepadHaptics.Play(HapticEvent.SaucePouring);
                }
            }
        }

        UpdateSqueeze();
        UpdateSauceBar();
        UpdateLandingMarker(insideZone);
    }

    /// <summary>Mientras vierte, la botella se aprieta a pulsos (más angosta y apenas más alta).</summary>
    private void UpdateSqueeze()
    {
        float pulse = isPouring
            ? (Mathf.Sin(Time.time * squeezeFrequency * 2f * Mathf.PI) * 0.5f + 0.5f) * squeezeAmount
            : 0f;
        squeezeFactor = new Vector2(1f - pulse, 1f + pulse * 0.5f);
        transform.localScale = new Vector3(startScale.x * squeezeFactor.x, startScale.y * squeezeFactor.y, startScale.z);
    }

    void OnWorldPointerUp()
    {
        if (!isDragging) return;
        isDragging = false;
        GamePause.OnPaused -= CancelDrag;

        if (isPouring || toppingRegistered)
        {
            if (!toppingRegistered)
                RegisterTopping();
        }

        StopPouring();
        HideLandingMarker();

        transform.position = startPosition;
        transform.rotation = startRotation;
        RestoreScale();

        if (selfRenderer != null)
            selfRenderer.sortingOrder = startSortingOrder;

        UpdateSauceBar();
    }

    private void RestoreScale()
    {
        transform.localScale = startScale;
        squeezeFactor = Vector2.one;
    }

    /// <summary>
    /// Aborta el arrastre en curso y devuelve el frasco a su sitio. Lo usa el panel al cerrarse
    /// o al cambiar de vista. Si ya se habia registrado el topping, el undo se encarga de revertirlo:
    /// aca no se toca el armado.
    /// </summary>
    public void CancelDrag()
    {
        if (!isDragging)
            return;

        isDragging = false;
        GamePause.OnPaused -= CancelDrag;
        StopPouring();
        HideLandingMarker();

        transform.position = startPosition;
        transform.rotation = startRotation;
        RestoreScale();

        if (selfRenderer != null)
            selfRenderer.sortingOrder = startSortingOrder;

        UpdateSauceBar();
    }

    private void StartPouring()
    {
        isPouring = true;
        lastSplatterTime = Time.time;
        streamProgress = 0f;
        streamFallSpeed = streamGrowSpeed;
        streamLanded = false;
        puddleRadius = splatterSpread * puddleStartSpread;
        perlinSeed = Random.Range(0f, 1000f);
        CreateSauceThread();
        SetBarPop(true);

        if (!toppingRegistered)
            RegisterTopping();
    }

    private void StopPouring()
    {
        // Lo que ya salió del frasco termina de caer en el plato.
        if (isPouring && streamFX != null)
            streamFX.Release(impactPoint.y, pourVolumeRate, PourRadius);

        isPouring = false;
        SetBarPop(false);

        // Toda vertida termina acá (soltar, cancelar, frasco vacío): se guarda una vez y no por frame.
        SaveSauceToStock();
    }

    private void InitializeSauceAmount()
    {
        float max = (toppingData != null) ? toppingData.maxSauceAmount : 100f;
        currentSauceAmount = max;
        sauceEmpty = false;
    }

    private void DrainSauce()
    {
        if (sauceEmpty) return;

        float rate = (toppingData != null) ? toppingData.sauceDrainRate : 10f;
        currentSauceAmount -= rate * Time.deltaTime;

        if (currentSauceAmount <= 0f)
        {
            currentSauceAmount = 0f;
            sauceEmpty = true;
            StopPouring();
        }
    }

    public float GetSauceRatio()
    {
        float max = (toppingData != null) ? toppingData.maxSauceAmount : 100f;
        if (max <= 0f) return 0f;
        return Mathf.Clamp01(currentSauceAmount / max);
    }

    public void RefillSauce()
    {
        float max = (toppingData != null) ? toppingData.maxSauceAmount : 100f;
        currentSauceAmount = max;
        sauceEmpty = false;
        UpdateSauceBar();
    }

    private void CreateSauceBar()
    {
        barRoot = new GameObject("SauceBar");
        barRoot.transform.SetParent(transform, false);
        barRoot.transform.localRotation = Quaternion.identity;
        barRoot.transform.localScale = Vector3.one;

        GameObject frameGo = new GameObject("BarFrame");
        frameGo.transform.SetParent(barRoot.transform, false);
        frameGo.transform.localScale = new Vector3(barPixelSize, barPixelSize, 1f);
        SpriteRenderer frame = frameGo.AddComponent<SpriteRenderer>();
        frame.sprite = GetBarFrameSprite(barWidthPixels);
        frame.sortingOrder = barSortingOrder;

        // El relleno arranca en el borde izquierdo del canal y crece hacia la derecha.
        GameObject fillGo = new GameObject("BarFill");
        fillGo.transform.SetParent(barRoot.transform, false);
        float innerCenterY = BarShadowRows + BarFrameInset + BarInnerHeight * 0.5f - BarFrameHeight * 0.5f;
        fillGo.transform.localPosition = new Vector3(
            (BarFrameInset - barWidthPixels * 0.5f) * barPixelSize,
            innerCenterY * barPixelSize,
            0f);
        barFill = fillGo.AddComponent<SpriteRenderer>();
        barFill.sprite = GetBarFillSprites()[0];
        barFill.sortingOrder = barSortingOrder + 1;
    }

    /// <summary>
    /// Siempre visible: en el panel muestra cuánta salsa queda y al arrastrar acompaña a la botella.
    /// Va apenas por encima de la tapa (o de la base, con la botella dada vuelta).
    /// El relleno avanza de a pixel entero y cambia de verde a amarillo a rojo por tramos.
    /// Mientras vierte se agranda (<see cref="barPouringScale"/>) creciendo hacia arriba: el borde
    /// de abajo queda donde estaba, sin tapar la botella.
    /// </summary>
    private void UpdateSauceBar()
    {
        if (barRoot == null || barFill == null) return;

        float ratio = GetSauceRatio();

        float barHalfHeight = BarFrameHeight * 0.5f * barPixelSize * Mathf.Abs(transform.lossyScale.y) / squeezeFactor.y;
        float barCenterY = GetContainerHalfHeight() + barGapAboveTop + barHalfHeight * (barPopScale - 1f);

        barRoot.transform.rotation = Quaternion.identity;
        barRoot.transform.position = transform.position + new Vector3(0f, barCenterY, 0f);
        // La barra no se aprieta con la botella.
        barRoot.transform.localScale = new Vector3(barPopScale / squeezeFactor.x, barPopScale / squeezeFactor.y, 1f);

        int innerWidth = barWidthPixels - BarFrameInset * 2;
        int fillPixels = ratio > 0f ? Mathf.Clamp(Mathf.RoundToInt(innerWidth * ratio), 1, innerWidth) : 0;

        barFill.enabled = fillPixels > 0;
        barFill.transform.localScale = new Vector3(fillPixels * barPixelSize, barPixelSize, 1f);

        int ramp = ratio >= barHighThreshold ? 0 : ratio >= barLowThreshold ? 1 : 2;
        barFill.sprite = GetBarFillSprites()[ramp];
    }

    /// <summary>Media altura del sprite en mundo, sin contar la rotación ni el apretón.</summary>
    private float GetContainerHalfHeight()
    {
        if (selfRenderer == null || selfRenderer.sprite == null)
            return 0f;

        Bounds b = selfRenderer.sprite.bounds;
        return (b.center.y + b.extents.y) * Mathf.Abs(transform.lossyScale.y) / squeezeFactor.y;
    }

    /// <summary>
    /// Marco de madera pixel art: contorno con esquinas recortadas, madera clara arriba y oscura
    /// abajo, canal oscuro adentro y una fila de sombra debajo. 1 unidad = 1 pixel.
    /// </summary>
    private static Sprite GetBarFrameSprite(int width)
    {
        if (cachedBarFrameSprite != null && Mathf.RoundToInt(cachedBarFrameSprite.rect.width) == width)
            return cachedBarFrameSprite;

        const int h = BarFrameHeight;
        var pixels = new Color32[width * h];
        int top = h - 1;
        int bottom = BarShadowRows;
        int innerBottom = bottom + BarFrameInset;
        int innerTop = innerBottom + BarInnerHeight - 1;

        for (int y = 0; y < h; y++)
        for (int x = 0; x < width; x++)
        {
            Color32 c = new Color32(0, 0, 0, 0);
            bool edgeX = x == 0 || x == width - 1;

            if (y < bottom)
                c = (x > 0 && x < width - 1) ? BarDropShadow : c;
            else if (y == bottom || y == top)
                c = edgeX ? c : BarOutline;
            else if (edgeX)
                c = BarOutline;
            else if (y == top - 1) c = BarWoodLight;
            else if (y == top - 2) c = BarWoodMid;
            else if (y == bottom + 1) c = BarWoodDarker;
            else if (y == bottom + 2) c = BarWoodDark;
            else if (x == 1) c = BarWoodMid;
            else if (x == 2) c = BarWoodDark;
            else if (x == width - 2) c = BarWoodDarker;
            else if (x == width - 3) c = BarWoodDark;
            else c = y == innerTop ? BarTrackShadow : BarTrack;

            pixels[y * width + x] = c;
        }

        cachedBarFrameSprite = CreatePixelSprite(width, h, pixels, new Vector2(0.5f, 0.5f), "SauceBarFrame");
        return cachedBarFrameSprite;
    }

    /// <summary>Una columna de 1 pixel por color (verde, amarillo, rojo), con pivote a la izquierda: se estira en X.</summary>
    private static Sprite[] GetBarFillSprites()
    {
        if (cachedBarFillSprites != null && cachedBarFillSprites[0] != null)
            return cachedBarFillSprites;

        cachedBarFillSprites = new Sprite[BarFillRamps.Length];
        for (int i = 0; i < BarFillRamps.Length; i++)
        {
            var pixels = new Color32[BarInnerHeight];
            for (int y = 0; y < BarInnerHeight; y++)
                pixels[y] = BarFillRamps[i][BarInnerHeight - 1 - y];

            cachedBarFillSprites[i] = CreatePixelSprite(1, BarInnerHeight, pixels, new Vector2(0f, 0.5f), "SauceBarFill" + i);
        }

        return cachedBarFillSprites;
    }

    private static Sprite CreatePixelSprite(int width, int height, Color32[] pixels, Vector2 pivot, string name)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = name
        };
        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, width, height), pivot, 1f, 0, SpriteMeshType.FullRect);
        sprite.name = name;
        return sprite;
    }

    /// <summary>Arranca el chorro pegado a la boca. Se dibuja en pixel art (<see cref="SauceStreamFX"/>).</summary>
    private void CreateSauceThread()
    {
        int segments = Mathf.Max(streamSegments, 2);
        if (streamPoints == null || streamPoints.Length != segments)
            streamPoints = new Vector3[segments];

        Vector3 origin = GetMouthPosition();
        lastStreamOrigin = origin;
        streamSway = 0f;
        streamSwayVelocity = 0f;
        for (int i = 0; i < segments; i++)
            streamPoints[i] = origin;

        // Los trocitos los dibuja el chorro; el frasco que se arrastra (6000) lo tapa en la boca.
        if (streamFX == null)
            streamFX = SauceStreamFX.Create(GetPalette(), 5998, EnsureDecal, OnTailLanded);

        // Medio ancho con el borde incluido: el contorno es parte del chorro, no una línea aparte.
        streamFX.BeginPour(sauceWidth * 0.65f, streamGrowSpeed, streamFallAcceleration, squeezeFrequency, streamChunkRate);
    }

    /// <summary>
    /// Paleta del pixel art de esta salsa, con lo del inspector. Los colores con alfa 0 salen del
    /// color base (oscurecido o aclarado). Se arma una vez.
    /// </summary>
    private SaucePalette GetPalette()
    {
        if (palette != null)
            return palette;

        Color body = sauceColor;
        body.a = 1f;
        Color Auto(Color picked, Color fallback) => picked.a > 0f ? picked : fallback;

        Color outline = Auto(sauceOutlineColor, body * 0.5f);
        outline.a = 1f;
        Color shadowColor = Auto(sauceShadowColor, body * 0.76f);
        shadowColor.a = 1f;

        palette = new SaucePalette
        {
            pixelSize = saucePixelSize,
            body = body,
            outline = outline,
            shadow = shadowColor,
            light = Auto(sauceLightColor, Color.Lerp(body, new Color(1f, 0.9f, 0.7f), 0.22f)),
            highlight = Auto(sauceHighlightColor, Color.Lerp(body, new Color(1f, 0.95f, 0.85f), 0.5f)),
            halo = sauceHaloColor,
            castShadow = new Color(outline.r * 0.5f, outline.g * 0.5f, outline.b * 0.5f, 0.32f),
            chunkDensity = puddleChunkDensity,
            seed = toppingData != null ? toppingData.name.GetHashCode() : GetInstanceID()
        };

        int count = sauceChunkColors != null ? sauceChunkColors.Length : 0;
        palette.chunkLight = new Color32[count];
        palette.chunkShade = new Color32[count];
        for (int i = 0; i < count; i++)
        {
            Color c = sauceChunkColors[i];
            c.a = 1f;
            palette.chunkLight[i] = c;
            palette.chunkShade[i] = new Color(c.r * ChunkShadeFactor, c.g * ChunkShadeFactor, c.b * ChunkShadeFactor, 1f);
        }

        return palette;
    }

    // La campana con la que cae la salsa: lo que abarca el pie del chorro.
    private float PourRadius => sauceWidth * 0.5f;

    /// <summary>
    /// Charco de este arrastre: lo crea al caer la primera salsa, sobre la loza del plato más
    /// cercano. Si el frasco ya tenía un charco en ese plato, el nuevo arranca con esa salsa y el
    /// viejo se esconde (el undo lo vuelve a mostrar).
    /// </summary>
    private SauceDecal EnsureDecal()
    {
        if (currentDecal != null)
            return currentDecal;

        Vector3 anchor = impactPoint;
        bool onPlate = BuildFoodDropZone.TryGetNearestPlateSurface(anchor, out Vector2 center, out Vector2 extents);
        if (!onPlate)
        {
            // Sin plato: un charco suelto alrededor del impacto.
            center = anchor;
            float r = splatterSpread * 4f + 0.3f;
            extents = new Vector2(r, r * perspectiveSquash);
        }

        int order = splatterSortingOrder + decalSortingCounter++ % 50;
        currentDecal = SauceDecal.Create(new Vector3(center.x, center.y, anchor.z), extents, onPlate,
            GetPalette(), perspectiveSquash, sauceThickness, order, splatterParent);

        SauceDecal previous = sauceDecals.Count > 0 ? sauceDecals[sauceDecals.Count - 1] : null;
        if (previous != null && currentDecal.TryInherit(previous))
            previous.SetVisible(false);

        sauceDecals.Add(currentDecal);
        return currentDecal;
    }

    /// <summary>Deja en el charco la salsa que cayó este frame, a lo largo de lo que se movió el impacto.</summary>
    private void PourIntoDecal()
    {
        SauceDecal decal = EnsureDecal();
        if (decal != null)
            decal.PourLine(lastPourImpact, impactPoint, pourVolumeRate * Time.deltaTime, PourRadius);
        lastPourImpact = impactPoint;
    }

    /// <summary>
    /// La salsa toca el plato (el chorro, o la cola de una vertida ya cortada): arranca el charco
    /// con un golpe de salsa y salta un puñado de gotitas.
    /// </summary>
    private void OnSauceLanded(Vector3 point)
    {
        SauceDecal decal = EnsureDecal();
        if (decal != null)
        {
            decal.Pour(point, pourVolumeRate * LandingPlopSeconds, PourRadius * 1.5f);
            decal.AddRipple(point);
        }

        for (int s = 0; s < landingSplashCount; s++)
            SpawnDroplet(point);
    }

    private void OnTailLanded(Vector3 point) => OnSauceLanded(point);

    private void UpdateSauceThread()
    {
        if (streamPoints == null) return;

        int segments = streamPoints.Length;
        Vector3 origin = GetMouthPosition();

        impactPoint = GetImpactPoint(origin, puddleRadius);
        float curveLength = EstimateStreamLength(origin, impactPoint);

        // Cae como un líquido: acelera hasta tocar el plato. Si el plato queda más cerca, se acorta al toque.
        streamFallSpeed += streamFallAcceleration * Time.deltaTime;
        streamProgress = Mathf.Min(streamProgress + streamFallSpeed * Time.deltaTime / curveLength, 1f);

        if (!streamLanded && streamProgress >= 1f)
        {
            streamLanded = true;
            lastPuddleImpact = impactPoint;
            lastPourImpact = impactPoint;
            lastRippleTime = Time.time;
            OnSauceLanded(impactPoint);
            // El próximo tic (vibración, gotita) sale sin esperar el intervalo.
            lastSplatterTime = Time.time - splatterInterval;
        }

        // Al mover el frasco el chorro se arquea hacia atrás como una sola curva, proporcional a la
        // velocidad y con tope. Nada de puntos persiguiéndose uno a otro: con movimientos rápidos
        // eso hacía que el hilo se estirara y serpenteara como un fideo.
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float mouthSpeedX = (origin.x - lastStreamOrigin.x) / dt;
        lastStreamOrigin = origin;
        float targetSway = Mathf.Clamp(-mouthSpeedX * streamSwayPerSpeed, -streamMaxSway, streamMaxSway);
        streamSway = Mathf.SmoothDamp(streamSway, targetSway, ref streamSwayVelocity, streamSwaySmoothTime, Mathf.Infinity, dt);

        // Vertiendo rápido el vaivén se apaga: el hilo queda limpio mientras se mueve.
        float wobbleScale = 1f - Mathf.Clamp01(Mathf.Abs(streamSway) / Mathf.Max(streamMaxSway, 0.0001f));

        streamPoints[0] = origin;

        float time = Time.time;

        for (int i = 1; i < segments; i++)
        {
            float t = (float)i / (segments - 1);
            float along = t * streamProgress;

            Vector3 pos = EvaluateStream(origin, impactPoint, along);

            // Arqueo y vaivén son máximos a mitad del chorro y nulos en las puntas: la boca queda
            // pegada al frasco y la punta cae justo sobre el aro.
            float belly = 4f * along * (1f - along);
            float noiseX = Mathf.PerlinNoise(
                perlinSeed + i * 0.5f,
                time * wobbleSpeed + i * 0.3f
            ) - 0.5f;
            pos.x += streamSway * belly + noiseX * wobbleAmplitude * 2f * wobbleScale * Mathf.Sin(t * Mathf.PI);

            pos.z = origin.z;
            streamPoints[i] = pos;
        }

        if (streamFX != null)
            streamFX.UpdateStream(streamPoints, streamLanded, streamFallSpeed);
    }

    /// <summary>
    /// Recorrido del chorro (t de 0 a 1): baja derecho desde la boca y, si el plato desvió el impacto
    /// a un costado, se curva recién en el último tramo. Así se lee como caída y no como una diagonal.
    /// </summary>
    private static Vector3 EvaluateStream(Vector3 origin, Vector3 impact, float t)
    {
        Vector3 control = new Vector3(origin.x, Mathf.Lerp(origin.y, impact.y, StreamStraightFraction), origin.z);
        float u = 1f - t;
        return u * u * origin + 2f * u * t * control + t * t * impact;
    }

    private static float EstimateStreamLength(Vector3 origin, Vector3 impact)
    {
        const int samples = 8;
        float length = 0f;
        Vector3 previous = origin;
        for (int i = 1; i <= samples; i++)
        {
            Vector3 p = EvaluateStream(origin, impact, (float)i / samples);
            length += Vector3.Distance(previous, p);
            previous = p;
        }

        return Mathf.Max(length, 0.0001f);
    }

    /// <summary>
    /// Dónde toca la salsa: <see cref="sauceLength"/> debajo de la boca, llevado adentro del plato.
    /// El margen deja lugar para el charco, así no se derrama por el borde.
    /// </summary>
    private Vector3 GetImpactPoint(Vector3 origin, float puddle)
    {
        Vector3 impact = origin + Vector3.down * sauceLength;
        float margin = splatterSize * 0.5f + puddle * 0.5f;
        BuildFoodDropZone.TryClampToPlateSurface(impact, margin, out impact);
        impact.z = origin.z;
        return impact;
    }

    /// <summary>
    /// Marca dónde va a caer la salsa mientras el frasco está en la zona de verter. Antes de verter:
    /// aro tenue que late y guía punteada que baja desde donde va a quedar la boca (con el frasco ya
    /// dado vuelta). Vertiendo: aro firme alrededor del charco, y el chorro reemplaza a la guía.
    /// </summary>
    private void UpdateLandingMarker(bool insideZone)
    {
        bool show = showLandingMarker && isDragging && (isPouring || (insideZone && !sauceEmpty));
        float targetAlpha = show ? (isPouring ? 0.9f : 0.45f + 0.2f * Mathf.Sin(Time.time * 2f * Mathf.PI * 1.5f)) : 0f;
        landingMarkerAlpha = Mathf.MoveTowards(landingMarkerAlpha, targetAlpha, 6f * Time.deltaTime);

        if (landingMarkerAlpha <= 0f)
        {
            HideLandingMarker();
            return;
        }

        EnsureLandingMarker();

        Vector3 mouth;
        Vector3 impact;
        if (isPouring)
        {
            mouth = GetMouthPosition();
            impact = impactPoint;
        }
        else
        {
            mouth = transform.position + GetInvertedMouthOffset();
            impact = GetImpactPoint(mouth, splatterSpread * puddleStartSpread);
        }

        // Vertiendo, el aro abraza el charco (el reservado o el dibujado, el más grande); si no,
        // queda en el tamaño mínimo.
        float diameter = landingMarkerSize;
        if (isPouring)
        {
            float drawn = currentDecal != null ? currentDecal.MeasureRadius(impact) : 0f;
            diameter = Mathf.Max(diameter, puddleRadius * 2f + splatterSize, (drawn + saucePixelSize * 2f) * 2f);
        }
        float ringScale = diameter / Mathf.Max(landingMarker.sprite.bounds.size.x, 0.0001f);
        landingMarker.transform.position = impact;
        landingMarker.transform.localScale = new Vector3(ringScale, ringScale * perspectiveSquash, 1f);
        landingMarker.color = new Color(landingMarkerColor.r, landingMarkerColor.g, landingMarkerColor.b,
            landingMarkerColor.a * landingMarkerAlpha);
        landingMarker.enabled = true;

        bool showPreview = !isPouring;
        previewLine.enabled = showPreview;
        if (!showPreview)
            return;

        for (int i = 0; i < PreviewLineSegments; i++)
            previewPoints[i] = EvaluateStream(mouth, impact, (float)i / (PreviewLineSegments - 1));
        previewLine.SetPositions(previewPoints);

        Color c = landingMarkerColor;
        c.a *= landingMarkerAlpha;
        previewLine.startColor = c;
        previewLine.endColor = c;
        previewLine.sharedMaterial.mainTextureOffset = new Vector2(-Time.time * previewFlowSpeed, 0f);
    }

    private void HideLandingMarker()
    {
        landingMarkerAlpha = 0f;
        if (landingMarker != null)
            landingMarker.enabled = false;
        if (previewLine != null)
            previewLine.enabled = false;
    }

    private void EnsureLandingMarker()
    {
        if (landingMarker == null)
        {
            landingMarker = new GameObject("SauceLandingMarker").AddComponent<SpriteRenderer>();
            landingMarker.sprite = GetLandingRingSprite();
            landingMarker.sortingOrder = landingMarkerSortingOrder;
        }

        if (previewLine == null)
        {
            previewLine = new GameObject("SaucePreviewLine").AddComponent<LineRenderer>();
            previewLine.positionCount = PreviewLineSegments;
            previewLine.widthMultiplier = previewLineWidth;
            previewLine.sharedMaterial = GetPreviewLineMaterial();
            previewLine.sharedMaterial.mainTextureScale = new Vector2(previewDashesPerUnit, 1f);
            previewLine.textureMode = LineTextureMode.Tile;
            previewLine.sortingOrder = landingMarkerSortingOrder + 1;
            previewLine.useWorldSpace = true;
            previewPoints = new Vector3[PreviewLineSegments];
        }
    }

    /// <summary>Aro pixel art blanco con contorno oscuro adentro y afuera, y un punto al centro. Se tiñe con el color del marcador.</summary>
    private static Sprite GetLandingRingSprite()
    {
        if (cachedLandingRingSprite != null)
            return cachedLandingRingSprite;

        const int size = 24;
        const float r = size * 0.5f;
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 white = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        Color32 outline = new Color32(BarOutline.r, BarOutline.g, BarOutline.b, 0xC8);

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x + 0.5f - r;
            float dy = y + 0.5f - r;
            float d = Mathf.Sqrt(dx * dx + dy * dy);

            Color32 c = clear;
            if (d > r) { }
            else if (d > r - 1f) c = outline;
            else if (d > r - 3f) c = white;
            else if (d > r - 4f) c = outline;
            else if (d <= 1.5f) c = white;
            else if (d <= 2.5f) c = outline;

            pixels[y * size + x] = c;
        }

        cachedLandingRingSprite = CreatePixelSprite(size, size, pixels, new Vector2(0.5f, 0.5f), "SauceLandingRing");
        return cachedLandingRingSprite;
    }

    /// <summary>Material de la guía: raya y hueco del mismo largo, repetidos a lo largo de la línea.</summary>
    private static Material GetPreviewLineMaterial()
    {
        if (cachedPreviewLineMaterial != null)
            return cachedPreviewLineMaterial;

        var tex = new Texture2D(4, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat,
            name = "SaucePreviewDash"
        };
        Color32 white = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        Color32 clear = new Color32(0xFF, 0xFF, 0xFF, 0x00);
        tex.SetPixels32(new[] { white, white, clear, clear });
        tex.Apply(false, true);

        cachedPreviewLineMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
        return cachedPreviewLineMaterial;
    }

    /// <summary>
    /// El charco crece mientras se vierte en el mismo lugar. Si el frasco se mueve, se angosta en
    /// proporción a lo que se movió: deja un hilo de salsa en vez de una mancha ancha.
    /// </summary>
    private void UpdatePuddle()
    {
        float minRadius = splatterSpread * puddleStartSpread;
        float moved = Vector2.Distance(impactPoint, lastPuddleImpact);
        lastPuddleImpact = impactPoint;

        puddleRadius = Mathf.MoveTowards(puddleRadius, splatterSpread, splatterSpread / puddleGrowTime * Time.deltaTime);
        puddleRadius = Mathf.Max(minRadius, puddleRadius - moved);
    }

    /// <summary>
    /// Una gotita salta del impacto en un arco corto y bajo, y moja el charco donde cae.
    /// Nunca sale del plato: el destino se lleva adentro del borde.
    /// </summary>
    private void SpawnDroplet(Vector3 from)
    {
        if (streamFX == null)
            return;

        Vector2 dir = Random.insideUnitCircle.normalized;
        float distance = dropletDistance * Random.Range(0.4f, 1f);

        Vector3 target = from + new Vector3(dir.x, dir.y * perspectiveSquash, 0f) * distance;
        BuildFoodDropZone.TryClampToPlateSurface(target, saucePixelSize, out target);
        target.z = from.z;

        streamFX.SpawnDroplet(from, target, Random.value < 0.35f ? 2 : 1);
    }

    public void ClearSplatters()
    {
        for (int i = 0; i < sauceDecals.Count; i++)
        {
            if (sauceDecals[i] != null)
                Destroy(sauceDecals[i].gameObject);
        }

        sauceDecals.Clear();
        currentDecal = null;
    }

    /// <summary>
    /// Destruye los charcos desde el índice indicado en adelante y vuelve a mostrar el último que
    /// queda (estaba escondido adentro del siguiente). Usado por el undo de una vertida.
    /// </summary>
    public void RemoveSplattersFrom(int startIndex)
    {
        if (startIndex < 0)
            startIndex = 0;

        for (int i = sauceDecals.Count - 1; i >= startIndex; i--)
        {
            if (sauceDecals[i] != null)
                Destroy(sauceDecals[i].gameObject);

            sauceDecals.RemoveAt(i);
        }

        currentDecal = null;
        if (sauceDecals.Count > 0 && sauceDecals[sauceDecals.Count - 1] != null)
            sauceDecals[sauceDecals.Count - 1].SetVisible(true);
    }

    /// <summary>Restaura la cantidad de salsa del frasco. Usado por el undo de una vertida.</summary>
    public void SetSauceAmount(float amount)
    {
        float max = (toppingData != null) ? toppingData.maxSauceAmount : 100f;
        currentSauceAmount = Mathf.Clamp(amount, 0f, max);
        sauceEmpty = currentSauceAmount <= 0f;
        SaveSauceToStock();
        UpdateSauceBar();
    }

    /// <summary>
    /// Agrega a la lista los charcos que están sobre el plato (su raíz es el centro de la loza; uno
    /// derramado en la tabla queda afuera). PlateDeliveryDraggable los arrastra con el plato: son el
    /// único visual de la salsa.
    /// </summary>
    public static void CollectPlateSplatters(List<Transform> into)
    {
        if (into == null)
            return;

        for (int i = 0; i < ActiveInstances.Count; i++)
        {
            ToppingDraggable instance = ActiveInstances[i];
            if (instance == null)
                continue;

            List<SauceDecal> decals = instance.sauceDecals;
            for (int j = 0; j < decals.Count; j++)
            {
                SauceDecal decal = decals[j];
                if (decal != null && BuildFoodDropZone.IsOnPlateSurface(decal.transform.position))
                    into.Add(decal.transform);
            }
        }
    }

    public static void ClearAllSplatters()
    {
        for (int i = 0; i < ActiveInstances.Count; i++)
        {
            if (ActiveInstances[i] != null)
                ActiveInstances[i].ClearSplatters();
        }

        decalSortingCounter = 0;
    }

    private void RegisterTopping()
    {
        if (toppingData == null)
        {
            Debug.LogWarning("[ToppingDraggable] " + gameObject.name +
                " has no ToppingSO assigned — cannot register topping.");
            return;
        }

        toppingRegistered = true;

        BuildFoodDropZone zone = FindDropZone();

        if (zone == null)
        {
            BuildStationSystem station = FindFirstObjectByType<BuildStationSystem>();
            if (station != null)
            {
                station.AddTopping(toppingData);
                BuildUndoHistory.Instance?.Push(new AddToppingUndoAction(
                    station, null, toppingData, false, this, sauceDecals.Count, currentSauceAmount));
                Debug.Log("[ToppingDraggable] Topping registrado (fallback): " + toppingData.toppingName);
                TutorialSignals.Raise(TutorialSignal.ToppingAdded, target: transform);
            }
            else
            {
                Debug.LogWarning("[ToppingDraggable] No BuildStationSystem found — topping not registered.");
            }
            return;
        }

        // Sin visual extra en el plato: la salsa vertida (las salpicaduras) ya lo muestra.
        zone.BuildStation.AddTopping(toppingData);
        BuildUndoHistory.Instance?.Push(new AddToppingUndoAction(
            zone.BuildStation, zone, toppingData, false, this, sauceDecals.Count, currentSauceAmount));
        Debug.Log("[ToppingDraggable] Topping registrado: " + toppingData.toppingName);
        TutorialSignals.Raise(TutorialSignal.ToppingAdded, target: transform);
    }

    private static BuildFoodDropZone FindDropZone()
    {
        IReadOnlyList<BuildFoodDropZone> zones = BuildFoodDropZone.Zones;
        for (int i = 0; i < zones.Count; i++)
        {
            if (zones[i] != null && zones[i].isActiveAndEnabled)
                return zones[i];
        }

        return null;
    }

    private bool IsInsideRotationZone()
    {
        if (rotationZone == null) return false;
        return rotationZone.OverlapPoint(transform.position);
    }

    private Vector3 GetMouseWorldPos()
    {
        Camera cam = Camera.main;
        if (cam == null) return transform.position;

        Vector3 pos = InputManager.PointerPosition;
        pos.z = Mathf.Abs(transform.position.z - cam.transform.position.z);
        Vector3 world = cam.ScreenToWorldPoint(pos);
        world.z = transform.position.z;
        return world;
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    private void EnsureCollider()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null)
            box = gameObject.AddComponent<BoxCollider2D>();

        if (selfRenderer != null && selfRenderer.sprite != null)
        {
            box.size = (Vector2)selfRenderer.sprite.bounds.size * 1.2f;
            box.offset = selfRenderer.sprite.bounds.center;
        }
        else
        {
            box.size = Vector2.one;
        }
    }

    private void EnsurePlaceholderVisual()
    {
        if (selfRenderer == null)
            selfRenderer = gameObject.AddComponent<SpriteRenderer>();

        if (selfRenderer.sprite == null)
        {
            Texture2D tex = new Texture2D(4, 4);
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();

            selfRenderer.sprite = Sprite.Create(
                tex,
                new Rect(0, 0, 4, 4),
                new Vector2(0.5f, 0.5f),
                4f
            );

            selfRenderer.color = placeholderColor;
        }
    }

    void OnValidate()
    {
        if (selfRenderer == null)
            selfRenderer = GetComponent<SpriteRenderer>();
    }
}
