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

    // ---- QA: provocar cada estado de forma determinística desde el inspector en Play Mode ----

    [ContextMenu("QA/Sumar un strike")]
    private void DebugAddStrike() => RegisterPatienceStrike();

    [ContextMenu("QA/Reiniciar strikes")]
    private void DebugReset() => ResetForNewNight();
}
