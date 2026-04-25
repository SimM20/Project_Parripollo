using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class CoolerSystem : MonoBehaviour
{
    [Header("Initial Stock")]
    [SerializeField] private List<CoolerStockEntry> initialStock = new List<CoolerStockEntry>();

    public event Action OnInventoryChanged;
    public event Action<MeatCutSO> OnMissingCutRequested;

    private readonly Dictionary<MeatCutSO, int> stockByCut = new Dictionary<MeatCutSO, int>();

    void Awake()
    {
        BuildInitialStockRuntime();
    }

    private void BuildInitialStockRuntime()
    {
        stockByCut.Clear();

        for (int i = 0; i < initialStock.Count; i++)
        {
            CoolerStockEntry entry = initialStock[i];
            if (entry == null || entry.cut == null || entry.amount <= 0)
                continue;

            if (!stockByCut.ContainsKey(entry.cut))
                stockByCut[entry.cut] = 0;

            stockByCut[entry.cut] += entry.amount;
        }
    }

    public int GetCount(MeatCutSO cut)
    {
        if (cut == null)
            return 0;

        return stockByCut.TryGetValue(cut, out int count) ? count : 0;
    }

    public IEnumerable<KeyValuePair<MeatCutSO, int>> EnumerateStock()
    {
        return stockByCut;
    }

    public void Add(MeatCutSO cut, int amount = 1)
    {
        if (cut == null || amount <= 0) return;

        if (!stockByCut.ContainsKey(cut))
            stockByCut[cut] = 0;

        stockByCut[cut] += amount;
        OnInventoryChanged?.Invoke();
    }

    public bool TryTake(MeatCutSO cut, int amount = 1)
    {
        if (cut == null)
            return false;

        if (amount <= 0) return true;

        int current = GetCount(cut);
        if (current < amount)
            return false;

        stockByCut[cut] = current - amount;
        OnInventoryChanged?.Invoke();
        return true;
    }

    public void InformMissingCut(MeatCutSO cut)
    {
        if (cut == null) return;
        Debug.Log("[CoolerSystem] Corte no disponible: " + cut.cutName);
        OnMissingCutRequested?.Invoke(cut);
    }

    public string GetDebugStockString()
    {
        StringBuilder sb = new StringBuilder("Hielera -> ");
        bool first = true;

        foreach (var kvp in stockByCut)
        {
            if (!first)
                sb.Append(", ");

            string cutName = kvp.Key != null ? kvp.Key.cutName : "Sin corte";
            sb.Append(cutName);
            sb.Append(": ");
            sb.Append(kvp.Value);
            first = false;
        }

        return sb.ToString();
    }
}

[Serializable]
public class CoolerStockEntry
{
    public MeatCutSO cut;
    [Min(0)] public int amount = 0;
}
