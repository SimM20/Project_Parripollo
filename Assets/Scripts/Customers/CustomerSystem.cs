using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;
using System;


public class CustomerSystem : MonoBehaviour
{
    [Header("Customer Limits")]
    [Min(1)]
    [Tooltip("Base de clientes simultáneos. Las mejoras de la tienda le suman encima.")]
    [SerializeField] private int maxSimultaneousCustomers = 3;

    // La jornada ya no tiene un máximo total de clientes: cuántos pasan depende de cuánto
    // dura la ventana y de qué tan rápido el jugador libera slots.

    // Base del inspector + bonus de las mejoras. Se resuelve una vez en Start.
    private int resolvedMaxSimultaneousCustomers;

    /// <summary>Clientes simultaneos de esta noche, ya con las mejoras compradas aplicadas.</summary>
    public int MaxSimultaneousCustomers =>
        resolvedMaxSimultaneousCustomers > 0
            ? resolvedMaxSimultaneousCustomers
            : Mathf.Max(1, maxSimultaneousCustomers);

    [Header("Spawning")]
    [Tooltip("Separación mínima entre dos entradas consecutivas, en segundos. Es una cola " +
             "global: nunca hay un timer por slot. Con varios slots libres el primero entra " +
             "ya y los demás se espacian por este valor.")]
    [Min(0f)]
    [SerializeField] private float spawnGapSeconds = 6f;

    [Tooltip("Arrancar la jornada sola al cargar la escena.")]
    [SerializeField] private bool autoStartNight = true;

    [Header("Fin de la ventana")]
    [Tooltip("Aviso cuando se agota la ventana y todavía quedan clientes adentro.")]
    [TextArea]
    [SerializeField]
    private string lastCustomersMessage =
        "Ya no va a pasar nadie más por acá. Estos son tus últimos clientes.";

    [Tooltip("Segundos que queda visible el aviso. No pausa ni bloquea nada.")]
    [Min(0.1f)]
    [SerializeField] private float lastCustomersMessageSeconds = 5f;

    /// <summary>Se dispara cuando se fue el último cliente del día. Solo una vez por jornada.</summary>
    public Action OnDayEnded;

    [Header("Slots (optional)")]
    [SerializeField] private List<Transform> slots = new List<Transform>();
    [SerializeField] private Vector3 autoFirstSlotPos = new Vector3(-6f, 3.2f, 0f);
    [SerializeField] private float autoSlotSpacing = 2.6f;
    [SerializeField] private Transform customersParent;

    [System.Serializable]
    public class CustomerPrefabEntry
    {
        public CustomerType type;
        public GameObject prefab;
        [Range(0f, 10f)] public float spawnWeight = 1f;
        [Range(0.2f, 3f)] public float patienceMultiplier = 1f;
    }

    [Header("Customer Prefabs")]
    [SerializeField] private List<CustomerPrefabEntry> customerPrefabs = new List<CustomerPrefabEntry>();

    [Header("Order Cuts")]
    [SerializeField]
    private List<WeightedOrderCut> availableOrderCuts =
    new List<WeightedOrderCut>();
    [SerializeField] private FoodAvailabilityService availabilityService;

    [Header("Order Toppings")]
    [Tooltip("Probabilidad de que un pedido al plato lleve toppings. Los pedidos con pan nunca llevan: el pan bloquea cualquier topping.")]
    [SerializeField] [Range(0f, 1f)] private float toppingOrderChance = 0.4f;
    [Tooltip("Máximo de toppings distintos por pedido. El pool sale de FoodCatalog.availableToppings.")]
    [SerializeField] [Min(1)] private int maxToppingsPerOrder = 2;

    [Header("Progresión")]
    [Tooltip("Calendario de desbloqueos y curva de duración de la jornada. Se le pasa al " +
             "CoalConsumptionTracker, que es el dueño del progreso.")]
    [SerializeField] private ProgressionConfigSO progression;

    [Header("Patience")]
    [SerializeField] private float basePatienceSeconds = 30f;

    [Header("Delivery Feedback")]
    [SerializeField] private CustomerFeedbackConfigSO feedbackConfig;
    
    public FoodCatalogSO Catalog => availabilityService != null ? availabilityService.Catalog : null;

