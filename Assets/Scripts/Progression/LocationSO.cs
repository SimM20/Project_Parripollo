using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Una parada de la ruta: una ubicación que se compra en la tienda y habilita cortes, variantes
/// y mejoras. Es un <see cref="ItemDataSO"/> para pasar por la tarjeta, el carrito y el freno de
/// los mínimos del próximo día igual que cualquier otra compra (<see cref="ItemDataSO.basePrice"/>
/// es el precio de la parada).
///
/// El orden y el modo de juego los define la <see cref="LocationRouteSO"/> que la contiene: la
/// misma parada puede estar en la ruta del modo rápido y en la del modo historia.
/// Hoy comprarla solo desbloquea contenido; el fondo propio de cada parada queda para cuando
/// estén los assets.
/// </summary>
[CreateAssetMenu(fileName = "Location", menuName = "Asado/Progresión/Parada")]
public class LocationSO : ItemDataSO
{
    [Header("Display")]
    public Sprite icon;

    [Header("Qué desbloquea")]
    [Tooltip("Cortes que pasan a venderse en la tienda y a pedirse desde esta parada.")]
    public List<MeatCutSO> unlockedCuts = new List<MeatCutSO>();

    [Tooltip("Variantes (plato o sándwich) que pasan a pedirse desde esta parada. Conviene que " +
             "acompañen a su corte.")]
    public List<ProductVariantSO> unlockedVariants = new List<ProductVariantSO>();

    [Tooltip("Mejoras que pasan a venderse en la tienda desde esta parada.")]
    public List<UpgradeSO> unlockedUpgrades = new List<UpgradeSO>();

    /// <summary>Nombres de lo que desbloquea que el jugador ve en la tienda: cortes y mejoras.</summary>
    public string DescribeUnlocks()
    {
        var names = new List<string>();

        for (int i = 0; i < unlockedCuts.Count; i++)
        {
            if (unlockedCuts[i] != null)
                names.Add(unlockedCuts[i].cutName);
        }

        for (int i = 0; i < unlockedUpgrades.Count; i++)
        {
            if (unlockedUpgrades[i] != null)
                names.Add(unlockedUpgrades[i].DisplayName);
        }

        return string.Join(", ", names);
    }
}
