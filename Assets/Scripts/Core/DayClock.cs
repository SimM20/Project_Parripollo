using System;
using UnityEngine;

/// <summary>
/// Reloj de la jornada: mapea la franja horaria del local (06:30 → 21:00 por defecto)
/// sobre una cantidad fija de segundos reales. Es el único dueño de la hora del juego;
/// el resto la lee, nadie la escribe.
///
/// El reloj NO termina el día por sí solo. Al llegar a la hora de cierre se detiene y
/// avisa con <see cref="OnClosingTime"/>: desde ahí no entra nadie más, pero los clientes
/// que ya están adentro se siguen atendiendo. El día termina cuando se va el último
/// (lo decide <see cref="CustomerSystem"/>), por más que afuera ya sea de noche.
///
/// Corre con Time.deltaTime, así que <see cref="GamePause"/> (timeScale = 0) lo congela
/// solo: no hay que acordarse de pausarlo a mano.
///
/// Es opcional: una escena sin DayClock (el tutorial) mantiene el modo viejo de
/// "atender una cantidad fija de clientes".
/// </summary>
public class DayClock : MonoBehaviour
{
    public static DayClock Instance { get; private set; }

    [Header("Horario del local")]
    [Tooltip("Hora de apertura en horas decimales: 6.5 = 06:30.")]
    [Range(0f, 24f)]
    [SerializeField] private float openingHour = 6.5f;

    [Tooltip("Hora de cierre en horas decimales: 21 = 21:00. A esa hora deja de entrar gente.")]
    [Range(0f, 24f)]
    [SerializeField] private float closingHour = 21f;

    [Header("Duración real")]
    [Tooltip("Segundos reales que tarda el reloj en ir de la apertura al cierre. 300 = 5 minutos.")]
    [Min(1f)]
    [SerializeField] private float dayDurationSeconds = 300f;

    [Header("Display")]
    [Tooltip("Redondeo de los minutos que muestra el HUD. Con 5 el reloj salta de 06:30 a 06:35: " +
             "a esta velocidad, mostrar cada minuto es ilegible.")]
    [Range(1, 30)]
    [SerializeField] private int displayMinuteStep = 5;

    [Tooltip("Texto del HUD una vez cerrado el local, mientras se atiende a los últimos clientes.")]
    [SerializeField] private string closedLabel = "CERRADO";

    /// <summary>Se dispara una sola vez, al llegar a la hora de cierre.</summary>
    public event Action OnClosingTime;

    /// <summary>True mientras el reloj avanza (entre la apertura y el cierre).</summary>
    public bool IsRunning { get; private set; }

    /// <summary>True desde que el reloj llegó a la hora de cierre. No vuelve a false hasta el próximo día.</summary>
    public bool HasClosed { get; private set; }

    /// <summary>True mientras todavía puede entrar gente nueva.</summary>
    public bool IsOpen => IsRunning && !HasClosed;

    public float OpeningHour => openingHour;
    public float ClosingHour => closingHour;

    /// <summary>Segundos reales que dura la jornada de punta a punta.</summary>
    public float DayDurationSeconds => dayDurationSeconds;

    /// <summary>Hora actual en horas decimales (6.5 = 06:30).</summary>
    public float CurrentHour { get; private set; }

    /// <summary>0 en la apertura, 1 en el cierre. Es la entrada de la curva de afluencia.</summary>
    public float Normalized01 =>
        dayDurationSeconds > 0f
            ? Mathf.Clamp01(elapsedSeconds / dayDurationSeconds)
            : 1f;

    /// <summary>Segundos reales que faltan para el cierre.</summary>
    public float RemainingRealSeconds => Mathf.Max(0f, dayDurationSeconds - elapsedSeconds);

    /// <summary>Hora actual como "HH:MM", ya redondeada al paso del display.</summary>
    public string TimeLabel => FormatHour(CurrentHour, displayMinuteStep);

    /// <summary>Lo que va al HUD: la hora mientras está abierto, el cartel de cerrado después.</summary>
    public string HudLabel => HasClosed ? closedLabel : TimeLabel;

    private float elapsedSeconds;
    private string lastPushedLabel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CurrentHour = openingHour;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start() => PushLabel();

    private void Update()
    {
        if (!IsRunning)
            return;

        elapsedSeconds += Time.deltaTime;

        float t = Normalized01;
        CurrentHour = Mathf.Lerp(openingHour, closingHour, t);

        PushLabel();

        if (t >= 1f)
            Close();
    }

    /// <summary>
    /// Arranca la jornada desde la hora de apertura. Lo llama <see cref="CustomerSystem.StartNight"/>:
    /// el reloj y la entrada de clientes empiezan juntos.
    /// </summary>
    public void StartDay()
    {
        elapsedSeconds = 0f;
        CurrentHour = openingHour;
        HasClosed = false;
        IsRunning = true;
        lastPushedLabel = null;

        PushLabel();

        Debug.Log(
            "[DayClock] Jornada iniciada: " + FormatHour(openingHour) +
            " → " + FormatHour(closingHour) +
            " en " + dayDurationSeconds + "s reales."
        );
    }

    /// <summary>
    /// Corta el reloj sin llegar a la hora de cierre (cambio de escena, final anticipado).
    /// No dispara <see cref="OnClosingTime"/>.
    /// </summary>
    public void StopDay() => IsRunning = false;

    /// <summary>
    /// Cierra el local antes de hora: el reloj salta a la hora de cierre, se detiene y
    /// dispara <see cref="OnClosingTime"/> como si se hubiera llegado naturalmente.
    /// Lo usa el sistema de strikes al alcanzar el límite. No hace nada si ya cerró.
    /// </summary>
    public void CloseEarly(string reason)
    {
        if (HasClosed)
            return;

        elapsedSeconds = dayDurationSeconds;

        Debug.Log("[DayClock] Cierre anticipado (" + reason + ").");

        Close();
    }

    private void Close()
    {
        if (HasClosed)
            return;

        IsRunning = false;
        HasClosed = true;
        CurrentHour = closingHour;

        PushLabel();

        Debug.Log(
            "[DayClock] Cerró el local a las " + FormatHour(closingHour) +
            ". No entra nadie más; el día termina cuando se vaya el último cliente."
        );

        OnClosingTime?.Invoke();
    }

    /// <summary>Manda el texto al HUD solo cuando cambia: si no, sería un string por frame.</summary>
    private void PushLabel()
    {
        string label = HudLabel;

        if (label == lastPushedLabel)
            return;

        lastPushedLabel = label;
        UIManager.Instance?.SetDayTime(label);
    }

    /// <summary>
    /// Horas decimales → "HH:MM". <paramref name="minuteStep"/> redondea los minutos hacia
    /// abajo (5 → 06:30, 06:35, 06:40...).
    /// </summary>
    public static string FormatHour(float hour, int minuteStep = 1)
    {
        int step = Mathf.Clamp(minuteStep, 1, 60);

        int totalMinutes = Mathf.FloorToInt(hour * 60f);
        totalMinutes = Mathf.Max(0, (totalMinutes / step) * step);

        int hours = (totalMinutes / 60) % 24;
        int minutes = totalMinutes % 60;

        return hours.ToString("00") + ":" + minutes.ToString("00");
    }

    private void OnValidate()
    {
        // Un cierre anterior o igual a la apertura dejaría la jornada en duración cero.
        if (closingHour <= openingHour)
            closingHour = Mathf.Min(24f, openingHour + 0.5f);
    }
}
