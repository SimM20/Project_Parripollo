using System.Text;
using TMPro;
using UnityEngine;

public class ShopHeader2D : MonoBehaviour
{
    [SerializeField] private ShopSystem shop;
    [SerializeField] private TextMeshPro shopNameText;
    [SerializeField] private TextMeshPro moneyText;
    [SerializeField] private TextMeshPro coalPriceText;
    [SerializeField] private TextMeshPro coalStockText;
    [SerializeField] private TextMeshPro estimatedConsumptionText;
    [SerializeField] private TextMeshPro recommendedText;
    [SerializeField] private TextMeshPro cartTotalText;
    [SerializeField] private TextMeshPro afterPurchaseText;
    [SerializeField] private TextMeshPro lowStockText;
    [SerializeField] private TextMeshPro statusText;
    [SerializeField] private ShopButton2D confirmButton;
    [SerializeField] private string shopName = "Parrilla 40";
    [SerializeField] private Color warningColor = new Color(0.95f, 0.35f, 0.25f);
    [SerializeField] private Color normalColor = Color.white;

    private bool started;

    void OnEnable()
    {
        if (shop != null)
        {
            shop.OnPurchaseResult += HandleResult;
            shop.OnCartChanged += Refresh;
            if (shop.Wallet != null) shop.Wallet.OnMoneyChanged += OnMoneyChanged;
            if (shop.Cooler != null) shop.Cooler.OnInventoryChanged += Refresh;
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
            shop.OnCartChanged -= Refresh;
            if (shop.Wallet != null) shop.Wallet.OnMoneyChanged -= OnMoneyChanged;
            if (shop.Cooler != null) shop.Cooler.OnInventoryChanged -= Refresh;
        }
    }

    private void OnMoneyChanged(float _) => Refresh();

    private void HandleResult(bool ok, string msg)
    {
        if (statusText != null) statusText.text = msg;
    }

    private void Refresh()
    {
        if (shopNameText != null) shopNameText.text = shopName;
        if (shop == null) return;

        var cfg = shop.Config;

        if (moneyText != null && shop.Wallet != null)
            moneyText.text = "Plata: $" + shop.Wallet.Money.ToString("F0");

        if (coalPriceText != null)
        {
            if (cfg != null && cfg.coal != null)
                coalPriceText.text = "Carbón: $" + cfg.coal.basePrice.ToString("F0") + " / bolsa";
            else
                coalPriceText.text = "Carbón: -";
        }

        if (coalStockText != null)
        {
            if (cfg != null && cfg.coal != null && shop.Cooler != null)
                coalStockText.text = "Stock carbón: " + shop.Cooler.GetCount(cfg.coal) + " u.";
            else
                coalStockText.text = "Stock carbón: -";
        }

        if (estimatedConsumptionText != null)
        {
            if (cfg != null)
                estimatedConsumptionText.text = "Consumo estimado: " + cfg.estimatedCoalConsumption + " u.";
            else
                estimatedConsumptionText.text = "Consumo estimado: -";
        }

        if (recommendedText != null)
        {
            if (cfg != null)
                recommendedText.text = "Recomendado: " + cfg.recommendedCoalBags + " bolsa(s)";
            else
                recommendedText.text = "Recomendado: -";
        }

        float total = shop.CartTotal();
        if (cartTotalText != null)
            cartTotalText.text = "Total carrito: $" + total.ToString("F0");

        float after = shop.MoneyAfterPurchase();
        if (afterPurchaseText != null)
        {
            afterPurchaseText.text = "Te queda: $" + after.ToString("F0");
            afterPurchaseText.color = after < 0f ? warningColor : normalColor;
        }

        if (lowStockText != null)
        {
            var low = shop.GetLowStockCuts();
            if (low.Count == 0)
            {
                lowStockText.text = "";
            }
            else
            {
                var sb = new StringBuilder("Stock bajo: ");
                for (int i = 0; i < low.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(low[i].cutName);
                }
                lowStockText.text = sb.ToString();
            }
        }

        if (confirmButton != null)
        {
            bool canBuy = shop.MoneyAfterPurchase() >= 0f && shop.CartTotal() > 0f;
            confirmButton.SetInteractable(canBuy);
        }
    }
}