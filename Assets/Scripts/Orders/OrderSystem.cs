using System.Collections.Generic;
using UnityEngine;

public class OrderSystem
{
    private readonly List<WeightedOrderCut> availableCuts;
    private readonly List<ToppingSO> availableToppings;
    private readonly float toppingChance;
    private readonly int maxToppingsPerOrder;
    private readonly List<SideSO> availableSides;
    private readonly float sideChance;
    private readonly int maxSidesPerOrder;

    public OrderSystem(List<WeightedOrderCut> cuts)
        : this(cuts, null, 0f, 0)
    {
    }

    public OrderSystem(
        List<WeightedOrderCut> cuts,
        IReadOnlyList<ToppingSO> toppings,
        float toppingChance,
        int maxToppingsPerOrder)
        : this(cuts, toppings, toppingChance, maxToppingsPerOrder, null, 0f, 0)
    {
    }

    /// <param name="toppings">Toppings (salsas) que un cliente puede pedir. Null o vacío: nunca pide toppings.</param>
    /// <param name="toppingChance">Probabilidad [0..1] de que un pedido al plato lleve toppings.</param>
    /// <param name="maxToppingsPerOrder">Máximo de toppings distintos por pedido.</param>
    /// <param name="sides">Guarniciones que un cliente puede pedir. Null o vacío: nunca pide guarniciones.</param>
    /// <param name="sideChance">Probabilidad [0..1] de que un pedido al plato lleve guarniciones.</param>
    /// <param name="maxSidesPerOrder">Máximo de guarniciones distintas por pedido.</param>
    public OrderSystem(
        List<WeightedOrderCut> cuts,
        IReadOnlyList<ToppingSO> toppings,
        float toppingChance,
        int maxToppingsPerOrder,
        IReadOnlyList<SideSO> sides,
        float sideChance,
        int maxSidesPerOrder)
    {
        availableCuts =
            cuts != null
                ? new List<WeightedOrderCut>(cuts)
                : new List<WeightedOrderCut>();

        availableToppings = CopyNonNull(toppings);
        this.toppingChance = Mathf.Clamp01(toppingChance);
        this.maxToppingsPerOrder = Mathf.Max(0, maxToppingsPerOrder);

        availableSides = CopyNonNull(sides);
        this.sideChance = Mathf.Clamp01(sideChance);
        this.maxSidesPerOrder = Mathf.Max(0, maxSidesPerOrder);
    }

    private static List<T> CopyNonNull<T>(IReadOnlyList<T> source) where T : Object
    {
        var result = new List<T>();

        if (source == null)
            return result;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null)
                result.Add(source[i]);
        }

        return result;
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

        // El pan bloquea salsas y guarniciones: solo los pedidos al plato pueden llevarlas.
        // Cada tipo se tira por separado, así un plato puede salir solo, solo con salsa,
        // solo con guarnición o con las dos.
        if (!order.IsSandwich)
        {
            AddRandomDistinct(availableToppings, toppingChance, maxToppingsPerOrder, order.toppings);
            AddRandomDistinct(availableSides, sideChance, maxSidesPerOrder, order.sides);
        }

        return order;
    }

    /// <summary>
    /// Con probabilidad <c>chance</c>, agrega a <c>target</c> entre 1 y <c>maxCount</c>
    /// elementos distintos elegidos al azar de <c>pool</c>.
    /// </summary>
    private static void AddRandomDistinct<T>(List<T> pool, float chance, int maxCount, List<T> target)
    {
        if (pool.Count == 0 ||
            maxCount <= 0 ||
            Random.value >= chance)
        {
            return;
        }

        int count = Random.Range(1, Mathf.Min(maxCount, pool.Count) + 1);

        // Fisher-Yates parcial sobre una copia: garantiza elementos distintos sin repetir tiradas.
        List<T> shuffled = new List<T>(pool);

        for (int i = 0; i < count; i++)
        {
            int j = Random.Range(i, shuffled.Count);
            T picked = shuffled[j];
            shuffled[j] = shuffled[i];
            shuffled[i] = picked;

            target.Add(picked);
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