    public Customer currentCustomer;

    public Customer SelectedCustomer { get; private set; }

    public bool IsDeliverySelectionActive { get; private set; }

    private OrderSystem orderSystem;
    private readonly List<Customer> activeCustomers = new List<Customer>();
    public IReadOnlyList<Customer> ActiveCustomers => activeCustomers;
    private CustomerView[] slotViews;
    private CustomerView dragHoverView;

    private int spawnedTonight;
    private int servedToday;
    private bool dayEnded;
    private Coroutine spawnRoutine;

    /// <summary>Momento de la última entrada. Es el único timer: la cola es global.</summary>
    private float lastSpawnTime = float.NegativeInfinity;

    /// <summary>
    /// True si la escena tiene ventana de entrada. Sin <see cref="DayClock"/> (tutorial) no
    /// hay entrada automática de clientes ni final de día automático: todo lo maneja el tutorial.
    /// </summary>
    private bool HasWindow => DayClock.Instance != null;

    /// <summary>
    /// True mientras se permite que entre gente nueva: con ventana, hasta que se agote.
    /// Sin ventana (tutorial) no hay restricción horaria — el tutorial pide sus propios
    /// spawns y nadie los bloquea. El final del día ya está cubierto aparte por
    /// <see cref="HasWindow"/> en <see cref="TryEndDay"/>.
    /// </summary>
    private bool DoorsOpen => !HasWindow || !DayClock.Instance.HasClosed;

    public bool IsReadyForSpawning =>
    orderSystem != null &&
    slotViews != null;

    void Start()
    {
        int currentDay =
            CoalConsumptionTracker.Instance != null
                ? CoalConsumptionTracker.Instance.CurrentNight
                : 1;

        resolvedMaxSimultaneousCustomers =
            CalculateMaxSimultaneousCustomers();

        Debug.Log(
            "[CustomerSystem] Iniciando día " + currentDay +
            " | Simultáneos: " + resolvedMaxSimultaneousCustomers +
            " (base " + Mathf.Max(1, maxSimultaneousCustomers) + ")"
        );

        // Los desbloqueos del día los aplica el tracker; acá solo se le acerca el catálogo
        // por si la escena lo tiene configurado y el tracker persistente no.
        if (CoalConsumptionTracker.Instance != null)
        {
            CoalConsumptionTracker.Instance.ConfigureProgression(
                progression,
                Catalog
            );
        }
        else
        {
            Debug.LogError(
                "[CustomerSystem] No existe CoalConsumptionTracker."
            );
        }

        List<WeightedOrderCut> cuts =
            GetUnlockedOrderCuts();

        if (cuts.Count == 0)
        {
            Debug.LogError(
                "[CustomerSystem] No hay cortes desbloqueados " +
                "con peso mayor que cero."
            );
        }

        for (int i = 0; i < cuts.Count; i++)
        {
            WeightedOrderCut entry = cuts[i];

            Debug.Log(
                "[CustomerSystem] Disponible para pedidos: " +
                entry.cut.cutName +
                " | Peso: " + entry.weight
            );
        }

        IReadOnlyList<ToppingSO> orderToppings =
            Catalog != null ? Catalog.GetAvailableToppings() : null;

        if (orderToppings == null || orderToppings.Count == 0)
        {
            Debug.LogWarning(
                "[CustomerSystem] El catálogo no tiene toppings: " +
                "ningún pedido llevará toppings."
            );
        }

        orderSystem = new OrderSystem(
            cuts,
            orderToppings,
            toppingOrderChance,
            maxToppingsPerOrder
        );

        slotViews = new CustomerView[
            resolvedMaxSimultaneousCustomers
        ];

        UIManager.Instance?.SetServedCustomers(0);
        UIManager.Instance?.SetArrivedCustomers(0);

        if (autoStartNight)
            StartDay();
    }
    /// <summary>
    /// Clientes simultaneos = base del inspector + los que sumaron las mejoras compradas
    /// en la tienda (`UpgradeSO` con `effectType == MaxSimultaneousCustomers`). El nivel de
    /// cada mejora vive en su propio SO, asi que el bonus sobrevive al cambio de escena.
    /// </summary>
    private int CalculateMaxSimultaneousCustomers()
    {
        int baseValue = Mathf.Max(1, maxSimultaneousCustomers);

        FoodCatalogSO catalog = Catalog;

        if (catalog == null)
        {
            Debug.LogWarning(
                "[CustomerSystem] Sin catálogo: no se aplican las mejoras de " +
                "clientes simultáneos."
            );

            return baseValue;
        }

        return baseValue + catalog.GetMaxSimultaneousCustomersBonus();
    }

