using UnityEngine;
using TMPro;

public class ShopHeaderUI : MonoBehaviour
{
    [SerializeField] private ShopSystem shop;
    [SerializeField] private TextMeshProUGUI shopNameText;
    [SerializeField] private TextMeshProUGUI moneyText;
    [SerializeField] private TextMeshProUGUI totalCoalText;   // ← NUEVO
    [SerializeField] private string shopName = "LA PARRILLA DE DON COCO";

    private bool started;

    void OnEnable()
    {
        if (shop != null)
        {
            if (shop.Wallet != null) shop.Wallet.OnMoneyChanged += OnMoneyChanged;
            if (shop.Cooler != null) shop.Cooler.OnInventoryChanged += Refresh;   // ← NUEVO
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
            if (shop.Wallet != null) shop.Wallet.OnMoneyChanged -= OnMoneyChanged;
            if (shop.Cooler != null) shop.Cooler.OnInventoryChanged -= Refresh;   // ← NUEVO
        }
    }

    private void OnMoneyChanged(float _) => Refresh();

    private void Refresh()
    {
        if (shopNameText != null) shopNameText.text = shopName;

        if (moneyText != null && shop != null && shop.Wallet != null)
            moneyText.text = $"${shop.Wallet.Money:N0}";

        if (totalCoalText != null && shop != null)
            totalCoalText.text = $"Carbon: {shop.GetTotalCoalUnits()} u.";
    }
}