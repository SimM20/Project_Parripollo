using System;
using UnityEngine;

/// <summary>
/// Ventana de entrada de clientes de la jornada. Es el único dueño del tiempo de día.
///
/// La ventana <b>no arranca al cargar la escena</b>: arranca exactamente cuando entra el
/// primer cliente (<see cref="BeginWindow"/>). Su duración la fija la curva de progresión
/// (<see cref="ProgressionConfigSO.GetWindowSeconds"/>): 3:00 el día 1, creciendo hasta un
/// tope de 7:00.
///
/// El reloj <b>no termina el día</b>. Al agotarse la ventana se detiene y avisa con
/// <see cref="OnClosingTime"/>: desde ahí no entra nadie más, pero los clientes que ya están
/// adentro se siguen atendiendo con sus reglas de siempre. El día termina cuando se va el
/// último (lo decide <see cref="CustomerSystem"/>).
///
/// Corre con Time.deltaTime, así que <see cref="GamePause"/> (timeScale = 0) lo congela solo.
///
/// Por pedido del spec, el tiempo restante <b>existe internamente pero no se le muestra al
/// jugador</b>: el mapeo a hora del día (06:30 → 21:00) y <see cref="HudLabel"/> quedan
/// listos para cuando se defina el HUD temporal, detrás de <see cref="pushToHud"/>.
///
/// Es opcional: una escena sin DayClock (el tutorial) no tiene ventana ni entrada automática
/// de clientes, y su día no termina solo.
/// </summary>
public class DayClock : MonoBehaviour
{
    public static DayClock Instance { get; private set; }

    [Header("Duración de la ventana")]
    [Tooltip("Segundos de ventana cuando no hay ProgressionConfig que la defina (tutorial, pruebas). " +
             "En la partida normal la pisa la curva de progresión del día.")]
    [Min(1f)]
    [SerializeField] private float fallbackWindowSeconds = 180f;

    [Header("Hora del día (interna)")]
    [Tooltip("Hora de apertura en horas decimales: 6.5 = 06:30.")]
    [Range(0f, 24f)]
    [SerializeField] private float openingHour = 6.5f;

    [Tooltip("Hora de cierre en horas decimales: 21 = 21:00. La ventana entera se mapea sobre esta franja.")]
    [Range(0f, 24f)]
    [SerializeField] private float closingHour = 21f;

    [Header("HUD (desactivado por spec)")]
    [Tooltip("El spec pide que el reloj NO se muestre todavía. Encender solo cuando se defina " +
             "el HUD temporal y exista un HudContainer de tipo Time en la escena.")]
    [SerializeField] private bool pushToHud = false;

    [Tooltip("Redondeo de los minutos mostrados. Con 5 el reloj salta de 06:30 a 06:35.")]
    [Range(1, 30)]
    [SerializeField] private int displayMinuteStep = 5;

    [Tooltip("Texto que reemplaza la hora una vez cerrada la ventana.")]
    [SerializeField] private string closedLabel = "CERRADO";

    /// <summary>Se dispara una sola vez, al agotarse la ventana de entrada.</summary>
    public event Action OnClosingTime;

    /// <summary>True mientras la ventana corre (ya entró el primer cliente y todavía no cerró).</summary>
    public bool IsRunning { get; private set; }

    /// <summary>True desde que la ventana se agotó. No vuelve a false hasta el próximo día.</summary>
    public bool HasClosed { get; private set; }

    /// <summary>True mientras todavía puede entrar gente nueva (incluido antes del primer cliente).</summary>
    public bool IsOpen => !HasClosed;

    /// <summary>Duración de la ventana de hoy, en segundos.</summary>
    public float WindowSeconds { get; private set; }

    public float ElapsedSeconds { get; private set; }

    /// <summary>Segundos de ventana que quedan. Es interno: no se le muestra al jugador.</summary>
    public float RemainingSeconds => Mathf.Max(0f, WindowSeconds - ElapsedSeconds);

    /// <summary>0 al abrir, 1 al cerrar.</summary>
    public float Normalized01 =>
        WindowSeconds > 0f ? Mathf.Clamp01(ElapsedSeconds / WindowSeconds) : 0f;

    public float OpeningHour => openingHour;
    public float ClosingHour => closingHour;

    /// <summary>Hora del día en horas decimales (6.5 = 06:30), mapeando la ventana sobre la franja.</summary>
    public float CurrentHour => Mathf.Lerp(openingHour, closingHour, Normalized01);

    /// <summary>Hora actual como "HH:MM", redondeada al paso del display.</summary>
    public string TimeLabel => FormatHour(CurrentHour, displayMinuteStep);

    /// <summary>Lo que iría al HUD: la hora mientras está abierto, el cartel de cerrado después.</summary>
    public string HudLabel => HasClosed ? closedLabel : TimeLabel;

    private string lastPushedLabel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        WindowSeconds = fallbackWindowSeconds;
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

        ElapsedSeconds += Time.deltaTime;

        PushLabel();

        if (ElapsedSeconds >= WindowSeconds)
            Close();
    }

    /// <summary>
    /// Deja la ventana lista para la jornada, sin arrancarla. La arranca el primer cliente.
    /// <paramref name="windowSeconds"/> en 0 o menos cae al valor de respaldo del inspector.
    /// </summary>
    public void PrepareDay(float windowSeconds)
    {
        WindowSeconds = windowSeconds > 0f ? windowSeconds : fallbackWindowSeconds;
        ElapsedSeconds = 0f;
        HasClosed = false;
        IsRunning = false;
        lastPushedLabel = null;

        PushLabel();

        Debug.Log(
            "[DayClock] Ventana preparada: " + FormatSeconds(WindowSeconds) +
            " (" + Mathf.RoundToInt(WindowSeconds) + "s). " +
            "Arranca cuando entre el primer cliente."
        );
    }

    /// <summary>
    /// Arranca el contador. Lo llama <see cref="CustomerSystem"/> al entrar el primer cliente
    /// del día. Llamarlo de nuevo no hace nada: la ventana arranca una sola vez.
    /// </summary>
    public void BeginWindow()
    {
        if (IsRunning || HasClosed)
            return;

        IsRunning = true;

        Debug.Log(
            "[DayClock] Entró el primer cliente: arranca la ventana de " +
            FormatSeconds(WindowSeconds) + "."
        );
    }

    /// <summary>
    /// Corta la ventana sin agotarla (cambio de escena, final anticipado desde el menú).
    /// No dispara <see cref="OnClosingTime"/>.
    /// </summary>
    public void StopDay() => IsRunning = false;

    private void Close()
    {
        if (HasClosed)
            return;

        IsRunning = false;
        HasClosed = true;
        ElapsedSeconds = WindowSeconds;

        PushLabel();

        Debug.Log("[DayClock] Se agotó la ventana de entrada: no entra nadie más.");

        OnClosingTime?.Invoke();
    }

    /// <summary>Manda el texto al HUD solo cuando cambia: si no, sería un string por frame.</summary>
    private void PushLabel()
    {
        if (!pushToHud)
            return;

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

    /// <summary>Segundos → "M:SS", para los logs de duración de la ventana.</summary>
    public static string FormatSeconds(float seconds)
    {
        int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
        return (total / 60) + ":" + (total % 60).ToString("00");
    }

    private void OnValidate()
    {
        // Un cierre anterior o igual a la apertura dejaría la franja horaria en cero.
        if (closingHour <= openingHour)
            closingHour = Mathf.Min(24f, openingHour + 0.5f);
    }
}
