using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;

/// <summary>Momentos del juego que hacen vibrar el gamepad. Cada uno tiene su patrón en <see cref="GamepadHaptics"/>.</summary>
public enum HapticEvent
{
    /// <summary>El cliente recibe el plato y lo acepta. Suave: el golpe fuerte es la plata.</summary>
    DeliveryAccepted,
    /// <summary>La plata aterriza en el contador del HUD (≈1 s después de entregar): golpe + tic-tic del conteo.</summary>
    MoneyLanded,
    /// <summary>El cliente rechaza el plato (corte equivocado o plato que no le sirve).</summary>
    DeliveryRejected,
    /// <summary>Se entregó carne cruda o quemada: el cliente se va asqueado y suma un strike.</summary>
    BadCookingDelivered,
    /// <summary>Una cara apoyada en la parrilla acaba de pasar a Quemado.</summary>
    MeatBurned,
    /// <summary>Un cliente se fue porque se le acabó la paciencia (suma strike).</summary>
    CustomerLeftAngry,

    // Los nuevos van siempre al final: el prefab guarda el evento como número.

    /// <summary>Se agarra una pieza: carne, carbón, plato, pan/guarnición, frasco o una unidad del stock.</summary>
    PieceGrabbed,
    /// <summary>Un corte se apoya en la parrilla (desde la grilla, el stock, la bandeja o el plato, o al caer de un flip).</summary>
    MeatOnGrill,
    /// <summary>Un carbón se apoya en la parrilla.</summary>
    CoalOnGrill,
    /// <summary>Algo no se pudo hacer: la pieza vuelve a su lugar, no hay corte sustituto, no alcanza la plata.</summary>
    ActionRefused,
    /// <summary>Arranca el flip de un corte (el aterrizaje es un <see cref="MeatOnGrill"/> más suave).</summary>
    MeatFlipped,
    /// <summary>Se rota el footprint de un corte mientras se arrastra.</summary>
    PieceRotated,
    /// <summary>Un corte entra al plato.</summary>
    MeatOnPlate,
    /// <summary>Pan, guarnición o topping al plato, o un corte que va a la bandeja.</summary>
    PiecePlaced,
    /// <summary>Cada salpicadura mientras se vierte una salsa (≈20 por segundo): un zumbido fino.</summary>
    SaucePouring,
    /// <summary>Se limpia el plato con comida encima.</summary>
    PlateCleared,
    /// <summary>Deshacer.</summary>
    UndoDone,
    /// <summary>Algo cae al tacho.</summary>
    Trashed,
    /// <summary>Se limpian cenizas (solo si había alguna).</summary>
    AshesCleaned,
    /// <summary>Cambio de capa de la parrilla (carne ↔ carbón).</summary>
    GrillLayerToggled,
    /// <summary>Se abre o cierra un panel lateral (stock / toppings).</summary>
    PanelToggled,
    /// <summary>Arrastrando el plato, el puntero entra sobre un cliente.</summary>
    DeliveryTargetHover,
    /// <summary>Llega un cliente.</summary>
    CustomerArrived,
    /// <summary>Se informó un corte faltante y el cliente cambió el pedido.</summary>
    OrderChanged,
    /// <summary>Cierra el local (hora de cierre o límite de strikes).</summary>
    ClosingTime,
    /// <summary>Compra concretada en la tienda.</summary>
    PurchaseDone,
    /// <summary>Se pidió un cambio de escena (botones del menú, pausa, fin de la noche, tienda). Suena también en pausa.</summary>
    SceneTransition,
    /// <summary>La cara apoyada de la carne seleccionada (burbuja abierta) pasa al punto siguiente. Quemado va por <see cref="MeatBurned"/>.</summary>
    MeatStateChanged,
}

/// <summary>Un golpe de vibración. Intensidades 0..1 por motor: <c>low</c> = peso (motor grande), <c>high</c> = golpecito (motor chico).</summary>
[Serializable]
public class HapticPulse
{
    [Range(0f, 1f)] public float low;
    [Range(0f, 1f)] public float high;
    [Min(0.01f)] public float seconds = 0.08f;
    [Tooltip("Silencio después del golpe (y entre repeticiones).")]
    [Min(0f)] public float gapAfter;
    [Min(1)] public int repeat = 1;
    [Tooltip("Cada repetición multiplica la intensidad por esto: < 1 se apaga de a poco.")]
    [Range(0f, 1f)] public float repeatFalloff = 1f;

