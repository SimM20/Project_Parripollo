using UnityEngine;

public class OperationalStateService : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GrillSystem grillSystem;
    [SerializeField] private BuildStationSystem buildStation;
    [SerializeField] private CoolerSystem coolerSystem;
    [SerializeField] private FoodCatalogSO catalog;

    /// <summary>
    /// Devuelve true si el jugador todavía puede completar alguna venta:
    /// - Hay carbón activo O carbón en stock: puede seguir cocinando.
    /// - No hay carbón pero SÍ hay comida entregable: puede terminar ventas.
    /// - No hay carbón NI comida entregable: fin de la noche.
    /// </summary>
    public bool CanContinueNight()
    {
        // Si hay carbón activo o en stock, siempre puede continuar
        if (HasAnyCoal()) return true;

        // Sin carbón: solo puede seguir si hay comida entregable o algo armándose
        if (grillSystem != null && grillSystem.HasDeliverableMeat()) return true;
        if (buildStation != null && buildStation.HasAnyCut) return true;

        // Sin carbón ni comida ya cocida: no hay venta posible
        return false;
    }

    /// <summary>Devuelve true si hay carbón activo o en stock.</summary>
    public bool HasAnyCoal()
    {
        if (grillSystem != null && grillSystem.HasActiveCoal()) return true;

        if (coolerSystem != null && catalog != null)
        {
            // Cualquier CoalSO con stock > 0 cuenta
            foreach (var entry in coolerSystem.EnumerateStock())
            {
                if (entry.Key is CoalSO && entry.Value > 0)
                    return true;
            }
        }
        return false;
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