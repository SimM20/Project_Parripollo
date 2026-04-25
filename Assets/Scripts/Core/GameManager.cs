using UnityEngine;

public class GameManager : MonoBehaviour
{
    public CustomerSystem customerSystem;
    public GrillSystem grillSystem;
    public CoolerSystem coolerSystem;
    public ViewManager viewManager;
    public MonoBehaviour meatTransferBuffer;
    public BuildStationSystem buildStationSystem;

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

    // NOTE: TryServeBuild requires buildStationSystem.AddCut() to be called from the
    // Build view's MeatHolder placement logic (not yet implemented). Until that wiring
    // exists, HasAnyCut will always be false and this path will not deliver orders.
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
            return;
        }

        string reason;
        bool valid = customer.order.IsSandwich
            ? buildStationSystem.TryBuildSandwich(out reason) != null
            : buildStationSystem.TryBuildPlatedDish(out reason) != null;

        if (!valid)
        {
            Debug.Log("❌ " + reason);
            return;
        }

        buildStationSystem.ClearAssembly();
        customerSystem.SpawnCustomer();
        Debug.Log("✔ Pedido entregado desde Build");
    }
}
