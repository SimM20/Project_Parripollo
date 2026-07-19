using System;
using System.Collections.Generic;
using UnityEngine;

public class ShopSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShopConfigSO config;
    [SerializeField] private FoodCatalogSO catalog;
    
    // Carrito paralelo para toppings
    private readonly Dictionary<ToppingSO, int> toppingCart = new Dictionary<ToppingSO, int>();

    public ToppingStock Toppings => ToppingStock.Instance;
    public ShopConfigSO Config => config;
    public FoodCatalogSO Catalog => catalog;
    public PlayerWallet Wallet => PlayerWallet.Instance;
    public CoolerSystem Cooler => CoolerSystem.Instance;

    private readonly Dictionary<ItemDataSO, int> cart = new Dictionary<ItemDataSO, int>();

    public ShopTabType CurrentTab { get; private set; } = ShopTabType.Coal;

    public event Action OnCartChanged;
    public event Action OnTabChanged;
    public event Action<bool, string> OnPurchaseResult;

    // ── Tabs ────────────────────────────────────────────────────────────
    public void SetTab(ShopTabType tab)
    {
        if (CurrentTab == tab) return;
        CurrentTab = tab;
        OnTabChanged?.Invoke();
    }

    // ── Items visibles según tab ────────────────────────────────────────
    public IReadOnlyList<ItemDataSO> GetItemsForCurrentTab()
        => GetItemsForTab(CurrentTab);

    public IReadOnlyList<ItemDataSO> GetItemsForTab(ShopTabType tab)
    {
        var list = new List<ItemDataSO>();

        switch (tab)
        {
            case ShopTabType.Coal:
                if (config != null && config.coal != null) list.Add(config.coal);
                break;

            case ShopTabType.Meat:
                if (catalog != null)
                {
                    var cuts = catalog.GetAllCuts();
                    for (int i = 0; i < cuts.Count; i++)
                        if (cuts[i] != null) list.Add(cuts[i]);
                }
                break;

            case ShopTabType.Upgrades:
                if (catalog != null)
                {
                    var upgrades = catalog.GetAllUpgrades();
                    for (int i = 0; i < upgrades.Count; i++)
                        if (upgrades[i] != null) list.Add(upgrades[i]);
                }
                break;
        }

        return list;
    }

    public IReadOnlyList<ToppingSO> GetToppings()
    {
        if (catalog == null) return Array.Empty<ToppingSO>();
        return catalog.GetAvailableToppings();
    }

    public bool IsPurchasable(ItemDataSO item)
    {
        if (item == null) return false;
        if (item is CoalSO) return true;
        if (item is MeatCutSO cut) return cut.isUnlocked;
        if (item is UpgradeSO up) return up.isUnlocked && !up.isPurchased;
        return false;
    }

    public bool IsToppingPurchasable(ToppingSO topping)
    {
        return topping != null;
    }

    // ── Sugerencia de carbón ────────────────────────────────────────────
    /// <summary>
    /// Unidades sugeridas de carbón = max(0, consumoPromedio - stockActual).
    /// </summary>
    public int GetSuggestedCoalUnits()
    {
        if (config == null || config.coal == null || Cooler == null) return 0;

        var tracker = CoalConsumptionTracker.Instance;
        float avgPerDay = tracker != null && tracker.DaysPlayed > 0
            ? tracker.AverageCoalPerDay
            : config.estimatedCoalConsumption;   // fallback al valor del GD si no hay datos

        int stock = Cooler.GetCount(config.coal);
        int needed = Mathf.CeilToInt(avgPerDay) - stock;
        return Mathf.Max(0, needed);
    }

    /// <summary>
    /// Convierte las unidades sugeridas a bolsas (redondeando hacia arriba).
    /// </summary>
    public int GetSuggestedCoalBags()
    {
        if (config == null || config.coal == null) return 0;
        int units = GetSuggestedCoalUnits();
        if (units == 0) return 0;
        int perBag = Mathf.Max(1, config.coal.unitsPerBag);
        return Mathf.CeilToInt((float)units / perBag);
    }

    // ── Estado del carrito ──────────────────────────────────────────────
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
        if (item is UpgradeSO && qty > 1) qty = 1;
        if (qty == 0) cart.Remove(item);
        else cart[item] = qty;

        OnCartChanged?.Invoke();
    }

    public void IncrementQty(ItemDataSO item, int delta = 1)
        => SetQty(item, GetCartQty(item) + delta);

    public int GetToppingCartQty(ToppingSO topping)
    {
        if (topping == null) return 0;
        return toppingCart.TryGetValue(topping, out int qty) ? qty : 0;
    }

    public void SetToppingQty(ToppingSO topping, int qty)
    {
        if (!IsToppingPurchasable(topping)) return;

        qty = Mathf.Max(0, qty);
        if (qty == 0) toppingCart.Remove(topping);
        else toppingCart[topping] = qty;

        OnCartChanged?.Invoke();
    }

    public void IncrementToppingQty(ToppingSO topping, int delta = 1)
        => SetToppingQty(topping, GetToppingCartQty(topping) + delta);

    public void ClearCart()
    {
        bool hadItems = cart.Count > 0 || toppingCart.Count > 0;
        cart.Clear();
        toppingCart.Clear();
        if (hadItems) OnCartChanged?.Invoke();
    }

    // ── Cálculos derivados ──────────────────────────────────────────────
    public float CartTotal()
    {
        float total = 0f;
        foreach (var kv in cart)
            if (kv.Key != null) total += kv.Key.basePrice * kv.Value;

        foreach (var kv in toppingCart)
            if (kv.Key != null) total += kv.Key.purchasePrice * kv.Value;

        return total;
    }
    public float MoneyAfterPurchase()
        => Wallet != null ? Wallet.Money - CartTotal() : -CartTotal();

    public int CartCoalBags()
    {
        int bags = 0;
        foreach (var kv in cart)
            if (kv.Key is CoalSO) bags += kv.Value;
        return bags;
    }

    // ── Avisos de stock bajo ────────────────────────────────────────────
    public List<MeatCutSO> GetLowStockCuts()
    {
        var result = new List<MeatCutSO>();
        if (catalog == null || Cooler == null || config == null) return result;

        var cuts = catalog.GetUnlockedCuts();
        for (int i = 0; i < cuts.Count; i++)
        {
            if (Cooler.GetCount(cuts[i]) <= config.lowStockThreshold)
                result.Add(cuts[i]);
        }
        return result;
    }

    // ── Transacción ─────────────────────────────────────────────────────
    public bool TryConfirmPurchase(out string message)
    {
        if (config == null || Wallet == null || Cooler == null)
        {
            message = "Tienda no configurada correctamente.";
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        if (cart.Count == 0 && toppingCart.Count == 0)
        {
            message = "El carrito está vacío.";
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        float total = CartTotal();
        if (!Wallet.CanAfford(total))
        {
            message = "Plata insuficiente. Total: $" + total.ToString("F0");
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        if (!Wallet.TrySpend(total))
        {
            message = "No se pudo procesar el pago.";
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        // Items normales (cooler)
        foreach (var kv in cart)
        {
            ItemDataSO item = kv.Key;
            int qty = kv.Value;
            if (item == null || qty <= 0) continue;

            if (item is CoalSO coal)
                Cooler.Add(coal, coal.unitsPerBag * qty);
            else if (item is UpgradeSO up)
                up.isPurchased = true;
            else
                Cooler.Add(item, qty);
        }

        // Toppings (stock separado)
        if (Toppings != null)
        {
            foreach (var kv in toppingCart)
            {
                if (kv.Key != null && kv.Value > 0)
                    Toppings.Add(kv.Key, kv.Value);
            }
        }

        cart.Clear();
        toppingCart.Clear();
        OnCartChanged?.Invoke();

        message = "Compra realizada por $" + total.ToString("F0");
        OnPurchaseResult?.Invoke(true, message);
        return true;
    }

    public bool TryBuyNow(ItemDataSO item, int qty, out string message)
    {
        if (config == null || Wallet == null || Cooler == null)
        {
            message = "Tienda no configurada correctamente.";
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        if (!IsPurchasable(item))
        {
            message = "Ese item no se puede comprar.";
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        qty = Mathf.Max(1, qty);
        if (item is UpgradeSO) qty = 1;

        float total = item.basePrice * qty;
        if (!Wallet.CanAfford(total))
        {
            message = "Plata insuficiente. Total: $" + total.ToString("F0");
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        if (!Wallet.TrySpend(total))
        {
            message = "No se pudo procesar el pago.";
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        if (item is CoalSO coal)
            Cooler.Add(coal, coal.unitsPerBag * qty);
        else if (item is UpgradeSO up)
            up.isPurchased = true;
        else
            Cooler.Add(item, qty);

        message = "Compra realizada por $" + total.ToString("F0");
        OnPurchaseResult?.Invoke(true, message);
        return true;
    }

    public bool TryBuyToppingNow(ToppingSO topping, int qty, out string message)
    {
        if (Wallet == null || Toppings == null)
        {
            message = "Tienda no configurada correctamente.";
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        if (!IsToppingPurchasable(topping))
        {
            message = "Ese topping no se puede comprar.";
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        qty = Mathf.Max(1, qty);
        float total = topping.purchasePrice * qty;
        if (!Wallet.CanAfford(total))
        {
            message = "Plata insuficiente. Total: $" + total.ToString("F0");
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        if (!Wallet.TrySpend(total))
        {
            message = "No se pudo procesar el pago.";
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        Toppings.Add(topping, qty);
        message = "Compra realizada por $" + total.ToString("F0");
        OnPurchaseResult?.Invoke(true, message);
        return true;
    }
}