    public HapticPulse() { }

    public HapticPulse(float low, float high, float seconds, float gapAfter = 0f, int repeat = 1, float repeatFalloff = 1f)
    {
        this.low = low; this.high = high; this.seconds = seconds; this.gapAfter = gapAfter;
        this.repeat = repeat; this.repeatFalloff = repeatFalloff;
    }
}

[Serializable]
public class HapticPattern
{
    public HapticEvent id;
    public bool enabled = true;
    public HapticPulse[] pulses = Array.Empty<HapticPulse>();
    [Tooltip("Sigue sonando con el juego en pausa (p. ej. un botón del menú de pausa que cambia de escena).")]
    public bool playsDuringPause;

    [Tooltip("Barra de luz (DualSense / DualShock 4): destello que vuelve al color base. 0 = sin destello.")]
    [Min(0f)] public float flashSeconds;
    public Color flashColor = Color.white;

    public HapticPattern() { }

    public HapticPattern(HapticEvent id, float flashSeconds, Color flashColor, params HapticPulse[] pulses)
    {
        this.id = id; this.flashSeconds = flashSeconds; this.flashColor = flashColor; this.pulses = pulses;
    }
}

/// <summary>
/// Vibración del gamepad. Vive en <c>Resources/InputManager.prefab</c> (DontDestroyOnLoad), al lado
/// de <see cref="InputManager"/>; los patrones se ajustan en el inspector del prefab.
///
///  • Eventos sueltos: <see cref="Play"/> con un <see cref="HapticEvent"/>. Varios a la vez se
///    mezclan tomando el máximo de cada motor (no se suman).
///  • Aviso de quemado: cada corte en la parrilla llama <see cref="ReportBurnRisk"/> con los
///    segundos que le faltan para quemarse; el más urgente marca un latido que se acelera.
///  • Barra de luz (DualSense / DualShock 4): semáforo de strikes de la noche (verde → amarillo →
///    naranja → rojo que respira al llegar al máximo), con destellos por evento encima.
///
/// Vibra solo el último gamepad usado y solo mientras se juega con él (con mouse no vibra nada).
/// Se calla en pausa (salvo los patrones con <c>playsDuringPause</c>), al perder el foco y al cambiar de escena. El DualSense va por
/// <see cref="DualSenseOutput"/> (anda también por Bluetooth y no apaga la barra de luz); el resto,
/// por el <c>SetMotorSpeeds</c> del Input System.
/// </summary>
[DefaultExecutionOrder(1000)]
public class GamepadHaptics : MonoBehaviour
{
    public static GamepadHaptics Instance { get; private set; }

    [Header("General")]
    [Tooltip("Apaga toda la vibración (y la barra de luz).")]
    [SerializeField] private bool vibrationEnabled = true;
    [Tooltip("Multiplica todas las intensidades.")]
    [SerializeField] [Range(0f, 2f)] private float intensity = 1f;
    [Tooltip("Improved necesita firmware 2.24+ del DualSense. Si con un DualSense no vibra nada, probar Classic.")]
    [SerializeField] private DualSenseRumbleMode dualSenseRumble = DualSenseRumbleMode.Improved;

    [Header("Barra de luz (DualSense / DualShock 4)")]
    [SerializeField] private bool useLightbar = true;
    [Tooltip("Semáforo de strikes, de más tranquilo a peor. El último es el del máximo de strikes, el anterior " +
             "el de 'te queda uno', y así hacia atrás; con más margen que colores se usa el primero.")]
    [SerializeField] private Color[] strikeColors =
    {
        new Color(0f, 1f, 0.15f),     // sin strikes
        new Color(1f, 0.8f, 0f),      // te quedan dos
        new Color(1f, 0.28f, 0f),     // te queda uno
        new Color(1f, 0f, 0f),        // máximo: no entra más gente
    };
    [Tooltip("Fuera de la noche (menú, tienda): no hay strikes que mostrar.")]
    [SerializeField] private Color lightbarIdle = new Color(0.8f, 0.8f, 1f);
    [Tooltip("Segundos que tarda en pasar de un color de strikes al siguiente.")]
    [SerializeField] [Min(0f)] private float strikeColorFadeSeconds = 0.5f;
    [Tooltip("Con el máximo de strikes la barra respira (sube y baja de brillo). Segundos por ciclo; 0 = fija.")]
    [SerializeField] [Min(0f)] private float limitBreathSeconds = 1.6f;
    [SerializeField] [Range(0f, 1f)] private float limitBreathMinBrightness = 0.25f;
    [Tooltip("Si un destello es casi del mismo color que la barra (rojo sobre rojo), parpadea a negro en vez de no verse.")]
    [SerializeField] [Range(0f, 1f)] private float flashContrastThreshold = 0.35f;
    [Tooltip("Cambios de color por segundo como máximo (los motores no se limitan).")]
    [SerializeField] [Min(1f)] private float lightbarMaxUpdatesPerSecond = 30f;
    [Tooltip("Color que deja al salir del juego.")]
    [SerializeField] private Color lightbarOnQuit = new Color(0f, 0.15f, 1f);

