#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

#if !DISABLESTEAMWORKS
using Steamworks;
using UnityEngine;

/// <summary>
/// Logros y stats contra Steam (ISteamUserStats). Desde el SDK 1.61 Steam sincroniza los stats
/// solo al iniciar: no hace falta <c>RequestCurrentStats</c> ni esperar un callback.
/// Solo se usa con <see cref="SteamBootstrap.IsInitialized"/> en true.
/// </summary>
public class SteamAchievementBackend : IAchievementBackend
{
    public string Name => "Steam";

    public bool IsUnlocked(string achievementId) =>
        SteamUserStats.GetAchievement(achievementId, out bool achieved) && achieved;

    public void Unlock(string achievementId)
    {
        if (!SteamUserStats.SetAchievement(achievementId))
            Debug.LogWarning("[Logros] Steam no conoce el logro '" + achievementId +
                             "'. ¿Está cargado y publicado en Steamworks con ese nombre de API?");
    }

    public int GetStat(string statId) =>
        SteamUserStats.GetStat(statId, out int value) ? value : 0;

    public void SetStat(string statId, int value)
    {
        if (!SteamUserStats.SetStat(statId, value))
            Debug.LogWarning("[Logros] Steam no conoce el stat '" + statId +
                             "' (o no es INT). ¿Está cargado y publicado en Steamworks?");
    }

    public void IndicateProgress(string achievementId, int current, int max)
    {
        if (current > 0 && current < max)
            SteamUserStats.IndicateAchievementProgress(achievementId, (uint)current, (uint)max);
    }

    public void Store() => SteamUserStats.StoreStats();

    public void ResetAll()
    {
        SteamUserStats.ResetAllStats(true);
        SteamUserStats.StoreStats();
        Debug.Log("[Logros] (Steam) Stats y logros de la cuenta activa en cero.");
    }
}
#endif
