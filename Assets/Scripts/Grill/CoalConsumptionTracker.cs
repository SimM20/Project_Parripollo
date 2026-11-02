using System.Collections.Generic;
using UnityEngine;

public class CoalConsumptionTracker : MonoBehaviour
{
    public static CoalConsumptionTracker Instance { get; private set; }

    [Header("Night Unlocks")]
    [Tooltip("Cortes que se desbloquean a partir de cierta noche.")]
    [SerializeField] private List<NightCutUnlock> cutUnlocks = new List<NightCutUnlock>();

    public int TotalCoalConsumed { get; private set; } = 0;
    public int DaysPlayed { get; private set; } = 0;

    /// <summary>
    /// La primera noche corresponde a DaysPlayed = 0.
    /// </summary>
    public int CurrentNight => DaysPlayed + 1;

    /// <summary>
    /// Promedio de unidades consumidas por día.
    /// </summary>
    public float AverageCoalPerDay =>
        DaysPlayed > 0
            ? (float)TotalCoalConsumed / DaysPlayed
            : 0f;

    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Si el tracker persistente no tenía cortes configurados,
            // toma los configurados en esta copia antes de destruirla.
            if (Instance.cutUnlocks.Count == 0 && cutUnlocks.Count > 0)
            {
                Instance.cutUnlocks = new List<NightCutUnlock>(cutUnlocks);

                Debug.Log(
                    "[NightProgression] Se copiaron los cortes de desbloqueo al " +
                    "tracker persistente: " + UnlockNames(cutUnlocks)
                );

                Instance.ApplyProgressionUnlocks();
            }

            Debug.Log(
                "[NightProgression] Se encontró otro CoalConsumptionTracker. " +
                "Se mantiene el existente. Noche actual: " +
                Instance.CurrentNight
            );

            Destroy(gameObject);
            return;
        }

        Instance = this;

        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        ApplyProgressionUnlocks();

        Debug.Log(
            "[NightProgression] Tracker iniciado. " +
            "Noches completadas: " + DaysPlayed +
            " | Noche actual: " + CurrentNight +
            " | Cortes desbloqueables: " + UnlockNames(cutUnlocks)
        );
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Suma unidades al consumo total de carbón.
    /// </summary>
    public void ReportConsumption(int units)
    {
        if (units <= 0)
            return;

        TotalCoalConsumed += units;
    }

    /// <summary>
    /// Se llama al finalizar una noche.
    /// </summary>
    public void RegisterDayCompleted()
    {
        int completedNight = CurrentNight;

        DaysPlayed++;

        Debug.Log(
            "[NightProgression] Terminó la noche " + completedNight +
            ". Noches completadas: " + DaysPlayed +
            " | Próxima noche: " + CurrentNight
        );

        ApplyProgressionUnlocks();
    }
    public void ConfigureCutUnlocks(IReadOnlyList<NightCutUnlock> unlocks)
    {
        if (unlocks == null || unlocks.Count == 0)
        {
            Debug.LogWarning(
                "[NightProgression] Se intentó configurar los desbloqueos sin cortes."
            );
            return;
        }

        cutUnlocks = new List<NightCutUnlock>(unlocks);

        Debug.Log(
            "[NightProgression] Cortes desbloqueables configurados: " +
            UnlockNames(cutUnlocks) +
            " | Noche actual: " + CurrentNight
        );

        ApplyProgressionUnlocks();
    }

    private void ApplyProgressionUnlocks()
    {
        if (cutUnlocks.Count == 0)
        {
            Debug.LogWarning(
                "[NightProgression] No se asignaron cortes desbloqueables " +
                "en CoalConsumptionTracker."
            );
            return;
        }

        for (int i = 0; i < cutUnlocks.Count; i++)
        {
            NightCutUnlock entry = cutUnlocks[i];

            if (entry != null && entry.cut != null)
                entry.cut.isUnlocked = CurrentNight >= entry.night;
        }

        Debug.Log(
            "[NightProgression] Noche actual: " + CurrentNight +
            " | Cortes: " + UnlockNames(cutUnlocks)
        );
    }

    private static string UnlockNames(List<NightCutUnlock> unlocks)
    {
        var names = new List<string>();

        for (int i = 0; i < unlocks.Count; i++)
        {
            NightCutUnlock entry = unlocks[i];

            if (entry != null && entry.cut != null)
                names.Add(entry.cut.cutName + " (noche " + entry.night + ")");
        }

        return names.Count > 0 ? string.Join(", ", names) : "SIN ASIGNAR";
    }

    /// <summary>
    /// Reinicia el progreso de una partida.
    /// Todavía debe llamarse desde el botón Nueva Partida.
    /// </summary>
    public void ResetProgress()
    {
        TotalCoalConsumed = 0;
        DaysPlayed = 0;

        ApplyProgressionUnlocks();

        Debug.Log(
            "[NightProgression] Progreso reiniciado. " +
            "Noche actual: " + CurrentNight
        );
    }
}