    [Header("Aviso de quemado (latido)")]
    [SerializeField] private bool burnWarning = true;
    [Tooltip("Segundos antes de quemarse en que arranca el latido.")]
    [SerializeField] [Min(0.1f)] private float burnWarningSeconds = 3f;
    [Tooltip("Segundos entre latidos: al empezar el aviso → justo antes de quemarse.")]
    [SerializeField] private Vector2 burnBeatPeriod = new Vector2(0.8f, 0.3f);
    [SerializeField] [Min(0.01f)] private float burnBeatSeconds = 0.05f;
    [Tooltip("Motor grande en cada latido: al empezar → justo antes de quemarse.")]
    [SerializeField] private Vector2 burnBeatLow = new Vector2(0.15f, 0.45f);
    [Tooltip("Motor chico en cada latido: al empezar → justo antes de quemarse.")]
    [SerializeField] private Vector2 burnBeatHigh = new Vector2(0f, 0.12f);
    [SerializeField] private Color burnLightbar = new Color(1f, 0f, 0f);

    [Header("Patrones")]
    [SerializeField] private List<HapticPattern> patterns = DefaultPatterns();

    private struct Segment { public float start, end, low, high; }

    private struct ActivePlay
    {
        public Segment[] segments;
        public float startTime, endTime;
        public float flashSeconds;
        public Color flashColor;
        public float strength;
        public bool playsDuringPause;
    }

    private readonly List<ActivePlay> playing = new List<ActivePlay>();
    private readonly Dictionary<HapticEvent, Segment[]> compiled = new Dictionary<HapticEvent, Segment[]>();
    private readonly Dictionary<HapticEvent, HapticPattern> byId = new Dictionary<HapticEvent, HapticPattern>();

    private float lastSampleTime;
    private float burnUrgency;          // máximo reportado este frame (0 = nada por quemarse)
    private float burnPhase = -1f;      // < 0: el latido arranca de cero en el próximo aviso
    private bool hasFocus = true;

    private Gamepad currentPad;
    private bool padIsSilent = true;
    private byte sentLow, sentHigh;
    private Color32 sentColor;
    private bool sentColorValid;
    private float lastColorSendTime = -1f;
    private Color baseColor;
    private bool baseColorValid;

    /// <summary>Prende o apaga toda la vibración (pensado para una futura opción del menú).</summary>
    public static bool Enabled
    {
        get => Instance != null && Instance.vibrationEnabled;
        set { if (Instance != null) Instance.vibrationEnabled = value; }
    }

    /// <summary>Dispara el patrón del evento. <paramref name="strength"/> escala sus intensidades.</summary>
    public static void Play(HapticEvent id, float strength = 1f)
    {
        if (Instance == null || !Instance.ShouldOutput()) return;
        Instance.PlayInternal(id, strength);
    }

    /// <summary>
    /// La llama cada corte que se está cocinando, una vez por frame, con los segundos que le faltan
    /// a su primera cara en quemarse. Se queda con el más urgente.
    /// </summary>
    public static void ReportBurnRisk(float secondsToBurn)
    {
        if (Instance == null || !Instance.burnWarning) return;
        if (float.IsInfinity(secondsToBurn) || secondsToBurn >= Instance.burnWarningSeconds) return;

        float urgency = 1f - Mathf.Clamp01(secondsToBurn / Instance.burnWarningSeconds);
        if (urgency > Instance.burnUrgency)
            Instance.burnUrgency = urgency;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        Compile();
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged += HandleSceneChanged;
    }

