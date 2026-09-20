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

    [Header("Delivery Preview Tints")]
    [Tooltip("Tinte de cada corte del plato mientras se arrastra sobre un cliente, según su desfase con el punto pedido.")]
    [SerializeField] private Color previewExactTint = new Color(0.65f, 1f, 0.7f);
    [SerializeField] private Color previewOffByOneTint = new Color(1f, 0.95f, 0.55f);
    [SerializeField] private Color previewOffByTwoTint = new Color(1f, 0.75f, 0.45f);
    [SerializeField] private Color previewBlockedTint = new Color(1f, 0.45f, 0.45f);

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

    /// <summary>
    /// Resultado de evaluar el plato armado contra un cliente. Lo produce
    /// <see cref="EvaluateDelivery"/> sin tocar nada: sirve tanto para el preview de la burbuja
    /// mientras se arrastra el plato como para la entrega real.
    /// </summary>
    public struct DeliveryEvaluation
    {
        public bool accepted;
        /// <summary>Mensaje para el jugador cuando <c>accepted == false</c>.</summary>
        public string rejectReason;
        /// <summary>Versión corta de <c>rejectReason</c> para la burbuja de hover.</summary>
        public string rejectShort;
        /// <summary>Rechazo por Crudo/Quemado; <c>validation</c> trae los índices afectados.</summary>
        public bool cookingBlocked;
        public CookingDeliveryEvaluator.DeliveryValidation validation;

        public float payment;
        public float tip;
        /// <summary>Peor desfase de toda la entrega: cocción de cada corte y extras (toppings/pan).</summary>
        public int worstOffset;
        public CustomerFeedbackState feedbackState;

        /// <summary>
        /// Toppings faltantes/sobrantes y pan de más, medidos en la misma escala de desfase
        /// que la cocción. <c>extrasNote</c> lo resume para el jugador; null si no hay errores.
        /// </summary>
        public CookingDeliveryEvaluator.ExtrasResult extras;
        public string extrasNote;

        /// <summary>
        /// Desfase por corte del plato, alineado con <c>BuildStationSystem.AssembledCuts</c>:
        /// 0 exacto, 1 aceptable, >=2 mitad de precio; <see cref="CutBlocked"/> si ese corte
        /// bloquea (crudo/quemado) o si el corte es el equivocado. Null cuando la evaluación
        /// no llegó a mirar los cortes (cliente inválido, plato vacío, falta pan...).
        /// </summary>
        public int[] cutOffsets;

        public const int CutBlocked = -1;
    }

    /// <summary>
    /// Evalúa la entrega del plato armado al cliente sin efectos secundarios (no muestra
    /// mensajes, no cobra, no limpia el plato). Aplica exactamente las mismas reglas que
    /// <see cref="TryDeliverToCustomer"/>, que es su único consumidor con efectos.
    /// </summary>
    public DeliveryEvaluation EvaluateDelivery(Customer customer)
    {
        var result = new DeliveryEvaluation();

        if (customer == null || customer.IsInFeedback)
        {
            result.rejectReason = "No hay un cliente válido seleccionado.";
            result.rejectShort = "Cliente no disponible";
            return result;
        }

        if (buildStationSystem == null || !buildStationSystem.HasAnyCut)
        {
            result.rejectReason = "No hay nada preparado para entregar.";
            result.rejectShort = "Plato vacío";
            return result;
        }

        MeatCutSO assembled = buildStationSystem.AssembledCuts[0];

        if (assembled != customer.order.PrimaryCut)
        {
            result.rejectReason = "Corte incorrecto. El cliente pidió: "
                + (customer.order.PrimaryCut != null ? customer.order.PrimaryCut.cutName : "otro corte") + ".";
            result.rejectShort = "Corte incorrecto";

            // Todos los cortes en rojo: ninguno le sirve a este cliente.
            result.cutOffsets = new int[buildStationSystem.AssembledCuts.Count];
            for (int i = 0; i < result.cutOffsets.Length; i++)
                result.cutOffsets[i] = DeliveryEvaluation.CutBlocked;
            return result;
        }

        string reason;

        bool valid = customer.order.IsSandwich
            ? buildStationSystem.TryBuildSandwich(out reason) != null
            : buildStationSystem.TryBuildPlatedDish(out reason) != null;

        if (!valid)
        {
            result.rejectReason = reason;
            result.rejectShort = reason;
            return result;
        }

        // ── Validación de cocción: Crudo/Quemado bloquean la entrega completa (atómica) ──
        // El tutorial exime al chorizo tutorial quemado para evitar un softlock; en GameScene
        // no hay TutorialManager, así que el predicado siempre es false y nada cambia.
        var cuts = buildStationSystem.AssembledCuts;
        var sideStates = buildStationSystem.AssembledCutSideStates;

        result.validation = CookingDeliveryEvaluator.Validate(
            sideStates, cuts, TutorialManager.IsBurnedDeliveryExempt);

        // ── Evaluación económica por corte: peor desfase de ambas caras ──
        // Se calcula aunque la entrega esté bloqueada: el preview del arrastre tiñe cada
        // corte por su desfase, y los que bloquean van en rojo.
        bool isSandwich = customer.order.IsSandwich;
        result.cutOffsets = new int[cuts.Count];

        // Parte del pago que todavía cobra precio completo: es la única que los extras pueden
        // recortar. Lo que ya se recortó por cocción (desfase >= 2) no se vuelve a partir.
        float unreducedPayment = 0f;

        for (int i = 0; i < cuts.Count; i++)
        {
            MeatCutSO cut = cuts[i];
            if (cut == null) continue;

            if (result.validation.rawIndices.Contains(i) || result.validation.burnedIndices.Contains(i))
            {
                result.cutOffsets[i] = DeliveryEvaluation.CutBlocked;
                continue;
            }

            float basePrice = isSandwich ? cut.sellPriceSandwich : cut.sellPricePlate;
            MeatStates requested = customer.order.GetRequestedState(i < customer.order.requestedStates.Count ? i : 0);

            var cutResult = CookingDeliveryEvaluator.EvaluateCut(sideStates[i].sideA, sideStates[i].sideB, requested, basePrice);
            result.cutOffsets[i] = cutResult.worstOffset;
            result.payment += cutResult.price;
            if (cutResult.worstOffset < 2)
                unreducedPayment += cutResult.price;
            if (cutResult.worstOffset > result.worstOffset)
                result.worstOffset = cutResult.worstOffset;
        }

        // ── Extras: toppings faltantes/sobrantes y pan de más, misma escala que la cocción ──
        // Cada error suma 1 de desfase: uno solo cuesta la propina, dos o más parten el precio.
        result.extras = CookingDeliveryEvaluator.EvaluateExtras(
            customer.order.toppings,
            buildStationSystem.AssembledToppings,
            isSandwich,
            buildStationSystem.HasBread);
        result.extrasNote = CookingDeliveryEvaluator.BuildExtrasMessage(result.extras);

        if (result.extras.offset > result.worstOffset)
            result.worstOffset = result.extras.offset;

        if (result.extras.offset >= 2)
            result.payment = (result.payment - unreducedPayment)
                           + CookingDeliveryEvaluator.ApplyReducedPrice(unreducedPayment);

        if (result.validation.IsBlocked)
        {
            result.cookingBlocked = true;
            result.rejectReason = BuildBlockedMessageWithCuts(result.validation, cuts);
            result.rejectShort = result.validation.burnedCount > 0
                ? (result.validation.rawCount > 0 ? "Crudo y quemado" : "Quemado")
                : "Crudo";
            result.payment = 0f;
            result.worstOffset = 0;
            return result;
        }

        // Evaluar propina y estado de satisfacción general según spec doc
        float primaryBasePrice = cuts.Count > 0 && cuts[0] != null
            ? (isSandwich ? cuts[0].sellPriceSandwich : cuts[0].sellPricePlate)
            : result.payment;

        var feedbackEval = CookingDeliveryEvaluator.EvaluateDeliveryFeedback(
            customer, primaryBasePrice, result.worstOffset);

        result.tip = feedbackEval.tipAmount;
        result.feedbackState = feedbackEval.state;
        result.accepted = true;
        return result;
    }

    /// <summary>
    /// Tiñe los cortes del plato según <c>eval.cutOffsets</c> mientras el plato está sobre
    /// un cliente. Sin offsets (rechazo que no mira los cortes) no tiñe nada.
    /// </summary>
    public void ShowDeliveryPreviewOnPlate(DeliveryEvaluation eval)
    {
        if (eval.cutOffsets == null || eval.cutOffsets.Length == 0)
        {
            ClearDeliveryPreviewOnPlate();
            return;
        }

        var tints = new System.Collections.Generic.List<Color>(eval.cutOffsets.Length);
        for (int i = 0; i < eval.cutOffsets.Length; i++)
        {
            int offset = eval.cutOffsets[i];
            Color tint = offset == DeliveryEvaluation.CutBlocked ? previewBlockedTint
                       : offset == 0 ? previewExactTint
                       : offset == 1 ? previewOffByOneTint
                       : previewOffByTwoTint;
            tints.Add(tint);
        }

        meatTransferBuffer?.SendMessage("SetPlateMeatTints", tints, SendMessageOptions.DontRequireReceiver);
    }

    public void ClearDeliveryPreviewOnPlate()
    {
        meatTransferBuffer?.SendMessage("ClearPlateMeatTints", SendMessageOptions.DontRequireReceiver);
    }

    /// <summary>
    /// Mensaje de bloqueo por cocción con el nombre de los cortes afectados, para que el
    /// jugador sepa cuál es sin adivinar (el plato además los resalta en rojo). Si hay
    /// quemados, indica la tecla configurada para limpiar el plato: es la única salida.
    /// </summary>
    private string BuildBlockedMessageWithCuts(
        CookingDeliveryEvaluator.DeliveryValidation validation,
        System.Collections.Generic.IReadOnlyList<MeatCutSO> cuts)
    {
        string message = CookingDeliveryEvaluator.BuildBlockedMessage(validation.rawCount, validation.burnedCount);

        var sb = new System.Text.StringBuilder();
        AppendCutNames(sb, "Crudo", validation.rawIndices, cuts);
        AppendCutNames(sb, "Quemado", validation.burnedIndices, cuts);

        if (sb.Length > 0)
            message = sb.ToString() + "\n" + message;

        if (validation.burnedCount > 0)
            message += "\nApretá " + clearPlateKey + " para limpiar el plato.";

        return message;
    }

    private static void AppendCutNames(
        System.Text.StringBuilder sb,
        string label,
        System.Collections.Generic.List<int> indices,
        System.Collections.Generic.IReadOnlyList<MeatCutSO> cuts)
    {
        if (indices == null || cuts == null) return;

        for (int i = 0; i < indices.Count; i++)
        {
            int index = indices[i];
            if (index < 0 || index >= cuts.Count || cuts[index] == null) continue;

            if (sb.Length > 0) sb.Append("  |  ");
            sb.Append(cuts[index].cutName).Append(": ").Append(label);
        }
    }

    /// <summary>
    /// Entrega el plato armado al cliente indicado. Es el único punto donde vive la lógica
    /// de entrega; la única entrada es el arrastre del plato con el mouse (PlateDeliveryDraggable).
    /// Devuelve true solo si la entrega se concretó y el plato quedó consumido; en cualquier
    /// rechazo devuelve false (el que arrastra usa eso para devolver el plato a su sitio).
    /// </summary>
    public bool TryDeliverToCustomer(Customer customer)
    {
        DeliveryEvaluation eval = EvaluateDelivery(customer);

        if (!eval.accepted)
        {
            DeliveryFeedbackText.Instance?.Show(eval.rejectReason);
            Debug.Log("❌ " + eval.rejectReason.Replace('\n', ' '));

            if (eval.cookingBlocked)
            {
                // Resaltar en el plato los cortes que bloquean, crudos y quemados por igual.
                var blockedIndices = new System.Collections.Generic.List<int>();
                if (eval.validation.rawIndices != null) blockedIndices.AddRange(eval.validation.rawIndices);
                if (eval.validation.burnedIndices != null) blockedIndices.AddRange(eval.validation.burnedIndices);
                meatTransferBuffer?.SendMessage("FlashPlateMeatVisuals", blockedIndices, SendMessageOptions.DontRequireReceiver);
            }

            return false;
        }

        // La entrega se acepta igual, pero el jugador tiene que saber por qué cobró menos.
        if (!string.IsNullOrEmpty(eval.extrasNote))
            DeliveryFeedbackText.Instance?.Show(eval.extrasNote);

        ClearBuildAssembly();
        meatTransferBuffer.SendMessage("ClearPlateMeatVisuals", SendMessageOptions.DontRequireReceiver);
        BuildFoodDropZone.ClearActivePlateVisuals();
        ToppingDraggable.ClearAllSplatters();

        // El popup se crea ANTES de sumar: así HudManager sabe que hay plata "en vuelo" y
        // recién actualiza el contador cuando aterriza.
        CustomerView paidView = customerSystem.GetViewForCustomer(customer);
        if (paidView != null)
            MoneyPopup.Spawn(paidView.transform.position, eval.payment + eval.tip);

        PlayerWallet.Instance?.Add(eval.payment + eval.tip);

        // Iniciar feedback de entrega (4 segundos con slot ocupado)
        customerSystem.TriggerDeliveryFeedback(customer, eval.payment, eval.tip, eval.feedbackState);

        Debug.Log("✔ Pedido entregado. Pago: " + eval.payment + " | Propinas: " + eval.tip
                  + " | Desfase: " + eval.worstOffset + " | Estado: " + eval.feedbackState
                  + (eval.extrasNote != null ? " | Extras: " + eval.extrasNote : ""));
        TutorialManager.NotifyProductDelivered();
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
