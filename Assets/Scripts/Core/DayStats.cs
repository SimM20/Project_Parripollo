using UnityEngine;

/// <summary>
/// Contadores de la jornada que tienen que sobrevivir al cambio de escena
/// (GameScene -> EndScene, donde ya no existe el <see cref="UIManager"/>).
/// Los escribe <see cref="CustomerSystem"/> y los lee la pantalla de resumen.
/// </summary>
public static class DayStats
{
    /// <summary>Clientes que aparecieron en la jornada. Arranca en 0 y solo sube.</summary>
    public static int CustomersToday { get; private set; }

    /// <summary>Arranca una jornada nueva: el contador vuelve a cero.</summary>
    public static void ResetDay()
    {
        CustomersToday = 0;
    }

    /// <summary>Guarda cuántos clientes aparecieron hasta ahora en la jornada.</summary>
    public static void SetCustomersToday(int customers)
    {
        CustomersToday = Mathf.Max(0, customers);
    }
}