    void Update()
    {
        // Tick paciencia + expulsión
        for (int i = activeCustomers.Count - 1; i >= 0; i--)
        {
            var c = activeCustomers[i];
            if (c.IsInFeedback) continue;

            c.UpdatePatience(Time.deltaTime);

            if (c.IsAngry)
            {
                TriggerAngryLeaveFeedback(c);
            }
        }
    }

    /// <summary>
    /// Deja la jornada lista y abre la cola de entrada. La ventana de tiempo NO arranca acá:
    /// arranca cuando entra el primer cliente (spec: "el tiempo de jornada comienza
    /// exactamente cuando entra el primer cliente").
    /// Sin <see cref="DayClock"/> en la escena (tutorial) no hay ventana ni entrada automática.
    /// </summary>
    public void StartDay()
    {
        ClearAllCustomers();

        spawnedTonight = 0;
        servedToday = 0;
        dayEnded = false;
        lastSpawnTime = float.NegativeInfinity;

        UIManager.Instance?.SetServedCustomers(0);
        UIManager.Instance?.SetArrivedCustomers(0);

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        DayClock clock = DayClock.Instance;

        if (clock == null)
        {
            Debug.Log(
                "[CustomerSystem] Escena sin DayClock: no hay ventana de entrada. " +
                "Los clientes solo entran por spawns forzados."
            );

            return;
        }

        clock.OnClosingTime -= HandleClosingTime;
        clock.OnClosingTime += HandleClosingTime;

        float window = CoalConsumptionTracker.Instance != null
            ? CoalConsumptionTracker.Instance.CurrentWindowSeconds
            : 0f;

        clock.PrepareDay(window);

        Debug.Log(
            "[CustomerSystem] Jornada lista | Ventana: " + DayClock.FormatSeconds(clock.WindowSeconds) +
            " | Slots: " + resolvedMaxSimultaneousCustomers +
            " | Separación entre entradas: " + spawnGapSeconds + "s"
        );

        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    /// <summary>
    /// Cola global de entrada. No hay intervalo periódico ni un timer por slot: mientras la
    /// ventana esté abierta, entra un cliente en cuanto haya un slot libre, respetando una
    /// separación mínima de <see cref="spawnGapSeconds"/> con la entrada anterior.
    ///
    /// De ahí salen solas las reglas del spec:
    /// · arranque del día con los 3 slots libres → entradas a 0 s, 6 s y 12 s;
    /// · un solo slot libre después de un rato → entra al toque;
    /// · dos o tres slots libres a la vez → el primero al toque, los demás cada 6 s;
    /// · un hueco que se libera durante la espera se suma a ESTA cola, no abre otra.
    /// </summary>
    IEnumerator SpawnLoop()
    {
        while (DoorsOpen)
        {
            // Sin lugar: esperar a que se libere un slot. La ventana sigue corriendo igual.
            if (activeCustomers.Count >= resolvedMaxSimultaneousCustomers ||
                GetNextFreeSlotIndex() < 0)
            {
                yield return null;
                continue;
            }

            float sinceLastSpawn = Time.time - lastSpawnTime;

            if (sinceLastSpawn < spawnGapSeconds)
            {
                yield return new WaitForSeconds(spawnGapSeconds - sinceLastSpawn);

                // Revalidar todo: la ventana pudo cerrar y el slot pudo volver a ocuparse.
                continue;
            }

            SpawnCustomer();
        }

        spawnRoutine = null;

        TryEndDay();
    }

    /// <summary>
    /// Se agotó la ventana: no entra nadie más y se cancela cualquier entrada pendiente.
    /// Si adentro ya no quedaba nadie, el día termina acá; si queda gente, se avisa y se
    /// sigue jugando normalmente hasta que se vaya el último.
    /// </summary>
    private void HandleClosingTime()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        Debug.Log(
            "[CustomerSystem] Se cerró la entrada con " + activeCustomers.Count +
            " cliente(s) adentro."
        );

        // El aviso no pausa ni bloquea nada: es solo texto.
        if (activeCustomers.Count > 0 && !string.IsNullOrEmpty(lastCustomersMessage))
            DeliveryFeedbackText.Instance?.Show(lastCustomersMessage, lastCustomersMessageSeconds);

        TryEndDay();
    }

