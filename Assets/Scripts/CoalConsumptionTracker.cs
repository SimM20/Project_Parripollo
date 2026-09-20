using UnityEngine;

public class CoalConsumptionTracker : MonoBehaviour
{
    public static CoalConsumptionTracker Instance { get; private set; }

    [Header("Progresión")]
    [Tooltip("Calendario de desbloqueos por día y curva de duración de la jornada.")]
    [SerializeField] private ProgressionConfigSO progression;

    [Tooltip("Catálogo sobre el que se escriben los desbloqueos de cortes y variantes.")]
    [SerializeField] private FoodCatalogSO catalog;

    public ProgressionConfigSO Progression => progression;

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

    
    /// <summary>
    /// Segundos que dura la ventana de entrada de clientes del día actual, según la curva
    /// de progresión. Sin configuración devuelve 0 y el reloj usa su propio valor de respaldo.
    /// </summary>
    public float CurrentWindowSeconds =>
        progression != null ? progression.GetWindowSeconds(CurrentNight) : 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // El tracker persistente puede venir de una escena anterior sin las referencias
            // de progresión: se las toma a esta copia antes de destruirla.
            if (Instance.CopyProgressionFrom(this))
                Instance.ApplyProgressionUnlocks();

            Debug.Log(
                "[NightProgression] Se encontró otro CoalConsumptionTracker. " +
                "Se mantiene el existente. Día actual: " +
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
            "Días completados: " + DaysPlayed +
            " | Día actual: " + CurrentNight +
            " | Progresión: " + (progression != null ? progression.name : "SIN ASIGNAR") +
            " | Catálogo: " + (catalog != null ? catalog.name : "SIN ASIGNAR")
        );
    }

    /// <summary>Copia las referencias que falten desde otra copia. True si cambió algo.</summary>
    private bool CopyProgressionFrom(CoalConsumptionTracker other)
    {
        bool changed = false;

        if (progression == null && other.progression != null)
        {
            progression = other.progression;
            changed = true;
        }

        if (catalog == null && other.catalog != null)
        {
            catalog = other.catalog;
            changed = true;
        }

        return changed;
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
    /// Se llama al finalizar una jornada. Sube el contador de días y aplica en el acto los
    /// desbloqueos del día siguiente: la tienda del final del día ya los ve, así que el
    /// jugador puede llegar al día nuevo con stock del corte recién incorporado.
    /// </summary>
    public void RegisterDayCompleted()
    {
        int completedDay = CurrentNight;

        DaysPlayed++;

        Debug.Log(
            "[NightProgression] Terminó el día " + completedDay +
            ". Días completados: " + DaysPlayed +
            " | Próximo día: " + CurrentNight
        );

        ApplyProgressionUnlocks();
    }

    /// <summary>
    /// Inyecta las referencias de progresión si el tracker no las tenía (por ejemplo, desde
    /// una escena que sí las configura) y reaplica los desbloqueos.
    /// </summary>
    public void ConfigureProgression(ProgressionConfigSO newProgression, FoodCatalogSO newCatalog)
    {
        bool changed = false;

        if (progression == null && newProgression != null)
        {
            progression = newProgression;
            changed = true;
        }

        if (catalog == null && newCatalog != null)
        {
            catalog = newCatalog;
            changed = true;
        }

        if (changed)
            ApplyProgressionUnlocks();
    }

    /// <summary>
    /// Reescribe el estado de desbloqueo de todos los cortes y variantes según el día actual.
    /// Es idempotente y no depende de lo que haya quedado guardado en los assets.
    /// </summary>
    private void ApplyProgressionUnlocks()
    {
        if (progression == null || catalog == null)
        {
            Debug.LogWarning(
                "[NightProgression] Falta " +
                (progression == null ? "el ProgressionConfig" : "el FoodCatalog") +
                " en CoalConsumptionTracker: no se aplican los desbloqueos por día."
            );
            return;
        }

        progression.ApplyUnlocks(CurrentNight, catalog);
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
            "Día actual: " + CurrentNight
        );
    }
}