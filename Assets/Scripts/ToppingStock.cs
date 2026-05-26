using System;
using System.Collections.Generic;
using UnityEngine;

public class ToppingStock : MonoBehaviour
{
    public static ToppingStock Instance { get; private set; }

    public event Action OnStockChanged;

    private readonly Dictionary<ToppingSO, int> stock = new Dictionary<ToppingSO, int>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public int GetCount(ToppingSO topping)
    {
        if (topping == null) return 0;
        return stock.TryGetValue(topping, out int count) ? count : 0;
    }

    public void Add(ToppingSO topping, int amount = 1)
    {
        if (topping == null || amount <= 0) return;

        if (!stock.ContainsKey(topping)) stock[topping] = 0;
        stock[topping] += amount;
        OnStockChanged?.Invoke();
    }

    public bool TryTake(ToppingSO topping, int amount = 1)
    {
        if (topping == null || amount <= 0) return false;

        int current = GetCount(topping);
        if (current < amount) return false;

        stock[topping] = current - amount;
        OnStockChanged?.Invoke();
        return true;
    }
}