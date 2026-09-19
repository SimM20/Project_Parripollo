using UnityEngine;

/// <summary>
/// Humo continuo de un corte quemado.
///
/// Va sobre el prefab que Meat instancia cuando IsAnySideBurned pasa a true, y se apaga
/// solo al desactivarse el GameObject. Nada que ver con FlipPuff, que es un golpe corto
/// de feedback al dar vuelta la carne.
///
/// Son bocanadas superpuestas que nacen chicas sobre la carne, suben, se expanden, rotan
/// lento y se disuelven. La turbulencia del modulo Noise es lo que le da la curvatura
/// organica; un quad con ruido nunca se lee como humo de verdad.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class BurnSmoke : MonoBehaviour
{
    [Header("Arranque")]
    [Tooltip("Al quemarse el corte el humo aparece ya empezado. Este valor son los segundos " +
             "de humo que se simulan de golpe: mas bajo = golpe inicial mas chico. 0 = sin golpe.")]
    [Range(0f, 3f)] [SerializeField] private float prewarmSeconds = 0.6f;

    [Header("Densidad")]
    [Tooltip("Bocanadas por segundo. Subilo para humo mas espeso.")]
    [SerializeField] private float emissionRate = 20f;
    [SerializeField] private int maxParticles = 90;

    [Header("Origen sobre la carne")]
    [Tooltip("Ancho de la zona de donde salen las bocanadas.")]
    [SerializeField] private float sourceWidth = 0.16f;
    [SerializeField] private float sourceHeight = 0.06f;

    [Header("Tamano")]
    [SerializeField] private float startSizeMin = 0.09f;
    [SerializeField] private float startSizeMax = 0.17f;
    [Tooltip("Cuanto se expande cada bocanada al subir.")]
    [SerializeField] private float expansion = 3.4f;

    [Header("Duracion")]
    [SerializeField] private float lifetimeMin = 1.3f;
    [SerializeField] private float lifetimeMax = 2.2f;

    [Header("Movimiento")]
    [SerializeField] private float riseSpeedMin = 0.55f;
    [SerializeField] private float riseSpeedMax = 1.00f;
    [Tooltip("Inclinacion de la columna en grados. Positivo = se va hacia la derecha.")]
    [Range(-45f, 45f)] [SerializeField] private float driftAngle = 6f;
    [Tooltip("Frenado progresivo: el humo pierde impulso mientras se dispersa.")]
    [Range(0f, 1f)] [SerializeField] private float damping = 0.12f;

    [Header("Turbulencia")]
    [Tooltip("Lo que curva el humo y evita que suba en linea recta.")]
    [SerializeField] private float noiseStrength = 0.22f;
    [SerializeField] private float noiseFrequency = 0.35f;
    [SerializeField] private float noiseScrollSpeed = 0.25f;

    [Header("Rotacion")]
    [SerializeField] private float spinDegreesPerSecond = 18f;

    [Header("Color")]
    [Tooltip("Tinte base. El gradiente de vida lo oscurece al nacer y lo aclara al disiparse.")]
    [SerializeField] private Color tint = new Color(0.74f, 0.72f, 0.69f, 1f);
    [Range(0f, 1f)] [SerializeField] private float opacity = 0.17f;
    [Tooltip("Hollin: que tan oscura sale la bocanada pegada a la carne.")]
    [Range(0f, 1f)] [SerializeField] private float sootDarkness = 0.35f;

    [Header("Sorting")]
    [SerializeField] private int sortingOrderOffset = 4;
    [SerializeField] private int fallbackSortingOrder = 6;

    private ParticleSystem ps;
    private ParticleSystemRenderer psRenderer;
    private SpriteRenderer owner;
    private MaterialPropertyBlock mpb;
    private float lastOwnerAlpha = -1f;

    private static readonly int ColorId = Shader.PropertyToID("_Color");

    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        psRenderer = GetComponent<ParticleSystemRenderer>();
        owner = GetComponentInParent<SpriteRenderer>();
        Configure();
    }

    void OnEnable()
    {
        lastOwnerAlpha = -1f;
        if (ps == null) return;

        // Golpe inicial controlado: pre-simulamos exactamente prewarmSeconds y seguimos
        // desde ahi. El prewarm nativo no sirve porque simula hasta llenar la vida de las
        // particulas (columna entera) sin importar duration.
        if (prewarmSeconds > 0f)
            ps.Simulate(prewarmSeconds, true, true);

        ps.Play(true);
    }

    /// <summary>
    /// GrillLayerToggle atenua la carne bajando el alpha de sus SpriteRenderer.
    /// El humo es un ParticleSystemRenderer, asi que no lo alcanza: lo replicamos a mano
    /// para que no quede un humo opaco flotando sobre una carne atenuada.
    /// </summary>
    void LateUpdate()
    {
        if (owner == null || psRenderer == null) return;

        float alpha = owner.color.a;
        if (Mathf.Approximately(alpha, lastOwnerAlpha)) return;

        lastOwnerAlpha = alpha;
        if (mpb == null) mpb = new MaterialPropertyBlock();
        psRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(ColorId, new Color(1f, 1f, 1f, alpha));
        psRenderer.SetPropertyBlock(mpb);
    }

    private void Configure()
    {
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;   // lo arranca OnEnable, despues de fijar duration
        // prewarm simula un ciclo completo (= duration) en el primer frame. Acortando
        // duration el golpe inicial es mas chico, sin tocar la densidad del humo continuo:
        // rateOverTime es por segundo y no depende de duration.
        // El prefab tiene playOnAwake apagado a proposito: si el sistema ya estuviera
        // reproduciendo, Unity rechaza el cambio de duration. Arranca en OnEnable.
        // El prewarm nativo queda apagado: lo hacemos a mano en OnEnable con Simulate(),
        // que si respeta los segundos pedidos. duration no se toca: Unity lo rechaza con el
        // sistema reproduciendo, y ademas no controla el tamano del golpe inicial.
        main.prewarm = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;   // el humo escala con el tamano del corte
        main.maxParticles = maxParticles;
        main.startSpeed = new ParticleSystem.MinMaxCurve(riseSpeedMin, riseSpeedMax);
        main.gravityModifier = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(startSizeMin, startSizeMax);
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeMin, lifetimeMax);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        Color baseColor = tint;
        baseColor.a = opacity;
        main.startColor = baseColor;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = emissionRate;

        // Caja chata sobre la superficie de la carne: las bocanadas no nacen del mismo punto.
        // Rotada -90 en X, la direccion de emision del Box (+Z local) apunta a +Y, y la
        // componente Z lo inclina. Es la via que si funciona: velocityOverLifetime no
        // aporta nada en este sistema.
        // Con la rotacion, el scale local (x, y, z) mapea a mundo (x, z, -y).
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(sourceWidth, 0.01f, sourceHeight);
        shape.position = Vector3.zero;
        shape.rotation = new Vector3(-90f, 0f, -driftAngle);

        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = damping > 0f;
        limit.dampen = damping;
        limit.limit = new ParticleSystem.MinMaxCurve(0.6f);

        // Crece rapido al principio y despues se abre lento, como el humo real.
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var growth = new AnimationCurve(
            new Keyframe(0f, 0.55f),
            new Keyframe(0.35f, 1.60f),
            new Keyframe(1f, Mathf.Max(0.6f, expansion)));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, growth);

        // Nace oscura (hollin pegado a la carne) y se aclara al disiparse.
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        float soot = Mathf.Lerp(1f, 0.45f, sootDarkness);
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(soot, soot * 0.97f, soot * 0.94f), 0f),
                new GradientColorKey(new Color(0.92f, 0.92f, 0.92f), 0.45f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.12f),
                new GradientAlphaKey(0.60f, 0.50f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var rotation = ps.rotationOverLifetime;
        rotation.enabled = spinDegreesPerSecond > 0f;
        float spin = spinDegreesPerSecond * Mathf.Deg2Rad;
        rotation.z = new ParticleSystem.MinMaxCurve(-spin, spin);

        // Lo que hace que el humo se curve en vez de subir como una columna recta.
        // Z en 0: la camara es perspectiva, moverse en profundidad cambiaria el tamano aparente.
        var noise = ps.noise;
        noise.enabled = noiseStrength > 0f;
        noise.separateAxes = true;
        noise.strengthX = new ParticleSystem.MinMaxCurve(noiseStrength);
        noise.strengthY = new ParticleSystem.MinMaxCurve(noiseStrength * 0.6f);
        noise.strengthZ = new ParticleSystem.MinMaxCurve(0f);
        noise.frequency = noiseFrequency;
        noise.scrollSpeed = new ParticleSystem.MinMaxCurve(noiseScrollSpeed);
        noise.damping = true;
        noise.octaveCount = 2;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        if (psRenderer != null)
        {
            psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            psRenderer.alignment = ParticleSystemRenderSpace.View;
            psRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psRenderer.receiveShadows = false;
            psRenderer.sortMode = ParticleSystemSortMode.OldestInFront;

            if (owner != null)
            {
                psRenderer.sortingLayerID = owner.sortingLayerID;
                psRenderer.sortingOrder = owner.sortingOrder + sortingOrderOffset;
            }
            else
            {
                psRenderer.sortingOrder = fallbackSortingOrder;
            }
        }
    }

    void OnValidate()
    {
        if (startSizeMax < startSizeMin) startSizeMax = startSizeMin;
        if (lifetimeMax < lifetimeMin) lifetimeMax = lifetimeMin;
        if (riseSpeedMax < riseSpeedMin) riseSpeedMax = riseSpeedMin;
        emissionRate = Mathf.Max(0f, emissionRate);
        maxParticles = Mathf.Max(1, maxParticles);
    }
}
