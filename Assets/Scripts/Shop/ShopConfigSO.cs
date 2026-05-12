// ShopConfigSO.cs
using UnityEngine;

[CreateAssetMenu(fileName = "ShopConfig", menuName = "Asado/Shop Config")]
public class ShopConfigSO : ScriptableObject
{
    [Header("Carbón")]
    public CoalSO coal;
    [Min(1)] public int minCoalPurchase = 1;
    [Tooltip("Consumo estimado de unidades de carbón por dia")]
    public int estimatedCoalConsumption = 8;
    [Tooltip("Compra mínima recomendada de bolsas de carbón")]
    public int recommendedCoalBags = 1;

    [Header("Avisos de stock")]
    [Tooltip("Por debajo de esta cantidad, se avisa stock bajo de un corte")]
    public int lowStockThreshold = 2;
}