using System;
using System.Collections.Generic;
using UnityEngine;

public class ShopSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShopConfigSO config;
    [SerializeField] private FoodCatalogSO catalog;

    private PlayerWallet wallet;
    private CoolerSystem coolerSystem;
    public ShopConfigSO Config => config;
    public PlayerWallet Wallet => wallet;
    public CoolerSystem Cooler => coolerSystem;
    public FoodCatalogSO Catalog => catalog;

    private readonly Dictionary<ItemDataSO, int> cart = new Dictionary<ItemDataSO, int>();

    public event Action OnCartChanged;
    public event Action<bool, string> OnPurchaseResult;
    
    void Start()
    {
        Debug.Log("Start del ShopSystem");
        
        wallet = PlayerWallet.Instance;
        coolerSystem = CoolerSystem.Instance;

        if (wallet == null)
            Debug.LogError("[ShopSystem] No se encontró PlayerWallet.Instance");
        if (coolerSystem == null)
            Debug.LogError("[ShopSystem] No se encontró CoolerSystem.Instance");
    }


    public IReadOnlyList<ItemDataSO> GetShopItems()
    {
        var list = new List<ItemDataSO>();

        if (config != null && config.coal != null)
            list.Add(config.coal);

        if (catalog != null)
        {
            var cuts = catalog.GetAllCuts();
            for (int i = 0; i < cuts.Count; i++)
                if (cuts[i] != null) list.Add(cuts[i]);
        }

        return list;
    }

    public bool IsPurchasable(ItemDataSO item)
    {
        if (item == null) return false;
        if (item is CoalSO) return true;
        if (item is MeatCutSO cut) return cut.isUnlocked;
        return false;
    }

    public int GetCartQty(ItemDataSO item)
    {
        if (item == null) return 0;
        return cart.TryGetValue(item, out int qty) ? qty : 0;
    }

    public IEnumerable<KeyValuePair<ItemDataSO, int>> EnumerateCart() => cart;

    public void SetQty(ItemDataSO item, int qty)
    {
        if (!IsPurchasable(item)) return;

        qty = Mathf.Max(0, qty);
        if (qty == 0) cart.Remove(item);
        else cart[item] = qty;

        OnCartChanged?.Invoke();
    }

    public void IncrementQty(ItemDataSO item, int delta = 1)
        => SetQty(item, GetCartQty(item) + delta);

    public void ClearCart()
    {
        if (cart.Count == 0) return;
        cart.Clear();
        OnCartChanged?.Invoke();
    }

    public float CartTotal()
    {
        float total = 0f;
        foreach (var kv in cart)
            if (kv.Key != null) total += kv.Key.basePrice * kv.Value;
        return total;
    }

    public float MoneyAfterPurchase()
        => wallet != null ? wallet.Money - CartTotal() : -CartTotal();

    public int CartCoalBags()
    {
        int bags = 0;
        foreach (var kv in cart)
            if (kv.Key is CoalSO) bags += kv.Value;
        return bags;
    }

    public int CartCoalUnits()
    {
        int units = 0;
        foreach (var kv in cart)
            if (kv.Key is CoalSO coal) units += coal.unitsPerBag * kv.Value;
        return units;
    }

    public List<MeatCutSO> GetLowStockCuts()
    {
        var result = new List<MeatCutSO>();
        if (catalog == null || coolerSystem == null || config == null) return result;

        var cuts = catalog.GetUnlockedCuts();
        for (int i = 0; i < cuts.Count; i++)
        {
            if (coolerSystem.GetCount(cuts[i]) <= config.lowStockThreshold)
                result.Add(cuts[i]);
        }
        return result;
    }

    public bool TryConfirmPurchase(out string message)
    {
        if (config == null || wallet == null || coolerSystem == null)
        {
            message = "Tienda no configurada correctamente.";
            Debug.Log(message);
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        if (CartCoalBags() < config.minCoalPurchase)
        {
            message = "Tenés que comprar al menos " + config.minCoalPurchase + " bolsa(s) de carbón.";
            Debug.Log(message);
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        float total = CartTotal();
        if (!wallet.CanAfford(total))
        {
            message = "Plata insuficiente. Total: $" + total.ToString("F0");
            Debug.Log(message);
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        wallet.TrySpend(total);

        foreach (var kv in cart)
        {
            ItemDataSO item = kv.Key;
            int qty = kv.Value;
            if (item == null || qty <= 0) continue;

            if (item is CoalSO coal)
                coolerSystem.Add(coal, coal.unitsPerBag * qty);
            else
                coolerSystem.Add(item, qty);
        }

        cart.Clear();
        OnCartChanged?.Invoke();

        message = "Compra realizada por $" + total.ToString("F0");
        Debug.Log(message);
        OnPurchaseResult?.Invoke(true, message);
        return true;
    }
}