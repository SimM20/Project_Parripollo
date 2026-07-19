using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ShopItemCell2D : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private SpriteRenderer selectionFrame;
    [SerializeField] private SpriteRenderer lockedOverlay;

    [Header("Text")]
    [SerializeField] private TextMeshPro nameText;
    [SerializeField] private TextMeshPro descriptionText;
    [SerializeField] private TextMeshPro priceText;
    [SerializeField] private TextMeshPro qtyText;
    [SerializeField] private TextMeshPro subtotalText;

    [Header("Buttons")]
    [SerializeField] private ShopButton2D minusButton;
    [SerializeField] private ShopButton2D plusButton;
    [SerializeField] private ShopButton2D buyButton;

    [Header("Colors")]
    [SerializeField] private Color normalIconColor = Color.white;
    [SerializeField] private Color lockedIconColor = new Color(1f, 1f, 1f, 0.35f);

    private ItemDataSO item;
    private ToppingSO toppingItem;
    private ShopSystem shop;
    private int pendingQty = 1;

    public ItemDataSO Item => item;
<<<<<<< Updated upstream
=======
    public ToppingSO ToppingItem => toppingItem;

    void Awake()
    {
        if (minusButton != null) minusButton.OnClicked += OnMinus;
        if (plusButton != null) plusButton.OnClicked += OnPlus;
        if (buyButton != null) buyButton.OnClicked += OnBuy;
    }

    void OnDestroy()
    {
        if (minusButton != null) minusButton.OnClicked -= OnMinus;
        if (plusButton != null) plusButton.OnClicked -= OnPlus;
        if (buyButton != null) buyButton.OnClicked -= OnBuy;
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
>>>>>>> Stashed changes

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

        if (iconRenderer != null)
        {
            iconRenderer.sprite = icon;
            iconRenderer.color = purchasable ? normalIconColor : lockedIconColor;
        }
        if (lockedOverlay != null) lockedOverlay.enabled = !purchasable;
        if (nameText != null) nameText.text = name;
        if (descriptionText != null) descriptionText.text = description;
        if (priceText != null) priceText.text = $"${price:F0}";
        if (qtyText != null) qtyText.text = pendingQty.ToString();
        if (subtotalText != null) subtotalText.text = $"Subtotal: ${price * pendingQty:F0}";

        bool canAfford = shop.Wallet != null && shop.Wallet.CanAfford(price * pendingQty);
        bool stepperOn = purchasable;
        if (minusButton != null) minusButton.SetInteractable(stepperOn && pendingQty > 1);
        if (plusButton != null) plusButton.SetInteractable(stepperOn);
        if (buyButton != null) buyButton.SetInteractable(stepperOn && canAfford);
    }

    public void SetSelected(bool selected)
    {
        if (selectionFrame != null) selectionFrame.enabled = selected;
    }

    private void OnMinus()
    {
<<<<<<< Updated upstream
        if (grid != null && item != null) grid.SelectItem(item);
    }

=======
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

>>>>>>> Stashed changes
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
        return ""; // Carbón y carne: descripciones opcionales, vacías por default
    }
}