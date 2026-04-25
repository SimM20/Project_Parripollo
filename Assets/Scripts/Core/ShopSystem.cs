using UnityEngine;

/// <summary>
/// Minimal shop logic layer for post-day purchases.
/// No UI is created here — logic only, testable via Inspector context menus.
/// Rules:
///   - Cuts are bought by unit.
///   - Coal is bought by bag (each bag = CoalStockSystem.CoalUnitsPerBag units).
///   - Player cannot leave the shop without buying at least 1 coal bag.
///   - New purchases add to leftover stock (do not replace it).
///   - No maximum storage limit for cuts or coal.
/// </summary>
public class ShopSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DayManager dayManager;
    [SerializeField] private CoolerSystem coolerSystem;
    [SerializeField] private CoalStockSystem coalStockSystem;
    [SerializeField] private FoodCatalogSO catalog;

    [Header("Prices")]
    [Tooltip("Price per coal bag. Configure in Inspector.")]
    [SerializeField] private float coalBagPrice = 50f; // [PLACEHOLDER]

    // Session tracking: bags bought this shop session (reset on OpenShop)
    private int bagsBoughtThisSession = 0;
    private bool shopOpen = false;

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>Opens the shop for a new purchase session.</summary>
    public void OpenShop()
    {
        shopOpen = true;
        bagsBoughtThisSession = 0;
        Debug.Log("[ShopSystem] Tienda abierta. Dinero disponible: $" + (dayManager != null ? dayManager.AvailableMoney : 0));
    }

    /// <summary>
    /// Buys a number of units of a cut and adds them to CoolerSystem stock.
    /// Returns true if purchase succeeded.
    /// </summary>
    public bool BuyCut(MeatCutSO cut, int amount)
    {
        if (!shopOpen)
        {
            Debug.LogWarning("[ShopSystem] Intento de compra con la tienda cerrada.");
            return false;
        }

        if (cut == null || amount <= 0) return false;

        float totalCost = cut.price * amount;

        if (dayManager == null || !dayManager.TrySpend(totalCost))
        {
            Debug.Log("[ShopSystem] Fondos insuficientes para comprar " + amount + "x " + cut.cutName);
            return false;
        }

        coolerSystem.Add(cut, amount);
        Debug.Log("[ShopSystem] Comprado: " + amount + "x " + cut.cutName + " por $" + totalCost
            + " | Restante: $" + dayManager.AvailableMoney);
        return true;
    }

    /// <summary>
    /// Buys a number of coal bags and adds them to CoalStockSystem.
    /// Returns true if purchase succeeded.
    /// </summary>
    public bool BuyCoal(int bags)
    {
        if (!shopOpen)
        {
            Debug.LogWarning("[ShopSystem] Intento de compra con la tienda cerrada.");
            return false;
        }

        if (bags <= 0) return false;

        float totalCost = coalBagPrice * bags;

        if (dayManager == null || !dayManager.TrySpend(totalCost))
        {
            Debug.Log("[ShopSystem] Fondos insuficientes para comprar " + bags + " bolsa(s) de carbon.");
            return false;
        }

        coalStockSystem.AddBags(bags);
        bagsBoughtThisSession += bags;
        Debug.Log("[ShopSystem] Comprado: " + bags + " bolsa(s) de carbon por $" + totalCost
            + " | Restante: $" + dayManager.AvailableMoney);
        return true;
    }

    /// <summary>
    /// Returns true if the player is allowed to leave the shop.
    /// Rule: at least 1 coal bag must have been purchased this session.
    /// </summary>
    public bool CanLeaveShop()
    {
        return bagsBoughtThisSession >= 1;
    }

    /// <summary>
    /// Finalizes the shop session and triggers the start of a new day.
    /// Only succeeds if CanLeaveShop() is true.
    /// </summary>
    public bool FinalizePurchase()
    {
        if (!CanLeaveShop())
        {
            Debug.Log("[ShopSystem] No se puede salir de la tienda sin comprar al menos 1 bolsa de carbon.");
            return false;
        }

        shopOpen = false;
        bagsBoughtThisSession = 0;

        if (dayManager != null)
            dayManager.StartNewDay();

        Debug.Log("[ShopSystem] Compra finalizada. Nuevo dia iniciado.");
        return true;
    }

    /// <summary>Returns the price for a single coal bag.</summary>
    public float CoalBagPrice => coalBagPrice;

    /// <summary>Returns how many bags were bought in the current session.</summary>
    public int BagsBoughtThisSession => bagsBoughtThisSession;

    // ── Debug ───────────────────────────────────────────────────────────────

    [ContextMenu("Debug: Open Shop")]
    public void DebugOpenShop() => OpenShop();

    [ContextMenu("Debug: Buy 1 Coal Bag")]
    public void DebugBuy1CoalBag() => BuyCoal(1);

    [ContextMenu("Debug: Finalize Purchase")]
    public void DebugFinalize() => FinalizePurchase();
}
