using UnityEngine;

/// <summary>
/// Progreso de la run por la ruta de paradas. Lógica sin estado propio: la parada alcanzada vive
/// en <see cref="CoalConsumptionTracker"/> (el DDOL que ya es la fuente de verdad del progreso de
/// la partida), así que se reinicia sola cuando ese objeto se destruye al reintentar o al volver
/// al menú. La ruta sale de <see cref="FoodCatalogSO.LocationRoute"/>.
///
/// Sin tracker o sin ruta todo se comporta como "parada inicial, nada para comprar".
/// </summary>
public static class LocationProgression
{
    /// <summary>Índice de la parada alcanzada en esta run. 0 = la inicial.</summary>
    public static int CurrentIndex =>
        CoalConsumptionTracker.Instance != null ? CoalConsumptionTracker.Instance.LocationIndex : 0;

    /// <summary>La parada en la que está el puesto, o null sin ruta.</summary>
    public static LocationSO GetCurrent(FoodCatalogSO catalog)
    {
        LocationRouteSO route = RouteOf(catalog);
        return route != null ? route.GetStop(Mathf.Min(CurrentIndex, route.Count - 1)) : null;
    }

    /// <summary>La próxima parada que se puede comprar, o null si no hay ruta o ya se llegó al final.</summary>
    public static LocationSO GetNext(FoodCatalogSO catalog)
    {
        LocationRouteSO route = RouteOf(catalog);
        return route != null ? route.GetStop(CurrentIndex + 1) : null;
    }

    /// <summary>True si esa parada es exactamente la próxima de la ruta: es la única que se vende.</summary>
    public static bool IsNext(FoodCatalogSO catalog, LocationSO location) =>
        location != null && GetNext(catalog) == location;

    /// <summary>True si la run ya pasó por esa parada (o está en ella).</summary>
    public static bool IsReached(FoodCatalogSO catalog, LocationSO location)
    {
        LocationRouteSO route = RouteOf(catalog);
        int index = route != null ? route.IndexOf(location) : -1;
        return index >= 0 && index <= CurrentIndex;
    }

    /// <summary>
    /// Deja los desbloqueos de la ruta al día con la parada alcanzada. Lo llaman el tracker al
    /// nacer y cada escena al arrancar (GameScene antes de sortear pedidos, la tienda antes de
    /// listar): es idempotente, y así ninguna escena depende de cómo quedaron los assets.
    /// </summary>
    public static void ApplyUnlocks(FoodCatalogSO catalog)
    {
        LocationRouteSO route = RouteOf(catalog);
        if (route != null)
            route.ApplyUnlocks(CurrentIndex);
    }

    /// <summary>
    /// Avanza a esa parada si es la próxima y aplica sus desbloqueos. No cobra: el precio lo
    /// maneja la tienda. Devuelve false si no era la próxima.
    /// </summary>
    public static bool TryAdvanceTo(FoodCatalogSO catalog, LocationSO location)
    {
        if (!IsNext(catalog, location) || CoalConsumptionTracker.Instance == null)
            return false;

        CoalConsumptionTracker.Instance.SetLocationIndex(CurrentIndex + 1);
        ApplyUnlocks(catalog);

        Debug.Log("[Progresión] Parada comprada: " + location.itemName +
                  " (" + (CurrentIndex + 1) + "/" + RouteOf(catalog).Count + ")");
        return true;
    }

    private static LocationRouteSO RouteOf(FoodCatalogSO catalog) =>
        catalog != null ? catalog.LocationRoute : null;
}
