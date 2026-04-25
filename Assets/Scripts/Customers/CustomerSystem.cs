using System.Collections.Generic;
using UnityEngine;

public class CustomerSystem : MonoBehaviour
{
    public Customer currentCustomer;

    [Header("Order Cuts")]
    [SerializeField] private List<MeatCutSO> availableOrderCuts = new List<MeatCutSO>();

    [Header("Optional: Catalog-driven order generation")]
    [SerializeField] private FoodAvailabilityService availabilityService;

    private OrderSystem orderSystem;

    void Start()
    {
        List<MeatCutSO> cuts = availableOrderCuts;

        if (availabilityService != null)
        {
            var catalogCuts = availabilityService.GetAvailableCuts();
            if (catalogCuts != null && catalogCuts.Count > 0)
                cuts = new List<MeatCutSO>(catalogCuts);
            else
                Debug.LogWarning("[CustomerSystem] FoodAvailabilityService returned no available cuts. Falling back to availableOrderCuts.");
        }

        orderSystem = new OrderSystem(cuts);
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