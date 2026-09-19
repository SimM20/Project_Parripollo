using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

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

    [Header("Input")]
    [SerializeField] private KeyCode stockPanelToggleKey = KeyCode.Q;
    [SerializeField] private KeyCode toppingsPanelToggleKey = KeyCode.T;
    [SerializeField] private KeyCode clearPlateKey = KeyCode.C;

    // Contexto de descarte de quemados: solo activo tras un intento de entrega bloqueado por quemados.
    private bool discardContextActive;
    private readonly System.Collections.Generic.List<int> discardBurnedIndices = new System.Collections.Generic.List<int>();

    // Cliente del intento bloqueado: la entrega es siempre por arrastre, así que no hay
    // "cliente seleccionado" contra el que revalidar cuando el jugador aprieta X.
    private Customer discardCustomer;

    public CustomerSystem Customers => customerSystem;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        // Vista única: la parrilla siempre está a la vista y siempre cocinando.
        if (grillSystem != null)
            grillSystem.SetMeatVisualsVisible(true);

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
        // El diálogo de oferta del tutorial tiene su propia pausa: mientras está abierto, ESC no abre el menú.
        if (Input.GetKeyDown(KeyCode.Escape) && !GamePause.IsDialogPaused)
        {
            if (UIManager.Instance != null)
            {
                if (UIManager.Instance.IsPaused)
                    UIManager.Instance.UnPauseGame();
                else
                    UIManager.Instance.PauseGame();
            }
        }

        if (GamePause.IsPaused)
            return;

        // ── Paneles laterales ──
        if (Input.GetKeyDown(stockPanelToggleKey) && StockPanelController.Instance != null)
        {
            if (StockPanelController.Instance.IsOpen || TutorialManager.CheckStockPanelOpenAllowed())
                StockPanelController.Instance.Toggle();
        }

        if (Input.GetKeyDown(toppingsPanelToggleKey) && ToppingsPanelController.Instance != null)
            ToppingsPanelController.Instance.Toggle();

        // ── Parrilla ──
        if (Input.GetKeyDown(KeyCode.Space))
            TryToggleGrillLayer();

        if (Input.GetKeyDown(KeyCode.R) && TutorialManager.CheckCleanAshesAllowed())
            CleanAshes();

        // ── Armado y entrega (todo dentro de la vista Parrilla) ──
        if (Input.GetKeyDown(KeyCode.X))
            TryDiscardBurnedCuts();

        if (Input.GetKeyDown(clearPlateKey) && TutorialManager.CheckClearBuildPlateAllowed())
        {
            ClearBuildAssembly();
            meatTransferBuffer?.SendMessage("ClearPlateMeatVisuals", SendMessageOptions.DontRequireReceiver);
            BuildFoodDropZone.ClearActivePlateVisuals();
            ToppingDraggable.ClearAllSplatters();
            Debug.Log("[Plato] Plato limpiado.");
        }

        if (Input.GetKeyDown(KeyCode.M))
        {
            Customer targetCustomer = customerSystem?.SelectedCustomer ?? customerSystem?.currentCustomer;
            Order order = targetCustomer?.order;
            if (targetCustomer != null && !targetCustomer.IsInFeedback && order?.PrimaryCut != null)
            {
                MeatCutSO missingCut = order.PrimaryCut;

                if (foodAvailabilityService != null)
                    foodAvailabilityService.InformMissingCut(missingCut);
                else
                    coolerSystem?.InformMissingItem(missingCut);

                Debug.Log("[Plato] Corte faltante informado: " + missingCut.cutName);

                // Buscar un corte sustituto disponible con stock
                MeatCutSO substituteCut = null;
                if (foodAvailabilityService != null)
                {
                    var available = foodAvailabilityService.GetAvailableCuts();
                    for (int i = 0; i < available.Count; i++)
                    {
                        if (available[i] != missingCut)
                        {
                            substituteCut = available[i];
                            break;
                        }
                    }
                }

                if (substituteCut != null)
                {
                    customerSystem.TriggerMissingCutChange(targetCustomer, substituteCut);
                    DeliveryFeedbackText.Instance?.Show($"El cliente aceptó cambiar a {substituteCut.cutName}. Propina anulada.");
                }
                else
                {
                    DeliveryFeedbackText.Instance?.Show("No hay otros cortes disponibles en stock para sustituir.");
                }
            }
        }
    }

    /// <summary>
    /// Cambia la capa de la parrilla (carne ↔ carbón) por teclado.
    /// Espejo exacto del botón de la escena: delega en el mismo GrillLayerToggle.Toggle(),
    /// así que sprite del botón y TutorialManager.NotifyGrillLayerChanged se mantienen sincronizados.
    /// Nunca mientras se arrastra un item: cambiar de capa a mitad de un drag invalidaría
    /// el drop y devolvería la pieza a su origen.
    /// </summary>
    private void TryToggleGrillLayer()
    {
        if (Input.GetMouseButton(0)) return;

        if (grillLayerToggle == null)
        {
            Debug.LogWarning("[GameManager] No hay GrillLayerToggle asignado: no se puede cambiar de capa con la barra espaciadora.");
            return;
        }

        grillLayerToggle.Toggle();
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
            Debug.Log($"[Grill] Se limpiaron {cleanedCount} montones de ceniza.");
    }

    private void ClearDiscardContext()
    {
        discardContextActive = false;
        discardCustomer = null;
        discardBurnedIndices.Clear();
    }

    /// <summary>
    /// Descarta los cortes quemados detectados en el último intento bloqueado y revalida la entrega
    /// contra el mismo cliente. Fuera del contexto de una entrega bloqueada por quemados, X no hace nada.
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
        Customer pendingCustomer = discardCustomer;
        ClearDiscardContext();
        Debug.Log("[Plato] Cortes quemados descartados: " + discarded);

        if (!buildStationSystem.HasAnyCut)
        {
            DeliveryFeedbackText.Instance?.Show("Se descartaron los cortes quemados. No queda nada para entregar.");
            return;
        }

        if (customerSystem != null && customerSystem.IsCustomerActive(pendingCustomer))
            TryDeliverToCustomer(pendingCustomer);
    }

    /// <summary>
    /// Entrega el plato armado al cliente indicado. Es el único punto donde vive la lógica
    /// de entrega; la única entrada es el arrastre del plato con el mouse (PlateDeliveryDraggable).
    /// Devuelve true solo si la entrega se concretó y el plato quedó consumido; en cualquier
    /// rechazo devuelve false (el que arrastra usa eso para devolver el plato a su sitio).
    /// </summary>
    public bool TryDeliverToCustomer(Customer customer)
    {
        if (customer == null || customer.IsInFeedback)
        {
            DeliveryFeedbackText.Instance?.Show("No hay un cliente válido seleccionado.");
            return false;
        }

        if (buildStationSystem == null || !buildStationSystem.HasAnyCut)
        {
            DeliveryFeedbackText.Instance?.Show("No hay nada preparado para entregar.");
            return false;
        }

        MeatCutSO assembled = buildStationSystem.AssembledCuts[0];

        if (assembled != customer.order.PrimaryCut)
        {
            Debug.Log("❌ Corte incorrecto. Pedido: " + customer.order.PrimaryCut?.cutName
                + " | Armado: " + assembled.cutName);

            DeliveryFeedbackText.Instance?.Show("Corte incorrecto. El cliente pidió: "
                + (customer.order.PrimaryCut != null ? customer.order.PrimaryCut.cutName : "otro corte") + ".");

            ClearDiscardContext();
            return false;
        }

        string reason;

        bool valid = customer.order.IsSandwich
            ? buildStationSystem.TryBuildSandwich(out reason) != null
            : buildStationSystem.TryBuildPlatedDish(out reason) != null;

        if (!valid)
        {
            Debug.Log("❌ " + reason);
            DeliveryFeedbackText.Instance?.Show(reason);
            ClearDiscardContext();
            return false;
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
                discardCustomer = customer;
            }
            else
            {
                discardContextActive = false;
                discardCustomer = null;
            }

            // El intento queda pendiente: tras descartar con X se revalida contra el mismo cliente.
            return false;
        }

        // ── Evaluación económica por corte: peor desfase de ambas caras ──
        var cuts = buildStationSystem.AssembledCuts;
        var sideStates = buildStationSystem.AssembledCutSideStates;
        bool isSandwich = customer.order.IsSandwich;

        float totalPayment = 0f;
        int overallWorstOffset = 0;

        for (int i = 0; i < cuts.Count; i++)
        {
            MeatCutSO cut = cuts[i];
            if (cut == null) continue;

            float basePrice = isSandwich ? cut.sellPriceSandwich : cut.sellPricePlate;
            MeatStates requested = customer.order.GetRequestedState(i < customer.order.requestedStates.Count ? i : 0);

            var cutResult = CookingDeliveryEvaluator.EvaluateCut(sideStates[i].sideA, sideStates[i].sideB, requested, basePrice);
            totalPayment += cutResult.price;
            if (cutResult.worstOffset > overallWorstOffset)
                overallWorstOffset = cutResult.worstOffset;

            Debug.Log("[Entrega] " + cut.cutName + " | Pedido: " + requested
                      + " | A: " + sideStates[i].sideA + " | B: " + sideStates[i].sideB
                      + " | Desfase: " + cutResult.worstOffset
                      + " | Pago: " + cutResult.price);
        }

        // Evaluar propina y estado de satisfacción general según spec doc
        float primaryBasePrice = cuts.Count > 0 && cuts[0] != null
            ? (isSandwich ? cuts[0].sellPriceSandwich : cuts[0].sellPricePlate)
            : totalPayment;

        var feedbackEval = CookingDeliveryEvaluator.EvaluateDeliveryFeedback(
            customer,
            primaryBasePrice,
            overallWorstOffset
        );

        float totalTips = feedbackEval.tipAmount;

        ClearBuildAssembly();
        meatTransferBuffer.SendMessage("ClearPlateMeatVisuals", SendMessageOptions.DontRequireReceiver);
        BuildFoodDropZone.ClearActivePlateVisuals();
        ToppingDraggable.ClearAllSplatters();
        PlayerWallet.Instance?.Add(totalPayment + totalTips);

        // Iniciar feedback de entrega (4 segundos con slot ocupado)
        customerSystem.TriggerDeliveryFeedback(customer, totalPayment, totalTips, feedbackEval.state);

        Debug.Log("✔ Pedido entregado. Pago: " + totalPayment + " | Propinas: " + totalTips + " | Estado: " + feedbackEval.state);
        TutorialManager.NotifyProductDelivered();
        ClearDiscardContext();
        return true;
    }

    public void EndNight()
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
