using System;
using System.Collections.Generic;
using UnityEngine;

public class ShopSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShopConfigSO config;
    [SerializeField] private FoodCatalogSO catalog;

    [Header("Derrota total de la run")]
    [Tooltip("Mínimos de recursos para arrancar el próximo día. Sin asignar, no se gatea nada.")]
    [SerializeField] private RunDefeatConfigSO runDefeatConfig;

    [Tooltip("Apagar en ShopTutorial: ahí los mínimos no aplican y bloquearían el tutorial.")]
    [SerializeField] private bool enforceRunMinimums = true;
    
    // Carrito paralelo para toppings
    private readonly Dictionary<ToppingSO, int> toppingCart = new Dictionary<ToppingSO, int>();

    public ToppingStock Toppings => ToppingStock.Instance;
    public ShopConfigSO Config => config;
    public FoodCatalogSO Catalog => catalog;
    public PlayerWallet Wallet => PlayerWallet.Instance;
    public CoolerSystem Cooler => CoolerSystem.Instance;
    public RunDefeatConfigSO RunDefeatConfig => runDefeatConfig;

    /// <summary>Si esta tienda aplica los mínimos del próximo día (display, gating y derrota).</summary>
    public bool EnforceRunMinimums => enforceRunMinimums && runDefeatConfig != null;

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
    private void Start()
    {
        // Primera llamada gana: guarda la combustión original de los carbones antes de que
        // cualquier mejora la pise. Lo consume el reinicio de run desde la pantalla de derrota.
        RunStateReset.CaptureBaseline(catalog);

        int currentNight =
            CoalConsumptionTracker.Instance != null
                ? CoalConsumptionTracker.Instance.CurrentNight
                : 1;

        Debug.Log(
            "[Shop] Tienda abierta. Noche actual: " + currentNight
        );

        if (catalog == null)
        {
            Debug.LogError("[Shop] No hay FoodCatalogSO asignado.");
            return;
        }

        var cuts = catalog.GetAllCuts();

        for (int i = 0; i < cuts.Count; i++)
        {
            MeatCutSO cut = cuts[i];

            if (cut == null)
                continue;

            Debug.Log(
                "[Shop] Corte: " + cut.cutName +
                " | isUnlocked: " + cut.isUnlocked +
                " | IsPurchasable: " + IsPurchasable(cut)
            );
        }
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

    /// <summary>
    /// Por qué un item no se puede comprar, sin contar la plata ni los mínimos del próximo día (esos
    /// dependen de la cantidad y los resuelve la compra). La tarjeta lo muestra con candado y leyenda.
    /// </summary>
    public enum PurchaseBlock
    {
        None,
        /// <summary>La mejora pide más reputación (<see cref="UpgradeSO.requiredReputationPerLevel"/>).</summary>
        Reputation,
        /// <summary>Todavía no se desbloqueó (un corte que llega en otra noche, una mejora apagada).</summary>
        Locked,
        /// <summary>La mejora ya está al máximo.</summary>
        Maxed,
        /// <summary>El frasco de salsa ya está lleno.</summary>
        JarFull,
        /// <summary>No es algo que venda la tienda.</summary>
        Unavailable
    }

    /// <summary>
    /// Ver <see cref="PurchaseBlock"/>. Con <see cref="PurchaseBlock.Reputation"/>,
    /// <paramref name="requiredReputation"/> es el nivel que falta alcanzar.
    /// </summary>
    public PurchaseBlock GetPurchaseBlock(ItemDataSO item, out int requiredReputation)
    {
        requiredReputation = 0;

        if (item == null) return PurchaseBlock.Unavailable;
        if (item is CoalSO) return PurchaseBlock.None;
        if (item is MeatCutSO cut) return cut.isUnlocked ? PurchaseBlock.None : PurchaseBlock.Locked;

        if (item is UpgradeSO up)
        {
            if (up.IsMaxed) return PurchaseBlock.Maxed;
            if (!up.isUnlocked) return PurchaseBlock.Locked;

            requiredReputation = up.RequiredReputationForNextLevel;
            return ReputationSystem.Level >= requiredReputation ? PurchaseBlock.None : PurchaseBlock.Reputation;
        }

        return PurchaseBlock.Unavailable;
    }

    /// <summary>La compra rellena el frasco: con el frasco lleno no hay nada que comprar.</summary>
    public PurchaseBlock GetPurchaseBlock(ToppingSO topping)
    {
        if (topping == null || Toppings == null) return PurchaseBlock.Unavailable;
        return Toppings.IsFull(topping) ? PurchaseBlock.JarFull : PurchaseBlock.None;
    }

    public bool IsPurchasable(ItemDataSO item) => GetPurchaseBlock(item, out _) == PurchaseBlock.None;

    public bool IsToppingPurchasable(ToppingSO topping) => GetPurchaseBlock(topping) == PurchaseBlock.None;

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

    /// <summary>Valor centinela: el item no tiene tope de cantidad.</summary>
    public const int UnlimitedQty = int.MaxValue;

    /// <summary>
    /// Maximo comprable de un item en una sola operacion. Las mejoras se compran de a un nivel.
    /// Fuente de verdad unica: la usan el carrito, la compra directa y los steppers de las celdas.
    /// </summary>
    public int GetMaxPurchaseQty(ItemDataSO item)
    {
        if (item is UpgradeSO) return 1;
        return UnlimitedQty;
    }

    /// <summary>Un topping es un relleno del frasco hasta el tope: comprar dos no suma nada.</summary>
    public int GetMaxPurchaseQty(ToppingSO topping) => 1;

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
        qty = Mathf.Min(qty, GetMaxPurchaseQty(item));
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

        qty = Mathf.Clamp(qty, 0, GetMaxPurchaseQty(topping));
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
            message = Loc.Get("shop.buy.not_configured");
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        if (cart.Count == 0 && toppingCart.Count == 0)
        {
            message = Loc.Get("shop.buy.empty_cart");
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        float total = CartTotal();
        if (!Wallet.CanAfford(total))
        {
            message = Loc.Format("shop.buy.not_enough_money", total.ToString("F0"));
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        if (!Wallet.TrySpend(total))
        {
            message = Loc.Get("shop.buy.payment_failed");
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
                up.Purchase();
            else
                Cooler.Add(item, qty);
        }

        // Toppings (stock separado): cada compra rellena el frasco
        if (Toppings != null)
        {
            foreach (var kv in toppingCart)
            {
                if (kv.Key != null && kv.Value > 0)
                    Toppings.Refill(kv.Key);
            }
        }

        cart.Clear();
        toppingCart.Clear();
        OnCartChanged?.Invoke();

        message = Loc.Format("shop.buy.done", total.ToString("F0"));
        OnPurchaseResult?.Invoke(true, message);
        TutorialSignals.Raise(TutorialSignal.ShopPurchased);
        return true;
    }

    public bool TryBuyNow(ItemDataSO item, int qty, out string message)
    {
        if (config == null || Wallet == null || Cooler == null)
        {
            message = Loc.Get("shop.buy.not_configured");
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        PurchaseBlock block = GetPurchaseBlock(item, out int requiredReputation);
        if (block != PurchaseBlock.None)
        {
            message = block == PurchaseBlock.Reputation
                ? Loc.Format("shop.buy.reputation_locked", requiredReputation)
                : Loc.Get("shop.buy.item_unavailable");
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        qty = Mathf.Clamp(qty, 1, GetMaxPurchaseQty(item));

        float total = item.basePrice * qty;
        if (!Wallet.CanAfford(total))
        {
            message = Loc.Format("shop.buy.not_enough_money", total.ToString("F0"));
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        if (!IsPurchaseAllowedByRunMinimums(item, qty))
        {
            message = Loc.Get("shop.buy.below_minimum");
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        if (!Wallet.TrySpend(total))
        {
            message = Loc.Get("shop.buy.payment_failed");
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        if (item is CoalSO coal)
            Cooler.Add(coal, coal.unitsPerBag * qty);
        else if (item is UpgradeSO up)
            up.Purchase();
        else
            Cooler.Add(item, qty);

        message = Loc.Format("shop.buy.done", total.ToString("F0"));
        OnPurchaseResult?.Invoke(true, message);
        TutorialSignals.Raise(TutorialSignal.ShopPurchased);
        return true;
    }
    
    /// <summary>
    /// Suma todas las unidades de carbón en el cooler, sin importar el tipo.
    /// </summary>
    public int GetTotalCoalUnits()
    {
        if (Cooler == null || catalog == null) return 0;

        int total = 0;
        foreach (var entry in Cooler.EnumerateStock())
        {
            if (entry.Key is CoalSO)
                total += entry.Value;
        }
        return total;
    }

    public bool TryBuyToppingNow(ToppingSO topping, int qty, out string message)
    {
        if (Wallet == null || Toppings == null)
        {
            message = Loc.Get("shop.buy.not_configured");
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        if (!IsToppingPurchasable(topping))
        {
            message = Loc.Get("shop.buy.topping_unavailable");
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        qty = Mathf.Clamp(qty, 1, GetMaxPurchaseQty(topping));
        float total = topping.purchasePrice * qty;
        if (!Wallet.CanAfford(total))
        {
            message = Loc.Format("shop.buy.not_enough_money", total.ToString("F0"));
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        if (!IsPurchaseAllowedByRunMinimums(topping, qty))
        {
            message = Loc.Get("shop.buy.below_minimum");
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        if (!Wallet.TrySpend(total))
        {
            message = Loc.Get("shop.buy.payment_failed");
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        Toppings.Refill(topping);
        message = Loc.Format("shop.buy.done", total.ToString("F0"));
        OnPurchaseResult?.Invoke(true, message);
        TutorialSignals.Raise(TutorialSignal.ShopPurchased);
        return true;
    }

    // ── Mínimos del próximo día / derrota total de la run ────────────────

    /// <summary>
    /// Suma todos los cortes del cooler, sin importar el tipo ni si están desbloqueados.
    /// El mínimo se compone con cualquier combinación: no hay mínimo individual por corte.
    /// Espejo de <see cref="GetTotalCoalUnits"/>.
    /// </summary>
    public int GetTotalMeatCuts()
    {
        if (Cooler == null) return 0;

        int total = 0;
        foreach (var entry in Cooler.EnumerateStock())
        {
            if (entry.Key is MeatCutSO)
                total += entry.Value;
        }
        return total;
    }

    /// <summary>
    /// Precio del corte comprable más barato: es el costo por unidad de carne que usa el
    /// evaluador para saber si el jugador todavía puede llegar al mínimo.
    /// </summary>
    public float GetCheapestPurchasableCutPrice(out bool anyAvailable)
    {
        anyAvailable = false;
        float cheapest = 0f;

        if (catalog == null) return 0f;

        var cuts = catalog.GetAllCuts();
        for (int i = 0; i < cuts.Count; i++)
        {
            MeatCutSO cut = cuts[i];
            if (cut == null || !IsPurchasable(cut)) continue;

            if (!anyAvailable || cut.basePrice < cheapest)
            {
                cheapest = cut.basePrice;
                anyAvailable = true;
            }
        }

        return cheapest;
    }

    /// <summary>Situación económica frente a los mínimos del próximo día, tal cual está ahora.</summary>
    public RunEconomyStatus GetRunStatus() => BuildRunStatus(0f, 0, 0);

    /// <summary>
    /// Arma la foto económica aplicando un delta hipotético. Con deltas en cero es el estado
    /// real; con los deltas de una compra sirve para saber si esa compra brickea la run.
    /// </summary>
    private RunEconomyStatus BuildRunStatus(float moneySpent, int meatGained, int coalGained)
    {
        // En EndScene el tracker ya sumó la jornada que terminó, así que CurrentNight es el
        // día que se intenta comenzar: exactamente la N de las fórmulas del spec.
        int day = CoalConsumptionTracker.Instance != null
            ? CoalConsumptionTracker.Instance.CurrentNight
            : 1;

        float money = (Wallet != null ? Wallet.Money : 0f) - moneySpent;

        int meatStock = GetTotalMeatCuts() + meatGained;

        int baseCoal = GetTotalCoalUnits();
        int coalStock = baseCoal + coalGained;

        // Comprar carbón por encima del tope del cooler tira la plata a la basura:
        // la simulación no debe contar unidades que el Add va a descartar.
        if (CoolerSystem.CoalStorageCap > 0 && coalStock > CoolerSystem.CoalStorageCap)
            coalStock = Mathf.Max(baseCoal, CoolerSystem.CoalStorageCap);

        float cheapestCut = GetCheapestPurchasableCutPrice(out bool anyCut);

        bool anyCoal = config != null && config.coal != null;
        float coalBagPrice = anyCoal ? config.coal.basePrice : 0f;
        int coalUnitsPerBag = anyCoal ? config.coal.unitsPerBag : 1;

        return RunEconomyEvaluator.Evaluate(
            day, runDefeatConfig, meatStock, coalStock, money,
            cheapestCut, anyCut, anyCoal, coalBagPrice, coalUnitsPerBag,
            CoolerSystem.CoalStorageCap
        );
    }

    /// <summary>
    /// False si comprar esto dejaría los mínimos del próximo día fuera de alcance. Comprar
    /// carne o carbón siempre acerca al mínimo, así que en la práctica solo bloquea mejoras
    /// y toppings (y compras de más de lo necesario).
    /// </summary>
    public bool IsPurchaseAllowedByRunMinimums(ItemDataSO item, int qty)
    {
        if (!EnforceRunMinimums || item == null) return true;

        qty = Mathf.Clamp(qty, 1, GetMaxPurchaseQty(item));
        float cost = item.basePrice * qty;

        int meatGained = 0;
        int coalGained = 0;

        if (item is CoalSO coal) coalGained = Mathf.Max(1, coal.unitsPerBag) * qty;
        else if (item is MeatCutSO) meatGained = qty;

        return BuildRunStatus(cost, meatGained, coalGained).CanContinue;
    }

    /// <summary>Igual que la sobrecarga de <see cref="ItemDataSO"/>: un topping nunca aporta al mínimo.</summary>
    public bool IsPurchaseAllowedByRunMinimums(ToppingSO topping, int qty)
    {
        if (!EnforceRunMinimums || topping == null) return true;

        return BuildRunStatus(topping.purchasePrice * Mathf.Max(1, qty), 0, 0).CanContinue;
    }

    // ── Locales (reputación) ────────────────────────────────────────────

    /// <summary>Por qué no se puede comprar el próximo local (ver <see cref="ReputationSystem.NextVenue"/>).</summary>
    public enum VenueBlock
    {
        None,
        /// <summary>Ya se tienen todos los locales.</summary>
        AllOwned,
        /// <summary>La reputación todavía no llega al nivel del local.</summary>
        Reputation,
        Money,
        /// <summary>Se puede pagar, pero dejaría los mínimos del próximo día fuera de alcance.</summary>
        Minimums,
        NotConfigured
    }

    public VenueBlock GetNextVenueBlock()
    {
        ReputationConfigSO.Venue next = ReputationSystem.NextVenue;

        if (next == null) return VenueBlock.AllOwned;
        if (ReputationSystem.Level < next.requiredLevel) return VenueBlock.Reputation;
        if (Wallet == null) return VenueBlock.NotConfigured;
        if (!Wallet.CanAfford(next.price)) return VenueBlock.Money;
        if (!IsVenuePurchaseAllowedByRunMinimums(next.price)) return VenueBlock.Minimums;
        return VenueBlock.None;
    }

    /// <summary>Mismo guard que las mejoras: un local no se paga con la plata del mínimo de mañana.</summary>
    public bool IsVenuePurchaseAllowedByRunMinimums(float price)
    {
        if (!EnforceRunMinimums) return true;

        return BuildRunStatus(price, 0, 0).CanContinue;
    }

    /// <summary>
    /// Compra el próximo local: cobra y le avisa a <see cref="ReputationSystem"/>, que levanta el tope
    /// de la reputación. Por ahora no hay escenarios nuevos, así que no cambia nada visual.
    /// </summary>
    public bool TryBuyNextVenue(out string message)
    {
        ReputationConfigSO.Venue next = ReputationSystem.NextVenue;
        VenueBlock block = GetNextVenueBlock();

        if (block != VenueBlock.None)
        {
            switch (block)
            {
                case VenueBlock.Reputation:
                    message = Loc.Format("shop.buy.reputation_locked", next.requiredLevel);
                    break;
                case VenueBlock.Money:
                    message = Loc.Format("shop.buy.not_enough_money", next.price.ToString("F0"));
                    break;
                case VenueBlock.Minimums:
                    message = Loc.Get("shop.buy.below_minimum");
                    break;
                case VenueBlock.NotConfigured:
                    message = Loc.Get("shop.buy.not_configured");
                    break;
                default:
                    message = Loc.Get("shop.buy.item_unavailable");
                    break;
            }

            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        if (!Wallet.TrySpend(next.price))
        {
            message = Loc.Get("shop.buy.payment_failed");
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        ReputationSystem.RegisterVenuePurchased();

        message = Loc.Format("shop.buy.done", next.price.ToString("F0"));
        OnPurchaseResult?.Invoke(true, message);
        TutorialSignals.Raise(TutorialSignal.ShopPurchased);
        return true;
    }
}
