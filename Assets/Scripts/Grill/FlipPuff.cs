using UnityEngine;

/// <summary>
/// Puff breve de vapor/humo al dar vuelta un corte. Es feedback de la ACCION de flip,
/// no del estado de coccion: sale igual con la carne cruda, hecha o quemada, y no tiene
/// relacion con el humo continuo de quemado (ver Meat.UpdateEffects / smokePrefab).
/// MeatCookStateFeedback reusa el mismo sistema (Play con intensidad y color) para la
/// bocanada que marca el cambio de punto.
///
/// Usa el ParticleSystem nativo de Unity emitiendo a mano: cada flip decide cuantas
/// particulas, de que tamano y con que velocidad, asi no se repite siempre la misma animacion.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class FlipPuff : MonoBehaviour
{
    [Header("Cantidad por flip")]
    [Min(1)] [SerializeField] private int minParticles = 1;
    [Min(1)] [SerializeField] private int maxParticles = 3;

    [Header("Tamano")]
    [SerializeField] private float sizeMin = 0.10f;
    [SerializeField] private float sizeMax = 0.26f;
    [Tooltip("Cuanto se expande la particula a lo largo de su vida (1 = no crece).")]
    [SerializeField] private float growth = 2.1f;

    [Header("Duracion (segundos)")]
    [SerializeField] private float lifetimeMin = 0.30f;
    [SerializeField] private float lifetimeMax = 0.60f;

    [Header("Movimiento")]
    [SerializeField] private float riseSpeedMin = 0.45f;
    [SerializeField] private float riseSpeedMax = 0.95f;
    [Tooltip("Dispersion horizontal de la velocidad inicial.")]
    [SerializeField] private float horizontalDrift = 0.35f;
    [Tooltip("Dispersion horizontal del punto de aparicion.")]
    [SerializeField] private float spawnSpread = 0.16f;
    [SerializeField] private float spawnHeightJitter = 0.05f;
    [Tooltip("Frenado progresivo: 0 = sigue de largo, 1 = se detiene enseguida.")]
    [Range(0f, 1f)] [SerializeField] private float damping = 0.4f;

    [Header("Rotacion")]
    [SerializeField] private float rotationRange = 180f;
    [SerializeField] private float angularSpeed = 90f;

    [Header("Color")]
    [SerializeField] private Color tint = new Color(0.88f, 0.86f, 0.82f, 1f);
    [Range(0f, 1f)] [SerializeField] private float opacityMin = 0.22f;
    [Range(0f, 1f)] [SerializeField] private float opacityMax = 0.48f;

    [Header("Sorting")]
    [Tooltip("Orden relativo al SpriteRenderer de la carne, para que el puff quede delante.")]
    [SerializeField] private int sortingOrderOffset = 5;
    [SerializeField] private int fallbackSortingOrder = 7;

    private ParticleSystem ps;

    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        Configure();
    }

    /// <summary>Dispara un puff en un punto del mundo. Cada llamada varia levemente.</summary>
    public void Play(Vector3 worldPosition)
    {
        Play(worldPosition, 1f, tint);
    }

    /// <summary>
    /// Variante con intensidad y color: la usa MeatCookStateFeedback para la bocanada del
    /// cambio de punto (vapor claro al principio, humo mas oscuro y cargado hacia el quemado).
    /// intensity escala la cantidad y el tamano de las particulas.
    /// </summary>
    public void Play(Vector3 worldPosition, float intensity, Color puffTint)
    {
        if (ps == null) return;

        intensity = Mathf.Max(0.1f, intensity);
        int count = Mathf.Max(1, Mathf.RoundToInt(Random.Range(minParticles, maxParticles + 1) * intensity));

        for (int i = 0; i < count; i++)
        {
            var ep = new ParticleSystem.EmitParams();

            ep.position = worldPosition + new Vector3(
                Random.Range(-spawnSpread, spawnSpread),
                Random.Range(-spawnHeightJitter, spawnHeightJitter),
                -0.02f);

            ep.startSize = Random.Range(sizeMin, sizeMax) * Mathf.Sqrt(intensity);
            ep.startLifetime = Random.Range(lifetimeMin, lifetimeMax);
            ep.rotation = Random.Range(-rotationRange, rotationRange);
            ep.angularVelocity = Random.Range(-angularSpeed, angularSpeed);

            ep.velocity = new Vector3(
                Random.Range(-horizontalDrift, horizontalDrift),
                Random.Range(riseSpeedMin, riseSpeedMax),
                0f);

            Color c = puffTint;
            c.a = puffTint.a * Random.Range(opacityMin, opacityMax);
            ep.startColor = c;

            ps.Emit(ep, 1);
        }
    }

    /// <summary>
    /// Deja el ParticleSystem listo desde codigo: sin emision automatica, en espacio de mundo
    /// (para que el squash del flip no deforme las particulas ya emitidas) y con las curvas
    /// de crecimiento y fade que definen la forma del puff.
    /// </summary>
    private void Configure()
    {
        var main = ps.main;
        main.playOnAwake = false;
        // En loop y reproduciendo, pero con la emision apagada: no emite nada solo,
        // y las particulas que metemos con Emit() si envejecen y se desvanecen.
        // Un sistema detenido no simula: las particulas quedarian congeladas para siempre.
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.startSpeed = 0f;
        main.startSize = 1f;
        main.startLifetime = 1f;
        main.gravityModifier = 0f;
        main.maxParticles = 64;

        var emission = ps.emission;
        emission.enabled = false;

        var shape = ps.shape;
        shape.enabled = false;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var growthCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, Mathf.Max(1f, growth)));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, growthCurve);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.18f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var limitVelocity = ps.limitVelocityOverLifetime;
        limitVelocity.enabled = damping > 0f;
        limitVelocity.dampen = damping;
        limitVelocity.limit = new ParticleSystem.MinMaxCurve(0.15f);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;

            SpriteRenderer owner = GetComponentInParent<SpriteRenderer>();
            if (owner != null)
            {
                renderer.sortingLayerID = owner.sortingLayerID;
                renderer.sortingOrder = owner.sortingOrder + sortingOrderOffset;
            }
            else
            {
                renderer.sortingOrder = fallbackSortingOrder;
            }
        }

        ps.Play();
    }

    void OnValidate()
    {
        if (maxParticles < minParticles) maxParticles = minParticles;
        if (sizeMax < sizeMin) sizeMax = sizeMin;
        if (lifetimeMax < lifetimeMin) lifetimeMax = lifetimeMin;
        if (riseSpeedMax < riseSpeedMin) riseSpeedMax = riseSpeedMin;
        if (opacityMax < opacityMin) opacityMax = opacityMin;
    }
}
