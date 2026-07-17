using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class CoolerSystem : MonoBehaviour
{
    [Header("Initial Stock")]
    [SerializeField] private List<InventoryStockEntry> initialStock = new List<InventoryStockEntry>();

    public event Action OnInventoryChanged;

    public event Action<ItemDataSO> OnMissingItemRequested;

    public static CoolerSystem Instance { get; private set; }

    // Static backup survives DDOL destruction; cleared explicitly on ReturnToMainMenu
    private static Dictionary<ItemDataSO, int> stockBackup = null;
    private static bool clearBackupOnNextInit = false;

    private readonly Dictionary<ItemDataSO, int> stockByItem = new Dictionary<ItemDataSO, int>();

    void Awake()
    {
        Debug.Log("[CoolerSystem] Awake — Instance is " + (Instance != null ? Instance.GetInstanceID().ToString() : "null"));
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        BuildInitialStockRuntime();
    }

    // Call before destroying CoolerSystem on ReturnToMainMenu so the next
    // new-game start gets a clean initialStock instead of the old run's data.
    public static void PrepareForNewGame()
    {
        clearBackupOnNextInit = true;
        stockBackup = null;
    }

    private void BuildInitialStockRuntime()
    {
        if (stockBackup != null && !clearBackupOnNextInit)
        {
            // DDOL was destroyed unexpectedly — restore saved stock instead of resetting
            stockByItem.Clear();
            foreach (var kv in stockBackup)
                stockByItem[kv.Key] = kv.Value;
            stockBackup = null;
            Debug.Log("[CoolerSystem] Stock restaurado desde backup.");
            return;
        }

        clearBackupOnNextInit = false;
        stockBackup = null;

        stockByItem.Clear();

        for (int i = 0; i < initialStock.Count; i++)
        {
            InventoryStockEntry entry = initialStock[i];
            if (entry == null || entry.item == null || entry.amount <= 0)
                continue;

            if (!stockByItem.ContainsKey(entry.item))
                stockByItem[entry.item] = 0;

            if (entry.item is CoalSO)
            {
                stockByItem[entry.item] = Mathf.Min(stockByItem[entry.item] + entry.amount, 40);
            }
            else
            {
                stockByItem[entry.item] += entry.amount;
            }
        }
    }

    public int GetCount(ItemDataSO item)
    {
        if (item == null)
            return 0;

        return stockByItem.TryGetValue(item, out int count) ? count : 0;
    }

    public IEnumerable<KeyValuePair<ItemDataSO, int>> EnumerateStock()
    {
        return stockByItem;
    }

    public void Add(ItemDataSO item, int amount = 1)
    {
        if (item == null || amount <= 0) return;

        if (!stockByItem.ContainsKey(item))
            stockByItem[item] = 0;

        int current = stockByItem[item];
        int newAmount = current + amount;

        if (item is CoalSO)
        {
            newAmount = Mathf.Min(newAmount, 40);
        }

        stockByItem[item] = newAmount;
        OnInventoryChanged?.Invoke();
    }

    public bool TryTake(ItemDataSO item, int amount = 1)
    {
        if (item == null)
            return false;

        if (amount <= 0) return true;

        int current = GetCount(item);
        if (current < amount)
            return false;

        stockByItem[item] = current - amount;
        OnInventoryChanged?.Invoke();
        return true;
    }

    public void InformMissingItem(ItemDataSO item)
    {
        if (item == null) return;
        Debug.Log("�tem no disponible: " + item.itemName);
        OnMissingItemRequested?.Invoke(item);
    }

    public string GetDebugStockString()
    {
        StringBuilder sb = new StringBuilder("Inventario -> ");
        bool first = true;

        foreach (var kvp in stockByItem)
        {
            if (!first)
                sb.Append(", ");

            string name = kvp.Key != null ? kvp.Key.itemName : "Sin �tem";
            sb.Append(name);
            sb.Append(": ");
            sb.Append(kvp.Value);
            first = false;
        }

        return sb.ToString();
    }
    
    void OnDestroy()
    {
        if (Instance == this)
        {
            if (!clearBackupOnNextInit)
                stockBackup = new Dictionary<ItemDataSO, int>(stockByItem);
            clearBackupOnNextInit = false;
            Instance = null;
        }
    }
}

[Serializable]
public class InventoryStockEntry
{
    public ItemDataSO item;
    [Min(0)] public int amount = 0;
}
