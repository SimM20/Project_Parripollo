using System;
using UnityEngine;

/// <summary>
/// Sistema de Strikes por Clientes Perdidos (spec v0.1).
/// Cuenta los clientes que se fueron porque su paciencia llegó a 0. Al alcanzar
/// <see cref="MaxStrikes"/> se activa el estado de cierre anticipado: CustomerSystem deja
/// de generar clientes nuevos y la noche termina cuando ya no queda ninguno activo.
///
/// Singleton de escena, null-safe: si no existe en la escena (ej. TutorialScene) los
/// llamadores usan los helpers estáticos y nada cambia (mismo patrón que TutorialManager).
/// Los strikes pertenecen sólo a la noche actual: <see cref="ResetForNewNight"/> los limpia.
/// </summary>
public class StrikeSystem : MonoBehaviour
{
    public static StrikeSystem Instance { get; private set; }

    [Header("Balance (sujeto a playtesting)")]
    [Min(1)]
    [Tooltip("Clientes perdidos por paciencia necesarios para que dejen de llegar clientes nuevos.")]
    [SerializeField] private int maxStrikes = 3;

    [Min(0f)]
    [Tooltip("Segundos que permanece visible el aviso '¡Te clavaron el cartel!' al llegar al límite.")]
    [SerializeField] private float limitNoticeSeconds = 5f;

    [Tooltip("Config de derrota total de la run. De acá sale el tope de noches seguidas con el cartel clavado.")]
    [SerializeField] private RunDefeatConfigSO runDefeatConfig;

    /// <summary>Strikes acumulados en la noche actual. Nunca supera <see cref="MaxStrikes"/>.</summary>
    public int CurrentStrikes { get; private set; }

    public int MaxStrikes => Mathf.Max(1, maxStrikes);

    public float LimitNoticeSeconds => Mathf.Max(0f, limitNoticeSeconds);

    /// <summary>Estado de cierre por strikes: se alcanzó el límite y ya no entran clientes nuevos.</summary>
    public bool IsLimitReached { get; private set; }

    /// <summary>
    /// True si la última noche jugada terminó por el sistema de strikes. Es estático para
    /// sobrevivir la carga de EndScene, donde el popup explicativo lo consume
    /// (<see cref="ConsumeNightEndedByStrikes"/>). Se limpia también al empezar una noche.
    /// </summary>
    public static bool LastNightEndedByStrikes { get; private set; }

    /// <summary>
    /// Noches SEGUIDAS que terminaron por el límite de strikes. Es estático por el mismo
    /// motivo que <see cref="LastNightEndedByStrikes"/>: tiene que sobrevivir el viaje
    /// GameScene -> EndScene -> GameScene, y StrikeSystem no existe en EndScene.
    /// A diferencia de los strikes, la racha NO se limpia al empezar una noche.
    /// </summary>
    public static int ConsecutiveStrikeNights { get; private set; }

    /// <summary>Tope de noches seguidas, cacheado del SO para que EndScene lo pueda leer.</summary>
    public static int MaxConsecutiveStrikeNights { get; private set; } = 3;

    /// <summary>Si la racha puede provocar derrota. Cacheado del SO igual que el tope.</summary>
    public static bool StrikeStreakDefeatEnabled { get; private set; } = true;

    /// <summary>La racha llegó al tope: la run está perdida.</summary>
    public static bool IsStrikeStreakDefeat
        => StrikeStreakDefeatEnabled &&
           ConsecutiveStrikeNights >= Mathf.Max(1, MaxConsecutiveStrikeNights);

    /// <summary>(strikes actuales, máximo). Dispara una vez por strike real, nunca por encima del máximo.</summary>
    public event Action<int, int> OnStrikeAdded;

    /// <summary>Dispara una sola vez por noche, al registrar el strike que alcanza el límite.</summary>
    public event Action OnLimitReached;

