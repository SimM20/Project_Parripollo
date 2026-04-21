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
        order.meat = GetRandomMeat();
        return order;
    }

    MeatCutSO GetRandomMeat()
    {
        if (availableCuts == null || availableCuts.Count == 0)
            return null;

        return availableCuts[Random.Range(0, availableCuts.Count)];
    }
}