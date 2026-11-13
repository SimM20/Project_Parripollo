using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ruta lineal de paradas de un modo de juego. La parada 0 es la inicial (se tiene desde el
/// arranque, su precio no se cobra) y las demás se compran en orden.
///
/// Es la única fuente de qué está desbloqueado: todo lo que figure en alguna parada queda
/// habilitado si esa parada ya se alcanzó y bloqueado si no. Lo que no figura en ninguna parada
/// no se toca (conserva el <c>isUnlocked</c> de su asset).
/// </summary>
[CreateAssetMenu(fileName = "LocationRoute", menuName = "Asado/Progresión/Ruta")]
public class LocationRouteSO : ScriptableObject
{
    [Tooltip("Paradas en orden. La primera es la inicial.")]
    [SerializeField] private List<LocationSO> stops = new List<LocationSO>();

    public int Count => stops.Count;

    public LocationSO GetStop(int index) =>
        index >= 0 && index < stops.Count ? stops[index] : null;

    public int IndexOf(LocationSO location) =>
        location != null ? stops.IndexOf(location) : -1;

    /// <summary>La parada que desbloquea ese corte, o null si ninguna lo menciona.</summary>
    public LocationSO FindStopUnlocking(MeatCutSO cut)
    {
        for (int i = 0; i < stops.Count; i++)
        {
            if (stops[i] != null && stops[i].unlockedCuts.Contains(cut))
                return stops[i];
        }
        return null;
    }

    /// <summary>La parada que desbloquea esa mejora, o null si ninguna la menciona.</summary>
    public LocationSO FindStopUnlocking(UpgradeSO upgrade)
    {
        for (int i = 0; i < stops.Count; i++)
        {
            if (stops[i] != null && stops[i].unlockedUpgrades.Contains(upgrade))
                return stops[i];
        }
        return null;
    }

    /// <summary>
    /// Fondo que corresponde a la parada alcanzada: el suyo o, si no tiene, el de la parada
    /// anterior más cercana que sí tenga (una parada sin fondo propio hereda el paisaje).
    /// Null si ninguna tiene: la escena se queda con su fondo.
    /// </summary>
    public GameObject ResolveBackground(int reachedIndex, bool forShop)
    {
        for (int i = Mathf.Min(reachedIndex, stops.Count - 1); i >= 0; i--)
        {
            if (stops[i] == null)
                continue;

            GameObject prefab = forShop ? stops[i].shopBackgroundPrefab : stops[i].backgroundPrefab;
            if (prefab != null)
                return prefab;
        }
        return null;
    }

    /// <summary>
    /// Deja el <c>isUnlocked</c> de cada corte, variante y mejora de la ruta según la parada
    /// alcanzada. Idempotente: se puede llamar al arrancar cada escena y después de cada compra.
    /// </summary>
    public void ApplyUnlocks(int reachedIndex)
    {
        for (int i = 0; i < stops.Count; i++)
        {
            LocationSO stop = stops[i];
            if (stop == null)
                continue;

            bool reached = i <= reachedIndex;

            for (int j = 0; j < stop.unlockedCuts.Count; j++)
            {
                if (stop.unlockedCuts[j] != null)
                    stop.unlockedCuts[j].isUnlocked = reached;
            }

            for (int j = 0; j < stop.unlockedVariants.Count; j++)
            {
                if (stop.unlockedVariants[j] != null)
                    stop.unlockedVariants[j].isUnlocked = reached;
            }

            for (int j = 0; j < stop.unlockedUpgrades.Count; j++)
            {
                if (stop.unlockedUpgrades[j] != null)
                    stop.unlockedUpgrades[j].isUnlocked = reached;
            }
        }
    }
}
