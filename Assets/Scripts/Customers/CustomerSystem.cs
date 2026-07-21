using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using System;


public class CustomerSystem : MonoBehaviour
{
    [Header("Customer Limits")]
    [Min(1)]
    [SerializeField] private int maxSimultaneousCustomers = 4;

    [Header("Customers Per Night")]
    [Min(1)]
    [Tooltip("Cantidad de clientes durante la primera noche.")]
    [SerializeField] private int customersFirstNight = 20;

    [Min(0)]
    [Tooltip("Cantidad de clientes que se agregan por cada nueva noche.")]
    [SerializeField] private int customersAddedPerNight = 5;

    [Min(1)]
    [Tooltip("Máximo absoluto de clientes que puede tener una noche.")]
    [SerializeField] private int maximumCustomersPerNight = 70;

    private int customersTargetTonight;

    [Header("Spawning")]
    [SerializeField] private float spawnIntervalSeconds = 6f;
    [SerializeField] private bool autoStartNight = true;
    public Action OnNightEnded;

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

    [Header("Night Progression")]
    [SerializeField] private MeatCutSO nightTwoCut;
    [SerializeField]
    private List<ProductVariantSO> nightTwoVariants =
        new List<ProductVariantSO>();
   
    [Header("Patience")]
    [SerializeField] private float basePatienceSeconds = 30f;
    
    public FoodCatalogSO Catalog => availabilityService != null ? availabilityService.Catalog : null;

    public Customer currentCustomer;

    public Customer SelectedCustomer { get; private set; }

    public bool IsDeliverySelectionActive { get; private set; }

    private OrderSystem orderSystem;
    private readonly List<Customer> activeCustomers = new List<Customer>();
    public IReadOnlyList<Customer> ActiveCustomers => activeCustomers;
    private CustomerView[] slotViews;

    private int spawnedTonight;
    private Coroutine spawnRoutine;

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

        Debug.Log(
            "[CustomerSystem] Iniciando noche " + currentNight +
            " | Clientes de esta noche: " + customersTargetTonight +
            " | Máximo configurado: " + maximumCustomersPerNight
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

        orderSystem = new OrderSystem(cuts);

        slotViews = new CustomerView[
            Mathf.Max(1, maxSimultaneousCustomers)
        ];

        UIManager.Instance?.SetTotalCustomers(
            customersTargetTonight
        );

        if (autoStartNight)
            StartNight();
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
    void Update()
    {
        // Tick paciencia + expulsión
        for (int i = activeCustomers.Count - 1; i >= 0; i--)
        {
            var c = activeCustomers[i];
            c.UpdatePatience(Time.deltaTime);

            if (c.IsAngry)
            {
                RemoveCustomer(c, "Se fue enojado");
            }
        }
    }

    public void StartNight()
    {
        ClearAllCustomers();
        spawnedTonight = 0;

        UIManager.Instance?.SetActualCustomers(
            spawnedTonight
        );

        UIManager.Instance?.SetTotalCustomers(
            customersTargetTonight
        );

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        spawnRoutine = StartCoroutine(SpawnLoop());

        Debug.Log(
            "[CustomerSystem] Noche iniciada. Objetivo: " +
            customersTargetTonight + " clientes."
        );
    }
    IEnumerator SpawnLoop()
    {
        while (spawnedTonight < customersTargetTonight)
        {
            yield return new WaitForSeconds(
                spawnIntervalSeconds
            );

            if (activeCustomers.Count >= maxSimultaneousCustomers)
                continue;

            SpawnCustomer();
        }
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

        // Los spawns normales respetan el límite de la noche.
        // El tutorial puede ignorarlo.
        if (!ignoreNightLimit &&
            spawnedTonight >= customersTargetTonight)
        {
            return;
        }

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

        UIManager.Instance?.SetActualCustomers(
            spawnedTonight
        );

        Debug.Log(
            "[CustomerSystem] Spawn " +
            customer.type +
            " en slot " +
            slotIndex +
            " | Pedido: " +
            order.PrimaryCut.cutName +
            " | Cliente " +
            spawnedTonight +
            "/" +
            customersTargetTonight +
            (ignoreNightLimit ? " | Spawn forzado" : "")
        );
    }

    public void SelectCustomer(Customer customer)
    {
        if (customer == null) return;

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
            CustomerHoverBubble.Instance.Show(view.Customer.order.ToHoverString(), view.transform);
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
        RemoveCustomer(customer, "Pedido entregado");
    }

    private void RemoveCustomer(Customer customer, string reason)
    {
        if (customer == null) return;

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

        if (spawnedTonight >= customersTargetTonight &&
     activeCustomers.Count == 0)
        {
            OnNightEnded?.Invoke();
        }
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
            if (slotViews[i] != null)
                return slotViews[i].Customer;
        }

        return activeCustomers[0];
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