    /// <summary>
    /// Termina el día cuando ya no puede entrar nadie y se fue el último cliente.
    /// Único lugar que dispara <see cref="OnDayEnded"/>, y lo hace una sola vez.
    /// Sin ventana (tutorial) el día nunca termina solo.
    /// </summary>
    private void TryEndDay()
    {
        if (!HasWindow || dayEnded || activeCustomers.Count > 0 || DoorsOpen)
            return;

        dayEnded = true;

        Debug.Log(
            "[CustomerSystem] Se fue el último cliente: termina el día. " +
            "Atendidos: " + servedToday + " de " + spawnedTonight + " que entraron."
        );

        OnDayEnded?.Invoke();
    }

    public void SpawnCustomer(bool ignoreNightLimit = false)
    {
        if (!IsReadyForSpawning)
        {
            Debug.LogWarning(
                "[CustomerSystem] No se puede generar un cliente todavía: " +
                "CustomerSystem no terminó de inicializarse."
            );

            return;
        }

        // Los spawns normales solo ocurren con la ventana abierta. El tutorial, que no tiene
        // ventana, entra siempre por acá con ignoreNightLimit.
        if (!ignoreNightLimit && !DoorsOpen)
            return;

        int slotIndex = GetNextFreeSlotIndex();

        if (slotIndex < 0)
            return;

        CustomerPrefabEntry entry = PickCustomerEntry();

        if (entry == null || entry.prefab == null)
        {
            Debug.LogWarning(
                "[CustomerSystem] Falta configurar Customer Prefabs."
            );

            return;
        }

        Order order = orderSystem.GenerateOrder();

        if (order == null || order.PrimaryCut == null)
        {
            Debug.LogWarning(
                "[CustomerSystem] No hay cortes disponibles para generar pedidos."
            );

            return;
        }

        Customer customer = new Customer();

        float patience =
            basePatienceSeconds *
            Mathf.Max(0.01f, entry.patienceMultiplier);

        customer.Init(
            entry.type,
            order,
            patience,
            slotIndex
        );

        Vector3 position = GetSlotPosition(slotIndex);

        GameObject customerObject = Instantiate(
            entry.prefab,
            position,
            Quaternion.identity,
            customersParent
        );

        CustomerView view =
            customerObject.GetComponent<CustomerView>();

        if (view == null)
        {
            Debug.LogWarning(
                "[CustomerSystem] El prefab no tiene CustomerView: " +
                entry.prefab.name
            );

            Destroy(customerObject);
            return;
        }

        view.Init(customer, this);

        slotViews[slotIndex] = view;
        activeCustomers.Add(customer);

        spawnedTonight++;
        lastSpawnTime = Time.time;

        // El tiempo de jornada arranca con el primer cliente, no al cargar la escena.
        if (spawnedTonight == 1)
            DayClock.Instance?.BeginWindow();

        if (SelectedCustomer == null)
            SelectCustomer(customer);

        AudioManager.Instance?.PlayNewClientBell();

        UIManager.Instance?.SetArrivedCustomers(
            spawnedTonight
        );

        Debug.Log(
            "[CustomerSystem] Spawn " +
            customer.type +
            " en slot " +
            slotIndex +
            " | Pedido: " +
            order.PrimaryCut.cutName +
            (order.IsSandwich
                ? " (" + order.bread.breadName + ")"
                : " al plato") +
            (order.toppings.Count > 0
                ? " + " + string.Join(", ", order.toppings.Select(t => t.toppingName))
                : "") +
            " | Cliente n° " +
            spawnedTonight +
            (DayClock.Instance != null
                ? " | Quedan " + DayClock.FormatSeconds(DayClock.Instance.RemainingSeconds) + " de ventana"
                : "") +
            (ignoreNightLimit ? " | Spawn forzado" : "")
        );
    }

