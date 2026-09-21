using UnityEngine;

/// <summary>Por qué terminó la run. <see cref="None"/> significa que la run sigue.</summary>
public enum RunDefeatReason
{
    None = 0,

    /// <summary>No se pueden alcanzar los mínimos del próximo día con el stock y la plata disponibles.</summary>
    InsufficientResources = 1,

    /// <summary>Se llegó al tope de noches seguidas cerradas por el límite de strikes.</summary>
    StrikeStreak = 2,
}

/// <summary>
/// Foto de la situación económica de la run frente a los mínimos del próximo día.
/// Es un valor puro: describe, no aplica nada.
/// </summary>
public struct RunEconomyStatus
{
    public int day;                          // Día que se intenta comenzar

    public int meatStock, coalStock;
    public int meatRequired, coalRequired;
    public int meatDeficit, coalDeficit;     // max(0, requerido - stock)

    public int coalBagsNeeded;               // ceil(coalDeficit / unitsPerBag)
    public float meatCost, coalCost;         // Costo de completar cada mínimo
    public float totalCost;
    public float money;

    /// <summary>No hay ningún corte desbloqueado a la venta para cubrir el faltante de carne.</summary>
    public bool noCutAvailableToBuy;

    /// <summary>No hay carbón a la venta para cubrir el faltante de carbón.</summary>
    public bool noCoalAvailableToBuy;

    /// <summary>El mínimo de carbón supera el tope de almacenamiento: es inalcanzable por diseño.</summary>
    public bool requirementAboveStorageCap;

    public bool MeetsMinimums => meatDeficit <= 0 && coalDeficit <= 0;

    /// <summary>Faltante que ninguna cantidad de plata puede cubrir.</summary>
    public bool HasBlockingGap
        => (meatDeficit > 0 && noCutAvailableToBuy)
        || (coalDeficit > 0 && noCoalAvailableToBuy)
        || requirementAboveStorageCap;

    // Tolerancia mínima: la plata es float y los precios se suman, no queremos perder
    // una run por un epsilon de redondeo.
    public bool CanAfford => money + 0.001f >= totalCost;

    /// <summary>Regla de supervivencia: o ya cumple los mínimos, o puede comprarlos.</summary>
    public bool CanContinue => MeetsMinimums || (!HasBlockingGap && CanAfford);

    /// <summary>Plata que queda libre después de reservar lo necesario para los mínimos.</summary>
    public float MoneyAfterMinimums => money - totalCost;
}

/// <summary>
/// Reglas de la Derrota Total de la Run (spec puntos 4 a 7). Static y puro, mismo criterio
/// que <see cref="CookingDeliveryEvaluator"/> / <see cref="DishValidator"/>: acá viven las
/// reglas, los efectos quedan afuera.
/// </summary>
public static class RunEconomyEvaluator
{
    /// <summary>
    /// Arma la foto económica del próximo día. Con <paramref name="config"/> nulo devuelve
    /// mínimos en 0, o sea "la run puede seguir": una config sin asignar nunca debe hacer
    /// perder al jugador.
    /// </summary>
    /// <param name="day">Día que se intenta comenzar.</param>
    /// <param name="cheapestCutPrice">Precio del corte desbloqueado más barato a la venta.</param>
    /// <param name="anyCutForSale">Si hay al menos un corte comprable.</param>
    /// <param name="anyCoalForSale">Si hay carbón comprable.</param>
    /// <param name="coalBagPrice">Precio de una bolsa de carbón.</param>
    /// <param name="coalUnitsPerBag">Unidades que aporta una bolsa.</param>
    /// <param name="coalStorageCap">Tope de carbón en el cooler. 0 o menos = sin tope.</param>
    public static RunEconomyStatus Evaluate(
        int day,
        RunDefeatConfigSO config,
        int meatStock,
        int coalStock,
        float money,
        float cheapestCutPrice,
        bool anyCutForSale,
        bool anyCoalForSale,
        float coalBagPrice,
        int coalUnitsPerBag,
        int coalStorageCap)
    {
        RunEconomyStatus status = default;

        status.day = Mathf.Max(1, day);
        status.money = money;
        status.meatStock = Mathf.Max(0, meatStock);
        status.coalStock = Mathf.Max(0, coalStock);

        status.meatRequired = config != null ? config.GetMeatRequirement(status.day) : 0;
        status.coalRequired = config != null ? config.GetCoalRequirement(status.day) : 0;

        status.meatDeficit = Mathf.Max(0, status.meatRequired - status.meatStock);
        status.coalDeficit = Mathf.Max(0, status.coalRequired - status.coalStock);

        int perBag = Mathf.Max(1, coalUnitsPerBag);
        status.coalBagsNeeded = Mathf.CeilToInt((float)status.coalDeficit / perBag);

        status.meatCost = status.meatDeficit * Mathf.Max(0f, cheapestCutPrice);
        status.coalCost = status.coalBagsNeeded * Mathf.Max(0f, coalBagPrice);
        status.totalCost = status.meatCost + status.coalCost;

        status.noCutAvailableToBuy = !anyCutForSale;
        status.noCoalAvailableToBuy = !anyCoalForSale;

        // El cooler capea el carbón (ver CoolerSystem.CoalStorageCap). Pasado ese punto el
        // mínimo no se puede alcanzar ni con plata infinita, así que lo reportamos como
        // derrota explícita en vez de dejar al jugador trabado sin explicación.
        status.requirementAboveStorageCap =
            coalStorageCap > 0 && status.coalRequired > coalStorageCap;

        return status;
    }
}
