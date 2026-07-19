using TMPro;
using UnityEngine;

public class ShopHeader2D : MonoBehaviour
{
    [SerializeField] private ShopSystem shop;
    [SerializeField] private TextMeshPro shopNameText;
    [SerializeField] private TextMeshPro moneyText;
    [SerializeField] private TextMeshPro statusText;
    [SerializeField] private string shopName = "Parrilla 40";

    private bool started;

    void OnEnable()
    {
        if (shop != null)
        {
            shop.OnPurchaseResult += HandleResult;
            if (shop.Wallet != null) shop.Wallet.OnMoneyChanged += OnMoneyChanged;
        }
        if (started) Refresh();
    }

    void Start()
    {
        started = true;
        Refresh();
    }

    void OnDisable()
    {
        if (shop != null)
        {
            shop.OnPurchaseResult -= HandleResult;
            if (shop.Wallet != null) shop.Wallet.OnMoneyChanged -= OnMoneyChanged;
        }
    }

    private void OnMoneyChanged(float _) => Refresh();

    private void HandleResult(bool ok, string msg)
    {
        if (statusText != null) statusText.text = msg;
    }

    private void Refresh()
    {
<<<<<<< Updated upstream
        if (shop == null) return;
        
        if (shop.Wallet == null || shop.Cooler == null) return;

        var cfg = shop.Config;

        if (moneyText != null && shop.Wallet != null)
            moneyText.text = "Plata: $" + shop.Wallet.Money.ToString("F0");

        if (coalPriceText != null && cfg != null && cfg.coal != null)
            coalPriceText.text = "Carbón: $" + cfg.coal.basePrice.ToString("F0") + " / bolsa";

        if (coalStockText != null && cfg != null && cfg.coal != null && shop.Cooler != null)
            coalStockText.text = "Stock carbón: " + shop.Cooler.GetCount(cfg.coal) + " u.";

        if (estimatedConsumptionText != null && cfg != null)
            estimatedConsumptionText.text = "Consumo estimado: " + cfg.estimatedCoalConsumption + " u.";

        if (recommendedText != null && cfg != null)
            recommendedText.text = "Recomendado: " + cfg.recommendedCoalBags + " bolsa(s)";

        float total = shop.CartTotal();
        if (cartTotalText != null)
            cartTotalText.text = "Total carrito: $" + total.ToString("F0");

        float after = shop.MoneyAfterPurchase();
        if (afterPurchaseText != null)
        {
            afterPurchaseText.text = "Te queda: $" + after.ToString("F0");
            afterPurchaseText.color = after < 0f ? warningColor : normalColor;
        }

        RefreshLowStock();

        if (confirmButton != null && cfg != null)
        {
            bool canBuy = shop.CartCoalBags() >= cfg.minCoalPurchase
                          && shop.MoneyAfterPurchase() >= 0f;
            confirmButton.SetInteractable(canBuy);
        }
    }

    private void RefreshLowStock()
    {
        if (lowStockText == null) return;

        var low = shop.GetLowStockCuts();
        if (low.Count == 0) { lowStockText.text = ""; return; }

        var sb = new StringBuilder("Stock bajo: ");
        for (int i = 0; i < low.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(low[i].cutName);
        }
        lowStockText.text = sb.ToString();
=======
        if (shopNameText != null) shopNameText.text = shopName;
        if (moneyText != null && shop != null && shop.Wallet != null)
            moneyText.text = $"${shop.Wallet.Money:F0}";
>>>>>>> Stashed changes
    }
}