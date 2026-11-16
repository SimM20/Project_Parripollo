#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reglas de los logros. Escucha eventos que el juego ya publica y decide qué se desbloquea;
/// ningún sistema de la partida sabe que existen los logros.
///
/// Fuentes:
/// - <see cref="GameManager.DeliveryCompleted"/>: entregas, racha, propinas, cortes.
/// - <see cref="GameManager.NightFinished"/>: jornadas y strikes.
/// - <see cref="TutorialSignals"/>: carbón en la parrilla, corte quemado, compras en la tienda
///   (es el bus de "cosas que hizo el jugador"; no depende de que el tutorial esté activo).
///
/// Dónde se guardan: Steam si <see cref="SteamBootstrap.IsInitialized"/>, si no un backend local
/// que solo avisa por consola. Se elige en cada llamada, así nada depende del orden de arranque.
/// Los nombres de API y los umbrales están en <see cref="AchievementIds"/>.
/// </summary>
public static class AchievementService
{
    private static readonly LocalAchievementBackend localBackend = new LocalAchievementBackend();
#if !DISABLESTEAMWORKS
    private static readonly SteamAchievementBackend steamBackend = new SteamAchievementBackend();
#endif

    // Logros ya mandados en esta sesión: evita repetir SetAchievement + StoreStats.
    private static readonly HashSet<string> unlockedThisSession = new HashSet<string>();

    // Cortes entregados en la run actual, para "Carta completa". La run se reconoce por su
    // CoalConsumptionTracker (DDOL que se recrea al reintentar o volver al menú).
    private static readonly HashSet<MeatCutSO> cutsDeliveredThisRun = new HashSet<MeatCutSO>();
    private static CoalConsumptionTracker runTracker;

    // Una carne quemada dispara MeatStateChanged otra vez si se da vuelta y se vuelve: se cuenta una sola.
    private static readonly HashSet<int> burnedMeatIds = new HashSet<int>();

    private static bool statsDirty;

