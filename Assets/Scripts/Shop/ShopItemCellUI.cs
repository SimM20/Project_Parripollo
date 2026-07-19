using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopItemCellUI : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Image lockedOverlay;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private TextMeshProUGUI qtyText;
    [SerializeField] private TextMeshProUGUI subtotalText;

    [Header("Buttons")]
    [SerializeField] private Button minusButton;
    [SerializeField] private Button plusButton;
    [SerializeField] private Button buyButton;

    [Header("Colors")]
    [SerializeField] private Color normalIconColor = Color.white;
    [SerializeField] private Color lockedIconColor = new Color(1f, 1f, 1f, 0.35f);

    private ItemDataSO item;
    private ToppingSO toppingItem;
    private ShopSystem shop;
    private int pendingQty = 1;

    void Awake()
    {
        if (minusButton != null) minusButton.onClick.AddListener(OnMinus);
        if (plusButton != null) plusButton.onClick.AddListener(OnPlus);
        if (buyButton != null) buyButton.onClick.AddListener(OnBuy);
    }

    void OnDestroy()
    {
        if (minusButton != null) minusButton.onClick.RemoveListener(OnMinus);
        if (plusButton != null) plusButton.onClick.RemoveListener(OnPlus);
        if (buyButton != null) buyButton.onClick.RemoveListener(OnBuy);
    }

    public void Bind(ItemDataSO data, ShopSystem shop)
    {
        this.item = data;
        this.toppingItem = null;
        this.shop = shop;
        pendingQty = 1;
        RefreshVisuals();
    }

    public void Bind(ToppingSO topping, ShopSystem shop)
    {
        this.item = null;
        this.toppingItem = topping;
        this.shop = shop;
        pendingQty = 1;
        RefreshVisuals();
    }

    public void RefreshVisuals()
    {
        if (shop == null) return;

        bool purchasable;
        Sprite icon;
        string name;
        string description = "";
        float price;

        if (toppingItem != null)
        {
            purchasable = shop.IsToppingPurchasable(toppingItem);
            icon = toppingItem.toppingSprite;
            name = toppingItem.toppingName;
            price = toppingItem.purchasePrice;
        }
        else if (item != null)
        {
            purchasable = shop.IsPurchasable(item);
            icon = ResolveIcon(item);
            name = ResolveName(item);
            description = ResolveDescription(item);
            price = item.basePrice;
        }
        else return;

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.color = purchasable ? normalIconColor : lockedIconColor;
        }
        if (lockedOverlay != null) lockedOverlay.enabled = !purchasable;
        if (nameText != null) nameText.text = name;
        if (descriptionText != null) descriptionText.text = description;
        if (priceText != null) priceText.text = $"${price:N0}";
        if (qtyText != null) qtyText.text = pendingQty.ToString();
        if (subtotalText != null) subtotalText.text = $"Subtotal: ${price * pendingQty:N0}";

        bool canAfford = shop.Wallet != null && shop.Wallet.CanAfford(price * pendingQty);
        if (minusButton != null) minusButton.interactable = purchasable && pendingQty > 1;
        if (plusButton != null) plusButton.interactable = purchasable;
        if (buyButton != null) buyButton.interactable = purchasable && canAfford;
    }

    private void OnMinus()
    {
        pendingQty = Mathf.Max(1, pendingQty - 1);
        RefreshVisuals();
    }

    private void OnPlus()
    {
        pendingQty++;
        RefreshVisuals();
    }

    private void OnBuy()
    {
        if (shop == null) return;

        if (toppingItem != null)
            shop.TryBuyToppingNow(toppingItem, pendingQty, out _);
        else if (item != null)
            shop.TryBuyNow(item, pendingQty, out _);

        pendingQty = 1;
        RefreshVisuals();
    }

    private static Sprite ResolveIcon(ItemDataSO item)
    {
        if (item is CoalSO coal) return coal.coalSprite;
        if (item is MeatCutSO cut) return cut.GetDefaultSprite();
        if (item is UpgradeSO up) return up.icon;
        return null;
    }

    private static string ResolveName(ItemDataSO item)
    {
        if (item == null) return "";
        if (item is MeatCutSO cut) return cut.cutName;
        return item.itemName;
    }

    private static string ResolveDescription(ItemDataSO item)
    {
        if (item is UpgradeSO up) return up.description ?? "";
        return "";
    }
}