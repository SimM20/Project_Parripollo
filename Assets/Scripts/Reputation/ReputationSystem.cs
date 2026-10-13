using System;
using UnityEngine;

/// <summary>Por qué cambió la reputación. Solo informa (HUD, log): las reglas viven en <see cref="ReputationConfigSO"/>.</summary>
public enum ReputationReason
{
    /// <summary>Entrega cobrada: suma o resta según cómo salió el plato.</summary>
    Delivery,
    /// <summary>Un cliente se fue enojado: sin paciencia o con un corte crudo/quemado.</summary>
    AngryCustomer,
    /// <summary>El jugador terminó la jornada antes de tiempo desde la pausa.</summary>
    EarlyDayEnd,
    /// <summary>Se compró un local: la experiencia no cambia, pero se levanta el tope.</summary>
    VenuePurchased,
    /// <summary>Arrancó una run nueva.</summary>
    RunReset,
    Debug
}

/// <summary>Un cambio de reputación ya aplicado.</summary>
public struct ReputationChange
{
    public ReputationReason reason;
    /// <summary>Lo que pidió la regla (+20, −15...).</summary>
    public int requested;
    /// <summary>Lo que entró de verdad: menos que lo pedido si toca el tope del local o el piso de 0.</summary>
    public int applied;
    public int levelBefore;
    public int levelAfter;

    public bool LeveledUp => levelAfter > levelBefore;
    public bool LeveledDown => levelAfter < levelBefore;
}

/// <summary>
/// Reputación de la run: un número de experiencia del que sale el nivel (0 a <see cref="MaxLevel"/>).
/// Sube entregando bien y baja con entregas malas, clientes enojados y jornadas cortadas a mano; puede
/// bajar de nivel. Más reputación = más clientes esperados en la jornada y más plata por corte, y es lo
/// que pide cada mejora de la tienda para poder comprarse.
///
/// Los locales (escenarios) ponen un tope: al llegar al nivel del próximo local la reputación deja de
/// crecer hasta comprarlo (hoy, en la tienda, sin cambio visual).
///
/// Estática, como la racha de <see cref="StrikeSystem"/>: la leen la partida y la tienda, sobrevive
/// los cambios de escena y no necesita nada en la escena. Arranca de cero en cada run
/// (<see cref="ResetRun"/>, desde <see cref="SceneManagementUtils"/>).
/// </summary>
public static class ReputationSystem
{
    private static ReputationConfigSO config;

    public static ReputationConfigSO Config
    {
        get
        {
            if (config == null) config = ReputationConfigSO.LoadOrDefault();
            return config;
        }
    }

    /// <summary>Experiencia juntada en la run, desde el nivel 0.</summary>
    public static int TotalXp { get; private set; }

    /// <summary>Locales comprados, contando el inicial. Arranca en 1.</summary>
    public static int OwnedVenues { get; private set; } = 1;

    /// <summary>Lo que ganó o perdió la reputación en la jornada actual (o la que acaba de terminar, en la tienda).</summary>
    public static int XpToday { get; private set; }

    /// <summary>Cualquier cambio: experiencia, nivel, local comprado o reinicio. Viene ya aplicado.</summary>
    public static event Action<ReputationChange> OnChanged;

    // ── Nivel ───────────────────────────────────────────────────────────

    public static int MaxLevel => Config.MaxLevel;

    public static int Level => Config.GetLevelForXp(TotalXp);

    public static bool IsMaxLevel => Level >= MaxLevel;

    /// <summary>Experiencia juntada dentro del nivel actual.</summary>
    public static int XpIntoLevel => TotalXp - Config.GetTotalXpForLevel(Level);

    /// <summary>Experiencia que pide el próximo nivel. 0 en el nivel máximo.</summary>
    public static int XpForNextLevel => IsMaxLevel ? 0 : Config.GetXpForLevel(Level + 1);

    /// <summary>Avance hacia el próximo nivel, de 0 a 1. 1 en el nivel máximo.</summary>
    public static float LevelProgress01 =>
        XpForNextLevel > 0 ? Mathf.Clamp01((float)XpIntoLevel / XpForNextLevel) : 1f;

    /// <summary>Reputación continua (nivel + avance dentro del nivel). La usan los multiplicadores.</summary>
    public static float Value => Level + (IsMaxLevel ? 0f : LevelProgress01);

    /// <summary>Nivel más alto alcanzable con los locales comprados.</summary>
    public static int LevelCap
    {
        get
        {
            ReputationConfigSO.Venue next = NextVenue;
            return next != null ? Mathf.Clamp(next.requiredLevel, 0, MaxLevel) : MaxLevel;
        }
    }

    /// <summary>Se llegó al tope del local: la reputación no crece hasta comprar el próximo.</summary>
    public static bool IsBlockedByVenue => !IsMaxLevel && Level >= LevelCap;

