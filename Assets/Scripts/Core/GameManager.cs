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

    // Contexto de descarte de quemados: solo activo tras un intento de entrega bloqueado por quemados.
    private bool discardContextActive;
    private readonly System.Collections.Generic.List<int> discardBurnedIndices = new System.Collections.Generic.List<int>();

    private void Start()
    {
        lastView = viewManager != null ? viewManager.CurrentView : ViewType.Grill;

        if (grillSystem != null)
            grillSystem.SetMeatVisualsVisible(lastView == ViewType.Grill);

        customerSystem.OnNightEnded += EndNight;

        CoalConsumptionTracker tracker = CoalConsumptionTracker.Instance;

        if (tracker == null)
        {
            Debug.LogError(
                "[GameManager] No se encontró CoalConsumptionTracker al comenzar la partida."
            );

            UIManager.Instance?.SetActualDay(1);
        }
        else
        {
            Debug.Log(
                "[GameManager] Comenzando noche " + tracker.CurrentNight +
                " | Noches completadas: " + tracker.DaysPlayed
            );

            UIManager.Instance?.SetActualDay(tracker.CurrentNight);
        }
       
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
            ClearDiscardContext();

            if (customerSystem != null && customerSystem.IsDeliverySelectionActive)
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

            if (Input.GetKeyDown(KeyCode.X))
                TryDiscardBurnedCuts();

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

    private void ClearBuildAssembly()
    {
        buildStationSystem.ClearAssembly();
    }

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

    private void ClearDiscardContext()
    {
        discardContextActive = false;
        discardBurnedIndices.Clear();
    }

    /// <summary>
    /// Descarta los cortes quemados detectados en el último intento bloqueado y revalida la entrega.
    /// Fuera del contexto de una entrega bloqueada por quemados, X no hace nada.
    /// </summary>
    private void TryDiscardBurnedCuts()
    {
        if (!discardContextActive || discardBurnedIndices.Count == 0)
            return;

        // Eliminar de mayor a menor índice para no invalidar los índices restantes.
        discardBurnedIndices.Sort();
        for (int i = discardBurnedIndices.Count - 1; i >= 0; i--)
        {
            int index = discardBurnedIndices[i];
            buildStationSystem.RemoveCutAt(index);
            meatTransferBuffer?.SendMessage("RemovePlateMeatVisualAt", index, SendMessageOptions.DontRequireReceiver);
        }

        int discarded = discardBurnedIndices.Count;
        ClearDiscardContext();
        Debug.Log("[Build] Cortes quemados descartados: " + discarded);

        if (!buildStationSystem.HasAnyCut)
        {
            DeliveryFeedbackText.Instance?.Show("Se descartaron los cortes quemados. No queda nada para entregar.");
            if (customerSystem != null && customerSystem.IsDeliverySelectionActive)
                customerSystem.EndDeliverySelection();
            return;
        }

        // Revalidar el intento: si sigue habiendo crudos, se bloquea de nuevo con el mensaje actualizado.
        if (customerSystem != null && customerSystem.IsDeliverySelectionActive)
            ConfirmDeliverySelection();
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
            ClearDiscardContext();
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
            ClearDiscardContext();
            return;
        }

        // ── Validación de cocción: Crudo/Quemado bloquean la entrega completa (atómica) ──
        // El tutorial exime al chorizo tutorial quemado para evitar un softlock; en GameScene
        // no hay TutorialManager, así que el predicado siempre es false y nada cambia.
        var validation = CookingDeliveryEvaluator.Validate(
            buildStationSystem.AssembledCutSideStates,
            buildStationSystem.AssembledCuts,
            TutorialManager.IsBurnedDeliveryExempt);

        if (validation.IsBlocked)
        {
            string blockedMessage = CookingDeliveryEvaluator.BuildBlockedMessage(validation.rawCount, validation.burnedCount);
            DeliveryFeedbackText.Instance?.Show(blockedMessage);
            Debug.Log("❌ Entrega bloqueada. Crudos: " + validation.rawCount + " | Quemados: " + validation.burnedCount);

            // Habilitar X solo si hay quemados descartables en este intento.
            discardBurnedIndices.Clear();
            if (validation.burnedCount > 0)
            {
                discardBurnedIndices.AddRange(validation.burnedIndices);
                discardContextActive = true;
            }
            else
            {
                discardContextActive = false;
            }

            // La selección de cliente queda activa: tras descartar con X se revalida el mismo intento.
            return;
        }

        // ── Evaluación económica por corte: peor desfase de ambas caras ──
        var cuts = buildStationSystem.AssembledCuts;
        var sideStates = buildStationSystem.AssembledCutSideStates;
        bool isSandwich = customer.order.IsSandwich;

        float totalPayment = 0f;
        float totalTips = 0f;

        for (int i = 0; i < cuts.Count; i++)
        {
            MeatCutSO cut = cuts[i];
            if (cut == null) continue;

            float basePrice = isSandwich ? cut.sellPriceSandwich : cut.sellPricePlate;
            MeatStates requested = customer.order.GetRequestedState(i < customer.order.requestedStates.Count ? i : 0);

            var cutResult = CookingDeliveryEvaluator.EvaluateCut(sideStates[i].sideA, sideStates[i].sideB, requested, basePrice);
            totalPayment += cutResult.price;

            if (cutResult.tipEligible)
                totalTips += CookingDeliveryEvaluator.CalculateTip(basePrice, customer.Patience01);

            Debug.Log("[Entrega] " + cut.cutName + " | Pedido: " + requested
                      + " | A: " + sideStates[i].sideA + " | B: " + sideStates[i].sideB
                      + " | Desfase: " + cutResult.worstOffset
                      + " | Pago: " + cutResult.price + " | Propina: " + (cutResult.tipEligible ? "sí" : "no"));
        }

        ClearBuildAssembly();
        meatTransferBuffer.SendMessage("ClearPlateMeatVisuals", SendMessageOptions.DontRequireReceiver);
        BuildFoodDropZone.ClearActivePlateVisuals();
        ToppingDraggable.ClearAllSplatters();
        PlayerWallet.Instance?.Add(totalPayment + totalTips);
        customerSystem.CompleteCustomer(customer);
        customerSystem.EndDeliverySelection();
        Debug.Log("✔ Pedido entregado desde Build. Pago: " + totalPayment + " | Propinas: " + totalTips);
        AudioManager.Instance.PlayTaskCompleted();
        TutorialManager.NotifyProductDelivered();
    }

    private void EndNight()
    {
        customerSystem.OnNightEnded -= EndNight;

        Debug.Log("[GameManager] Terminando la noche.");

        CoalConsumptionTracker tracker = CoalConsumptionTracker.Instance;

        if (tracker == null)
        {
            tracker = FindFirstObjectByType<CoalConsumptionTracker>();
        }

        if (tracker == null)
        {
            Debug.LogError(
                "[GameManager] No existe ningún CoalConsumptionTracker. " +
                "La noche terminó, pero no se pudo registrar el progreso."
            );
        }
        else
        {
            int nightBefore = tracker.CurrentNight;

            tracker.RegisterDayCompleted();

            Debug.Log(
                "[GameManager] Noche " + nightBefore +
                " completada correctamente. " +
                "Próxima noche: " + tracker.CurrentNight
            );
        }

        SceneManagementUtils.LoadSceneByName("EndScene");
    }

    private void OnDestroy() => customerSystem.OnNightEnded -= EndNight;
}