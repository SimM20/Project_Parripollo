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
    [SerializeField] private ShopSystem shopSystem;
    [SerializeField] private PlayerWallet wallet;
    [SerializeField] private GrillLayerToggle grillLayerToggle;

    private ViewType lastView;

    private void Start()
    {
        lastView = viewManager != null ? viewManager.CurrentView : ViewType.Grill;

        if (grillSystem != null)
            grillSystem.SetMeatVisualsVisible(lastView == ViewType.Grill);

        customerSystem.OnNightEnded += EndNight;

        int currentDay = (CoalConsumptionTracker.Instance?.DaysPlayed ?? 0) + 1;
        UIManager.Instance?.SetActualDay(currentDay);
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

            if (grillLayerToggle != null)
                grillLayerToggle.RefreshVisibility();
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

            if (Input.GetKeyDown(KeyCode.R))
            {
                ClearBuildAssembly();
                meatTransferBuffer?.SendMessage("ClearPlateMeatVisuals", SendMessageOptions.DontRequireReceiver);
                BuildFoodDropZone.ClearActivePlateVisuals();
                ToppingDraggable.ClearAllSplatters();
                Debug.Log("[Build] Plato limpiado.");
            }

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
            PlayerWallet.Instance?.Add(customer.order.PrimaryCut.sellPricePlate);
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
        ToppingDraggable.ClearAllSplatters();
        float sellPrice = customer.order.IsSandwich ? assembled.sellPriceSandwich : assembled.sellPricePlate;
        PlayerWallet.Instance?.Add(sellPrice);
        customerSystem.CompleteCustomer(customer);
        Debug.Log("✔ Pedido entregado desde Build");
        AudioManager.Instance.PlayTaskCompleted();
    }

    private void EndNight()
    {
        customerSystem.OnNightEnded -= EndNight;

        CoalConsumptionTracker.Instance?.RegisterDayCompleted();

        SceneManagementUtils.LoadSceneByName("EndScene");
    }

    private void OnDestroy() => customerSystem.OnNightEnded -= EndNight;
}
