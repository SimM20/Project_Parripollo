using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomerSystem : MonoBehaviour
{
    [Header("Caps (Editor)")]
    [SerializeField] private int maxSimultaneousCustomers = 4;
    [SerializeField] private int maxCustomersPerNight = 20;

    [Header("Spawning")]
    [SerializeField] private float spawnIntervalSeconds = 6f;
    [SerializeField] private bool autoStartNight = true;

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
    [SerializeField] private List<MeatCutSO> availableOrderCuts = new List<MeatCutSO>();
    [SerializeField] private FoodAvailabilityService availabilityService;

    [Header("Patience")]
    [SerializeField] private float basePatienceSeconds = 30f;

    // Compat: tu GameManager usa esto hoy
    public Customer currentCustomer;

    public Customer SelectedCustomer { get; private set; }

    private OrderSystem orderSystem;
    private readonly List<Customer> activeCustomers = new List<Customer>();
    private CustomerView[] slotViews;

    private int spawnedTonight;
    private Coroutine spawnRoutine;

    void Start()
    {
        // Resolver cuts disponibles (tu lógica actual)
        List<MeatCutSO> cuts = availableOrderCuts;

        if (availabilityService != null)
        {
            var catalogCuts = availabilityService.GetAvailableCuts();
            if (catalogCuts != null && catalogCuts.Count > 0)
                cuts = new List<MeatCutSO>(catalogCuts);
            else
                Debug.LogWarning("[CustomerSystem] FoodAvailabilityService no devolvió cortes. Uso availableOrderCuts.");
        }

        orderSystem = new OrderSystem(cuts);

        slotViews = new CustomerView[Mathf.Max(1, maxSimultaneousCustomers)];

        if (autoStartNight)
            StartNight();
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

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        while (spawnedTonight < maxCustomersPerNight)
        {
            yield return new WaitForSeconds(spawnIntervalSeconds);

            if (activeCustomers.Count >= maxSimultaneousCustomers)
                continue;

            SpawnCustomer();
        }

        // Cuando ya spawneaste el máximo, simplemente dejás que termine la cola activa.
        // Podés disparar un evento cuando activeCustomers.Count llegue a 0.
    }

    public void SpawnCustomer()
    {
        if (spawnedTonight >= maxCustomersPerNight)
            return;

        int slotIndex = GetNextFreeSlotIndex();
        if (slotIndex < 0)
            return;

        var entry = PickCustomerEntry();
        if (entry == null || entry.prefab == null)
        {
            Debug.LogWarning("[CustomerSystem] Falta configurar customerPrefabs.");
            return;
        }

        Order order = orderSystem.GenerateOrder();
        if (order == null || order.PrimaryCut == null)
        {
            Debug.LogWarning("[CustomerSystem] No hay cortes configurados para generar pedidos.");
            return;
        }

        var customer = new Customer();
        float patience = basePatienceSeconds * Mathf.Max(0.01f, entry.patienceMultiplier);
        customer.Init(entry.type, order, patience, slotIndex);

        var pos = GetSlotPosition(slotIndex);
        var go = Instantiate(entry.prefab, pos, Quaternion.identity, customersParent);

        var view = go.GetComponent<CustomerView>();
        if (view == null)
        {
            Debug.LogWarning("[CustomerSystem] El prefab no tiene CustomerView: " + entry.prefab.name);
            Destroy(go);
            return;
        }

        view.Init(customer, this);

        slotViews[slotIndex] = view;
        activeCustomers.Add(customer);
        spawnedTonight++;

        // auto-selección
        if (SelectedCustomer == null)
            SelectCustomer(customer);

        Debug.Log($"[CustomerSystem] Spawn {customer.type} en slot {slotIndex}: {order.PrimaryCut.cutName}");
    }

    public void SelectCustomer(Customer customer)
    {
        if (customer == null) return;

        SelectedCustomer = customer;
        currentCustomer = customer; // compat con GameManager

        // Refresh selection visuals
        for (int i = 0; i < slotViews.Length; i++)
        {
            if (slotViews[i] == null) continue;
            slotViews[i].RefreshSelection(slotViews[i].Customer == SelectedCustomer);
        }
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
        }

        Debug.Log("[CustomerSystem] Remove: " + reason);

        // opcional: compactar slots (corrés a la izquierda para no dejar huecos)
        CompactSlots();
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
            return customerPrefabs[Random.Range(0, customerPrefabs.Count)];

        float r = Random.value * total;
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
    }
}
