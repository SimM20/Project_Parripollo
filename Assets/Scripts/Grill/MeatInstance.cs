using UnityEngine;

/// <summary>
/// Corte de carne con audio de chisporroteo. El sonido se decide UNA vez por frame y por
/// corte, a partir del calor total que recibe de todos los slots que ocupa. Antes se
/// evaluaba dentro de Cook(), que GridSlot llama una vez por slot: un corte de 2x1 con
/// brasa debajo de un solo slot recibia Play() y Stop() en el mismo frame.
///
/// Mezcla: softSound y hardSound corren en loop en dos AudioSources propios y se funden
/// por volumen (crossfade de potencia constante) segun el calor, que se suaviza en el
/// tiempo. No se cambia de clip nunca: eso era lo que sonaba a "switch".
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MeatInstance : Meat
{
    [Header("Sizzle Mix")]
    [Tooltip("Calor promedio por slot hasta el cual suena solo softSound. Por encima empieza a entrar hardSound.")]
    [SerializeField] private float softOnlyHeat = 2f;

    [Tooltip("Calor promedio por slot a partir del cual suena solo hardSound. " +
             "El calor de un slot va de 0 a 10; un carbon fresco solo aporta 6.5.")]
    [SerializeField] private float hardOnlyHeat = 7f;

    [Tooltip("Segundos que tarda la mezcla en seguir un cambio de calor. Evita saltos al poner o sacar carbon.")]
    [SerializeField] private float mixSmoothTime = 0.6f;

    [Tooltip("Volumen del conjunto con poco calor (soft puro). Con calor maximo llega a 1.")]
    [Range(0f, 1f)]
    [SerializeField] private float volumeAtLowHeat = 0.65f;

    [Header("Sizzle Fades")]
    [Tooltip("Segundos de fade in al empezar a chisporrotear.")]
    [SerializeField] private float fadeInTime = 0.35f;

    [Tooltip("Segundos de fade out al levantar el corte o quedarse sin calor.")]
    [SerializeField] private float fadeOutTime = 0.25f;

    [Header("Sizzle Variation")]
    [Tooltip("Variacion de pitch aleatoria por corte (+/-). Evita que varios cortes suenen como un solo loop duplicado.")]
    [Range(0f, 0.2f)]
    [SerializeField] private float pitchVariation = 0.04f;

    [Tooltip("Arranca cada loop en un punto aleatorio del clip por el mismo motivo.")]
    [SerializeField] private bool randomizeStartOffset = true;

    // Por debajo de esto el corte se considera frio (mismo corte que IsCurrentlyCooking).
    private const float MIN_SIZZLE_HEAT = 0.01f;

    private AudioSource baseSource;   // el del prefab: queda libre para one-shots (flip)
    private AudioSource softSource;
    private AudioSource hardSource;

    private float mix;          // 0 = solo soft, 1 = solo hard (suavizado)
    private float mixVelocity;
    private float fade;         // 0 = mudo, 1 = sonando (fade in/out)
    private bool isSizzling;

    protected override void Awake()
    {
        base.Awake();
        baseSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        baseSource.playOnAwake = false;
        baseSource.loop = false;
        CreateSizzleSources();
    }

    /// <summary>
    /// Dos fuentes en un hijo propio, asi el AudioSource del prefab no cambia de volumen y
    /// Meat.PlayFlipSound() (PlayOneShot sobre ese source) suena siempre a volumen pleno.
    /// </summary>
    private void CreateSizzleSources()
    {
        Transform root = new GameObject("SizzleAudio").transform;
        root.SetParent(transform, false);

        float pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        softSource = CreateLoopSource(root.gameObject, softSound, pitch);
        hardSource = CreateLoopSource(root.gameObject, hardSound, pitch);
    }

    private AudioSource CreateLoopSource(GameObject host, AudioClip clip, float pitch)
    {
        if (clip == null) return null;

        AudioSource src = host.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.volume = 0f;
        src.pitch = pitch;

        // Hereda el ruteo/espacialidad del source del prefab para respetar lo que este configurado ahi.
        src.outputAudioMixerGroup = baseSource.outputAudioMixerGroup;
        src.spatialBlend = baseSource.spatialBlend;
        src.priority = baseSource.priority;
        src.dopplerLevel = 0f;
        return src;
    }

    // LateUpdate: corre despues de GrillSystem.Update (propagacion) y de GridSlot.Update
    // (coccion), asi el calor de los slots ya es el definitivo de este frame.
    private void LateUpdate()
    {
        UpdateSizzleAudio();
    }

    private void UpdateSizzleAudio()
    {
        if (softSource == null && hardSource == null) return;

        bool shouldSizzle = ShouldSizzle(out float heatPerSlot);

        if (shouldSizzle && !isSizzling)
            StartLoops();

        // Fade del conjunto: sube al apoyar, baja al levantar o quedarse sin calor.
        float fadeTarget = shouldSizzle ? 1f : 0f;
        float fadeTime = shouldSizzle ? fadeInTime : fadeOutTime;
        fade = fadeTime > 0f
            ? Mathf.MoveTowards(fade, fadeTarget, Time.deltaTime / fadeTime)
            : fadeTarget;

        // Mezcla soft/hard segun calor, suavizada en el tiempo. Cuando no chisporrotea se
        // congela donde estaba para que el fade out no cambie de caracter.
        if (shouldSizzle)
        {
            float targetMix = Mathf.InverseLerp(softOnlyHeat, hardOnlyHeat, heatPerSlot);
            mix = Mathf.SmoothDamp(mix, targetMix, ref mixVelocity, mixSmoothTime);
        }

        ApplyVolumes();

        if (!shouldSizzle && fade <= 0f && isSizzling)
            StopLoops();
    }

    /// <summary>
    /// Crossfade de potencia constante: cos/sin en vez de lineal. Con una mezcla lineal el
    /// punto medio (0.5 + 0.5) suena mas bajo que los extremos; con cos/sin la energia
    /// total se mantiene y la transicion no "respira".
    /// </summary>
    private void ApplyVolumes()
    {
        float angle = mix * Mathf.PI * 0.5f;
        float master = fade * Mathf.Lerp(volumeAtLowHeat, 1f, mix);

        float softVol = Mathf.Cos(angle) * master;
        float hardVol = Mathf.Sin(angle) * master;

        // Si falta uno de los dos clips, el otro cubre todo el rango.
        if (softSource == null) hardVol = master;
        if (hardSource == null) softVol = master;

        if (softSource != null) softSource.volume = softVol;
        if (hardSource != null) hardSource.volume = hardVol;
    }

    /// <summary>
    /// El corte chisporrotea si esta apoyado en la parrilla (no agarrado), la coccion no esta
    /// pausada por el tutorial y recibe algo de calor. Devuelve el calor promedio por slot
    /// para que un corte grande no suene "fuerte" solo por ocupar mas lugar.
    /// </summary>
    private bool ShouldSizzle(out float heatPerSlot)
    {
        heatPerSlot = 0f;

        if (!IsOnGrill || TutorialManager.IsCookingPaused)
            return false;

        float total = GetTotalHeatReceived();
        if (total <= MIN_SIZZLE_HEAT)
            return false;

        heatPerSlot = total / Mathf.Max(1, OccupiedSlotCount);
        return true;
    }

    private void StartLoops()
    {
        isSizzling = true;
        StartLoop(softSource);
        StartLoop(hardSource);
    }

    private void StartLoop(AudioSource src)
    {
        if (src == null || src.isPlaying) return;

        if (randomizeStartOffset && src.clip != null)
            src.time = Random.Range(0f, src.clip.length * 0.95f);

        src.Play();
    }

    private void StopLoops()
    {
        isSizzling = false;
        if (softSource != null) softSource.Stop();
        if (hardSource != null) hardSource.Stop();
    }

    /// <summary>Corte seco: para casos donde el objeto deja de existir o se desactiva y no hay tiempo de fade.</summary>
    private void SilenceImmediately()
    {
        fade = 0f;
        ApplyVolumes();
        StopLoops();
    }

    public override void OnDisable()
    {
        base.OnDisable();
        if (softSource != null || hardSource != null)
            SilenceImmediately();
    }
}
