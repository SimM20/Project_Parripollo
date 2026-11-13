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

    [Tooltip("Apagado, la tienda no exige los mínimos de la run.")]
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

    /// <summary>
    /// Cambió qué se vende (se compró una parada: hay cortes y mejoras nuevos, y la próxima
    /// parada es otra). La grilla tiene que rearmarse, no alcanza con refrescar las tarjetas.
    /// </summary>
    public event Action OnCatalogChanged;
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
        // Todas las compras (y sus rechazos) pasan por este evento: la vibración se engancha una sola vez.
        OnPurchaseResult += (ok, _) => GamepadHaptics.Play(ok ? HapticEvent.PurchaseDone : HapticEvent.ActionRefused);

        // Primera llamada gana: guarda la combustión original de los carbones antes de que
        // cualquier mejora la pise. Lo consume el reinicio de run desde la pantalla de derrota.
        RunStateReset.CaptureBaseline(catalog);

        // Desbloqueos al día con la parada alcanzada antes de listar nada.
        LocationProgression.ApplyUnlocks(catalog);

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
                    // La próxima parada va primera: es la compra que habilita el resto.
                    LocationSO nextStop = LocationProgression.GetNext(catalog);
                    if (nextStop != null) list.Add(nextStop);

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
        if (item is UpgradeSO up) return up.isUnlocked && !up.IsMaxed;
        if (item is LocationSO stop) return LocationProgression.IsNext(catalog, stop);
        return false;
    }

    /// <summary>La compra rellena el frasco: con el frasco lleno no hay nada que comprar.</summary>
    public bool IsToppingPurchasable(ToppingSO topping)
    {
        return topping != null && Toppings != null && !Toppings.IsFull(topping);
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

    /// <summary>Valor centinela: el item no tiene tope de cantidad.</summary>
    public const int UnlimitedQty = int.MaxValue;

    /// <summary>
    /// Maximo comprable de un item en una sola operacion. Las mejoras se compran de a un nivel
    /// y las paradas de a una.
    /// Fuente de verdad unica: la usan el carrito, la compra directa y los steppers de las celdas.
    /// </summary>
    public int GetMaxPurchaseQty(ItemDataSO item)
    {
        if (item is UpgradeSO || item is LocationSO) return 1;
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

    public IEnumerable<KeyValuePair<ToppingSO, int>> EnumerateToppingCart() => toppingCart;

    /// <summary>Hay algo en el carrito (items o toppings).</summary>
    public bool HasCartItems => cart.Count > 0 || toppingCart.Count > 0;

    // ── Agregar / quitar del carrito (capa uGUI) ────────────────────────
    /// <summary>
    /// Se pueden sumar <paramref name="qty"/> unidades al carrito: el item se vende, no pasa el tope,
    /// la plata alcanza para TODO el carrito y el carrito resultante no rompe los mínimos del próximo día.
    /// </summary>
    public bool CanAddToCart(ItemDataSO item, int qty, out bool blockedByMinimums)
    {
        blockedByMinimums = false;
        if (!IsPurchasable(item) || qty <= 0) return false;
        if (qty > GetMaxPurchaseQty(item) - GetCartQty(item)) return false;

        float cost = item.basePrice * qty;
        if (Wallet == null || !Wallet.CanAfford(CartTotal() + cost)) return false;

        int meat = item is MeatCutSO ? qty : 0;
        int coal = item is CoalSO coalItem ? Mathf.Max(1, coalItem.unitsPerBag) * qty : 0;

        if (CartKeepsRunAlive(cost, meat, coal)) return true;

        // Una parada se puede comprar aunque deje los mínimos fuera de alcance: es la apuesta del
        // jugador. La UI lo avisa (StopRisksRun / CartRisksRun) y pide confirmar antes de pagar.
        if (item is LocationSO) return true;

        // Con el carrito ya en riesgo por una parada, la carne y el carbón solo pueden ayudar.
        if ((meat > 0 || coal > 0) && !CartKeepsRunAlive(0f, 0, 0)) return true;

        blockedByMinimums = true;
        return false;
    }

    /// <summary>Hay una parada en el carrito: su compra puede saltearse el freno de los mínimos.</summary>
    private bool CartHasStop
    {
        get
        {
            foreach (var kv in cart)
                if (kv.Key is LocationSO) return true;
            return false;
        }
    }

    /// <summary>
    /// El carrito lleva una parada y, pagándolo, los mínimos del próximo día quedan fuera de
    /// alcance: la run se pierde apenas se paga. La UI tiene que avisarlo y pedir confirmación.
    /// </summary>
    public bool CartRisksRun => EnforceRunMinimums && CartHasStop && !CartKeepsRunAlive(0f, 0, 0);

    /// <summary>Comprar esa parada (o pagarla, si ya está en el carrito) deja la run sin los mínimos.</summary>
    public bool StopRisksRun(LocationSO stop)
    {
        if (!EnforceRunMinimums || stop == null) return false;

        float extra = GetCartQty(stop) > 0 ? 0f : stop.basePrice;
        return !CartKeepsRunAlive(extra, 0, 0);
    }

    /// <summary>Igual que la sobrecarga de <see cref="ItemDataSO"/>: un topping nunca aporta al mínimo.</summary>
    public bool CanAddToCart(ToppingSO topping, int qty, out bool blockedByMinimums)
    {
        blockedByMinimums = false;
        if (!IsToppingPurchasable(topping) || qty <= 0) return false;
        if (qty > GetMaxPurchaseQty(topping) - GetToppingCartQty(topping)) return false;

        float cost = topping.purchasePrice * qty;
        if (Wallet == null || !Wallet.CanAfford(CartTotal() + cost)) return false;

        blockedByMinimums = !CartKeepsRunAlive(cost, 0, 0);
        return !blockedByMinimums;
    }

    public bool TryAddToCart(ItemDataSO item, int qty)
    {
        if (!CanAddToCart(item, qty, out _)) return false;

        SetQty(item, GetCartQty(item) + qty);
        TutorialSignals.Raise(TutorialSignal.ShopCartAdded);
        return true;
    }

    public bool TryAddToCart(ToppingSO topping, int qty)
    {
        if (!CanAddToCart(topping, qty, out _)) return false;

        SetToppingQty(topping, GetToppingCartQty(topping) + qty);
        TutorialSignals.Raise(TutorialSignal.ShopCartAdded);
        return true;
    }

    /// <summary>Saca la línea entera. No pasa por <see cref="IsPurchasable"/>: lo que está en el carrito siempre se puede sacar.</summary>
    public void RemoveFromCart(ItemDataSO item)
    {
        if (item != null && cart.Remove(item)) OnCartChanged?.Invoke();
    }

    public void RemoveFromCart(ToppingSO topping)
    {
        if (topping != null && toppingCart.Remove(topping)) OnCartChanged?.Invoke();
    }

    /// <summary>
    /// El carrito se puede pagar: tiene algo, alcanza la plata y no rompe los mínimos del próximo
    /// día. Con una parada adentro se puede pagar aunque los rompa (ver <see cref="CartRisksRun"/>).
    /// </summary>
    public bool CanConfirmCart()
    {
        if (!HasCartItems || Wallet == null) return false;
        return Wallet.CanAfford(CartTotal()) && (CartKeepsRunAlive(0f, 0, 0) || CartHasStop);
    }

    /// <summary>Los mínimos siguen alcanzables pagando el carrito actual más un extra hipotético.</summary>
    private bool CartKeepsRunAlive(float extraCost, int extraMeat, int extraCoal)
    {
        if (!EnforceRunMinimums) return true;

        int meat = extraMeat;
        int coal = extraCoal;
        foreach (var kv in cart)
        {
            if (kv.Key is MeatCutSO) meat += kv.Value;
            else if (kv.Key is CoalSO coalItem) coal += Mathf.Max(1, coalItem.unitsPerBag) * kv.Value;
        }

        return BuildRunStatus(CartTotal() + extraCost, meat, coal).CanContinue;
    }

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

        // Sacar una línea puede dejar el resto del carrito fuera de los mínimos (p. ej. queda la
        // mejora y se fue el carbón): el guard se repite acá con el carrito entero.
        // Con una parada en el carrito el jugador ya fue avisado y confirmó: se paga igual.
        if (!CartKeepsRunAlive(0f, 0, 0) && !CartHasStop)
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

        // Items normales (cooler), mejoras y paradas
        bool catalogChanged = false;
        foreach (var kv in cart)
        {
            ItemDataSO item = kv.Key;
            int qty = kv.Value;
            if (item == null || qty <= 0) continue;

            catalogChanged |= Deliver(item, qty);
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
        if (catalogChanged) OnCatalogChanged?.Invoke();
        OnCartChanged?.Invoke();

        message = Loc.Format("shop.buy.done", total.ToString("F0"));
        OnPurchaseResult?.Invoke(true, message);
        TutorialSignals.Raise(TutorialSignal.ShopPurchased);
        return true;
    }

    /// <summary>
    /// Entrega una compra ya pagada: stock al cooler, nivel de mejora o avance de parada.
    /// Devuelve true si cambió qué se vende en la tienda (se compró una parada).
    /// </summary>
    private bool Deliver(ItemDataSO item, int qty)
    {
        if (item is CoalSO coal)
            Cooler.Add(coal, coal.unitsPerBag * qty);
        else if (item is UpgradeSO up)
            up.Purchase();
        else if (item is LocationSO stop)
            return LocationProgression.TryAdvanceTo(catalog, stop);
        else
            Cooler.Add(item, qty);

        return false;
    }

    public bool TryBuyNow(ItemDataSO item, int qty, out string message)
    {
        if (config == null || Wallet == null || Cooler == null)
        {
            message = Loc.Get("shop.buy.not_configured");
            OnPurchaseResult?.Invoke(false, message);
            return false;
        }

        if (!IsPurchasable(item))
        {
            message = Loc.Get("shop.buy.item_unavailable");
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

        if (Deliver(item, qty)) OnCatalogChanged?.Invoke();

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

        int coalStock = GetTotalCoalUnits() + coalGained;

        float cheapestCut = GetCheapestPurchasableCutPrice(out bool anyCut);

        bool anyCoal = config != null && config.coal != null;
        float coalBagPrice = anyCoal ? config.coal.basePrice : 0f;
        int coalUnitsPerBag = anyCoal ? config.coal.unitsPerBag : 1;

        return RunEconomyEvaluator.Evaluate(
            day, runDefeatConfig, meatStock, coalStock, money,
            cheapestCut, anyCut, anyCoal, coalBagPrice, coalUnitsPerBag
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
}