    /// <summary>Lo que se gane ahora no entra (nivel máximo o tope del local). Lo que se pierda, sí.</summary>
    public static bool IsCapped => IsMaxLevel || IsBlockedByVenue;

    // ── Efectos ─────────────────────────────────────────────────────────

    /// <summary>Multiplicador de clientes esperados en la jornada (lo resuelve CustomerSystem al abrir).</summary>
    public static float CustomersMultiplier => Config.GetCustomersMultiplier(Value);

    /// <summary>Multiplicador del precio de cada corte entregado (lo aplica GameManager en cada entrega).</summary>
    public static float MoneyMultiplier => Config.GetMoneyMultiplier(Value);

    // ── Locales ─────────────────────────────────────────────────────────

    public static int VenueCount => Config.VenueCount;

    /// <summary>Próximo local a comprar. Null si ya se tienen todos.</summary>
    public static ReputationConfigSO.Venue NextVenue =>
        OwnedVenues < VenueCount ? Config.GetVenue(OwnedVenues) : null;

    /// <summary>Hay un próximo local y la reputación ya alcanza para comprarlo.</summary>
    public static bool IsNextVenueUnlocked
    {
        get
        {
            ReputationConfigSO.Venue next = NextVenue;
            return next != null && Level >= next.requiredLevel;
        }
    }

    // ── Cambios ─────────────────────────────────────────────────────────

    /// <summary>
    /// Suma (o resta) experiencia y avisa. Lo ganado no pasa del tope del local ni del nivel máximo;
    /// lo perdido no baja de 0 y puede bajar de nivel. Devuelve lo que entró de verdad.
    /// </summary>
    public static int AddXp(int amount, ReputationReason reason)
    {
        if (amount == 0) return 0;

        int levelBefore = Level;
        int before = TotalXp;
        int after;

        if (amount > 0)
        {
            int cap = Config.GetTotalXpForLevel(LevelCap);
            after = before >= cap ? before : Mathf.Min(before + amount, cap);
        }
        else
        {
            after = Mathf.Max(0, before + amount);
        }

        TotalXp = after;
        int applied = after - before;
        XpToday += applied;

        var change = new ReputationChange
        {
            reason = reason,
            requested = amount,
            applied = applied,
            levelBefore = levelBefore,
            levelAfter = Level
        };

        Debug.Log(
            "[Reputation] " + FormatSigned(amount) + " (" + reason + ")" +
            (applied != amount ? " | entró " + FormatSigned(applied) : "") +
            " | Nivel " + change.levelAfter + " (" + XpIntoLevel + "/" + XpForNextLevel + ")" +
            (IsBlockedByVenue ? " | tope del local" : "")
        );

        OnChanged?.Invoke(change);
        return applied;
    }

    /// <summary>
    /// Se compró el próximo local: sube el tope de la reputación. El cobro lo hace la tienda
    /// (<see cref="ShopSystem.TryBuyNextVenue"/>). Hoy no cambia nada visual.
    /// </summary>
    public static void RegisterVenuePurchased()
    {
        if (NextVenue == null) return;

        OwnedVenues++;

        Debug.Log("[Reputation] Local comprado (" + OwnedVenues + "/" + VenueCount + "). Nuevo tope: nivel " + LevelCap + ".");

        RaiseUnchanged(ReputationReason.VenuePurchased);
    }

    /// <summary>Abre una jornada: el balance del día vuelve a 0.</summary>
    public static void BeginDay()
    {
        XpToday = 0;
    }

    /// <summary>Run nueva: reputación 0, solo el local inicial.</summary>
    public static void ResetRun()
    {
        TotalXp = 0;
        OwnedVenues = 1;
        XpToday = 0;

        RaiseUnchanged(ReputationReason.RunReset);
    }

    /// <summary>"+20" / "-15" / "0". Con guion común: las fuentes del juego no tienen el signo menos tipográfico.</summary>
    public static string FormatSigned(int value) => value > 0 ? "+" + value : value.ToString();

    /// <summary>Lo que dice la barra: "120/400", o el tope del local / el nivel máximo. Lo usan el HUD y la tienda.</summary>
    public static string FormatProgress()
    {
        if (IsMaxLevel) return Loc.Get("reputation.max");
        if (IsBlockedByVenue) return Loc.Get("reputation.capped");
        return XpIntoLevel + "/" + XpForNextLevel;
    }

    private static void RaiseUnchanged(ReputationReason reason)
    {
        int level = Level;
        OnChanged?.Invoke(new ReputationChange
        {
            reason = reason,
            levelBefore = level,
            levelAfter = level
        });
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        // El Editor puede entrar a Play sin domain reload: sin esto se heredaría la reputación (y los
        // suscriptores ya destruidos) de la sesión anterior. Mismo recurso que StrikeSystem.
        TotalXp = 0;
        OwnedVenues = 1;
        XpToday = 0;
        OnChanged = null;
        config = null;
    }
}
