using UnityEngine;

public class CoalConsumptionTracker : MonoBehaviour
{
    public static CoalConsumptionTracker Instance { get; private set; }

    public int TotalCoalConsumed { get; private set; } = 0;
    public int DaysPlayed { get; private set; } = 0;

    /// <summary>
    /// Promedio de unidades consumidas por día.
    /// </summary>
    public float AverageCoalPerDay =>
        DaysPlayed > 0 ? (float)TotalCoalConsumed / DaysPlayed : 0f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Suma al contador del dia</summary>
    public void ReportConsumption(int units)
    {
        if (units <= 0) return;
        TotalCoalConsumed += units;
    }

    /// <summary>Llama al terminar el dia para calcular promedio</summary>
    public void RegisterDayCompleted()
    {
        DaysPlayed++;
    }
}