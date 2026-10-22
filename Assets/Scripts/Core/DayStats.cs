using UnityEngine;

/// <summary>
/// Contadores de la jornada que tienen que sobrevivir al cambio de escena
/// (GameScene -> EndScene, donde ya no existe el <see cref="UIManager"/>).
/// Los escribe <see cref="CustomerSystem"/> / <see cref="GameManager"/> y los lee la pantalla de resumen.
/// </summary>
public static class DayStats
{
    /// <summary>Clientes que aparecieron en la jornada. Arranca en 0 y solo sube.</summary>
    public static int CustomersToday { get; private set; }

    /// <summary>Lo cobrado por la comida en la jornada, sin propinas.</summary>
    public static float SalesToday { get; private set; }

    /// <summary>Propinas cobradas en la jornada.</summary>
    public static float TipsToday { get; private set; }

    /// <summary>Entregas perfectas seguidas en curso. La usa la propina (bonus de racha).</summary>
    public static int PerfectStreak { get; private set; }

    /// <summary>Racha más larga de la jornada, para el resumen.</summary>
    public static int BestStreak { get; private set; }

    /// <summary>Arranca una jornada nueva: todo vuelve a cero.</summary>
    public static void ResetDay()
    {
        CustomersToday = 0;
        SalesToday = 0f;
        TipsToday = 0f;
        PerfectStreak = 0;
        BestStreak = 0;
    }

    /// <summary>Guarda cuántos clientes aparecieron hasta ahora en la jornada.</summary>
    public static void SetCustomersToday(int customers)
    {
        CustomersToday = Mathf.Max(0, customers);
    }

    /// <summary>Registra un cobro. Una entrega perfecta estira la racha; cualquier otra la corta.</summary>
    public static void RegisterDelivery(float payment, float tip, bool perfect)
    {
        SalesToday += Mathf.Max(0f, payment);
        TipsToday += Mathf.Max(0f, tip);

        if (!perfect)
        {
            BreakStreak();
            return;
        }

        PerfectStreak++;
        if (PerfectStreak > BestStreak) BestStreak = PerfectStreak;
    }

    /// <summary>Un cliente que se va enojado o un plato crudo/quemado cortan la racha.</summary>
    public static void BreakStreak() => PerfectStreak = 0;
}
