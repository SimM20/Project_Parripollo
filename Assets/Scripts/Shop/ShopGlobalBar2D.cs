using System.Text;
using TMPro;
using UnityEngine;

public class ShopGlobalBar2D : MonoBehaviour
{
    [SerializeField] private ShopSystem shop;

    [Header("Texts")]
    [SerializeField] private TextMeshPro moneyText;
    [SerializeField] private TextMeshPro coalPriceText;
    [SerializeField] private TextMeshPro coalStockText;
    [SerializeField] private TextMeshPro estimatedConsumptionText;
    [SerializeField] private TextMeshPro recommendedText;
    [SerializeField] private TextMeshPro cartTotalText;
    [SerializeField] private TextMeshPro afterPurchaseText;
    [SerializeField] private TextMeshPro statusText;
    [SerializeField] private TextMeshPro lowStockText;

    [Header("Confirm")]
    [SerializeField] private ShopButton2D confirmButton;
    [SerializeField] private ShopButton2D continueButton;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color warningColor = new Color(1f, 0.5f, 0.4f);
    
    private bool started = false;

    void Awake()
    {
        if (confirmButton != null) confirmButton.OnClicked += OnConfirmClicked;
        
        if (continueButton != null) continueButton.OnClicked += OnContinueClicked;
    }

    void OnDestroy()
    {
        if (confirmButton != null) confirmButton.OnClicked -= OnConfirmClicked;
        
        if (continueButton != null) continueButton.OnClicked -= OnContinueClicked;
    }

    void OnEnable()
    {
        if (shop == null) return;

        shop.OnCartChanged += Refresh;
        shop.OnPurchaseResult += HandleResult;

        if (shop.Wallet != null) shop.Wallet.OnMoneyChanged += OnMoneyChanged;
        if (shop.Cooler != null) shop.Cooler.OnInventoryChanged += Refresh;

        if (started) Refresh();
    }
    void Start()
    {
        started = true;
        Refresh();
    }

    void OnDisable()
    {
        if (shop == null) return;

        shop.OnCartChanged -= Refresh;
        shop.OnPurchaseResult -= HandleResult;

        if (shop.Wallet != null) shop.Wallet.OnMoneyChanged -= OnMoneyChanged;
        if (shop.Cooler != null) shop.Cooler.OnInventoryChanged -= Refresh;
    }
    private void OnMoneyChanged(float _) => Refresh();

    private void OnConfirmClicked()
    {
        Debug.Log("Confirmed Button Clicked");
        shop.TryConfirmPurchase(out _);
    }

    private void OnContinueClicked()
    {
        SceneManagementUtils.LoadSceneByName("GameScene");
    }

    private void HandleResult(bool success, string message)
    {
        if (statusText == null) return;
        statusText.text = message;
        statusText.color = success ? normalColor : warningColor;
    }

    private void Refresh()
    {
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
    }
}