    public void SelectCustomer(Customer customer)
    {
        if (customer == null || customer.IsInFeedback) return;

        SelectedCustomer = customer;
        currentCustomer = customer; // compat con GameManager

        RefreshSelectionVisuals();
    }

    /// <summary>Entra al modo de selección de cliente para entregar. Arranca en el primer cliente disponible.</summary>
    public bool BeginDeliverySelection()
    {
        var first = GetFirstActiveCustomer();
        if (first == null) return false;

        IsDeliverySelectionActive = true;
        SelectCustomer(first);
        TutorialManager.NotifyDeliverySelectionBegun();
        return true;
    }

    public void EndDeliverySelection()
    {
        IsDeliverySelectionActive = false;
        RefreshSelectionVisuals();
        CustomerHoverBubble.Instance?.Hide();
    }

    /// <summary>Navega al cliente ocupado siguiente (+1) o anterior (-1) por orden de slot, con wrap.</summary>
    public void SelectAdjacentCustomer(int direction)
    {
        if (SelectedCustomer == null)
        {
            var first = GetFirstActiveCustomer();
            if (first != null) SelectCustomer(first);
            return;
        }

        int n = slotViews.Length;
        int start = SelectedCustomer.slotIndex;

        for (int step = 1; step < n; step++)
        {
            int idx = ((start + direction * step) % n + n) % n;
            if (slotViews[idx] != null)
            {
                SelectCustomer(slotViews[idx].Customer);
                return;
            }
        }
    }

    /// <summary>True si el cliente sigue esperando (no se fue enojado ni fue atendido).</summary>
    public bool IsCustomerActive(Customer customer)
    {
        return customer != null && activeCustomers.Contains(customer) && !customer.IsInFeedback;
    }

    /// <summary>
    /// Resalta al cliente que está bajo el mouse mientras se arrastra el plato,
    /// con el mismo recuadro y burbuja que el modo de selección por teclado.
    /// Pasar null limpia el resaltado y restaura el estado del modo por teclado.
    /// </summary>
    public void SetDeliveryDragHover(CustomerView view)
    {
        if (dragHoverView == view) return;

        dragHoverView = view;

        if (view != null && view.Customer != null && !view.Customer.IsInFeedback)
        {
            CustomerSelectionFrame.Instance?.ShowOver(view);

            if (view.Customer.order != null)
            {
                // Mientras se arrastra el plato, la misma evaluación que usa la entrega real
                // alimenta dos previews: la línea de pago en la burbuja y el tinte por corte
                // sobre el plato (verde exacto / amarillo desfase 1 / naranja mitad / rojo bloquea).
                string message = view.Customer.order.ToHoverString();

                if (GameManager.Instance != null)
                {
                    GameManager.DeliveryEvaluation eval = GameManager.Instance.EvaluateDelivery(view.Customer);
                    message += "\n" + BuildDeliveryPreviewLine(eval);
                    GameManager.Instance.ShowDeliveryPreviewOnPlate(eval);
                }

                CustomerHoverBubble.Instance?.Show(
                    message,
                    view.transform,
                    view.GetDishSprite());
            }

            return;
        }

        GameManager.Instance?.ClearDeliveryPreviewOnPlate();
        RefreshSelectionVisuals();

        if (!IsDeliverySelectionActive)
            CustomerHoverBubble.Instance?.Hide();
    }

    /// <summary>
    /// Línea de preview económico para la burbuja de arrastre.
    /// Colores: misma paleta que CustomerFeedbackBubble.
    /// </summary>
    private static string BuildDeliveryPreviewLine(GameManager.DeliveryEvaluation eval)
    {
        if (!eval.accepted)
            return "<color=#EF4444>" + eval.rejectShort + "</color>";

        string payment = "$" + (int)eval.payment;
        string line;

        if (eval.tip > 0f)
            line = "<color=#4ADE80>" + payment + " + $" + (int)eval.tip + " propina</color>";
        else if (eval.worstOffset >= 2)
            line = "<color=#F59E0B>" + payment + " (mitad) - sin propina</color>";
        else
            line = "<color=#FACC15>" + payment + " - sin propina</color>";

        // Toppings/pan mal entregados: se acepta igual, pero se avisa qué falló.
        if (!string.IsNullOrEmpty(eval.extrasNote))
            line += "\n<color=#FACC15>" + eval.extrasNote + "</color>";

        return line;
    }

