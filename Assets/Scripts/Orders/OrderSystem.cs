using System.Collections.Generic;
using UnityEngine;

public class OrderSystem
{
    private readonly List<WeightedOrderCut> availableCuts;
    private readonly List<ToppingSO> availableToppings;
    private readonly float toppingChance;
    private readonly int maxToppingsPerOrder;

    public OrderSystem(List<WeightedOrderCut> cuts)
        : this(cuts, null, 0f, 0)
    {
    }

    /// <param name="toppings">Toppings que un cliente puede pedir. Null o vacío: nunca pide toppings.</param>
    /// <param name="toppingChance">Probabilidad [0..1] de que un pedido al plato lleve toppings.</param>
    /// <param name="maxToppingsPerOrder">Máximo de toppings distintos por pedido.</param>
    public OrderSystem(
        List<WeightedOrderCut> cuts,
        IReadOnlyList<ToppingSO> toppings,
        float toppingChance,
        int maxToppingsPerOrder)
    {
        availableCuts =
            cuts != null
                ? new List<WeightedOrderCut>(cuts)
                : new List<WeightedOrderCut>();

        availableToppings = new List<ToppingSO>();

        if (toppings != null)
        {
            for (int i = 0; i < toppings.Count; i++)
            {
                if (toppings[i] != null)
                    availableToppings.Add(toppings[i]);
            }
        }

        this.toppingChance = Mathf.Clamp01(toppingChance);
        this.maxToppingsPerOrder = Mathf.Max(0, maxToppingsPerOrder);
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

        // El pan bloquea cualquier topping: solo los pedidos al plato pueden llevarlos.
        if (!order.IsSandwich)
            AddRandomToppings(order);

        return order;
    }

    /// <summary>
    /// Con probabilidad <c>toppingChance</c>, agrega entre 1 y <c>maxToppingsPerOrder</c>
    /// toppings distintos elegidos al azar del pool disponible.
    /// </summary>
    private void AddRandomToppings(Order order)
    {
        if (availableToppings.Count == 0 ||
            maxToppingsPerOrder <= 0 ||
            Random.value >= toppingChance)
        {
            return;
        }

        int maxCount = Mathf.Min(maxToppingsPerOrder, availableToppings.Count);
        int count = Random.Range(1, maxCount + 1);

        // Fisher-Yates parcial sobre una copia: garantiza toppings distintos sin repetir tiradas.
        List<ToppingSO> pool = new List<ToppingSO>(availableToppings);

        for (int i = 0; i < count; i++)
        {
            int j = Random.Range(i, pool.Count);
            ToppingSO picked = pool[j];
            pool[j] = pool[i];
            pool[i] = picked;

            order.toppings.Add(picked);
        }
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

        // Protecci�n contra errores de precisi�n decimal.
        return lastValidCut;
    }
}