    private void OnValidate()
    {
        if (Application.isPlaying) Compile();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= HandleSceneChanged;
        Release(currentPad, quitting: true);
        Instance = null;
    }

    private void OnApplicationQuit() => Release(currentPad, quitting: true);

    // Sin "Run In Background" el LateUpdate deja de correr al perder el foco: se calla acá mismo.
    private void OnApplicationFocus(bool focus)
    {
        hasFocus = focus;
        if (focus) return;

        Release(currentPad, quitting: false);
        currentPad = null;
        playing.Clear();
        burnPhase = -1f;
    }

    private void HandleSceneChanged(UnityEngine.SceneManagement.Scene from, UnityEngine.SceneManagement.Scene to)
    {
        playing.Clear();
        burnPhase = -1f;
    }

    // LateUpdate con orden alto: los cortes ya reportaron su riesgo de quemado en su Update.
    private void LateUpdate()
    {
        float now = Time.unscaledTime;
        float urgency = burnUrgency;
        burnUrgency = 0f;

        Gamepad target = ShouldOutput() ? InputManager.ActiveGamepad : null;
        if (target != currentPad)
        {
            Release(currentPad, quitting: false);
            currentPad = target;
            padIsSilent = true;
            sentColorValid = false;
        }

        if (currentPad == null)
        {
            playing.Clear();
            burnPhase = -1f;
            lastSampleTime = now;
            return;
        }

        // En pausa no se acumula nada: al volver no tiene que sonar lo que quedó a medias.
        // Solo sigue lo que se disparó desde la pausa misma (cambio de escena del menú de pausa).
        if (GamePause.IsPaused)
        {
            for (int i = playing.Count - 1; i >= 0; i--)
                if (!playing[i].playsDuringPause) playing.RemoveAt(i);
            burnPhase = -1f;
            urgency = 0f;
        }

        float low = 0f, high = 0f;
        Color baseLight = UpdateBaseColor();
        Color lightbar = baseLight;

        SamplePlaying(now, baseLight, ref low, ref high, ref lightbar);
        SampleBurnWarning(urgency, ref low, ref high, ref lightbar);

        lastSampleTime = now;
        Output(currentPad, low * intensity, high * intensity, lightbar);
    }

    private bool ShouldOutput() => vibrationEnabled && hasFocus && InputManager.UsingGamepad;

    // ── Patrones ──

    private void PlayInternal(HapticEvent id, float strength)
    {
        if (!compiled.TryGetValue(id, out Segment[] segments) || !byId.TryGetValue(id, out HapticPattern pattern))
            return;

        float now = Time.unscaledTime;
        float end = segments.Length > 0 ? segments[segments.Length - 1].end : 0f;
        playing.Add(new ActivePlay
        {
            segments = segments,
            startTime = now,
            endTime = now + Mathf.Max(end, useLightbar ? pattern.flashSeconds : 0f),
            flashSeconds = pattern.flashSeconds,
            flashColor = pattern.flashColor,
            strength = Mathf.Max(0f, strength),
            playsDuringPause = pattern.playsDuringPause,
        });
    }

    private void SamplePlaying(float now, Color baseLight, ref float low, ref float high, ref Color lightbar)
    {
        float strongestFlash = 0f;

        for (int i = playing.Count - 1; i >= 0; i--)
        {
            ActivePlay play = playing[i];

            // Intervalo de este frame en tiempo del patrón: un golpe más corto que un frame
            // igual se siente, porque cuenta si se superpone con el frame y no solo si cae justo.
            float from = lastSampleTime - play.startTime;
            float to = now - play.startTime;

            Segment[] segs = play.segments;
            for (int s = 0; s < segs.Length; s++)
            {
                if (segs[s].start >= to || segs[s].end <= from) continue;
                low = Mathf.Max(low, segs[s].low * play.strength);
                high = Mathf.Max(high, segs[s].high * play.strength);
            }

            if (play.flashSeconds > 0f && to < play.flashSeconds)
            {
                float k = 1f - Mathf.Clamp01(to / play.flashSeconds);
                k *= k;   // ease-out: el color vuelve rápido al principio
                if (k > strongestFlash)
                {
                    strongestFlash = k;
                    lightbar = Color.Lerp(baseLight, Contrast(baseLight, play.flashColor), k);
                }
            }

            if (now >= play.endTime)
                playing.RemoveAt(i);
        }
    }

