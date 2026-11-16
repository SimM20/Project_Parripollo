/// <summary>
/// Nombres de API de los logros y stats. Tienen que coincidir letra por letra con los que se
/// cargan en el panel de Steamworks (Stats &amp; Achievements). Nombre visible y descripción en
/// español e inglés: Resources/Localization/Achievements.csv. Lista completa en STEAMWORKS.md.
/// ⚠️ Un nombre de API publicado no se cambia más: Steam lo usa para saber quién tiene qué.
/// </summary>
public static class AchievementIds
{
    // ── Parrilla y calor ──
    public const string FirstFire = "ACH_FIRST_FIRE";
    public const string FirstBurn = "ACH_FIRST_BURN";

    // ── Entregas, cortes y punto ──
    public const string FirstDelivery = "ACH_FIRST_DELIVERY";
    public const string PerfectStreak5 = "ACH_PERFECT_STREAK_5";
    public const string PerfectStreak15 = "ACH_PERFECT_STREAK_15";
    public const string FullMenu = "ACH_FULL_MENU";
    public const string Deliveries100 = "ACH_DELIVERIES_100";
    public const string Deliveries1000 = "ACH_DELIVERIES_1000";

    // ── Propinas ──
    public const string GreatTip = "ACH_GREAT_TIP";
    public const string Tips5000 = "ACH_TIPS_5000";

    // ── Jornadas ──
    public const string FirstNight = "ACH_FIRST_NIGHT";
    public const string CleanNight = "ACH_CLEAN_NIGHT";
    public const string Week = "ACH_NIGHTS_7";

    // ── Paradas de la ruta ──
    public const string FirstStop = "ACH_FIRST_STOP";
    public const string RouteEnd = "ACH_ROUTE_END";

    // ── Mejoras ──
    public const string FirstUpgrade = "ACH_FIRST_UPGRADE";
    public const string AllUpgrades = "ACH_ALL_UPGRADES";

    // ── Strikes ──
    public const string StrikeLimit = "ACH_STRIKE_LIMIT";
    public const string Comeback = "ACH_COMEBACK";

    // ── Clientes ──
    /// <summary>Entregarle al cliente especial (el enano bostero), que viene a lo sumo una vez por día.</summary>
    public const string SpecialCustomer = "ACH_SPECIAL_CUSTOMER";

    /// <summary>Todos, en el orden en que conviene cargarlos en Steamworks.</summary>
    public static readonly string[] All =
    {
        FirstFire, FirstBurn,
        FirstDelivery, PerfectStreak5, PerfectStreak15, FullMenu, Deliveries100, Deliveries1000,
        GreatTip, Tips5000,
        FirstNight, CleanNight, Week,
        FirstStop, RouteEnd,
        FirstUpgrade, AllUpgrades,
        StrikeLimit, Comeback,
        SpecialCustomer,
    };
}

/// <summary>Stats de Steam (todas INT). Igual que los logros: el nombre de API no se cambia.</summary>
public static class StatIds
{
    /// <summary>Entregas cobradas en total (todas las runs). Progreso de los logros de entregas.</summary>
    public const string Deliveries = "STAT_DELIVERIES";

    /// <summary>Propinas cobradas en total, en pesos del juego. Progreso de <see cref="AchievementIds.Tips5000"/>.</summary>
    public const string TipsTotal = "STAT_TIPS_TOTAL";

    /// <summary>Jornadas terminadas (no cortadas a mano desde la pausa).</summary>
    public const string NightsCompleted = "STAT_NIGHTS_COMPLETED";

    /// <summary>Cortes que se quemaron en la parrilla.</summary>
    public const string CutsBurned = "STAT_CUTS_BURNED";

    /// <summary>Mejor racha de entregas perfectas. Solo sube.</summary>
    public const string BestStreak = "STAT_BEST_STREAK";

    public static readonly string[] All = { Deliveries, TipsTotal, NightsCompleted, CutsBurned, BestStreak };
}

/// <summary>Umbrales de los logros con progreso. Si se cambian, cambiarlos también en Steamworks.</summary>
public static class AchievementThresholds
{
    public const int PerfectStreakShort = 5;
    public const int PerfectStreakLong = 15;
    public const int DeliveriesFew = 100;
    public const int DeliveriesMany = 1000;
    public const int TipsTotal = 5000;
    public const int NightsInRun = 7;
}
