using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Qué carteles del tutorial ya aprendió el jugador y si las ayudas están prendidas. Se guarda en
/// init.cfg a través de <see cref="GameSettings"/> (<c>TutorialDone</c> y <c>TutorialHints</c>): lo
/// aprendido no vuelve a aparecer, ni en la noche siguiente ni al abrir el juego de nuevo.
/// </summary>
public static class TutorialProgress
{
    private static readonly HashSet<string> learned = new HashSet<string>();
    private static bool loaded;

    /// <summary>Opción "Ayudas" del menú. Apagadas, los directores retiran todos los carteles y no muestran más.</summary>
    public static bool HintsEnabled => GameSettings.Current.tutorialHints;

    /// <summary>Se olvidó todo lo aprendido (<see cref="Reset"/>).</summary>
    public static event Action OnReset;

    // Con "Enter Play Mode" sin domain reload el estático sobrevive entre sesiones de play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        learned.Clear();
        loaded = false;
        OnReset = null;
    }

    public static bool IsLearned(string hintId)
    {
        EnsureLoaded();
        return !string.IsNullOrEmpty(hintId) && learned.Contains(hintId);
    }

    public static void MarkLearned(string hintId)
    {
        EnsureLoaded();
        if (!string.IsNullOrEmpty(hintId) && learned.Add(hintId))
            GameSettings.SaveLearnedHints(learned);
    }

    /// <summary>Todo el tutorial vuelve a empezar.</summary>
    public static void Reset()
    {
        EnsureLoaded();
        learned.Clear();
        GameSettings.SaveLearnedHints(learned);
        OnReset?.Invoke();
    }

    private static void EnsureLoaded()
    {
        if (loaded)
            return;

        loaded = true;
        learned.Clear();
        foreach (string hintId in GameSettings.LearnedHints)
            learned.Add(hintId);
    }
}