    private void RefreshSelectionVisuals()
    {
        if (slotViews == null) return;

        CustomerView selectedView = null;

        for (int i = 0; i < slotViews.Length; i++)
        {
            if (slotViews[i] == null) continue;

            bool isSelected = IsDeliverySelectionActive && slotViews[i].Customer == SelectedCustomer;
            slotViews[i].RefreshSelection(isSelected);

            if (isSelected)
                selectedView = slotViews[i];
        }

        if (selectedView != null)
        {
            CustomerSelectionFrame.Instance?.ShowOver(selectedView);
            ShowSelectedOrderBubble();
        }
        else
        {
            CustomerSelectionFrame.Instance?.Hide();
        }
    }

    /// <summary>
    /// Muestra la burbuja de pedido sobre el cliente seleccionado si el modo de
    /// selección de entrega está activo; si no, la oculta. También la usa
    /// CustomerView al salir el mouse del hover para restaurar la burbuja.
    /// </summary>
    public void ShowSelectedOrderBubble()
    {
        if (CustomerHoverBubble.Instance == null) return;

        var view = IsDeliverySelectionActive ? GetSelectedView() : null;

        if (view != null && view.Customer?.order != null)
            CustomerHoverBubble.Instance.Show(view.Customer.order.ToHoverString(), view.transform, view.GetDishSprite());
        else
            CustomerHoverBubble.Instance.Hide();
    }

    private CustomerView GetSelectedView()
    {
        if (slotViews == null || SelectedCustomer == null) return null;

        for (int i = 0; i < slotViews.Length; i++)
        {
            if (slotViews[i] != null && slotViews[i].Customer == SelectedCustomer)
                return slotViews[i];
        }

        return null;
    }

    public void CompleteCustomer(Customer customer)
    {
        TriggerDeliveryFeedback(customer, 0f, 0f, CustomerFeedbackState.EntregaExcelente);
    }

    /// <summary>
    /// Inicia el feedback de entrega (4 segundos). El cliente permanece en su slot
    /// durante todo el feedback sin pausar el gameplay ni liberar el slot antes de tiempo.
    /// Al cumplirse los 4 segundos, el cliente se retira y se libera su posición.
    /// </summary>
    public void TriggerDeliveryFeedback(
        Customer customer,
        float payment,
        float tip,
        CustomerFeedbackState state)
    {
        if (customer == null) return;

        servedToday++;
        UIManager.Instance?.SetServedCustomers(servedToday);

        customer.StartFeedback();

        // Limpiar selecciones previas
        if (dragHoverView != null && dragHoverView.Customer == customer)
        {
            dragHoverView = null;
            CustomerSelectionFrame.Instance?.Hide();
            CustomerHoverBubble.Instance?.Hide();
        }

        if (SelectedCustomer == customer)
        {
            SelectedCustomer = null;
            currentCustomer = null;

            var next = GetFirstActiveCustomer();
            if (next != null)
                SelectCustomer(next);
            else if (IsDeliverySelectionActive)
                EndDeliverySelection();
        }

        CustomerView view = GetViewForCustomer(customer);
        if (view != null)
        {
            view.ShowFeedback(
                state,
                payment,
                tip,
                false,
                () => RemoveCustomer(customer, "Pedido entregado"),
                feedbackConfig
            );
        }
        else
        {
            RemoveCustomer(customer, "Pedido entregado");
        }
    }

