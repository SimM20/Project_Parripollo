using System.Collections.Generic;
using UnityEngine;

public class OrderSystem
{
    private readonly List<MeatCutSO> availableCuts;

    public OrderSystem(List<MeatCutSO> cuts)
    {
        availableCuts = cuts;
    }

    public Order GenerateOrder()
    {
        Order order = new Order();
        MeatCutSO cut = GetRandomMeat();
        if (cut == null)
            return order;

        order.SetSingleCut(cut);

        bool makeSandwich = cut.servingMode == ServingMode.SandwichOnly
                         || (cut.servingMode == ServingMode.Both && Random.value < 0.5f);

        if (makeSandwich && cut.requiredBread != null)
            order.bread = cut.requiredBread;

        return order;
    }

    MeatCutSO GetRandomMeat()
    {
        if (availableCuts == null || availableCuts.Count == 0)
            return null;

        return availableCuts[Random.Range(0, availableCuts.Count)];
    }
}