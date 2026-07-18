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
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (UIManager.Instance != null)
            {
                if (UIManager.Instance.IsPaused)
                    UIManager.Instance.UnPauseGame();
                else
                    UIManager.Instance.PauseGame();
            }
        }

        if (UIManager.Instance != null && UIManager.Instance.IsPaused)
            return;

        if (viewManager != null)
        {
            if (Input.GetKeyDown(KeyCode.Q)) viewManager.Show(ViewType.Cooler);
            if (Input.GetKeyDown(KeyCode.W)) viewManager.Show(ViewType.Grill);
            if (Input.GetKeyDown(KeyCode.E)) viewManager.Show(ViewType.Build);

            if (Input.GetKeyDown(KeyCode.LeftArrow)) viewManager.PreviousView();
            if (Input.GetKeyDown(KeyCode.RightArrow)) viewManager.NextView();
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

        if (currentView == ViewType.Grill)
        {
            if (Input.GetKeyDown(KeyCode.Space))
                TryServe();

            if (Input.GetKeyDown(KeyCode.R))
                CleanAshes();

            if (lastView != ViewType.Grill)
            {
                if (meatTransferBuffer != null)
                    meatTransferBuffer.SendMessage("MoveToMeatHolder", SendMessageOptions.DontRequireReceiver);

                if (coalTransferBuffer != null)
                    coalTransferBuffer.SendMessage("MoveToCoalHolder", SendMessageOptions.DontRequireReceiver);

                if (grillLayerToggle != null)
                    grillLayerToggle.RefreshVisibility();
            }
        }

        if (currentView == ViewType.Build && lastView != ViewType.Build && meatTransferBuffer != null)
            meatTransferBuffer.SendMessage("MoveToBuildMeatHolder", SendMessageOptions.DontRequireReceiver);


        if (currentView != lastView && currentView != ViewType.Build
            && customerSystem != null && customerSystem.IsDeliverySelectionActive)
        {
            customerSystem.EndDeliverySelection();
        }

        lastView = currentView;

        // Build Selected
        if (currentView == ViewType.Build)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (customerSystem != null && customerSystem.IsDeliverySelectionActive)
                    ConfirmDeliverySelection();
                else
                    TryEnterDeliverySelection();
            }

            bool selectingCustomer = customerSystem != null && customerSystem.IsDeliverySelectionActive;

            if (selectingCustomer)
            {
                if (Input.GetKeyDown(KeyCode.A))
                    customerSystem.SelectAdjacentCustomer(-1);

                if (Input.GetKeyDown(KeyCode.D))
                    customerSystem.SelectAdjacentCustomer(1);
            }

            var customer = customerSystem?.currentCustomer;
            var order = customer?.order;

            if (!selectingCustomer && Input.GetKeyDown(KeyCode.R))
            {
                ClearBuildAssembly();
                meatTransferBuffer?.SendMessage("ClearPlateMeatVisuals", SendMessageOptions.DontRequireReceiver);
                BuildFoodDropZone.ClearActivePlateVisuals();
                ToppingDraggable.ClearAllSplatters();
                Debug.Log("[Build] Plato limpiado.");
            }

            if (!selectingCustomer && Input.GetKeyDown(KeyCode.M) && order?.PrimaryCut != null)
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

    private void ClearBuildAssembly() => buildStationSystem.ClearAssembly();

    private void CleanAshes()
    {
        int cleanedCount = 0;

        for (int i = Coal.ActiveCoals.Count - 1; i >= 0; i--)
        {
            Coal coal = Coal.ActiveCoals[i];

            if (coal != null && coal.state == CoalStates.Ceniza)
            {
                coal.ReleaseOccupiedSlots();

                Destroy(coal.gameObject);

                cleanedCount++;
            }
        }

        if (cleanedCount > 0)
        {
            Debug.Log($"[Grill] Se limpiaron {cleanedCount} montones de ceniza.");
        }
    }

    private void TryEnterDeliverySelection()
    {
        if (buildStationSystem == null)
        {
            Debug.Log("[TryEnterDeliverySelection] BuildStationSystem no asignado en GameManager.");
            return;
        }

        if (!buildStationSystem.HasAnyCut)
        {
            DeliveryFeedbackText.Instance?.Show("No hay nada preparado para entregar.");
            return;
        }

        if (customerSystem == null || !customerSystem.BeginDeliverySelection())
            DeliveryFeedbackText.Instance?.Show("No hay clientes esperando.");
    }

    private void ConfirmDeliverySelection()
    {
        var customer = customerSystem.SelectedCustomer;

        if (customer == null)
        {
            DeliveryFeedbackText.Instance?.Show("No hay un cliente seleccionado.");
            customerSystem.EndDeliverySelection();
            return;
        }

        if (buildStationSystem == null || !buildStationSystem.HasAnyCut)
        {
            DeliveryFeedbackText.Instance?.Show("No hay nada preparado para entregar.");
            customerSystem.EndDeliverySelection();
            return;
        }

        MeatCutSO assembled = buildStationSystem.AssembledCuts[0];

        if (assembled != customer.order.PrimaryCut)
        {
            Debug.Log("❌ Corte incorrecto. Pedido: " + customer.order.PrimaryCut?.cutName 
                + " | Armado: " + assembled.cutName);

            DeliveryFeedbackText.Instance?.Show("Corte incorrecto. El cliente pidió: " 
                + (customer.order.PrimaryCut != null ? customer.order.PrimaryCut.cutName : "otro corte") + ".");

            customerSystem.EndDeliverySelection();
            return;
        }

        string reason;

        bool valid = customer.order.IsSandwich
            ? buildStationSystem.TryBuildSandwich(out reason) != null
            : buildStationSystem.TryBuildPlatedDish(out reason) != null;

        if (!valid)
        {
            Debug.Log("❌ " + reason);
            DeliveryFeedbackText.Instance?.Show(reason);
            customerSystem.EndDeliverySelection();
            return;
        }

        ClearBuildAssembly();
        meatTransferBuffer.SendMessage("ClearPlateMeatVisuals", SendMessageOptions.DontRequireReceiver);
        BuildFoodDropZone.ClearActivePlateVisuals();
        ToppingDraggable.ClearAllSplatters();
        float sellPrice = customer.order.IsSandwich ? assembled.sellPriceSandwich : assembled.sellPricePlate;
        PlayerWallet.Instance?.Add(sellPrice);
        customerSystem.CompleteCustomer(customer);
        customerSystem.EndDeliverySelection();
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