    /// <summary>
    /// Feedback de abandono por paciencia 0 (Estado 6: No paga / se va).
    /// Permanece 4 segundos en su slot con Pedido: $0 y Propina: $0 antes de retirarse.
    /// </summary>
    public void TriggerAngryLeaveFeedback(Customer customer)
    {
        if (customer == null || customer.IsInFeedback) return;

        customer.StartFeedback();

        if (dragHoverView != null && dragHoverView.Customer == customer)
        {
            dragHoverView = null;
            CustomerSelectionFrame.Instance?.Hide();
            CustomerHoverBubble.Instance?.Hide();
        }

        if (SelectedCustomer == customer)
        {
            SelectedCustomer = null;
            currentCustomer = null;

            var next = GetFirstActiveCustomer();
            if (next != null)
                SelectCustomer(next);
            else if (IsDeliverySelectionActive)
                EndDeliverySelection();
        }

        CustomerView view = GetViewForCustomer(customer);
        if (view != null)
        {
            view.ShowFeedback(
                CustomerFeedbackState.NoPagaSeVa,
                0f,
                0f,
                false,
                () => RemoveCustomer(customer, "Se fue enojado"),
                feedbackConfig
            );
        }
        else
        {
            RemoveCustomer(customer, "Se fue enojado");
        }
    }

    /// <summary>
    /// Feedback de aceptación de cambio por faltante (Estado 5).
    /// Muestra Pedido: pendiente y Propina: anulada durante 4 segundos.
    /// Al finalizar, el cliente continúa en la escena con el nuevo pedido.
    /// </summary>
    public void TriggerMissingCutChange(Customer customer, MeatCutSO newCut)
    {
        if (customer == null || newCut == null) return;

        Order order = customer.order;
        order.SetSingleCut(newCut, order.GetRequestedState(0));

        // El sustituto puede cambiar el modo de servicio: lleva pan solo si el corte lo admite
        // (y el pan que ese corte exige), y con pan no queda ningún topping.
        bool wantsSandwich =
            newCut.servingMode == ServingMode.SandwichOnly ||
            (newCut.servingMode == ServingMode.Both && order.IsSandwich);

        order.bread = wantsSandwich ? newCut.requiredBread : null;

        if (order.IsSandwich)
            order.toppings.Clear();

        customer.IsTipAnulada = true;

        CustomerView view = GetViewForCustomer(customer);
        if (view != null)
        {
            view.ShowFeedback(
                CustomerFeedbackState.CambioPorFaltante,
                0f,
                0f,
                true,
                () =>
                {
                    Debug.Log($"[CustomerSystem] Cambio por faltante completado: {newCut.cutName}. Propina anulada.");
                    RefreshSelectionVisuals();
                },
                feedbackConfig
            );
        }
    }

    public CustomerView GetViewForCustomer(Customer customer)
    {
        if (slotViews == null || customer == null) return null;

        for (int i = 0; i < slotViews.Length; i++)
        {
            if (slotViews[i] != null && slotViews[i].Customer == customer)
                return slotViews[i];
        }

        return null;
    }

    private void RemoveCustomer(Customer customer, string reason)
    {
        if (customer == null) return;

        // El cliente resaltado por el arrastre se va: soltar el recuadro antes de destruir la view.
        if (dragHoverView != null && dragHoverView.Customer == customer)
        {
            dragHoverView = null;
            CustomerSelectionFrame.Instance?.Hide();
            CustomerHoverBubble.Instance?.Hide();
        }

        // destruir view
        int slotIndex = customer.slotIndex;
        if (slotIndex >= 0 && slotIndex < slotViews.Length && slotViews[slotIndex] != null)
        {
            Destroy(slotViews[slotIndex].gameObject);
            slotViews[slotIndex] = null;
        }

        activeCustomers.Remove(customer);

        // si era el seleccionado, seleccionar otro (primero que exista)
        if (SelectedCustomer == customer)
        {
            SelectedCustomer = null;
            currentCustomer = null;

            var next = GetFirstActiveCustomer();
            if (next != null)
                SelectCustomer(next);
            else if (IsDeliverySelectionActive)
                EndDeliverySelection();
        }

        Debug.Log("[CustomerSystem] Remove: " + reason);

        // opcional: compactar slots (corrés a la izquierda para no dejar huecos)
        CompactSlots();

        TryEndDay();
    }

    private void OnDestroy()
    {
        if (DayClock.Instance != null)
            DayClock.Instance.OnClosingTime -= HandleClosingTime;
    }

