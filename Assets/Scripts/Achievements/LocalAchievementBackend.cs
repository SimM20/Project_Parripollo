using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Backend sin Steam: el editor sin Steam abierto o una build con DISABLESTEAMWORKS. Guarda en
/// memoria y avisa por consola, así se pueden probar los logros jugando en el editor. No
/// persiste nada: cada sesión de juego arranca de cero.
/// </summary>
public class LocalAchievementBackend : IAchievementBackend
{
    private readonly HashSet<string> unlocked = new HashSet<string>();
    private readonly Dictionary<string, int> stats = new Dictionary<string, int>();

    public string Name => "local";

    public bool IsUnlocked(string achievementId) => unlocked.Contains(achievementId);

    public void Unlock(string achievementId) => unlocked.Add(achievementId); // el log lo hace AchievementService

    public int GetStat(string statId) => stats.TryGetValue(statId, out int value) ? value : 0;

    public void SetStat(string statId, int value) => stats[statId] = value;

    public void IndicateProgress(string achievementId, int current, int max) { }

    public void Store() { }

    public void ResetAll()
    {
        Clear();
        Debug.Log("[Logros] (local) Logros y stats en cero.");
    }

    /// <summary>Lo mismo que <see cref="ResetAll"/> pero sin log: para el arranque de cada sesión de Play.</summary>
    public void Clear()
    {
        unlocked.Clear();
        stats.Clear();
    }
}