    /// <summary>Inicio de noche: contador en 0 y cierre desactivado.</summary>
    public event Action OnReset;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (runDefeatConfig != null)
        {
            MaxConsecutiveStrikeNights = runDefeatConfig.MaxConsecutiveStrikeNights;
            StrikeStreakDefeatEnabled = runDefeatConfig.strikeStreakDefeatEnabled;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Inicio de jornada: strikes = 0, spawn habilitado, HUD reiniciado.</summary>
    public void ResetForNewNight()
    {
        CurrentStrikes = 0;
        IsLimitReached = false;
        LastNightEndedByStrikes = false;

        // OJO: ConsecutiveStrikeNights NO se toca acá. Los strikes son de la noche, la racha
        // es de la run y solo la mueve RegisterNightResult al cerrar la jornada.

        OnReset?.Invoke();
    }

    /// <summary>
    /// Suma exactamente 1 strike por un cliente cuya paciencia llegó a 0. Es la única causa
    /// válida: cualquier otra retirada (faltante de stock, etc.) no debe pasar por acá.
    /// Devuelve false si el contador ya estaba saturado: el cliente se retira igual, pero el
    /// HUD sigue mostrando el máximo.
    /// </summary>
    public bool RegisterPatienceStrike()
    {
        if (IsLimitReached)
            return false;

        CurrentStrikes = Mathf.Min(CurrentStrikes + 1, MaxStrikes);

        Debug.Log("[StrikeSystem] Strike " + CurrentStrikes + "/" + MaxStrikes);

        OnStrikeAdded?.Invoke(CurrentStrikes, MaxStrikes);

        if (CurrentStrikes >= MaxStrikes)
        {
            IsLimitReached = true;

            Debug.Log("[StrikeSystem] Límite alcanzado: se bloquea la llegada de clientes nuevos.");

            OnLimitReached?.Invoke();
        }

        return true;
    }

    /// <summary>
    /// CustomerSystem lo llama al terminar la noche con el cierre activo, justo antes de
    /// cargar EndScene, para que el popup explicativo sepa que debe mostrarse.
    /// </summary>
    public void MarkNightEndedByStrikes()
    {
        LastNightEndedByStrikes = true;
    }

    /// <summary>Lee y limpia el flag de cierre anticipado. Lo usa el popup de EndScene.</summary>
    public static bool ConsumeNightEndedByStrikes()
    {
        bool value = LastNightEndedByStrikes;
        LastNightEndedByStrikes = false;
        return value;
    }

    // ---- Helpers estáticos null-safe para los llamadores (CustomerSystem) ----

    /// <summary>True si hay sistema de strikes en la escena y ya se alcanzó el límite.</summary>
    public static bool IsSpawnBlocked => Instance != null && Instance.IsLimitReached;

    // ---- Racha de noches cerradas por strikes (derrota total de la run) ----

    /// <summary>
    /// Cierre de jornada: suma o resetea la racha. Lo llama <see cref="GameManager.EndNight"/>,
    /// que es el único embudo de fin de día (tanto el último cliente como el botón del menú
    /// de pausa terminan ahí).
    /// </summary>
    public static void RegisterNightResult(bool endedByStrikes)
    {
        ConsecutiveStrikeNights = endedByStrikes ? ConsecutiveStrikeNights + 1 : 0;

        Debug.Log(
            "[StrikeSystem] Noche cerrada " + (endedByStrikes ? "por strikes" : "normalmente") +
            ". Racha: " + ConsecutiveStrikeNights + "/" + MaxConsecutiveStrikeNights
        );
    }

    /// <summary>Vuelve la racha a cero. Lo usa el reinicio de run desde la pantalla de derrota.</summary>
    public static void ResetStreak()
    {
        ConsecutiveStrikeNights = 0;
        LastNightEndedByStrikes = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        // El Editor puede entrar a Play sin domain reload: los estáticos de la sesión
        // anterior arrastrarían una racha ajena (mismo recurso que SlidingPanel).
        ConsecutiveStrikeNights = 0;
        LastNightEndedByStrikes = false;
        MaxConsecutiveStrikeNights = 3;
        StrikeStreakDefeatEnabled = true;
    }

    // ---- QA: provocar cada estado de forma determinística desde el inspector en Play Mode ----

    [ContextMenu("QA/Sumar un strike")]
    private void DebugAddStrike() => RegisterPatienceStrike();

    [ContextMenu("QA/Reiniciar strikes")]
    private void DebugReset() => ResetForNewNight();

    [ContextMenu("QA/Sumar noche cerrada por strikes")]
    private void DebugAddStrikeNight() => RegisterNightResult(true);

    [ContextMenu("QA/Reiniciar racha de noches")]
    private void DebugResetStreak() => ResetStreak();
}
