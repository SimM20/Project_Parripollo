using UnityEngine;

public class OperationalStateService : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GrillSystem grillSystem;
    [SerializeField] private BuildStationSystem buildStation;
    [SerializeField] private FoodCatalogSO catalog;
    
    private CoolerSystem coolerSystem => CoolerSystem.Instance;

    /// <summary>
    /// Devuelve true si el jugador todavía puede completar alguna venta:
    /// - Hay carbón activo O carbón en stock: puede seguir cocinando.
    /// - No hay carbón pero SÍ hay comida entregable: puede terminar ventas.
    /// - No hay carbón NI comida entregable: fin de la noche.
    /// </summary>
    public bool CanContinueNight()
    {
        bool hasCoal = HasAnyCoal();
        bool hasDeliverable = grillSystem != null && grillSystem.HasDeliverableMeat();
        bool hasCutInBuild = buildStation != null && buildStation.HasAnyCut;

        Debug.Log($"[OperationalState] hasCoal={hasCoal}, hasDeliverable={hasDeliverable}, hasCutInBuild={hasCutInBuild}");

        if (hasCoal) return true;
        if (hasDeliverable) return true;
        if (hasCutInBuild) return true;
        return false;
    }

    public bool HasAnyCoal()
    {
        if (grillSystem != null && grillSystem.HasActiveCoal())
        {
            Debug.Log("[OperationalState] HasAnyCoal: hay carbón activo en grill");
            return true;
        }

        if (coolerSystem == null)
        {
            Debug.LogWarning("[OperationalState] coolerSystem es null");
            return false;
        }

        if (catalog == null)
        {
            Debug.LogWarning("[OperationalState] catalog es igual a null → esto rompe el chequeo de stock");
        }

        int totalCoalInCooler = 0;
        foreach (var entry in coolerSystem.EnumerateStock())
        {
            if (entry.Key is CoalSO)
                totalCoalInCooler += entry.Value;
        }
        Debug.Log($"[OperationalState] Coal total en cooler: {totalCoalInCooler}");
        return totalCoalInCooler > 0;
    }
    /// <summary>Motivo del cierre. Útil para la pantalla de fin de jornada.</summary>
    public NightEndReason GetEndReason()
    {
        if (!HasAnyCoal() && !HasDeliverable())
            return NightEndReason.NoCoalNoDeliverable;

        if (!HasAnyCoal() && HasRawOnly())
            return NightEndReason.NoCoalOnlyRaw;

        return NightEndReason.Normal;
    }

    private bool HasDeliverable()
    {
        if (grillSystem != null && grillSystem.HasDeliverableMeat()) return true;
        if (buildStation != null && buildStation.HasAnyCut) return true;
        return false;
    }

    private bool HasRawOnly()
    {
        return grillSystem != null && grillSystem.HasRawOrCookingMeat();
    }
}

public enum NightEndReason
{
    Normal,                     // fin normal por cantidad de clientes atendidos
    NoCoalNoDeliverable,        // sin carbón y sin comida entregable
    NoCoalOnlyRaw,              // sin carbón, solo carne cruda (imposible cocinar)
    ClosedByStrikes             // reutilizable con el sistema anterior de strikes
}