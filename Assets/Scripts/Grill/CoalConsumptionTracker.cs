using System.Collections.Generic;
using UnityEngine;

public class CoalConsumptionTracker : MonoBehaviour
{
    public static CoalConsumptionTracker Instance { get; private set; }

    [Header("Progresión")]
    [Tooltip("Catálogo con la ruta de paradas. Con esto el tracker deja los desbloqueos al día " +
             "apenas nace, antes de que cualquier sistema de la escena los lea.")]
    [SerializeField] private FoodCatalogSO catalog;

    public int TotalCoalConsumed { get; private set; } = 0;
    public int DaysPlayed { get; private set; } = 0;

    /// <summary>
    /// La primera noche corresponde a DaysPlayed = 0.
    /// </summary>
    public int CurrentNight => DaysPlayed + 1;

    /// <summary>
    /// Parada de la ruta alcanzada en esta run (0 = la inicial). Vive acá porque este objeto es
    /// el progreso de la partida: al destruirse (reintentar, volver al menú) la ruta vuelve a 0.
    /// Se lee y se mueve por <see cref="LocationProgression"/>.
    /// </summary>
    public int LocationIndex { get; private set; } = 0;

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

        LocationProgression.ApplyUnlocks(catalog);

        Debug.Log(
            "[NightProgression] Tracker iniciado. " +
            "Noches completadas: " + DaysPlayed +
            " | Noche actual: " + CurrentNight +
            " | Parada: " + LocationIndex
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
    }

    /// <summary>Mueve la parada alcanzada. Solo lo llama <see cref="LocationProgression"/>.</summary>
    public void SetLocationIndex(int index)
    {
        LocationIndex = Mathf.Max(0, index);
    }

    /// <summary>
    /// Reinicia el progreso de una partida.
    /// Todavía debe llamarse desde el botón Nueva Partida.
    /// </summary>
    public void ResetProgress()
    {
        TotalCoalConsumed = 0;
        DaysPlayed = 0;
        LocationIndex = 0;

        LocationProgression.ApplyUnlocks(catalog);

        Debug.Log(
            "[NightProgression] Progreso reiniciado. " +
            "Noche actual: " + CurrentNight
        );
    }
}