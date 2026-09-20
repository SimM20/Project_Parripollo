using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;
using System;


public class CustomerSystem : MonoBehaviour
{
    [Header("Customer Limits")]
    [Min(1)]
    [Tooltip("Base de clientes simultaneos. Las mejoras de la tienda le suman encima.")]
    [SerializeField] private int maxSimultaneousCustomers = 4;

    [Header("Customers Per Night")]
    [Min(1)]
    [Tooltip("Clientes esperados durante la primera jornada. Con reloj no es un cupo: " +
             "reparte el ritmo de llegada entre la apertura y el cierre.")]
    [SerializeField] private int customersFirstNight = 20;

    [Min(0)]
    [Tooltip("Clientes esperados que se agregan por cada nueva jornada. Como el día dura " +
             "siempre lo mismo, sumar clientes es apretar el ritmo de llegada.")]
    [SerializeField] private int customersAddedPerNight = 5;

    [Min(1)]
    [Tooltip("Techo absoluto de clientes esperados en una jornada.")]
    [SerializeField] private int maximumCustomersPerNight = 70;

    /// <summary>
    /// Clientes esperados en la jornada. Con <see cref="DayClock"/> no es un cupo duro:
    /// solo fija el ritmo promedio de llegada (ver <see cref="BaseSpawnIntervalSeconds"/>),
    /// y los que entren de verdad dependen del horario y de los slots libres. Sin reloj
    /// (tutorial) sí es el cupo fijo de siempre.
    /// </summary>
    private int customersTargetTonight;

    // Base del inspector + bonus de las mejoras. Se resuelve una vez en Start.
    private int resolvedMaxSimultaneousCustomers;

    /// <summary>Clientes simultaneos de esta noche, ya con las mejoras compradas aplicadas.</summary>
    public int MaxSimultaneousCustomers =>
        resolvedMaxSimultaneousCustomers > 0
            ? resolvedMaxSimultaneousCustomers
            : Mathf.Max(1, maxSimultaneousCustomers);

    [Header("Spawning")]
    [Tooltip("Segundos entre clientes cuando NO hay DayClock en la escena (tutorial). " +
             "Con reloj, el intervalo sale de repartir los clientes esperados en la jornada.")]
    [SerializeField] private float spawnIntervalSeconds = 6f;

    [Tooltip("Arrancar la jornada sola al cargar la escena.")]
    [SerializeField] private bool autoStartNight = true;

    [Tooltip("Cómo se reparte la llegada de clientes a lo largo de la jornada: 0 = apertura, " +
             "1 = cierre. Es un multiplicador del ritmo (2 = entra el doble que el promedio, " +
             "0.5 = la mitad). Se normaliza sola, así que el total del día lo sigue mandando " +
             "la cantidad de clientes esperados. Una curva plana en 1 = llegada pareja.")]
    [SerializeField] private AnimationCurve affluenceCurve = DefaultAffluenceCurve();

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

    [Header("Night Progression")]
    [SerializeField] private MeatCutSO nightTwoCut;
    [SerializeField]
    private List<ProductVariantSO> nightTwoVariants =
        new List<ProductVariantSO>();
   
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
    private float affluenceAverage = 1f;
    private Coroutine spawnRoutine;

    /// <summary>
    /// True mientras todavía puede entrar gente: con reloj, hasta la hora de cierre;
    /// sin reloj, hasta cubrir el cupo de la noche.
    /// </summary>
    private bool DoorsOpen
    {
        get
        {
            DayClock clock = DayClock.Instance;

            return clock != null
                ? !clock.HasClosed
                : spawnedTonight < customersTargetTonight;
        }
    }

    /// <summary>
    /// Ritmo promedio de llegada: reparte los clientes esperados a lo largo de toda la
    /// jornada. Sin reloj cae al intervalo fijo del inspector.
    /// </summary>
    private float BaseSpawnIntervalSeconds
    {
        get
        {
            DayClock clock = DayClock.Instance;

            if (clock == null)
                return Mathf.Max(0.1f, spawnIntervalSeconds);

            return Mathf.Max(
                0.1f,
                clock.DayDurationSeconds / Mathf.Max(1, customersTargetTonight)
            );
        }
    }

