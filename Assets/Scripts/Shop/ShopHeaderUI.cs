using UnityEngine;
using TMPro;

public class ShopHeaderUI : MonoBehaviour
{
    [SerializeField] private ShopSystem shop;
    [SerializeField] private TextMeshProUGUI shopNameText;
    [SerializeField] private TextMeshProUGUI moneyText;
    [SerializeField] private string shopName = "LA PARRILLA DE DON COCO";

    private bool started;

    void OnEnable()
    {
        if (shop != null && shop.Wallet != null)
            shop.Wallet.OnMoneyChanged += OnMoneyChanged;
        if (started) Refresh();
    }

    void Start()
    {
        started = true;
        Refresh();
    }

    void OnDisable()
    {
        if (shop != null && shop.Wallet != null)
            shop.Wallet.OnMoneyChanged -= OnMoneyChanged;
    }

    private void OnMoneyChanged(float _) => Refresh();

    private void Refresh()
    {
        if (shopNameText != null) shopNameText.text = shopName;
        if (moneyText != null && shop != null && shop.Wallet != null)
            moneyText.text = $"${shop.Wallet.Money:N0}";
    }
}