    private void SampleBurnWarning(float urgency, ref float low, ref float high, ref Color lightbar)
    {
        if (urgency <= 0f)
        {
            burnPhase = -1f;
            return;
        }

        float period = Mathf.Lerp(burnBeatPeriod.x, burnBeatPeriod.y, urgency);
        float dt = Time.unscaledDeltaTime;

        // El primer latido sale apenas empieza el aviso.
        bool beat;
        if (burnPhase < 0f)
        {
            burnPhase = 0f;
            beat = true;
        }
        else
        {
            burnPhase += dt / Mathf.Max(0.05f, period);
            if (burnPhase >= 1f) burnPhase -= Mathf.Floor(burnPhase);
            beat = burnPhase * period < Mathf.Max(burnBeatSeconds, dt);
        }

        if (beat)
        {
            low = Mathf.Max(low, Mathf.Lerp(burnBeatLow.x, burnBeatLow.y, urgency));
            high = Mathf.Max(high, Mathf.Lerp(burnBeatHigh.x, burnBeatHigh.y, urgency));
        }

        // La barra late con el mismo ritmo, más roja cuanto más cerca está de quemarse.
        float glow = (1f - burnPhase) * (1f - burnPhase) * urgency;
        lightbar = Color.Lerp(lightbar, Contrast(lightbar, burnLightbar), glow);
    }

    // ── Barra de luz: semáforo de strikes ──

    /// <summary>Color de reposo de este frame: el de los strikes de la noche (con fundido y respiración) o el de fuera de la noche.</summary>
    private Color UpdateBaseColor()
    {
        StrikeSystem strikes = StrikeSystem.Instance;
        Color target = strikes != null ? StrikeColor(strikes) : lightbarIdle;

        if (!baseColorValid || strikeColorFadeSeconds <= 0f)
            baseColor = target;
        else
            baseColor = Vector4.MoveTowards(baseColor, target, Time.unscaledDeltaTime / strikeColorFadeSeconds * 1.75f);
        baseColorValid = true;

        if (strikes != null && strikes.IsLimitReached && limitBreathSeconds > 0f)
        {
            float wave = 0.5f + 0.5f * Mathf.Cos(Time.unscaledTime * 2f * Mathf.PI / limitBreathSeconds);
            return baseColor * Mathf.Lerp(limitBreathMinBrightness, 1f, wave);
        }
        return baseColor;
    }

    private Color StrikeColor(StrikeSystem strikes)
    {
        if (strikeColors == null || strikeColors.Length == 0) return lightbarIdle;

        // Por lo que falta para el máximo, no por lo que se lleva: si el máximo cambia, rojo sigue
        // siendo "no entra más gente" y naranja "te queda uno".
        int remaining = strikes.IsLimitReached ? 0 : Mathf.Max(0, strikes.MaxStrikes - strikes.CurrentStrikes);
        int last = strikeColors.Length - 1;
        return strikeColors[last - Mathf.Min(remaining, last)];
    }

    /// <summary>El destello, o negro si se parece demasiado a la barra (rojo sobre rojo): así se ve igual.</summary>
    private Color Contrast(Color under, Color flash)
    {
        Vector3 a = new Vector3(under.r, under.g, under.b);
        Vector3 b = new Vector3(flash.r, flash.g, flash.b);
        return Vector3.Distance(a, b) < flashContrastThreshold ? Color.black : flash;
    }

    private void Compile()
    {
        compiled.Clear();
        byId.Clear();

        var list = new List<Segment>();
        var declared = new HashSet<HapticEvent>();
        if (patterns != null)
            foreach (HapticPattern pattern in patterns)
                if (pattern != null && declared.Add(pattern.id) && pattern.enabled)
                    CompilePattern(pattern, list);

        // Un evento que el prefab todavía no tiene suena con su patrón de fábrica: agregar uno nuevo
        // no obliga a tocar el prefab (para editarlo, menú contextual "Agregar patrones que faltan").
        foreach (HapticPattern pattern in DefaultPatterns())
            if (declared.Add(pattern.id))
                CompilePattern(pattern, list);
    }

    [ContextMenu("Agregar patrones que faltan")]
    private void AddMissingDefaultPatterns()
    {
        patterns ??= new List<HapticPattern>();
        foreach (HapticPattern pattern in DefaultPatterns())
            if (!patterns.Exists(p => p != null && p.id == pattern.id))
                patterns.Add(pattern);

        if (Application.isPlaying) Compile();
    }

