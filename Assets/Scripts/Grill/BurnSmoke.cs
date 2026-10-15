using UnityEngine;

/// <summary>
/// Humo de un corte en la parrilla: acompaña toda la coccion y se dispara al quemarse.
///
/// Va sobre el prefab que Meat instancia la primera vez que el corte recibe calor. Meat le avisa
/// cada frame si tiene que humear, que tan cerca de quemarse esta la cara apoyada y si el corte
/// ya se quemo (<see cref="SetState"/>). El humo interpola entre tres perfiles:
///  • Coccion (inicio): hilos de vapor claros y finos, con la carne recien apoyada.
///  • Coccion (fin): mas denso y grisaceo, con la cara apoyada a punto de quemarse.
///  • Quemado: humo negro, espeso y ancho, que arranca con un golpe de bocanadas.
///
/// Las bocanadas nacen repartidas por toda la superficie de la pieza (<see cref="SetSourceSize"/>)
/// y cada una lleva una deriva lateral propia: asi el humo rodea la carne y se abre en vez de
/// subir en una columna. Al dejar de humear se corta la emision y las bocanadas que ya salieron
/// terminan de disiparse. Nada que ver con FlipPuff, el golpe corto de feedback del flip.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class BurnSmoke : MonoBehaviour
{
    [System.Serializable]
    private struct Profile
    {
        [Tooltip("Bocanadas por segundo.")]
        public float emissionRate;
        [Tooltip("Parte de la pieza (ancho y alto) de donde salen las bocanadas. 1 = toda la superficie.")]
        [Range(0.1f, 1.5f)] public float coverage;
        public float sizeMin;
        public float sizeMax;
        public float lifetimeMin;
        public float lifetimeMax;
        public float riseSpeedMin;
        public float riseSpeedMax;
        [Tooltip("Velocidad lateral maxima de cada bocanada: lo que abre el humo hacia los costados.")]
        public float lateralSpread;
        [Tooltip("Turbulencia: curva el humo y evita que suba en linea recta.")]
        public float turbulence;
        [Range(0f, 1f)] public float opacity;
        [Tooltip("0 = vapor claro (steamColor), 1 = hollin (sootColor).")]
        [Range(0f, 1f)] public float darkness;
        [Tooltip("Variacion irregular de la emision: 0 = constante, 1 = rafagas marcadas.")]
        [Range(0f, 1f)] public float gustiness;

        public static Profile Lerp(Profile a, Profile b, float t)
        {
            return new Profile
            {
                emissionRate = Mathf.Lerp(a.emissionRate, b.emissionRate, t),
                coverage = Mathf.Lerp(a.coverage, b.coverage, t),
                sizeMin = Mathf.Lerp(a.sizeMin, b.sizeMin, t),
                sizeMax = Mathf.Lerp(a.sizeMax, b.sizeMax, t),
                lifetimeMin = Mathf.Lerp(a.lifetimeMin, b.lifetimeMin, t),
                lifetimeMax = Mathf.Lerp(a.lifetimeMax, b.lifetimeMax, t),
                riseSpeedMin = Mathf.Lerp(a.riseSpeedMin, b.riseSpeedMin, t),
                riseSpeedMax = Mathf.Lerp(a.riseSpeedMax, b.riseSpeedMax, t),
                lateralSpread = Mathf.Lerp(a.lateralSpread, b.lateralSpread, t),
                turbulence = Mathf.Lerp(a.turbulence, b.turbulence, t),
                opacity = Mathf.Lerp(a.opacity, b.opacity, t),
                darkness = Mathf.Lerp(a.darkness, b.darkness, t),
                gustiness = Mathf.Lerp(a.gustiness, b.gustiness, t)
            };
        }

        public void Sanitize()
        {
            emissionRate = Mathf.Max(0f, emissionRate);
            if (sizeMax < sizeMin) sizeMax = sizeMin;
            if (lifetimeMax < lifetimeMin) lifetimeMax = lifetimeMin;
            if (riseSpeedMax < riseSpeedMin) riseSpeedMax = riseSpeedMin;
            lateralSpread = Mathf.Max(0f, lateralSpread);
            turbulence = Mathf.Max(0f, turbulence);
        }
    }

    [Header("Perfiles")]
    [Tooltip("Carne recien apoyada: vapor claro y finito.")]
    [SerializeField] private Profile cookingStart = new Profile
    {
        emissionRate = 7f, coverage = 0.75f,
        sizeMin = 0.11f, sizeMax = 0.20f,
        lifetimeMin = 1.1f, lifetimeMax = 1.8f,
        riseSpeedMin = 0.35f, riseSpeedMax = 0.60f,
        lateralSpread = 0.14f, turbulence = 0.16f,
        opacity = 0.13f, darkness = 0f, gustiness = 0.25f
    };

    [Tooltip("Cara apoyada a punto de quemarse: mas denso y grisaceo. Entre inicio y fin se " +
             "interpola segun lo cerca que esta la cara apoyada de quemarse.")]
    [SerializeField] private Profile cookingEnd = new Profile
    {
        emissionRate = 17f, coverage = 0.85f,
        sizeMin = 0.15f, sizeMax = 0.28f,
        lifetimeMin = 1.3f, lifetimeMax = 2.3f,
        riseSpeedMin = 0.45f, riseSpeedMax = 0.85f,
        lateralSpread = 0.22f, turbulence = 0.24f,
        opacity = 0.21f, darkness = 0.3f, gustiness = 0.3f
    };

    [Tooltip("Corte quemado: humo negro, espeso y ancho.")]
    [SerializeField] private Profile burned = new Profile
    {
        emissionRate = 42f, coverage = 1.05f,
        sizeMin = 0.22f, sizeMax = 0.40f,
        lifetimeMin = 1.6f, lifetimeMax = 2.8f,
        riseSpeedMin = 0.65f, riseSpeedMax = 1.20f,
        lateralSpread = 0.38f, turbulence = 0.36f,
        opacity = 0.42f, darkness = 0.85f, gustiness = 0.55f
    };

    [Header("Transiciones")]
    [Tooltip("Que tan rapido sigue el humo los cambios de coccion (en perfiles por segundo).")]
    [Min(0.01f)] [SerializeField] private float blendSpeed = 1.5f;
    [Tooltip("Velocidad de las rafagas (ver gustiness de cada perfil).")]
    [SerializeField] private float gustFrequency = 1.6f;

    [Header("Puff de quemado")]
    [Tooltip("Bocanadas que salen de golpe en el momento en que el corte se quema.")]
    [Min(0)] [SerializeField] private int burnBurst = 70;
    [Tooltip("Opacidad de las bocanadas del golpe: hollin puro, sin importar el perfil.")]
    [Range(0f, 1f)] [SerializeField] private float burnOpacity = 0.95f;
    [Tooltip("Cuanto mas grandes salen las bocanadas del golpe que las del perfil de quemado.")]
    [SerializeField] private float burnSizeBoost = 2.4f;
    [Tooltip("Cuanto mas rapido suben y se abren las bocanadas del golpe.")]
    [SerializeField] private float burnSpeedBoost = 2.5f;
    [Tooltip("Cuanto mas duran las bocanadas del golpe.")]
    [SerializeField] private float burnLifetimeBoost = 1.25f;
    [Tooltip("Segundos que se suman (negativo = restan) a la vida de las bocanadas del golpe, despues del boost.")]
    [SerializeField] private float burnLifetimeOffset = -0.5f;
    [Tooltip("Pico de emision extra despues del golpe (4 = el quintuple del perfil) y segundos que tarda en apagarse.")]
    [SerializeField] private float burnEmissionBoost = 4f;
    [Min(0.01f)] [SerializeField] private float burnEmissionDecay = 2f;

    [Header("Puff de cambio de punto")]
    [Tooltip("Bocanadas de golpe con el puff mas suave (Jugoso) y con el mas fuerte (Pasado).")]
    [Min(0)] [SerializeField] private int puffCountMin = 4;
    [Min(0)] [SerializeField] private int puffCountMax = 11;
    [Tooltip("Cuanto mas cargadas (opacidad) salen las bocanadas del puff que las del perfil.")]
    [SerializeField] private float puffOpacityBoost = 2f;
    [Tooltip("Cuanto mas grandes salen las bocanadas del puff que las del perfil.")]
    [SerializeField] private float puffSizeBoost = 1.35f;
    [Tooltip("Cuanto mas rapido suben y se abren las bocanadas del puff.")]
    [SerializeField] private float puffSpeedBoost = 1.6f;
    [Tooltip("Pico de emision extra despues del puff (2 = el triple del perfil) y segundos que tarda en apagarse.")]
    [SerializeField] private float puffEmissionBoost = 2f;
    [Min(0.01f)] [SerializeField] private float puffEmissionDecay = 0.35f;

    [Header("Forma")]
    [Tooltip("Cuanto se expande cada bocanada al subir.")]
    [SerializeField] private float expansion = 3f;
    [Tooltip("Inclinacion de la columna en grados. Positivo = se va hacia la derecha.")]
    [Range(-45f, 45f)] [SerializeField] private float driftAngle = 4f;
    [Tooltip("Frenado progresivo: el humo pierde impulso mientras se dispersa.")]
    [Range(0f, 1f)] [SerializeField] private float damping = 0.12f;
    [SerializeField] private float noiseFrequency = 0.45f;
    [SerializeField] private float noiseScrollSpeed = 0.3f;
    [SerializeField] private float spinDegreesPerSecond = 20f;
    [Tooltip("Tope de bocanadas vivas. Tiene que alcanzar para el perfil de quemado mas el golpe " +
             "inicial y su pico de emision: lo que pase el tope no se emite.")]
    [Min(1)] [SerializeField] private int maxParticles = 400;

    [Header("Color")]
    [SerializeField] private Color steamColor = new Color(0.90f, 0.89f, 0.87f, 1f);
    [SerializeField] private Color sootColor = new Color(0.16f, 0.15f, 0.14f, 1f);

    [Header("Sorting")]
    [SerializeField] private int sortingOrderOffset = 4;
    [SerializeField] private int fallbackSortingOrder = 6;

    private ParticleSystem ps;
    private ParticleSystemRenderer psRenderer;
    private SpriteRenderer owner;
    private MaterialPropertyBlock mpb;
    private float lastOwnerAlpha = -1f;

    // 0 = coccion inicio, 1 = coccion fin, 2 = quemado. Negativo = todavia sin estado.
    private float level = -1f;
    private float appliedLevel = -1f;
    private Profile current;
    private Vector2 sourceSize = new Vector2(0.3f, 0.3f);
    private bool shapeDirty = true;
    private bool wasBurned;
    private float gustSeed;
    private float puffBoost;
    private float burnBoost;

    private static readonly int ColorId = Shader.PropertyToID("_Color");

    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        psRenderer = GetComponent<ParticleSystemRenderer>();
        owner = GetComponentInParent<SpriteRenderer>();
        gustSeed = Random.Range(0f, 100f);
        Configure();

        current = cookingStart;
        ApplyProfile();
    }

    void OnEnable()
    {
        lastOwnerAlpha = -1f;
    }

    /// <summary>
    /// Lo llama Meat cada frame. <paramref name="emitting"/> prende o corta la emision (las
    /// bocanadas que ya salieron terminan su vida). <paramref name="cookProgress01"/> es lo cerca
    /// que esta la cara apoyada de quemarse: 0 cruda, 1 al borde de Quemado.
    /// </summary>
    public void SetState(bool emitting, float cookProgress01, bool isBurned)
    {
        if (ps == null) return;

        float target = isBurned ? 2f : Mathf.Clamp01(cookProgress01);
        bool justBurned = isBurned && !wasBurned;
        wasBurned = isBurned;

        // El momento de quemarse se tiene que notar: salta directo al perfil de quemado
        // en vez de esperar la transicion.
        level = (level < 0f || justBurned) ? target : Mathf.MoveTowards(level, target, blendSpeed * Time.deltaTime);
        ApplyLevel();

        if (emitting && !ps.isEmitting)
            ps.Play(true);
        else if (!emitting && ps.isEmitting)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        if (justBurned && ps.isPlaying)
            BurnPuff();

        if (puffBoost > 0f)
            puffBoost = Mathf.MoveTowards(puffBoost, 0f, puffEmissionBoost * Time.deltaTime / puffEmissionDecay);

        if (burnBoost > 0f)
            burnBoost = Mathf.MoveTowards(burnBoost, 0f, burnEmissionBoost * Time.deltaTime / burnEmissionDecay);

        ApplyEmission();
    }

    /// <summary>
    /// Golpe de humo negro al quemarse: una nube de hollin puro, mucho mas cargada, grande, rapida
    /// y duradera que el perfil de quemado, y un pico de emision que tarda un par de segundos en
    /// apagarse. Lo dispara SetState en el frame en que el corte se quema (el perfil ya es el de quemado).
    /// </summary>
    private void BurnPuff()
    {
        Color color = sootColor;
        color.a = burnOpacity;

        // La posicion sale de la forma (toda la superficie de la pieza); el resto se pisa aca.
        // La velocidad va en espacio de mundo: el nodo FX no rota.
        var ep = new ParticleSystem.EmitParams { startColor = color };
        for (int i = 0; i < burnBurst; i++)
        {
            ep.startSize = Random.Range(current.sizeMin, current.sizeMax) * burnSizeBoost;
            ep.startLifetime = Mathf.Max(0.2f,
                Random.Range(current.lifetimeMin, current.lifetimeMax) * burnLifetimeBoost + burnLifetimeOffset);
            ep.velocity = new Vector3(
                Random.Range(-current.lateralSpread, current.lateralSpread) * burnSpeedBoost,
                Random.Range(current.riseSpeedMin, current.riseSpeedMax) * burnSpeedBoost,
                0f);
            ps.Emit(ep, 1);
        }

        burnBoost = burnEmissionBoost;
    }

    /// <summary>
    /// Golpe corto de humo: unas bocanadas de una, mas cargadas, grandes y rapidas que las del
    /// perfil, y un pico de emision que se apaga en unas decimas. Lo dispara Meat cuando la cara
    /// apoyada pasa al punto siguiente. <paramref name="strength01"/>: 0 el primer cambio (Jugoso),
    /// 1 el ultimo antes de quemarse (Pasado). Quemado no pasa por aca: tiene su propio golpe.
    /// </summary>
    public void Puff(float strength01)
    {
        if (ps == null || !ps.isEmitting) return;

        strength01 = Mathf.Clamp01(strength01);
        int count = Mathf.RoundToInt(Mathf.Lerp(puffCountMin, puffCountMax, strength01));

        Color color = Color.Lerp(steamColor, sootColor, current.darkness);
        color.a = Mathf.Min(1f, current.opacity * puffOpacityBoost);

        // La posicion sale de la forma (toda la superficie de la pieza); el resto se pisa aca.
        // La velocidad va en espacio de mundo: el nodo FX no rota.
        var ep = new ParticleSystem.EmitParams { startColor = color };
        for (int i = 0; i < count; i++)
        {
            ep.startSize = Random.Range(current.sizeMin, current.sizeMax) * puffSizeBoost;
            ep.velocity = new Vector3(
                Random.Range(-current.lateralSpread, current.lateralSpread) * puffSpeedBoost,
                Random.Range(current.riseSpeedMin, current.riseSpeedMax) * puffSpeedBoost,
                0f);
            ps.Emit(ep, 1);
        }

        puffBoost = Mathf.Max(puffBoost, puffEmissionBoost * Mathf.Lerp(0.6f, 1f, strength01));
        ApplyEmission();
    }

    /// <summary>
    /// Tamaño de la pieza en el espacio local del humo. Las bocanadas nacen repartidas por esa
    /// superficie (por la coverage del perfil), no desde un punto.
    /// </summary>
    public void SetSourceSize(Vector2 size)
    {
        size = new Vector2(Mathf.Max(0.05f, size.x), Mathf.Max(0.05f, size.y));
        if ((size - sourceSize).sqrMagnitude < 0.000001f) return;

        sourceSize = size;
        shapeDirty = true;
        ApplyShape();
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

    private void ApplyLevel()
    {
        if (Mathf.Abs(level - appliedLevel) < 0.002f) return;

        appliedLevel = level;
        current = level <= 1f
            ? Profile.Lerp(cookingStart, cookingEnd, level)
            : Profile.Lerp(cookingEnd, burned, level - 1f);

        ApplyProfile();
    }

    /// <summary>Vuelca el perfil actual en los modulos. Afecta a las bocanadas que nacen de aca en mas.</summary>
    private void ApplyProfile()
    {
        var main = ps.main;
        main.startSize = new ParticleSystem.MinMaxCurve(current.sizeMin, current.sizeMax);
        main.startLifetime = new ParticleSystem.MinMaxCurve(current.lifetimeMin, current.lifetimeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(current.riseSpeedMin, current.riseSpeedMax);

        Color color = Color.Lerp(steamColor, sootColor, current.darkness);
        color.a = current.opacity;
        main.startColor = color;

        // Deriva lateral propia de cada bocanada: lo que abre el humo hacia los costados.
        var velocity = ps.velocityOverLifetime;
        velocity.x = new ParticleSystem.MinMaxCurve(-current.lateralSpread, current.lateralSpread);

        var limit = ps.limitVelocityOverLifetime;
        limit.limit = new ParticleSystem.MinMaxCurve(Mathf.Max(0.2f, current.riseSpeedMin * 1.1f));

        var noise = ps.noise;
        noise.strengthX = new ParticleSystem.MinMaxCurve(current.turbulence);
        noise.strengthY = new ParticleSystem.MinMaxCurve(current.turbulence * 0.6f);

        shapeDirty = true;
        ApplyShape();
        ApplyEmission();
    }

    private void ApplyShape()
    {
        if (!shapeDirty) return;
        shapeDirty = false;

        // Rotada -90 en X, el scale local (x, y, z) de la caja mapea a mundo (x, z, -y):
        // x es el ancho de la pieza y z su alto.
        var shape = ps.shape;
        shape.scale = new Vector3(sourceSize.x * current.coverage, 0.01f, sourceSize.y * current.coverage);
    }

    private void ApplyEmission()
    {
        float gust = 1f;
        if (current.gustiness > 0f)
        {
            float n = Mathf.PerlinNoise(gustSeed, Time.time * gustFrequency) * 2f - 1f;
            gust = Mathf.Max(0f, 1f + current.gustiness * n);
        }

        var emission = ps.emission;
        emission.rateOverTime = current.emissionRate * gust * (1f + puffBoost + burnBoost);
    }

    /// <summary>Lo que no cambia con el perfil: se fija una vez.</summary>
    private void Configure()
    {
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;   // lo arranca SetState cuando la carne empieza a cocinarse
        main.prewarm = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;   // el humo escala con el tamano del corte
        main.maxParticles = maxParticles;
        main.gravityModifier = 0f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        var emission = ps.emission;
        emission.enabled = true;

        // Caja chata sobre la superficie de la carne: las bocanadas no nacen del mismo punto.
        // Rotada -90 en X, la direccion de emision del Box (+Z local) apunta a +Y, y la
        // componente Z la inclina hacia driftAngle. El tamaño lo pone ApplyShape.
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = Vector3.zero;
        shape.rotation = new Vector3(-90f, 0f, -driftAngle);

        // Unity exige que x, y y z usen el mismo modo de curva: si no, ignora el modulo entero.
        // Z en 0: la camara es perspectiva, moverse en profundidad cambiaria el tamano aparente.
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = damping > 0f;
        limit.dampen = damping;

        // Crece rapido al principio y despues se abre lento, como el humo real.
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var growth = new AnimationCurve(
            new Keyframe(0f, 0.55f),
            new Keyframe(0.35f, 1.60f),
            new Keyframe(1f, Mathf.Max(0.6f, expansion)));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, growth);

        // Un poco mas oscura pegada a la carne y se aclara al disiparse. El tono base
        // (vapor u hollin) lo pone el perfil en startColor.
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.80f, 0.78f, 0.76f), 0f),
                new GradientColorKey(Color.white, 0.45f),
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
        var noise = ps.noise;
        noise.enabled = true;
        noise.separateAxes = true;
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
        cookingStart.Sanitize();
        cookingEnd.Sanitize();
        burned.Sanitize();
        maxParticles = Mathf.Max(1, maxParticles);
        if (puffCountMax < puffCountMin) puffCountMax = puffCountMin;
    }
}
