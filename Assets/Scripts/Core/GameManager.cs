using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private CustomerSystem customerSystem;
    [SerializeField] private GrillSystem grillSystem;
    [SerializeField] private CoolerSystem coolerSystem;
    [SerializeField] private ViewManager viewManager;
    [SerializeField] private MonoBehaviour meatTransferBuffer;
    [SerializeField] private MonoBehaviour coalTransferBuffer;
    [SerializeField] private BuildStationSystem buildStationSystem;
    [SerializeField] private FoodCatalogSO catalog;
    [SerializeField] private FoodAvailabilityService foodAvailabilityService;

    private ViewType lastView;

    private void Start()
    {
        lastView = viewManager != null ? viewManager.CurrentView : ViewType.Grill;

        if (grillSystem != null)
            grillSystem.SetMeatVisualsVisible(lastView == ViewType.Grill);

        customerSystem.OnNightEnded += EndNight;
    }

    private void Update()
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

        if (currentView != lastView)
        {
            bool buildActive = currentView == ViewType.Build;
            BuildFoodDropZone.SetActivePlateVisualsVisible(buildActive);
            meatTransferBuffer?.SendMessage("SetPlateMeatVisualsVisible", buildActive, SendMessageOptions.DontRequireReceiver);
        }

        if (currentView == ViewType.Grill && lastView != ViewType.Grill)
        {
            if (meatTransferBuffer != null)
                meatTransferBuffer.SendMessage("MoveToMeatHolder", SendMessageOptions.DontRequireReceiver);

            if (coalTransferBuffer != null)
                coalTransferBuffer.SendMessage("MoveToCoalHolder", SendMessageOptions.DontRequireReceiver);
        }

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

            // R — clear the plate (reset assembly + plate visuals, holder stays)
            if (Input.GetKeyDown(KeyCode.R))
            {
                ClearBuildAssembly();
                meatTransferBuffer?.SendMessage("ClearPlateMeatVisuals", SendMessageOptions.DontRequireReceiver);
                BuildFoodDropZone.ClearActivePlateVisuals();
                Debug.Log("[Build] Plato limpiado.");
            }

            // M — inform missing cut (prefer FoodAvailabilityService; fall back to CoolerSystem)
            if (Input.GetKeyDown(KeyCode.M) && order?.PrimaryCut != null)
            {
                if (foodAvailabilityService != null)
                    foodAvailabilityService.InformMissingCut(order.PrimaryCut);
                else
                    coolerSystem?.InformMissingItem(order.PrimaryCut);

                Debug.Log("[Build] Corte faltante informado: " + order.PrimaryCut.cutName);
            }
        }
    }

    private void TryServe()
    {
        var customer = customerSystem.currentCustomer;

        if (customer == null) return;

        var meat = grillSystem.GetCookedMeat(customer.order.meat);

        if (meat != null)
        {
            Debug.Log("✔ Pedido correcto");

            grillSystem.RemoveMeat(meat);
            customerSystem.SpawnCustomer();
            customerSystem.CompleteCustomer(customer);
        }
    }

    private void ClearBuildAssembly()
    {
        buildStationSystem.ClearAssembly();
    }

    private void TryServeBuild()
    {
        if (buildStationSystem == null)
        {
            Debug.Log("[TryServeBuild] BuildStationSystem no asignado en GameManager.");
            return;
        }

        var customer = customerSystem?.currentCustomer;
        if (customer == null) return;

        if (!buildStationSystem.HasAnyCut)
            return;

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

        ClearBuildAssembly();
        meatTransferBuffer.SendMessage("ClearPlateMeatVisuals", SendMessageOptions.DontRequireReceiver);
        BuildFoodDropZone.ClearActivePlateVisuals();
        customerSystem.CompleteCustomer(customer);
        Debug.Log("✔ Pedido entregado desde Build");
    }

    private void EndNight()
    {
        customerSystem.OnNightEnded -= EndNight;

        //TODO: pantalla de finalizacion del juego
    }

    private void OnDestroy() => customerSystem.OnNightEnded += EndNight;
}
