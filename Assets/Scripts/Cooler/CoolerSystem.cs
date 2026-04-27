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

    private readonly Dictionary<ItemDataSO, int> stockByItem = new Dictionary<ItemDataSO, int>();

    void Awake() => BuildInitialStockRuntime();

    private void BuildInitialStockRuntime()
    {
        stockByItem.Clear();

        for (int i = 0; i < initialStock.Count; i++)
        {
            InventoryStockEntry entry = initialStock[i];
            if (entry == null || entry.item == null || entry.amount <= 0)
                continue;

            if (!stockByItem.ContainsKey(entry.item))
                stockByItem[entry.item] = 0;

            stockByItem[entry.item] += entry.amount;
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

        stockByItem[item] += amount;
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
        Debug.Log("Ítem no disponible: " + item.itemName);
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

            string name = kvp.Key != null ? kvp.Key.itemName : "Sin ítem";
            sb.Append(name);
            sb.Append(": ");
            sb.Append(kvp.Value);
            first = false;
        }

        return sb.ToString();
    }
}

[Serializable]
public class InventoryStockEntry
{
    public ItemDataSO item;
    [Min(0)] public int amount = 0;
}
