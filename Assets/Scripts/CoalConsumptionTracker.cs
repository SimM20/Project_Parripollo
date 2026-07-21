using UnityEngine;

public class CoalConsumptionTracker : MonoBehaviour
{
    public static CoalConsumptionTracker Instance { get; private set; }

    [Header("Night Unlocks")]
    [Tooltip("Corte que se desbloquea al comenzar la segunda noche.")]
    [SerializeField] private MeatCutSO nightTwoCut;

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
            // Si el tracker persistente no tenía el corte configurado,
            // toma la referencia configurada en esta copia antes de destruirla.
            if (Instance.nightTwoCut == null && nightTwoCut != null)
            {
                Instance.nightTwoCut = nightTwoCut;

                Debug.Log(
                    "[NightProgression] Se copió el corte de desbloqueo al " +
                    "tracker persistente: " + nightTwoCut.cutName
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
            " | Corte noche 2: " +
            (nightTwoCut != null ? nightTwoCut.cutName : "SIN ASIGNAR")
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
    public void ConfigureNightTwoCut(MeatCutSO cut)
    {
        if (cut == null)
        {
            Debug.LogWarning(
                "[NightProgression] Se intentó configurar el corte de noche 2 con null."
            );
            return;
        }

        nightTwoCut = cut;

        Debug.Log(
            "[NightProgression] Corte de noche 2 configurado: " +
            nightTwoCut.cutName +
            " | Noche actual: " + CurrentNight
        );

        ApplyProgressionUnlocks();
    }

    private void ApplyProgressionUnlocks()
    {
        if (nightTwoCut == null)
        {
            Debug.LogWarning(
                "[NightProgression] No se asignó el corte de la noche 2 " +
                "en CoalConsumptionTracker."
            );
            return;
        }

        bool shouldBeUnlocked = CurrentNight >= 2;

        nightTwoCut.isUnlocked = shouldBeUnlocked;

        Debug.Log(
            "[NightProgression] Noche actual: " + CurrentNight +
            " | Corte: " + nightTwoCut.cutName +
            " | Desbloqueado: " + nightTwoCut.isUnlocked
        );
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