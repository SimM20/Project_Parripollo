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

    [Header("Requisitos mínimos por día")]
    [Tooltip("Cortes de carne mínimos para arrancar el Día 1.")]
    [Min(0)] public int meatMinimumBase = 5;

    [Tooltip("Cortes adicionales requeridos por cada día que pasa.")]
    [Min(0)] public int meatMinimumIncrementPerDay = 2;

    [Tooltip("Unidades de carbón mínimas para arrancar el Día 1.")]
    [Min(0)] public int coalMinimumBase = 10;

    [Tooltip("Carbón adicional requerido por cada día que pasa.")]
    [Min(0)] public int coalMinimumIncrementPerDay = 1;

    /// <summary>Cortes mínimos requeridos para arrancar el día N.</summary>
    public int GetMeatMinimumForDay(int dayNumber)
    {
        int d = Mathf.Max(1, dayNumber);
        return meatMinimumBase + meatMinimumIncrementPerDay * (d - 1);
    }

    /// <summary>Carbón mínimo requerido para arrancar el día N.</summary>
    public int GetCoalMinimumForDay(int dayNumber)
    {
        int d = Mathf.Max(1, dayNumber);
        return coalMinimumBase + coalMinimumIncrementPerDay * (d - 1);
    }
}