    public bool IsReadyForSpawning =>
    orderSystem != null &&
    slotViews != null &&
    customersTargetTonight > 0;

    void Start()
    {
        int currentNight =
            CoalConsumptionTracker.Instance != null
                ? CoalConsumptionTracker.Instance.CurrentNight
                : 1;

        customersTargetTonight =
            CalculateCustomersForNight(currentNight);

        resolvedMaxSimultaneousCustomers =
            CalculateMaxSimultaneousCustomers();

        Debug.Log(
            "[CustomerSystem] Iniciando noche " + currentNight +
            " | Clientes de esta noche: " + customersTargetTonight +
            " | Máximo configurado: " + maximumCustomersPerNight +
            " | Simultáneos: " + resolvedMaxSimultaneousCustomers +
            " (base " + Mathf.Max(1, maxSimultaneousCustomers) + ")"
        );

        // Comunica al tracker cuál es el corte desbloqueable.
        if (CoalConsumptionTracker.Instance != null)
        {
            CoalConsumptionTracker.Instance.ConfigureNightTwoCut(
                nightTwoCut
            );
        }
        else
        {
            Debug.LogError(
                "[CustomerSystem] No existe CoalConsumptionTracker."
            );
        }

        // Desbloqueo de variantes.
        bool variantsUnlocked = currentNight >= 2;

        for (int i = 0; i < nightTwoVariants.Count; i++)
        {
            ProductVariantSO variant = nightTwoVariants[i];

            if (variant != null)
                variant.isUnlocked = variantsUnlocked;
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

        DayStats.ResetDay();
        UIManager.Instance?.SetServedCustomers(0);
        UIManager.Instance?.SetArrivedCustomers(0);

        if (autoStartNight)
            StartDay();
    }
    private int CalculateCustomersForNight(int nightNumber)
    {
        int safeNight = Mathf.Max(1, nightNumber);
        int safeBase = Mathf.Max(1, customersFirstNight);
        int safeIncrement = Mathf.Max(0, customersAddedPerNight);
        int safeMaximum = Mathf.Max(safeBase, maximumCustomersPerNight);

        int calculated =
            safeBase + ((safeNight - 1) * safeIncrement);

        return Mathf.Min(calculated, safeMaximum);
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
    /// Abre el local: arranca el reloj de la jornada y la llegada de clientes.
    /// Sin <see cref="DayClock"/> en la escena (tutorial) mantiene el modo viejo de cupo fijo.
    /// </summary>
    public void StartDay()
    {
        ClearAllCustomers();

        spawnedTonight = 0;
        servedToday = 0;
        dayEnded = false;

        DayStats.ResetDay();
        UIManager.Instance?.SetServedCustomers(0);
        UIManager.Instance?.SetArrivedCustomers(0);

        CacheAffluenceAverage();

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        DayClock clock = DayClock.Instance;

        if (clock != null)
        {
            clock.OnClosingTime -= HandleClosingTime;
            clock.OnClosingTime += HandleClosingTime;
            clock.StartDay();

            Debug.Log(
                "[CustomerSystem] Jornada iniciada de " +
                DayClock.FormatHour(clock.OpeningHour) + " a " +
                DayClock.FormatHour(clock.ClosingHour) +
                " | Clientes esperados: " + customersTargetTonight +
                " (uno cada " + BaseSpawnIntervalSeconds.ToString("0.0") + "s en promedio)"
            );
        }
        else
        {
            Debug.Log(
                "[CustomerSystem] Jornada iniciada sin reloj (cupo fijo). Objetivo: " +
                customersTargetTonight + " clientes."
            );
        }

        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        while (DoorsOpen)
        {
            yield return new WaitForSeconds(NextSpawnDelaySeconds());

            // El local pudo cerrar mientras esperábamos: nadie entra después de hora.
            if (!DoorsOpen)
                break;

            if (activeCustomers.Count >= resolvedMaxSimultaneousCustomers)
                continue;

            SpawnCustomer();
        }

        spawnRoutine = null;

        TryEndDay();
    }

    /// <summary>
    /// Cuánto falta para el próximo cliente. La curva de afluencia aprieta o afloja el
    /// ritmo según la hora (mediodía y noche son los picos), sin mover el total del día:
    /// la curva se divide por su propio promedio.
    /// </summary>
    private float NextSpawnDelaySeconds()
    {
        float interval = BaseSpawnIntervalSeconds;

        DayClock clock = DayClock.Instance;

        if (clock == null || affluenceCurve == null || affluenceCurve.length == 0)
            return interval;

        float multiplier = Mathf.Max(
            0.05f,
            affluenceCurve.Evaluate(clock.Normalized01) / affluenceAverage
        );

        return interval / multiplier;
    }

    /// <summary>
    /// Promedio de la curva de afluencia sobre la jornada. Dividir por él deja la curva
    /// como puro reparto: cambia CUÁNDO entra la gente, no cuánta.
    /// </summary>
    private void CacheAffluenceAverage()
    {
        affluenceAverage = 1f;

        if (affluenceCurve == null || affluenceCurve.length == 0)
            return;

        const int samples = 64;
        float sum = 0f;

        for (int i = 0; i < samples; i++)
            sum += Mathf.Max(0f, affluenceCurve.Evaluate((i + 0.5f) / samples));

        float average = sum / samples;

        if (average <= 0.01f)
        {
            Debug.LogWarning(
                "[CustomerSystem] La curva de afluencia es casi cero en toda la jornada: " +
                "no entraría nadie. Se ignora y la llegada queda pareja."
            );

            return;
        }

        affluenceAverage = average;
    }

    /// <summary>
    /// Cerró el local: no entra nadie más. Si adentro ya no quedaba nadie el día termina
    /// acá; si queda gente, sigue hasta que se vaya el último.
    /// </summary>
    private void HandleClosingTime()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        Debug.Log(
            "[CustomerSystem] Cerró el local con " + activeCustomers.Count +
            " cliente(s) adentro."
        );

        TryEndDay();
    }

    /// <summary>
    /// Termina el día cuando ya no puede entrar nadie y se fue el último cliente.
    /// Único lugar que dispara <see cref="OnDayEnded"/>, y lo hace una sola vez.
    /// </summary>
    private void TryEndDay()
    {
        if (dayEnded || activeCustomers.Count > 0 || DoorsOpen)
            return;

        dayEnded = true;

        Debug.Log(
            "[CustomerSystem] Se fue el último cliente: termina el día. " +
            "Atendidos: " + servedToday + "/" + spawnedTonight + "."
        );

        OnDayEnded?.Invoke();
    }

    /// <summary>
    /// Curva por defecto: mañana floja, pico del mediodía, bajón de la siesta y pico de la
    /// noche. En una jornada de 06:30 a 21:00, t = 0.4 cae cerca de las 12:30 y t = 0.93,
    /// de las 20:00.
    /// </summary>
    private static AnimationCurve DefaultAffluenceCurve()
    {
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, 0.5f),
            new Keyframe(0.20f, 0.7f),
            new Keyframe(0.40f, 2f),
            new Keyframe(0.50f, 1.8f),
            new Keyframe(0.62f, 0.8f),
            new Keyframe(0.80f, 0.9f),
            new Keyframe(0.93f, 2f),
            new Keyframe(1f, 1.5f)
        );

        for (int i = 0; i < curve.length; i++)
            curve.SmoothTangents(i, 0f);

        return curve;
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

        // Los spawns normales respetan el horario del local (o el cupo, sin reloj).
        // El tutorial puede ignorarlo.
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

        if (SelectedCustomer == null)
            SelectCustomer(customer);

        AudioManager.Instance?.PlayNewClientBell();

        DayStats.SetCustomersToday(spawnedTonight);

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
                ? " | " + DayClock.Instance.TimeLabel
                : "/" + customersTargetTonight) +
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
  
    private void ApplyNightUnlocks()
    {
        int currentNight =
            (CoalConsumptionTracker.Instance?.DaysPlayed ?? 0) + 1;

        bool patyUnlocked = currentNight >= 2;

        if (nightTwoCut != null)
        {
            nightTwoCut.isUnlocked = patyUnlocked;

            Debug.Log(
                "[Progression] Noche actual: " + currentNight +
                " | Paty desbloqueado: " + patyUnlocked
            );
        }
        else
        {
            Debug.LogWarning(
                "[Progression] No se asignó el corte de la noche 2 en CustomerSystem."
            );
        }
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
