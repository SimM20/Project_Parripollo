using System.Collections.Generic;
using UnityEngine;

public class CustomerSystem : MonoBehaviour
{
    public Customer currentCustomer;

    [Header("Order Cuts")]
    [SerializeField] private List<MeatCutSO> availableOrderCuts = new List<MeatCutSO>();

    private OrderSystem orderSystem;

    void Start()
    {
        orderSystem = new OrderSystem(availableOrderCuts);
        SpawnCustomer();
    }

    void Update()
    {
        if (currentCustomer != null)
        {
            currentCustomer.UpdatePatience(Time.deltaTime);

            if (currentCustomer.IsAngry)
            {
                Debug.Log("Cliente se fue enojado");
                SpawnCustomer();
            }
        }
    }

    public void SpawnCustomer()
    {
        currentCustomer = new Customer();

        Order order = orderSystem.GenerateOrder();
        if (order.meat == null)
        {
            Debug.LogWarning("No hay cortes configurados para pedidos en CustomerSystem");
            return;
        }

        currentCustomer.Init(order, 30f);

        Debug.Log("Nuevo cliente pide: " + order.meat.cutName);
    }
}