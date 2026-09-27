using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Qué carteles del tutorial ya aprendió el jugador y si las ayudas están prendidas.
///
/// Por ahora vive en memoria: sobrevive a los cambios de escena (lo aprendido la primera noche no
/// vuelve a aparecer en la segunda) pero no a cerrar el juego. Guardarlo en init.cfg y prender o
/// apagar las ayudas desde Opciones es la fase 4 de PLAN_TUTORIAL_CONTEXTUAL.md.
/// </summary>
public static class TutorialProgress
{
    private static readonly HashSet<string> learned = new HashSet<string>();

    /// <summary>Con las ayudas apagadas, los directores retiran todos los carteles y no muestran más.</summary>
    public static bool HintsEnabled { get; set; } = true;

    // Con "Enter Play Mode" sin domain reload el estático sobrevive entre sesiones de play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        learned.Clear();
        HintsEnabled = true;
    }

    public static bool IsLearned(string hintId) => !string.IsNullOrEmpty(hintId) && learned.Contains(hintId);

    public static void MarkLearned(string hintId)
    {
        if (!string.IsNullOrEmpty(hintId))
            learned.Add(hintId);
    }

    /// <summary>Todo el tutorial vuelve a empezar.</summary>
    public static void Reset() => learned.Clear();
}