    private void CompilePattern(HapticPattern pattern, List<Segment> list)
    {
        list.Clear();
        float t = 0f;
        if (pattern.pulses != null)
        {
            foreach (HapticPulse pulse in pattern.pulses)
            {
                if (pulse == null) continue;
                float scale = 1f;
                for (int r = 0; r < Mathf.Max(1, pulse.repeat); r++)
                {
                    list.Add(new Segment { start = t, end = t + pulse.seconds, low = pulse.low * scale, high = pulse.high * scale });
                    t += pulse.seconds + pulse.gapAfter;
                    scale *= pulse.repeatFalloff;
                }
            }
        }

        byId[pattern.id] = pattern;
        compiled[pattern.id] = list.ToArray();
    }

    // ── Salida ──

    private void Output(Gamepad pad, float low, float high, Color lightbar)
    {
        byte lowByte = (byte)Mathf.RoundToInt(Mathf.Clamp01(low) * 255f);
        byte highByte = (byte)Mathf.RoundToInt(Mathf.Clamp01(high) * 255f);
        Color32 color = lightbar;
        bool wantsColor = useLightbar && pad is DualShockGamepad;

        bool motorsChanged = padIsSilent ? (lowByte | highByte) != 0 : lowByte != sentLow || highByte != sentHigh;
        bool colorChanged = wantsColor && (!sentColorValid || !SameColor(color, sentColor));

        // La respiración y los fundidos cambian el color casi todos los frames: sin tope, sería un
        // reporte por frame. Si los motores cambian, el color viaja en el mismo reporte igual.
        float now = Time.unscaledTime;
        if (colorChanged && !motorsChanged && sentColorValid && now - lastColorSendTime < 1f / lightbarMaxUpdatesPerSecond)
            colorChanged = false;
        if (!motorsChanged && !colorChanged) return;

        if (!Send(pad, lowByte / 255f, highByte / 255f, wantsColor ? lightbar : (Color?)null))
            return;

        sentLow = lowByte;
        sentHigh = highByte;
        padIsSilent = (lowByte | highByte) == 0;
        if (wantsColor)
        {
            sentColor = color;
            sentColorValid = true;
            lastColorSendTime = now;
        }
    }

    private bool Send(Gamepad pad, float low, float high, Color? lightbar)
    {
        switch (pad)
        {
            case DualSenseGamepadHID dualSense:
                return DualSenseOutput.Send(dualSense, low, high, lightbar, dualSenseRumble);

            case DualShock4GamepadHID dualShock4 when lightbar.HasValue:
                // Motores y color en un solo comando: dos seguidos pueden pisarse (ver el doc del Input System).
                return dualShock4.SetMotorSpeedsAndLightBarColor(low, high, lightbar.Value);

            default:
                pad.SetMotorSpeeds(low, high);
                return true;
        }
    }

    private void Release(Gamepad pad, bool quitting)
    {
        if (pad == null || !pad.added) return;

        Color? color = null;
        if (useLightbar && pad is DualShockGamepad && sentColorValid)
            color = quitting ? lightbarOnQuit : (baseColorValid ? baseColor : lightbarIdle);

        if (pad is DualSenseGamepadHID || (pad is DualShock4GamepadHID && color.HasValue))
            Send(pad, 0f, 0f, color);
        else
            pad.ResetHaptics();

        padIsSilent = true;
        sentLow = sentHigh = 0;
        if (quitting) sentColorValid = false;
    }

