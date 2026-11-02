using System.Collections;
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
    [Tooltip("Color of the sauce thread. Used for the LineRenderer.")]
    [SerializeField] private Color sauceColor = new Color(0.18f, 0.54f, 0.14f, 1f);

    [Tooltip("Width of the sauce thread at the top (pour origin).")]
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

    [Header("Sauce Chunks")]
    [Tooltip("Colores de los trocitos que bajan por el chorro y quedan en el cordón (cebolla, morrón...). " +
             "Vacío = salsa lisa.")]
    [SerializeField] private Color[] sauceChunkColors;

    [Tooltip("Trocitos por segundo que salen por el chorro.")]
    [SerializeField, Min(0f)] private float streamChunkRate = 150f;

    [Tooltip("Trocitos por unidad de mundo que quedan en el cordón al mover el frasco.")]
    [SerializeField, Min(0f)] private float trailChunksPerUnit = 70f;

    [Tooltip("Tamaño en mundo de un pixel de los trocitos (un pixel del frasco es 0.006). " +
             "Un poco más grandes que el arte para que se lean en el chorro.")]
    [SerializeField, Min(0.001f)] private float sauceChunkPixelSize = 0.008f;

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

    [Header("Sauce Splatters")]
    [Tooltip("Seconds between each splatter spawn. Lower = denser sauce.")]
    [SerializeField] private float splatterInterval = 0.05f;

    [Tooltip("Base size of each splatter in world units.")]
    [SerializeField] private float splatterSize = 0.15f;

    [Tooltip("± random variation added to the base size.")]
    [SerializeField] private float splatterSizeVariation = 0.05f;

    [Tooltip("Radio máximo del charco alrededor del impacto, una vez que creció del todo.")]
    [SerializeField] private float splatterSpread = 0.1f;

    [Tooltip("Fracción de splatterSpread con la que nace el charco cuando el chorro toca el plato.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float puddleStartSpread = 0.85f;

    [Tooltip("Manchas que aparecen de golpe cuando el chorro toca el plato, así el charco arranca con cuerpo.")]
    [SerializeField, Min(0)] private int landingSplashCount = 9;

    [Tooltip("Segundos vertiendo en el mismo lugar hasta que el charco llega a splatterSpread. " +
             "Al mover el frasco el charco se angosta: deja un hilo en vez de manchar todo.")]
    [SerializeField, Min(0.01f)] private float puddleGrowTime = 0.8f;

    [Tooltip("Tamaño de cada mancha (sobre splatterSize) con el charco completo. Las primeras salen al 70%.")]
    [SerializeField, Min(0.1f)] private float puddleFullSizeScale = 1.3f;

    [Header("Sauce Trail")]
    [Tooltip("Ancho del cordón de salsa que queda al mover el frasco mientras vierte.")]
    [SerializeField, Min(0.01f)] private float sauceTrailWidth = 0.12f;

    [Tooltip("Velocidad del impacto (unidades/s) desde la que se deja cordón en vez de charco.")]
    [SerializeField, Min(0f)] private float trailMinSpeed = 0.5f;

    [Tooltip("Velocidad del impacto a la que el cordón queda en su ancho más fino.")]
    [SerializeField, Min(0.01f)] private float trailFastSpeed = 4f;

    [Tooltip("Multiplicador del ancho moviendo despacio (se junta más salsa).")]
    [SerializeField, Min(0.1f)] private float trailSlowWidthScale = 1.3f;

    [Tooltip("Multiplicador del ancho moviendo rápido (el cordón se estira).")]
    [SerializeField, Min(0.1f)] private float trailFastWidthScale = 0.7f;

    [Tooltip("Distancia mínima entre puntos del cordón. Menos = curva más suave.")]
    [SerializeField, Min(0.005f)] private float trailPointSpacing = 0.03f;

    [Tooltip("Cuánto varía el grosor del cordón a lo largo (0 = parejo, 1 = de casi nada al doble).")]
    [SerializeField, Range(0f, 1f)] private float trailWidthJitter = 0.45f;

    [Tooltip("Largo (unidades de mundo) de cada bulto del cordón. Menos = más desparejo; muy corto queda en zigzag.")]
    [SerializeField, Min(0.01f)] private float trailLumpLength = 0.13f;

    [Tooltip("Chance por tic de splatterInterval de que caiga una mancha sobre el cordón mientras se mueve el frasco.")]
    [SerializeField, Range(0f, 1f)] private float trailSplatterChance = 0.85f;

    [Tooltip("Tamaño de esas manchas respecto de splatterSize. Chicas: ensucian el borde sin parecer pinceladas.")]
    [SerializeField, Min(0.1f)] private float trailSplatterSizeScale = 0.75f;

    [Tooltip("Cuánto se corren de la línea, en anchos del cordón. Cerca de 1 rompen el borde.")]
    [SerializeField, Min(0f)] private float trailSplatterSpread = 0.8f;

    [Tooltip("Chance de gotita por mancha del cordón (aparte de dropletChance, que es la del charco).")]
    [SerializeField, Range(0f, 1f)] private float trailDropletChance = 0.4f;

    [Tooltip("Optional splatter sprites, already drawn in the table's perspective and colored. " +
             "One is picked at random per splatter. If empty, a circle is generated and tinted with sauceColor.")]
    [SerializeField] private Sprite[] splatterSprites;

    [Tooltip("Vertical squash of the 2.5D table: splatters spread in an ellipse instead of a line.")]
    [Range(0.2f, 1f)]
    [SerializeField] private float perspectiveSquash = 0.6f;

    [Tooltip("Chance per splatter of a tiny droplet bouncing out of the impact.")]
    [Range(0f, 1f)]
    [SerializeField] private float dropletChance = 0.12f;

    [Tooltip("How far (world units) a bouncing droplet lands from the impact.")]
    [SerializeField] private float dropletDistance = 0.1f;

    [Tooltip("Sorting order for splatters (should be above the food sprite).")]
    [SerializeField] private int splatterSortingOrder = 5500;

    [Tooltip("Parent transform for splatters (e.g. the food GameObject). " +
             "If null, splatters are spawned in world space.")]
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

    private LineRenderer sauceThread;
    private LineRenderer sauceThreadOutline;
    private static Material sauceThreadMaterial;
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

    /// <summary>Un cordón de salsa: contorno oscuro, cuerpo y brillo, con los mismos puntos.</summary>
    private class SauceTrail
    {
        public GameObject root;
        public LineRenderer outline;
        public LineRenderer body;
        public LineRenderer gloss;
        public readonly List<Vector3> points = new List<Vector3>();
        public readonly List<float> widths = new List<float>();
        public float length;
        public float glossSeed;
    }

    private const int MaxTrailPoints = 256;
    private SauceTrail trail;
    private Vector3 lastTrailImpact;

    /// <summary>Un trocito que baja por el chorro: cuánto recorrió desde la boca y de qué lado del hilo va.</summary>
    private class StreamChunk
    {
        public SpriteRenderer renderer;
        public float distance;
        public float speed;
        public float side;
    }

    private readonly List<StreamChunk> streamChunks = new List<StreamChunk>();
    private AnimationCurve streamWidthCurve;
    private float[] streamLengths;
    private float streamChunkTimer;
    private float trailChunkBudget;
    private Sprite[] chunkSprites;

    // Formas de los trocitos, de arriba hacia abajo: L color, S sombra, . vacío.
    private static readonly string[][] ChunkShapes =
    {
        new[] { "LL", "LS" },
        new[] { "LLL", "LLS" },
        new[] { "LLS" },
        new[] { "L", "S" },
        new[] { "LL" },
    };
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
    private readonly List<GameObject> activeSplatters = new List<GameObject>();
    private Sprite generatedCircleSprite;

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

        for (int i = 0; i < streamChunks.Count; i++)
        {
            if (streamChunks[i].renderer != null)
                Destroy(streamChunks[i].renderer.gameObject);
        }
        streamChunks.Clear();
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

    void OnWorldPointerDrag()
    {
        if (!isDragging) return;

        transform.position = GetMouseWorldPos();

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

            if (sauceThread != null)
                UpdateSauceThread();

            // El charco recién empieza cuando el chorro toca el plato, no apenas se da vuelta el frasco.
            if (streamLanded)
            {
                UpdatePuddle();

                float impactSpeed = Vector2.Distance(impactPoint, lastTrailImpact) / Mathf.Max(Time.deltaTime, 0.0001f);
                lastTrailImpact = impactPoint;
                UpdateSauceTrail(impactSpeed);

                // Quieto se junta charco; moviéndose deja el cordón con manchas chicas pegadas,
                // no las del charco (esas, desparramadas, quedaban como pinceladas).
                if (Time.time - lastSplatterTime >= splatterInterval)
                {
                    if (impactSpeed < trailMinSpeed)
                        SpawnSplatter();
                    else if (Random.value < trailSplatterChance)
                        SpawnTrailSplatter();
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
        streamChunkTimer = 0f;
        trailChunkBudget = 0f;
        CreateSauceThread();
        SetBarPop(true);

        if (!toppingRegistered)
            RegisterTopping();
    }

    private void StopPouring()
    {
        isPouring = false;
        SetBarPop(false);
        // El cordón queda en el plato; la próxima vertida empieza uno nuevo.
        trail = null;

        if (sauceThread != null)
        {
            Destroy(sauceThread.gameObject);
            sauceThread = null;
        }

        if (sauceThreadOutline != null)
        {
            Destroy(sauceThreadOutline.gameObject);
            sauceThreadOutline = null;
        }

        for (int i = 0; i < streamChunks.Count; i++)
        {
            if (streamChunks[i].renderer != null)
                streamChunks[i].renderer.enabled = false;
        }

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

    private void CreateSauceThread()
    {
        if (sauceThread != null) return;

        int segments = Mathf.Max(streamSegments, 2);

        // Contorno oscuro detrás del chorro, como el borde del resto del arte. Los trocitos van
        // arriba del hilo (5999) y abajo del frasco que se arrastra (6000).
        Color outlineColor = new Color(sauceColor.r * 0.45f, sauceColor.g * 0.45f, sauceColor.b * 0.45f, sauceColor.a);
        sauceThreadOutline = CreateStreamLine("SauceThreadOutline", sauceWidth * 1.35f, outlineColor, 5997, segments);
        sauceThread = CreateStreamLine("SauceThread", sauceWidth, sauceColor, 5998, segments);
        streamWidthCurve = sauceThread.widthCurve;

        streamPoints = new Vector3[segments];
        Vector3 origin = GetMouthPosition();
        lastStreamOrigin = origin;
        streamSway = 0f;
        streamSwayVelocity = 0f;
        for (int i = 0; i < segments; i++)
            streamPoints[i] = origin;

        sauceThread.SetPositions(streamPoints);
        sauceThreadOutline.SetPositions(streamPoints);
    }

    private static LineRenderer CreateStreamLine(string name, float width, Color color, int sortingOrder, int segments)
    {
        if (sauceThreadMaterial == null)
            sauceThreadMaterial = new Material(Shader.Find("Sprites/Default"));

        LineRenderer line = new GameObject(name).AddComponent<LineRenderer>();
        line.positionCount = segments;
        // Gruesa en la boca, se afina al caer y se abre apenas al tocar el plato.
        line.widthMultiplier = width;
        line.widthCurve = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.6f, 0.65f), new Keyframe(1f, 0.85f));
        line.sharedMaterial = sauceThreadMaterial;
        line.startColor = color;
        line.endColor = new Color(color.r, color.g, color.b, color.a * 0.7f);
        line.sortingOrder = sortingOrder;
        line.useWorldSpace = true;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        return line;
    }

    private void UpdateSauceThread()
    {
        if (sauceThread == null || streamPoints == null) return;

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
            lastTrailImpact = impactPoint;
            trail = CreateSauceTrail(impactPoint);
            // El primer contacto salpica varias manchas de una, y la siguiente sale sin esperar el intervalo.
            for (int s = 0; s < landingSplashCount; s++)
                SpawnSplatter();
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

        sauceThread.SetPositions(streamPoints);
        if (sauceThreadOutline != null)
            sauceThreadOutline.SetPositions(streamPoints);

        UpdateStreamChunks();
    }

    /// <summary>
    /// Trocitos (cebolla, morrón...) que bajan por el chorro con la misma caída que la salsa: salen
    /// de la boca, aceleran y se pierden al llegar a la punta, donde ya los muestran las salpicaduras.
    /// </summary>
    private void UpdateStreamChunks()
    {
        Sprite[] sprites = GetChunkSprites();
        if (sprites == null)
            return;

        int segments = streamPoints.Length;
        if (streamLengths == null || streamLengths.Length != segments)
            streamLengths = new float[segments];

        streamLengths[0] = 0f;
        for (int i = 1; i < segments; i++)
            streamLengths[i] = streamLengths[i - 1] + Vector2.Distance(streamPoints[i - 1], streamPoints[i]);
        float visibleLength = streamLengths[segments - 1];

        streamChunkTimer += streamChunkRate * Time.deltaTime;
        while (streamChunkTimer >= 1f)
        {
            streamChunkTimer -= 1f;
            SpawnStreamChunk(sprites);
        }

        for (int i = 0; i < streamChunks.Count; i++)
        {
            StreamChunk chunk = streamChunks[i];
            if (!chunk.renderer.enabled)
                continue;

            chunk.speed += streamFallAcceleration * Time.deltaTime;
            chunk.distance += chunk.speed * Time.deltaTime;
            if (chunk.distance >= visibleLength)
            {
                chunk.renderer.enabled = false;
                continue;
            }

            chunk.renderer.transform.position = SampleStream(chunk.distance, chunk.side);
        }
    }

    private void SpawnStreamChunk(Sprite[] sprites)
    {
        StreamChunk chunk = null;
        for (int i = 0; i < streamChunks.Count; i++)
        {
            if (!streamChunks[i].renderer.enabled)
            {
                chunk = streamChunks[i];
                break;
            }
        }

        if (chunk == null)
        {
            SpriteRenderer sr = new GameObject("SauceStreamChunk").AddComponent<SpriteRenderer>();
            sr.sortingOrder = 5999;
            chunk = new StreamChunk { renderer = sr };
            streamChunks.Add(chunk);
        }

        chunk.renderer.sprite = sprites[Random.Range(0, sprites.Length)];
        chunk.renderer.transform.localScale = Vector3.one * sauceChunkPixelSize;
        chunk.distance = 0f;
        chunk.speed = streamGrowSpeed;
        chunk.side = Random.Range(-0.55f, 0.55f);
        chunk.renderer.transform.position = streamPoints[0];
        chunk.renderer.enabled = true;
    }

    /// <summary>Punto del chorro a esa distancia de la boca, corrido hacia un costado dentro del ancho del hilo.</summary>
    private Vector3 SampleStream(float distance, float side)
    {
        int last = streamPoints.Length - 1;
        int i = 1;
        while (i < last && streamLengths[i] < distance)
            i++;

        Vector3 a = streamPoints[i - 1];
        Vector3 b = streamPoints[i];
        float segmentLength = streamLengths[i] - streamLengths[i - 1];
        float k = segmentLength > 0.0001f ? (distance - streamLengths[i - 1]) / segmentLength : 0f;
        Vector3 pos = Vector3.Lerp(a, b, k);

        Vector2 dir = (Vector2)(b - a);
        if (dir.sqrMagnitude > 0.000001f)
        {
            dir.Normalize();
            float t = streamLengths[last] > 0.0001f ? distance / streamLengths[last] : 0f;
            float halfWidth = sauceWidth * 0.5f * (streamWidthCurve != null ? streamWidthCurve.Evaluate(t) : 1f);
            pos += new Vector3(-dir.y, dir.x, 0f) * (side * halfWidth);
        }

        return pos;
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

        // Vertiendo, el aro abraza el charco; si no, queda en el tamaño mínimo.
        float diameter = Mathf.Max(landingMarkerSize, isPouring ? puddleRadius * 2f + splatterSize : 0f);
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
    /// Cordón nuevo con su raíz en el impacto. Vive con las salpicaduras (undo, limpiar, viajar con
    /// el plato): sus líneas están en espacio local, así se mueven con la raíz.
    /// </summary>
    private SauceTrail CreateSauceTrail(Vector3 start)
    {
        var t = new SauceTrail { root = new GameObject("SauceTrail"), glossSeed = Random.Range(0f, 100f) };
        t.root.transform.position = start;
        if (splatterParent != null)
            t.root.transform.SetParent(splatterParent, true);

        // Debajo de las manchas: el charco que se junta al frenar tapa el cordón, no al revés.
        // Los trocitos van arriba del brillo (splatterSortingOrder - 1).
        Color outlineColor = new Color(sauceColor.r * 0.45f, sauceColor.g * 0.45f, sauceColor.b * 0.45f, sauceColor.a);
        Color glossColor = Color.Lerp(sauceColor, Color.white, 0.45f);
        glossColor.a = 0.8f;
        t.outline = CreateTrailLine(t.root.transform, "Outline", outlineColor, splatterSortingOrder - 4);
        t.body = CreateTrailLine(t.root.transform, "Body", sauceColor, splatterSortingOrder - 3);
        t.gloss = CreateTrailLine(t.root.transform, "Gloss", glossColor, splatterSortingOrder - 2);

        t.points.Add(start);
        t.points.Add(start);
        t.widths.Add(sauceTrailWidth);
        t.widths.Add(sauceTrailWidth);
        RebuildSauceTrail(t);

        activeSplatters.Add(t.root);
        return t;
    }

    private static LineRenderer CreateTrailLine(Transform parent, string name, Color color, int sortingOrder)
    {
        if (sauceThreadMaterial == null)
            sauceThreadMaterial = new Material(Shader.Find("Sprites/Default"));

        LineRenderer line = new GameObject(name).AddComponent<LineRenderer>();
        line.transform.SetParent(parent, false);
        line.useWorldSpace = false;
        line.sharedMaterial = sauceThreadMaterial;
        line.startColor = color;
        line.endColor = color;
        line.sortingOrder = sortingOrder;
        line.numCapVertices = 6;
        line.numCornerVertices = 4;
        line.alignment = LineAlignment.View;
        return line;
    }

    /// <summary>
    /// Estira el cordón hasta el impacto. Moviendo despacio queda grueso (se junta salsa) y rápido se
    /// afina. En la tabla 2.5D un trazo horizontal se ve más finito que uno vertical. A lo largo tiene
    /// bultos y angostamientos (ruido), así no queda un trazo de marcador.
    /// </summary>
    private void UpdateSauceTrail(float impactSpeed)
    {
        if (trail == null || trail.root == null)
            return;

        int n = trail.points.Count;
        Vector3 anchor = trail.points[n - 2];
        Vector2 dir = (Vector2)(impactPoint - anchor);
        float dirX = dir.sqrMagnitude > 0.000001f ? Mathf.Abs(dir.normalized.x) : 0f;

        float along = trail.length + dir.magnitude;
        float lump = 1f + trailWidthJitter * (Mathf.PerlinNoise(perlinSeed + along / trailLumpLength, 0.37f) * 2f - 1f);

        float speed01 = Mathf.InverseLerp(trailMinSpeed, trailFastSpeed, impactSpeed);
        float width = sauceTrailWidth
                      * Mathf.Lerp(trailSlowWidthScale, trailFastWidthScale, speed01)
                      * Mathf.Lerp(1f, perspectiveSquash, dirX)
                      * lump;

        // El último punto es la punta viva pegada al chorro; al alejarse lo suficiente queda fijo y nace otro.
        trail.points[n - 1] = impactPoint;
        trail.widths[n - 1] = width;

        if (Vector2.Distance(anchor, impactPoint) >= trailPointSpacing)
        {
            if (n >= MaxTrailPoints)
            {
                trail = CreateSauceTrail(impactPoint);
                return;
            }

            SpawnTrailChunks(anchor, impactPoint, width);
            trail.length = along;
            trail.points.Add(impactPoint);
            trail.widths.Add(width);
        }

        RebuildSauceTrail(trail);
    }

    /// <summary>
    /// Deja trocitos sobre el tramo nuevo del cordón, repartidos a lo ancho. Son hijos de la raíz del
    /// cordón: se van con el undo y viajan con el plato.
    /// </summary>
    private void SpawnTrailChunks(Vector3 from, Vector3 to, float width)
    {
        Sprite[] sprites = GetChunkSprites();
        if (sprites == null)
            return;

        Vector2 dir = (Vector2)(to - from);
        trailChunkBudget += dir.magnitude * trailChunksPerUnit;
        if (trailChunkBudget < 1f)
            return;

        dir.Normalize();
        Vector3 normal = new Vector3(-dir.y, dir.x, 0f);

        while (trailChunkBudget >= 1f)
        {
            trailChunkBudget -= 1f;

            Vector3 pos = Vector3.Lerp(from, to, Random.value) + normal * (Random.Range(-0.3f, 0.3f) * width);
            pos.z = to.z;

            SpriteRenderer sr = new GameObject("SauceChunk").AddComponent<SpriteRenderer>();
            sr.sprite = sprites[Random.Range(0, sprites.Length)];
            sr.sortingOrder = splatterSortingOrder - 1;
            sr.transform.position = pos;
            sr.transform.localScale = Vector3.one * sauceChunkPixelSize;
            sr.transform.SetParent(trail.root.transform, true);
        }
    }

    /// <summary>Trocitos pixel art de cada color de <see cref="sauceChunkColors"/>, con sombra. Null si la salsa es lisa.</summary>
    private Sprite[] GetChunkSprites()
    {
        if (sauceChunkColors == null || sauceChunkColors.Length == 0)
            return null;

        if (chunkSprites != null)
            return chunkSprites;

        chunkSprites = new Sprite[sauceChunkColors.Length * ChunkShapes.Length];
        for (int c = 0; c < sauceChunkColors.Length; c++)
        {
            Color baseColor = sauceChunkColors[c];
            Color32 light = baseColor;
            Color32 shade = new Color(baseColor.r * ChunkShadeFactor, baseColor.g * ChunkShadeFactor,
                baseColor.b * ChunkShadeFactor, baseColor.a);
            Color32 clear = new Color32(0, 0, 0, 0);

            for (int s = 0; s < ChunkShapes.Length; s++)
            {
                string[] rows = ChunkShapes[s];
                int h = rows.Length;
                int w = rows[0].Length;
                var pixels = new Color32[w * h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // Las formas se escriben de arriba hacia abajo; la textura arranca por abajo.
                    char p = rows[h - 1 - y][x];
                    pixels[y * w + x] = p == 'L' ? light : p == 'S' ? shade : clear;
                }

                chunkSprites[c * ChunkShapes.Length + s] =
                    CreatePixelSprite(w, h, pixels, new Vector2(0.5f, 0.5f), "SauceChunk" + c + "_" + s);
            }
        }

        return chunkSprites;
    }

    private static void RebuildSauceTrail(SauceTrail t)
    {
        int n = t.points.Count;
        var local = new Vector3[n];
        var glossLocal = new Vector3[n];
        Transform root = t.root.transform;

        float total = 0f;
        var distances = new float[n];
        for (int i = 0; i < n; i++)
        {
            if (i > 0)
                total += Vector2.Distance(t.points[i - 1], t.points[i]);
            distances[i] = total;

            Vector3 p = t.points[i];
            local[i] = root.InverseTransformPoint(p);
            // El brillo va corrido hacia arriba, donde le pega la luz al cordón.
            glossLocal[i] = root.InverseTransformPoint(p + Vector3.up * t.widths[i] * 0.18f);
        }

        AnimationCurve bodyCurve = new AnimationCurve();
        AnimationCurve outlineCurve = new AnimationCurve();
        AnimationCurve glossCurve = new AnimationCurve();
        float lastTime = -1f;
        for (int i = 0; i < n; i++)
        {
            float time = total > 0.0001f ? distances[i] / total : (float)i / Mathf.Max(n - 1, 1);
            if (time <= lastTime)
                continue;
            lastTime = time;

            float w = t.widths[i];
            // El brillo aparece a tramos: corrido de punta a punta parecía un tubo de plástico.
            float glossAmount = Mathf.Clamp01((Mathf.PerlinNoise(t.glossSeed + i * 0.3f, 7.1f) - 0.4f) * 4f);
            bodyCurve.AddKey(time, w);
            outlineCurve.AddKey(time, w * 1.35f);
            glossCurve.AddKey(time, w * 0.3f * glossAmount);
        }

        ApplyTrailLine(t.outline, local, outlineCurve);
        ApplyTrailLine(t.body, local, bodyCurve);
        ApplyTrailLine(t.gloss, glossLocal, glossCurve);
    }

    private static void ApplyTrailLine(LineRenderer line, Vector3[] positions, AnimationCurve widthCurve)
    {
        line.positionCount = positions.Length;
        line.SetPositions(positions);
        line.widthMultiplier = 1f;
        line.widthCurve = widthCurve;
    }

    private void SpawnSplatter()
    {
        // 0 recién nacido, 1 charco completo: las manchas crecen con el charco, así se nota que se junta salsa.
        float growth = splatterSpread > 0f ? Mathf.Clamp01(puddleRadius / splatterSpread) : 1f;
        float size = Mathf.Max(
            splatterSize * Mathf.Lerp(0.7f, puddleFullSizeScale, growth) + Random.Range(-splatterSizeVariation, splatterSizeVariation),
            0.01f);

        // La tabla es 2.5D: la salsa se reparte en una elipse aplastada, no en una línea.
        // Más denso al centro que en el borde, como un charco que se expande.
        Vector2 spread = Random.insideUnitCircle * puddleRadius * Mathf.Lerp(0.5f, 1f, Random.value);
        Vector3 point = impactPoint + new Vector3(spread.x, spread.y * perspectiveSquash, 0f);
        BuildFoodDropZone.TryClampToPlateSurface(point, size * 0.5f, out point);
        point.z = impactPoint.z;

        GameObject splatterGo = CreateSplatter(point, size);
        ScalePunch.PopIn(splatterGo.transform, 0.3f, 0.12f);

        if (Random.value < dropletChance)
            SpawnDroplet(point, size);
    }

    /// <summary>
    /// Mancha chica que cae sobre el cordón mientras se mueve el frasco, corrida apenas de la línea:
    /// ensucia el borde y lo deja desparejo. A veces salta una gotita.
    /// </summary>
    private void SpawnTrailSplatter()
    {
        float size = Mathf.Max(
            splatterSize * trailSplatterSizeScale + Random.Range(-splatterSizeVariation, splatterSizeVariation) * trailSplatterSizeScale,
            0.01f);

        Vector2 offset = Random.insideUnitCircle * (sauceTrailWidth * trailSplatterSpread);
        Vector3 point = impactPoint + new Vector3(offset.x, offset.y * perspectiveSquash, 0f);
        BuildFoodDropZone.TryClampToPlateSurface(point, size * 0.5f, out point);
        point.z = impactPoint.z;

        GameObject splatterGo = CreateSplatter(point, size);
        ScalePunch.PopIn(splatterGo.transform, 0.3f, 0.12f);

        if (Random.value < trailDropletChance)
            SpawnDroplet(point, size);
    }

    private GameObject CreateSplatter(Vector3 position, float size)
    {
        GameObject splatterGo = new GameObject("SauceSplatter");
        splatterGo.transform.position = position;

        SpriteRenderer sr = splatterGo.AddComponent<SpriteRenderer>();
        bool customSprite = splatterSprites != null && splatterSprites.Length > 0;
        sr.sprite = customSprite ? splatterSprites[Random.Range(0, splatterSprites.Length)] : GetSplatterSprite();
        sr.sortingOrder = splatterSortingOrder;

        if (customSprite)
        {
            // Los sprites ya traen color, perspectiva y brillo: solo se varía apenas el tono.
            float tone = Random.Range(0.88f, 1f);
            sr.color = new Color(tone, tone, tone, 1f);
        }
        else
        {
            Color c = sauceColor;
            c.a = Random.Range(0.7f, 1f);
            sr.color = c;
        }

        // Sin rotación al azar (rompería la perspectiva de la tabla): solo espejo horizontal.
        float scale = size / Mathf.Max(sr.sprite.bounds.size.x, 0.0001f);
        float flip = Random.value < 0.5f ? -1f : 1f;
        float squash = customSprite ? 1f : perspectiveSquash;
        splatterGo.transform.localScale = new Vector3(scale * flip, scale * squash, 1f);

        if (splatterParent != null)
            splatterGo.transform.SetParent(splatterParent, true);

        activeSplatters.Add(splatterGo);
        return splatterGo;
    }

    /// <summary>
    /// Una gotita salta del impacto en un arco corto y bajo, y queda como salpicadura chica.
    /// Nunca sale del plato: el destino se lleva adentro del borde.
    /// </summary>
    private void SpawnDroplet(Vector3 from, float size)
    {
        Vector2 dir = Random.insideUnitCircle.normalized;
        float distance = dropletDistance * Random.Range(0.5f, 1f);
        float dropletSize = Mathf.Max(size * 0.3f, 0.01f);

        Vector3 target = from + new Vector3(dir.x, dir.y * perspectiveSquash, 0f) * distance;
        BuildFoodDropZone.TryClampToPlateSurface(target, dropletSize * 0.5f, out target);
        target.z = from.z;

        GameObject droplet = CreateSplatter(from, dropletSize);
        StartCoroutine(FlyDroplet(droplet.transform, from, target));
    }

    private static IEnumerator FlyDroplet(Transform droplet, Vector3 from, Vector3 to)
    {
        const float duration = 0.14f;
        // El salto es proporcional a la distancia: una gota que cae cerca apenas se levanta.
        float arcHeight = Vector2.Distance(from, to) * 0.3f;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (droplet == null)
                yield break;

            float k = t / duration;
            droplet.position = Vector3.Lerp(from, to, k) + Vector3.up * (arcHeight * 4f * k * (1f - k));
            yield return null;
        }

        if (droplet != null)
            droplet.position = to;
    }

    private Sprite GetSplatterSprite()
    {
        if (generatedCircleSprite != null)
            return generatedCircleSprite;

        generatedCircleSprite = GenerateCircleSprite();
        return generatedCircleSprite;
    }

    private Sprite GenerateCircleSprite()
    {
        const int size = 32;
        const float radius = size * 0.5f;
        const float edgeSoftness = 1.5f;

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - radius + 0.5f;
                float dy = y - radius + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha;
                if (dist <= radius - edgeSoftness)
                    alpha = 1f;
                else if (dist >= radius)
                    alpha = 0f;
                else
                    alpha = 1f - (dist - (radius - edgeSoftness)) / edgeSoftness;

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            size
        );
    }

    public void ClearSplatters()
    {
        for (int i = 0; i < activeSplatters.Count; i++)
        {
            if (activeSplatters[i] != null)
                Destroy(activeSplatters[i]);
        }

        activeSplatters.Clear();
    }

    /// <summary>Destruye los splatters desde el índice indicado en adelante. Usado por el undo de una vertida.</summary>
    public void RemoveSplattersFrom(int startIndex)
    {
        if (startIndex < 0)
            startIndex = 0;

        for (int i = activeSplatters.Count - 1; i >= startIndex; i--)
        {
            if (activeSplatters[i] != null)
                Destroy(activeSplatters[i]);

            activeSplatters.RemoveAt(i);
        }
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
    /// Agrega a la lista las salpicaduras que cayeron sobre el plato (las derramadas en la tabla quedan
    /// afuera). PlateDeliveryDraggable las arrastra con el plato: son el único visual de la salsa.
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

            List<GameObject> splatters = instance.activeSplatters;
            for (int j = 0; j < splatters.Count; j++)
            {
                GameObject splatter = splatters[j];
                if (splatter != null && BuildFoodDropZone.IsOnPlateSurface(splatter.transform.position))
                    into.Add(splatter.transform);
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
                    station, null, toppingData, false, this, activeSplatters.Count, currentSauceAmount));
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
            zone.BuildStation, zone, toppingData, false, this, activeSplatters.Count, currentSauceAmount));
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