    private void CompactSlots()
    {
        // mueve views hacia el primer slot libre
        for (int i = 0; i < slotViews.Length; i++)
        {
            if (slotViews[i] != null) continue;

            int j = i + 1;
            while (j < slotViews.Length && slotViews[j] == null) j++;

            if (j >= slotViews.Length) break;

            // mover j -> i
            var view = slotViews[j];
            slotViews[i] = view;
            slotViews[j] = null;

            // actualizar customer.slotIndex + mover transform
            view.Customer.slotIndex = i;
            view.transform.position = GetSlotPosition(i);
        }
    }

    private Customer GetFirstActiveCustomer()
    {
        if (activeCustomers.Count == 0) return null;

        // intentar respetar el orden de slots (0..n)
        for (int i = 0; i < slotViews.Length; i++)
        {
            if (slotViews[i] != null && slotViews[i].Customer != null && !slotViews[i].Customer.IsInFeedback)
                return slotViews[i].Customer;
        }

        for (int i = 0; i < activeCustomers.Count; i++)
        {
            if (!activeCustomers[i].IsInFeedback)
                return activeCustomers[i];
        }

        return null;
    }

    private int GetNextFreeSlotIndex()
    {
        for (int i = 0; i < slotViews.Length; i++)
            if (slotViews[i] == null)
                return i;

        return -1;
    }

    private Vector3 GetSlotPosition(int slotIndex)
    {
        if (slots != null && slotIndex >= 0 && slotIndex < slots.Count && slots[slotIndex] != null)
            return slots[slotIndex].position;

        return autoFirstSlotPos + Vector3.right * (autoSlotSpacing * slotIndex);
    }

    private CustomerPrefabEntry PickCustomerEntry()
    {
        if (customerPrefabs == null || customerPrefabs.Count == 0)
            return null;

        float total = 0f;
        for (int i = 0; i < customerPrefabs.Count; i++)
            total += Mathf.Max(0f, customerPrefabs[i].spawnWeight);

        if (total <= 0.0001f)
            return customerPrefabs[UnityEngine.Random.Range(0, customerPrefabs.Count)];

        float r = UnityEngine.Random.value * total;
        float acc = 0f;

        for (int i = 0; i < customerPrefabs.Count; i++)
        {
            acc += Mathf.Max(0f, customerPrefabs[i].spawnWeight);
            if (r <= acc)
                return customerPrefabs[i];
        }

        return customerPrefabs[customerPrefabs.Count - 1];
    }

    private void ClearAllCustomers()
    {
        // destruir views
        if (slotViews != null)
        {
            for (int i = 0; i < slotViews.Length; i++)
            {
                if (slotViews[i] != null)
                    Destroy(slotViews[i].gameObject);
                slotViews[i] = null;
            }
        }

        activeCustomers.Clear();
        SelectedCustomer = null;
        currentCustomer = null;
        dragHoverView = null;
        IsDeliverySelectionActive = false;
        CustomerSelectionFrame.Instance?.Hide();
        CustomerHoverBubble.Instance?.Hide();
    }
  
    private List<WeightedOrderCut> GetUnlockedOrderCuts()
    {
        var result = new List<WeightedOrderCut>();

        Debug.Log(
            "[CustomerSystem] Cortes configurados: " +
            availableOrderCuts.Count
        );

        for (int i = 0; i < availableOrderCuts.Count; i++)
        {
            WeightedOrderCut entry = availableOrderCuts[i];

            if (entry == null)
            {
                Debug.LogWarning(
                    "[CustomerSystem] Element " + i +
                    " de Available Order Cuts está vacío."
                );

                continue;
            }

            MeatCutSO cut = entry.cut;

            if (cut == null)
            {
                Debug.LogWarning(
                    "[CustomerSystem] Element " + i +
                    " no tiene un corte asignado."
                );

                continue;
            }

            Debug.Log(
                "[CustomerSystem] Corte: " + cut.cutName +
                " | Desbloqueado: " + cut.isUnlocked +
                " | Peso: " + entry.weight
            );

            if (!cut.isUnlocked)
                continue;

            if (entry.weight <= 0f)
                continue;

            result.Add(entry);
        }

        return result;
    }
}