    public static IAchievementBackend Backend
    {
        get
        {
#if !DISABLESTEAMWORKS
            if (SteamBootstrap.IsInitialized) return steamBackend;
#endif
            return localBackend;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        // Sin domain reload los estáticos de la sesión anterior siguen acá. Los eventos de los
        // que se cuelga también los limpia su dueño en esta misma fase.
        unlockedThisSession.Clear();
        cutsDeliveredThisRun.Clear();
        burnedMeatIds.Clear();
        runTracker = null;
        statsDirty = false;
        localBackend.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Subscribe()
    {
        GameManager.DeliveryCompleted -= OnDeliveryCompleted;
        GameManager.DeliveryCompleted += OnDeliveryCompleted;

        GameManager.NightFinished -= OnNightFinished;
        GameManager.NightFinished += OnNightFinished;

        TutorialSignals.Raised -= OnSignal;
        TutorialSignals.Raised += OnSignal;

        SceneManagementUtils.OnSceneLoaded -= OnSceneLoaded;
        SceneManagementUtils.OnSceneLoaded += OnSceneLoaded;

        SteamBootstrap.OnBeforeShutdown -= Flush;
        SteamBootstrap.OnBeforeShutdown += Flush;
    }

    // Los logros nunca pueden cortar la partida: un error acá se loguea y el evento sigue su camino
    // (la entrega se cobra igual, la jornada se cierra igual).
    private static void OnDeliveryCompleted(GameManager.DeliveryReport report) => Safe(() => HandleDelivery(report));
    private static void OnNightFinished(GameManager.NightReport night) => Safe(() => HandleNightFinished(night));
    private static void OnSignal(TutorialSignal signal, TutorialSignalArgs args) => Safe(() => HandleSignal(signal, args));
    private static void OnSceneLoaded() => Safe(HandleSceneLoaded);

    private static void Safe(System.Action action)
    {
        try
        {
            action();
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Logros] Error procesando un evento (el juego sigue): " + e);
        }
    }

    // ── API pública ─────────────────────────────────────────────────────

    /// <summary>Desbloquea y manda enseguida (para que el aviso de Steam salga en el momento).</summary>
    public static void Unlock(string achievementId)
    {
        if (string.IsNullOrEmpty(achievementId) || unlockedThisSession.Contains(achievementId))
            return;

        IAchievementBackend backend = Backend;
        unlockedThisSession.Add(achievementId);

        if (backend.IsUnlocked(achievementId))
            return;

        backend.Unlock(achievementId);
        backend.Store();
        statsDirty = false; // StoreStats manda también los stats pendientes
        Debug.Log("[Logros] Desbloqueado (" + backend.Name + "): " + achievementId +
                  " — " + Loc.GetOrDefault("achievement." + achievementId + ".name", achievementId));
    }

    /// <summary>Suma a un stat INT. Se manda a Steam en el próximo <see cref="Flush"/>.</summary>
    public static int AddStat(string statId, int amount)
    {
        if (amount == 0) return Backend.GetStat(statId);

        IAchievementBackend backend = Backend;
        int value = backend.GetStat(statId) + amount;
        backend.SetStat(statId, value);
        statsDirty = true;
        return value;
    }

    /// <summary>Sube un stat INT solo si el valor nuevo es mayor (récords).</summary>
    public static int RaiseStat(string statId, int value)
    {
        IAchievementBackend backend = Backend;
        int current = backend.GetStat(statId);
        if (value <= current) return current;

        backend.SetStat(statId, value);
        statsDirty = true;
        return value;
    }

    /// <summary>Manda los stats pendientes. Lo llaman el fin de jornada, el cambio de escena y el apagado de Steam.</summary>
    public static void Flush()
    {
        if (!statsDirty) return;

        Backend.Store();
        statsDirty = false;
    }

    /// <summary>Solo QA (menú Parripollo/Steam del editor): borra logros y stats de la cuenta activa.</summary>
    public static void ResetAllForQA()
    {
        unlockedThisSession.Clear();
        Backend.ResetAll();
        statsDirty = false;
    }

    // ── Reglas ──────────────────────────────────────────────────────────

    private static void HandleDelivery(GameManager.DeliveryReport report)
    {
        Unlock(AchievementIds.FirstDelivery);

        if (report.SpecialCustomer) Unlock(AchievementIds.SpecialCustomer);

        int deliveries = AddStat(StatIds.Deliveries, 1);
        UnlockAtThreshold(AchievementIds.Deliveries100, deliveries, AchievementThresholds.DeliveriesFew);
        UnlockAtThreshold(AchievementIds.Deliveries1000, deliveries, AchievementThresholds.DeliveriesMany);

        // Racha de entregas perfectas (la misma que paga el bonus de propina).
        RaiseStat(StatIds.BestStreak, report.PerfectStreak);
        if (report.PerfectStreak >= AchievementThresholds.PerfectStreakShort) Unlock(AchievementIds.PerfectStreak5);
        if (report.PerfectStreak >= AchievementThresholds.PerfectStreakLong) Unlock(AchievementIds.PerfectStreak15);

        // Propinas: la plata es float, el stat es en pesos enteros.
        if (report.TipTier == TipTier.Great) Unlock(AchievementIds.GreatTip);

        int tip = Mathf.FloorToInt(Mathf.Max(0f, report.Tip));
        if (tip > 0)
        {
            int tips = AddStat(StatIds.TipsTotal, tip);
            UnlockAtThreshold(AchievementIds.Tips5000, tips, AchievementThresholds.TipsTotal);
        }

        TrackCutForFullMenu(report.Cut);
    }

    private static void HandleNightFinished(GameManager.NightReport night)
    {
        if (!night.EndedEarlyByPlayer)
        {
            Unlock(AchievementIds.FirstNight);
            AddStat(StatIds.NightsCompleted, 1);
        }

        // Noche limpia: jornada completa, con ventas y sin ningún cliente perdido.
        if (!night.Failed && night.Strikes == 0 && night.Sales > 0f)
            Unlock(AchievementIds.CleanNight);

        if (night.NightsPlayedInRun >= AchievementThresholds.NightsInRun)
            Unlock(AchievementIds.Week);

        if (night.EndedByStrikes)
            Unlock(AchievementIds.StrikeLimit);

        // Remontada: venías a una noche de perder la run por el cartel y la cerraste bien.
        int brink = Mathf.Max(1, StrikeSystem.MaxConsecutiveStrikeNights - 1);
        if (!night.Failed && StrikeSystem.StrikeStreakDefeatEnabled && night.PreviousStrikeNightStreak >= brink)
            Unlock(AchievementIds.Comeback);

        Flush();
    }

    private static void HandleSignal(TutorialSignal signal, TutorialSignalArgs args)
    {
        switch (signal)
        {
            case TutorialSignal.CoalPlacedOnGrill:
                Unlock(AchievementIds.FirstFire);
                break;

            case TutorialSignal.MeatStateChanged:
                if (args.Meat != null && args.Meat.state == MeatStates.Quemado && burnedMeatIds.Add(args.Meat.GetInstanceID()))
                {
                    Unlock(AchievementIds.FirstBurn);
                    AddStat(StatIds.CutsBurned, 1);
                }
                break;

            case TutorialSignal.ShopPurchased:
                CheckRouteAndUpgrades();
                break;
        }
    }

    private static void HandleSceneLoaded()
    {
        burnedMeatIds.Clear();
        Flush();
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static void UnlockAtThreshold(string achievementId, int value, int threshold)
    {
        if (value >= threshold)
            Unlock(achievementId);
        else if (!unlockedThisSession.Contains(achievementId) && IsProgressMilestone(value, threshold))
            Backend.IndicateProgress(achievementId, value, threshold);
    }

    // Avisos de progreso al 25, 50 y 75 %: más seguido molesta.
    private static bool IsProgressMilestone(int value, int threshold)
    {
        if (threshold < 4) return false;
        int quarter = threshold / 4;
        return value % quarter == 0;
    }

    private static FoodCatalogSO CurrentCatalog =>
        CoalConsumptionTracker.Instance != null ? CoalConsumptionTracker.Instance.Catalog : null;

    private static void TrackCutForFullMenu(MeatCutSO cut)
    {
        if (cut == null) return;

        if (runTracker != CoalConsumptionTracker.Instance)
        {
            runTracker = CoalConsumptionTracker.Instance;
            cutsDeliveredThisRun.Clear();
        }

        cutsDeliveredThisRun.Add(cut);

        // La carta completa son los cortes que desbloquea la ruta (el de tutorial no cuenta).
        LocationRouteSO route = CurrentCatalog != null ? CurrentCatalog.LocationRoute : null;
        if (route == null) return;

        bool anyCut = false;
        for (int i = 0; i < route.Count; i++)
        {
            LocationSO stop = route.GetStop(i);
            if (stop == null) continue;

            for (int j = 0; j < stop.unlockedCuts.Count; j++)
            {
                MeatCutSO routeCut = stop.unlockedCuts[j];
                if (routeCut == null) continue;

                anyCut = true;
                if (!cutsDeliveredThisRun.Contains(routeCut))
                    return;
            }
        }

        if (anyCut)
            Unlock(AchievementIds.FullMenu);
    }

    private static void CheckRouteAndUpgrades()
    {
        FoodCatalogSO catalog = CurrentCatalog;
        if (catalog == null) return;

        // Paradas: la 0 es la inicial, la primera comprada es la 1.
        LocationRouteSO route = catalog.LocationRoute;
        int stop = LocationProgression.CurrentIndex;
        if (route != null && route.Count > 1)
        {
            if (stop >= 1) Unlock(AchievementIds.FirstStop);
            if (stop >= route.Count - 1) Unlock(AchievementIds.RouteEnd);
        }

        // Mejoras: viven en los assets y se resetean al reintentar (RunStateReset).
        var upgrades = catalog.GetAllUpgrades();
        bool anyBought = false;
        bool allMaxed = upgrades.Count > 0;
        for (int i = 0; i < upgrades.Count; i++)
        {
            UpgradeSO upgrade = upgrades[i];
            if (upgrade == null) continue;

            if (upgrade.CurrentLevel > 0) anyBought = true;
            if (!upgrade.IsMaxed) allMaxed = false;
        }

        if (anyBought) Unlock(AchievementIds.FirstUpgrade);
        if (allMaxed) Unlock(AchievementIds.AllUpgrades);
    }
}