    private static bool SameColor(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;

    // ── Valores de fábrica (punto de partida para iterar en el prefab) ──

    private static List<HapticPattern> DefaultPatterns() => new List<HapticPattern>
    {
        // Apoyar el plato en el cliente: un toque, nada más. El golpe fuerte es la plata.
        new HapticPattern(HapticEvent.DeliveryAccepted, 0f, Color.white,
            new HapticPulse(0.12f, 0.25f, 0.05f)),

        // La plata entra al contador: golpe seco + tic-tic que se apaga mientras cuenta.
        // Destello blanco: el verde ya es "sin strikes" y no se vería.
        new HapticPattern(HapticEvent.MoneyLanded, 0.6f, new Color(1f, 1f, 0.85f),
            new HapticPulse(0.45f, 0.85f, 0.07f, gapAfter: 0.05f),
            new HapticPulse(0f, 0.45f, 0.022f, gapAfter: 0.04f, repeat: 5, repeatFalloff: 0.8f)),

        // "No, no": dos golpes pesados.
        new HapticPattern(HapticEvent.DeliveryRejected, 0.5f, new Color(1f, 0f, 0f),
            new HapticPulse(0.85f, 0.35f, 0.10f, gapAfter: 0.07f),
            new HapticPulse(0.65f, 0.25f, 0.12f)),

        // Carne cruda o quemada entregada: strike. Golpe largo que se apaga.
        new HapticPattern(HapticEvent.BadCookingDelivered, 0.9f, new Color(1f, 0f, 0f),
            new HapticPulse(1f, 0.6f, 0.18f, gapAfter: 0.04f),
            new HapticPulse(0.6f, 0.25f, 0.25f)),

        // Se quemó (cualquier pieza, seleccionada o no): golpe que corta el latido del aviso y un
        // chamuscado que se apaga. Más fuerte que el tuc tuc de cambio de punto.
        new HapticPattern(HapticEvent.MeatBurned, 0.6f, new Color(0.6f, 0.05f, 0f),
            new HapticPulse(0.65f, 0.55f, 0.1f, gapAfter: 0.04f),
            new HapticPulse(0.35f, 0.2f, 0.05f, gapAfter: 0.03f, repeat: 3, repeatFalloff: 0.75f)),

        // Se fue sin que lo atiendan: un golpe y un rumor que se apaga (se distingue del rechazo,
        // que son dos golpes secos). La barra cambia de color sola con el strike.
        new HapticPattern(HapticEvent.CustomerLeftAngry, 0.7f, new Color(1f, 0f, 0f),
            new HapticPulse(0.8f, 0.3f, 0.12f, gapAfter: 0.06f),
            new HapticPulse(0.45f, 0.1f, 0.06f, repeat: 6, repeatFalloff: 0.8f)),

        // ── Manos: acciones frecuentes. Casi todo en el motor chico y corto, para no tapar lo importante ──

        // Agarrar: un clic apenas perceptible, confirma que la pieza está en la mano.
        new HapticPattern(HapticEvent.PieceGrabbed, 0f, Color.white,
            new HapticPulse(0.05f, 0.22f, 0.03f)),

        // Carne a la parrilla: apoyo + chisporroteo que se apaga.
        new HapticPattern(HapticEvent.MeatOnGrill, 0f, Color.white,
            new HapticPulse(0.3f, 0.25f, 0.05f, gapAfter: 0.03f),
            new HapticPulse(0f, 0.18f, 0.02f, gapAfter: 0.035f, repeat: 4, repeatFalloff: 0.75f)),

        // Carbón: más pesado y seco que la carne, sin chisporroteo.
        new HapticPattern(HapticEvent.CoalOnGrill, 0f, Color.white,
            new HapticPulse(0.45f, 0.1f, 0.07f)),

        // "Nop": dos tics suaves. Se distingue del rechazo del cliente, que son dos golpes pesados.
        new HapticPattern(HapticEvent.ActionRefused, 0f, Color.white,
            new HapticPulse(0.12f, 0.3f, 0.03f, gapAfter: 0.06f, repeat: 2, repeatFalloff: 0.8f)),

        // Despegue del flip; el aterrizaje lo pone MeatOnGrill.
        new HapticPattern(HapticEvent.MeatFlipped, 0f, Color.white,
            new HapticPulse(0f, 0.3f, 0.03f)),

        new HapticPattern(HapticEvent.PieceRotated, 0f, Color.white,
            new HapticPulse(0f, 0.2f, 0.02f)),

        // Corte al plato: golpe medio, más presente que un pan o una guarnición.
        new HapticPattern(HapticEvent.MeatOnPlate, 0f, Color.white,
            new HapticPulse(0.25f, 0.35f, 0.06f)),

        new HapticPattern(HapticEvent.PiecePlaced, 0f, Color.white,
            new HapticPulse(0.1f, 0.28f, 0.04f)),

        // Se dispara con cada salpicadura (cada 0.05 s): pulsos casi pegados = zumbido fino del chorro.
        new HapticPattern(HapticEvent.SaucePouring, 0f, Color.white,
            new HapticPulse(0.06f, 0.12f, 0.03f)),

        // Barrido: golpe y tics que se apagan.
        new HapticPattern(HapticEvent.PlateCleared, 0f, Color.white,
            new HapticPulse(0.15f, 0.35f, 0.04f, gapAfter: 0.02f),
            new HapticPulse(0.05f, 0.25f, 0.03f, gapAfter: 0.02f, repeat: 3, repeatFalloff: 0.7f)),

        // "Tic-toc" hacia atrás.
        new HapticPattern(HapticEvent.UndoDone, 0f, Color.white,
            new HapticPulse(0f, 0.3f, 0.025f, gapAfter: 0.05f),
            new HapticPulse(0.1f, 0.2f, 0.03f)),

        // Cae al tacho: golpe sordo + rebote.
        new HapticPattern(HapticEvent.Trashed, 0f, Color.white,
            new HapticPulse(0.5f, 0.25f, 0.08f, gapAfter: 0.04f),
            new HapticPulse(0.2f, 0.1f, 0.05f)),

        // Cepillado: tres pasadas que se apagan.
        new HapticPattern(HapticEvent.AshesCleaned, 0f, Color.white,
            new HapticPulse(0.2f, 0.15f, 0.04f, gapAfter: 0.03f, repeat: 3, repeatFalloff: 0.8f)),

        new HapticPattern(HapticEvent.GrillLayerToggled, 0f, Color.white,
            new HapticPulse(0.08f, 0.35f, 0.03f)),

        // Panel que se desliza: un roce. Al cerrar suena más suave (strength).
        new HapticPattern(HapticEvent.PanelToggled, 0f, Color.white,
            new HapticPulse(0.08f, 0.15f, 0.05f)),

        // Plato sobre un cliente: con gamepad el cursor salta entre clientes y esto marca cada uno.
        new HapticPattern(HapticEvent.DeliveryTargetHover, 0f, Color.white,
            new HapticPulse(0f, 0.2f, 0.025f)),

        // ── Mundo ──

        // Llega un cliente: rumor grave que crece (se acerca) y una campanita. Sin golpes dobles,
        // para no confundirse con el tuc tuc del cambio de punto.
        new HapticPattern(HapticEvent.CustomerArrived, 0f, Color.white,
            new HapticPulse(0.05f, 0f, 0.05f),
            new HapticPulse(0.1f, 0f, 0.05f),
            new HapticPulse(0.16f, 0.05f, 0.06f, gapAfter: 0.05f),
            new HapticPulse(0f, 0.25f, 0.025f)),

        new HapticPattern(HapticEvent.OrderChanged, 0f, Color.white,
            new HapticPulse(0.15f, 0.3f, 0.05f, gapAfter: 0.05f),
            new HapticPulse(0f, 0.25f, 0.03f)),

        // Cierra el local: campanazo que se apaga. Destello ámbar (el rojo/verde ya son strikes).
        new HapticPattern(HapticEvent.ClosingTime, 0.8f, new Color(1f, 0.55f, 0.05f),
            new HapticPulse(0.55f, 0.5f, 0.12f, gapAfter: 0.06f),
            new HapticPulse(0.3f, 0.25f, 0.08f, gapAfter: 0.05f, repeat: 3, repeatFalloff: 0.7f)),

        // Caja registradora: tic + golpe. Destello verde (fuera de la noche la barra es blanca).
        new HapticPattern(HapticEvent.PurchaseDone, 0.4f, new Color(0.2f, 1f, 0.3f),
            new HapticPulse(0f, 0.45f, 0.025f, gapAfter: 0.05f),
            new HapticPulse(0.35f, 0.6f, 0.07f)),

        // Cambio de escena: golpe firme y cola corta. Entra en el fundido a negro (0.35 s) y
        // suena también desde el menú de pausa.
        new HapticPattern(HapticEvent.SceneTransition, 0f, Color.white,
            new HapticPulse(0.3f, 0.4f, 0.08f, gapAfter: 0.04f),
            new HapticPulse(0.12f, 0.15f, 0.1f)) { playsDuringPause = true },

        // Cambio de punto de la carne seleccionada: "tuc tuc" suave.
        new HapticPattern(HapticEvent.MeatStateChanged, 0f, Color.white,
            new HapticPulse(0.12f, 0.22f, 0.035f, gapAfter: 0.08f, repeat: 2, repeatFalloff: 0.85f)),
    };
}
