using System.Collections.Generic;
using UnityEngine;

public class OrderSystem
{
    private readonly List<WeightedOrderCut> availableCuts;

    public OrderSystem(List<WeightedOrderCut> cuts)
    {
        availableCuts =
            cuts != null
                ? new List<WeightedOrderCut>(cuts)
                : new List<WeightedOrderCut>();
    }

    public Order GenerateOrder()
    {
        Order order = new Order();

        MeatCutSO cut = GetRandomMeat();

        if (cut == null)
            return order;

        order.SetSingleCut(
            cut,
            GetRandomRequestedState()
        );

        bool makeSandwich =
            cut.servingMode == ServingMode.SandwichOnly ||
            (
                cut.servingMode == ServingMode.Both &&
                Random.value < 0.5f
            );

        if (makeSandwich && cut.requiredBread != null)
            order.bread = cut.requiredBread;

        return order;
    }

    /// <summary>
    /// Punto solicitable: Jugoso a Pasado.
    /// Nunca Crudo ni Quemado.
    /// </summary>
    private static MeatStates GetRandomRequestedState()
    {
        return (MeatStates)Random.Range(
            (int)MeatStates.Jugoso,
            (int)MeatStates.Pasado + 1
        );
    }

    private MeatCutSO GetRandomMeat()
    {
        if (availableCuts == null ||
            availableCuts.Count == 0)
        {
            Debug.LogWarning(
                "[OrderSystem] No hay cortes disponibles."
            );

            return null;
        }

        float totalWeight = 0f;

        for (int i = 0; i < availableCuts.Count; i++)
        {
            WeightedOrderCut entry = availableCuts[i];

            if (entry == null ||
                entry.cut == null ||
                entry.weight <= 0f)
            {
                continue;
            }

            totalWeight += entry.weight;
        }

        if (totalWeight <= 0f)
        {
            Debug.LogWarning(
                "[OrderSystem] Todos los cortes tienen peso 0."
            );

            return null;
        }

        float randomValue =
            Random.value * totalWeight;

        float accumulatedWeight = 0f;
        MeatCutSO lastValidCut = null;

        for (int i = 0; i < availableCuts.Count; i++)
        {
            WeightedOrderCut entry = availableCuts[i];

            if (entry == null ||
                entry.cut == null ||
                entry.weight <= 0f)
            {
                continue;
            }

            lastValidCut = entry.cut;
            accumulatedWeight += entry.weight;

            if (randomValue <= accumulatedWeight)
                return entry.cut;
        }

        // Protección contra errores de precisión decimal.
        return lastValidCut;
    }
}