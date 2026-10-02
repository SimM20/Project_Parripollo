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
/// Se calla en pausa, al perder el foco y al cambiar de escena. El DualSense va por
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
    /// a la cara apoyada para quemarse. Se queda con el más urgente.
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
        if (GamePause.IsPaused)
        {
            playing.Clear();
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
        if (patterns == null) return;

        var list = new List<Segment>();
        foreach (HapticPattern pattern in patterns)
        {
            if (pattern == null || !pattern.enabled || byId.ContainsKey(pattern.id)) continue;

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

        // Se quemó: un "pff" corto que corta el latido del aviso.
        new HapticPattern(HapticEvent.MeatBurned, 0.4f, new Color(0.6f, 0.05f, 0f),
            new HapticPulse(0.4f, 0.5f, 0.12f)),

        // Se fue sin que lo atiendan: un golpe y un rumor que se apaga (se distingue del rechazo,
        // que son dos golpes secos). La barra cambia de color sola con el strike.
        new HapticPattern(HapticEvent.CustomerLeftAngry, 0.7f, new Color(1f, 0f, 0f),
            new HapticPulse(0.8f, 0.3f, 0.12f, gapAfter: 0.06f),
            new HapticPulse(0.45f, 0.1f, 0.06f, repeat: 6, repeatFalloff: 0.8f)),
    };
}
