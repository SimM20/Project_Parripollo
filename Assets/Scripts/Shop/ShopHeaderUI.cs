using UnityEngine;
using TMPro;

public class ShopHeaderUI : MonoBehaviour
{
    [SerializeField] private ShopSystem shop;
    [SerializeField] private TextMeshProUGUI shopNameText;
    [SerializeField] private TextMeshProUGUI moneyText;
    [SerializeField] private TextMeshProUGUI totalCoalText;   // ← NUEVO
    [Tooltip("Cartel con los clientes que pasaron por el local en la jornada que acaba de terminar.")]
    [SerializeField] private TextMeshProUGUI customersTodayText;
    [Tooltip("Nombre del local: es la marca, no se traduce.")]
    [SerializeField] private string shopName = "LA PARRILLA DE DON COCO";

    private bool started;

    void OnEnable()
    {
        Loc.OnTextsChanged += Refresh;
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
        Loc.OnTextsChanged -= Refresh;
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
            totalCoalText.text = Loc.Format("shop.header.coal", shop.GetTotalCoalUnits());

        // Clientes de la jornada que acaba de terminar (lo dejó CustomerSystem en DayStats).
        if (customersTodayText != null)
        {
            int customers = DayStats.CustomersToday;
            customersTodayText.text = Loc.Format(customers == 1 ? "shop.header.customers.one" : "shop.header.customers", customers);
        }
    }
}