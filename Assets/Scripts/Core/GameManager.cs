using UnityEngine;

public class GameManager : MonoBehaviour
{
    public CustomerSystem customerSystem;
    public GrillSystem grillSystem;
    public CoolerSystem coolerSystem;
    public ViewManager viewManager;
    public MonoBehaviour meatTransferBuffer;
    public BuildStationSystem buildStationSystem;
    public FoodCatalogSO catalog;
    public FoodAvailabilityService foodAvailabilityService;

    private ViewType lastView;

    void Start()
    {
        lastView = viewManager != null ? viewManager.CurrentView : ViewType.Grill;

        if (grillSystem != null)
            grillSystem.SetMeatVisualsVisible(lastView == ViewType.Grill);
    }

    void Update()
    {
        if (viewManager != null && Input.GetKeyDown(KeyCode.Tab))
        {
            viewManager.Toggle();

            if (coolerSystem != null && viewManager.CurrentView == ViewType.Cooler)
                Debug.Log(coolerSystem.GetDebugStockString());
        }

        ViewType currentView = viewManager != null ? viewManager.CurrentView : ViewType.Grill;

        if (currentView != lastView && grillSystem != null)
            grillSystem.SetMeatVisualsVisible(currentView == ViewType.Grill);

        if (currentView == ViewType.Grill && lastView != ViewType.Grill && meatTransferBuffer != null)
            meatTransferBuffer.SendMessage("MoveToMeatHolder", SendMessageOptions.DontRequireReceiver);

        if (currentView == ViewType.Build && lastView != ViewType.Build && meatTransferBuffer != null)
            meatTransferBuffer.SendMessage("MoveToBuildMeatHolder", SendMessageOptions.DontRequireReceiver);

        lastView = currentView;

        if (currentView == ViewType.Grill && Input.GetKeyDown(KeyCode.Space))
            TryServe();

        if (currentView == ViewType.Build && Input.GetKeyDown(KeyCode.Space))
            TryServeBuild();

        if (currentView == ViewType.Build)
        {
            var customer = customerSystem?.currentCustomer;
            var order = customer?.order;

            // B — set bread for sandwich orders
            if (Input.GetKeyDown(KeyCode.B) && buildStationSystem != null && order != null)
            {
                if (order.IsSandwich && order.bread != null)
                {
                    buildStationSystem.SetBread(order.bread);
                    Debug.Log("[Build] Pan asignado: " + order.bread.breadName);
                }
                else
                    Debug.Log("[Build] El pedido no requiere pan.");
            }

            // S — add first available side
            if (Input.GetKeyDown(KeyCode.S) && buildStationSystem != null && catalog != null)
            {
                var sides = catalog.GetAvailableSides();
                if (sides.Count > 0)
                {
                    buildStationSystem.AddSide(sides[0]);
                    Debug.Log("[Build] Acompañamiento agregado: " + sides[0].sideName);
                }
            }

            // T — add first available topping
            if (Input.GetKeyDown(KeyCode.T) && buildStationSystem != null && catalog != null)
            {
                var toppings = catalog.GetAvailableToppings();
                if (toppings.Count > 0)
                {
                    buildStationSystem.AddTopping(toppings[0]);
                    Debug.Log("[Build] Topping agregado: " + toppings[0].toppingName);
                }
            }

            // M — inform missing cut (prefer FoodAvailabilityService; fall back to CoolerSystem)
            if (Input.GetKeyDown(KeyCode.M) && order?.PrimaryCut != null)
            {
                if (foodAvailabilityService != null)
                    foodAvailabilityService.InformMissingCut(order.PrimaryCut);
                else
                    coolerSystem?.InformMissingCut(order.PrimaryCut);

                Debug.Log("[Build] Corte faltante informado: " + order.PrimaryCut.cutName);
            }
        }
    }

    void TryServe()
    {
        var customer = customerSystem.currentCustomer;

        if (customer == null) return;

        var meat = grillSystem.GetCookedMeat(customer.order.meat);

        if (meat != null)
        {
            Debug.Log("✔ Pedido correcto");

            grillSystem.RemoveMeat(meat);
            customerSystem.SpawnCustomer();
        }
        else
        {
            Debug.Log("❌ Carne incorrecta o no lista");
        }
    }

    void ClearBuildAssembly()
    {
        buildStationSystem.ClearAssembly();
        meatTransferBuffer.SendMessage("ClearBuildMeatHolder", SendMessageOptions.DontRequireReceiver);
    }

    void TryServeBuild()
    {
        if (buildStationSystem == null)
        {
            Debug.Log("[TryServeBuild] BuildStationSystem no asignado en GameManager.");
            return;
        }

        var customer = customerSystem?.currentCustomer;
        if (customer == null) return;

        if (!buildStationSystem.HasAnyCut)
        {
            Debug.Log("❌ Sin corte en el armado. (Recordá agregar corte al plato en Build view)");
            return;
        }

        MeatCutSO assembled = buildStationSystem.AssembledCuts[0];
        if (assembled != customer.order.PrimaryCut)
        {
            Debug.Log("❌ Corte incorrecto. Pedido: " + customer.order.PrimaryCut?.cutName
                      + " | Armado: " + assembled.cutName);
            ClearBuildAssembly();
            return;
        }

        string reason;
        bool valid = customer.order.IsSandwich
            ? buildStationSystem.TryBuildSandwich(out reason) != null
            : buildStationSystem.TryBuildPlatedDish(out reason) != null;

        if (!valid)
        {
            Debug.Log("❌ " + reason);
            ClearBuildAssembly();
            return;
        }

        ClearBuildAssembly();
        customerSystem.SpawnCustomer();
        Debug.Log("✔ Pedido entregado desde Build");
    }
}
