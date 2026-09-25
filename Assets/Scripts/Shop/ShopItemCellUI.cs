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

    [Tooltip("Cuánto tiene ya el jugador de este item (o el nivel, si es una mejora).")]
    [SerializeField] private TextMeshProUGUI stockText;

    [Header("Formatos")]
    // En mayúsculas: la tienda usa Bungee, que es una tipografía de titulares.
    [SerializeField] private string stockFormat = "TENÉS: {0}";
    [SerializeField] private string coalStockFormat = "TENÉS: {0} U.";
    [SerializeField] private string coalNameFormat = "{0} x{1}";
    // La descripción va en Nunito (texto de cuerpo), no en Bungee: minúsculas normales.
    [SerializeField] private string coalBagFormat = "Bolsa de {0} unidades";
    [SerializeField] private string upgradeLevelFormat = "NIVEL {0}/{1}";
    [Tooltip("Solo se muestra comprando más de una unidad: con una, el total es el precio.")]
    [SerializeField] private string subtotalFormat = "Total: ${0:N0}";

    [Header("Buttons")]
    [Tooltip("Contenedor de −/cantidad/+. Se oculta en los items que se compran de a uno (mejoras).")]
    [SerializeField] private GameObject stepperRoot;
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

    private int MaxQty => (shop != null && item != null) ? shop.GetMaxPurchaseQty(item) : ShopSystem.UnlimitedQty;

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

        pendingQty = Mathf.Clamp(pendingQty, 1, MaxQty);

        bool purchasable;
        Sprite icon;
        string name;
        string description = "";
        string stock;
        float price;

        if (toppingItem != null)
        {
            purchasable = shop.IsToppingPurchasable(toppingItem);
            icon = toppingItem.toppingSprite;
            name = toppingItem.toppingName;
            price = toppingItem.purchasePrice;
            stock = string.Format(stockFormat, shop.Toppings != null ? shop.Toppings.GetCount(toppingItem) : 0);
        }
        else if (item != null)
        {
            purchasable = shop.IsPurchasable(item);
            icon = ResolveIcon(item);
            name = ResolveName(item);
            description = ResolveDescription(item);
            price = item.basePrice;
            stock = ResolveStock(item);
        }
        else return;

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.color = purchasable ? normalIconColor : lockedIconColor;
            // Sin sprite, un Image dibuja un cuadrado blanco: mejor no mostrar nada.
            iconImage.enabled = icon != null;
        }
        if (lockedOverlay != null) lockedOverlay.enabled = !purchasable;
        if (nameText != null) nameText.text = name;
        if (descriptionText != null) descriptionText.text = description;
        if (stockText != null) stockText.text = stock;
        if (priceText != null) priceText.text = $"${price:N0}";
        if (qtyText != null) qtyText.text = pendingQty.ToString();
        if (subtotalText != null) subtotalText.text = pendingQty > 1 ? string.Format(subtotalFormat, price * pendingQty) : "";
        if (stepperRoot != null) stepperRoot.SetActive(MaxQty > 1);

        bool canAfford = shop.Wallet != null && shop.Wallet.CanAfford(price * pendingQty);

        // Spec punto 7: no se permite gastar la plata que hace falta para el mínimo del
        // próximo día. Acá solo se apaga el botón; el guard real vive en ShopSystem.
        bool allowedByMinimums = toppingItem != null
            ? shop.IsPurchaseAllowedByRunMinimums(toppingItem, pendingQty)
            : shop.IsPurchaseAllowedByRunMinimums(item, pendingQty);

        SetInteractable(minusButton, purchasable && pendingQty > 1);
        SetInteractable(plusButton, purchasable && pendingQty < MaxQty);
        SetInteractable(buyButton, purchasable && canAfford && allowedByMinimums);
    }

    // El ColorTint del Button solo oscurece la chapa: el texto también se apaga, así un botón
    // deshabilitado no se confunde con uno que tiene el mouse encima.
    private static void SetInteractable(Button button, bool interactable)
    {
        if (button == null) return;
        button.interactable = interactable;

        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.alpha = interactable ? 1f : 0.45f;
    }

    private void OnMinus()
    {
        pendingQty = Mathf.Max(1, pendingQty - 1);
        RefreshVisuals();
    }

    private void OnPlus()
    {
        pendingQty = Mathf.Min(pendingQty + 1, MaxQty);
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

    private string ResolveName(ItemDataSO item)
    {
        if (item == null) return "";
        if (item is MeatCutSO cut) return cut.cutName;
        if (item is CoalSO coal && coal.unitsPerBag > 1)
            return string.Format(coalNameFormat, coal.itemName, coal.unitsPerBag);
        return item.itemName;
    }

    private string ResolveDescription(ItemDataSO item)
    {
        // Una compra de carbón suma unitsPerBag unidades al cooler, no una.
        if (item is CoalSO coal)
            return coal.unitsPerBag > 1 ? string.Format(coalBagFormat, coal.unitsPerBag) : "";

        if (item is UpgradeSO up)
        {
            string text = up.description ?? "";

            // Las mejoras de varios niveles muestran en que nivel van. Si la celda tiene
            // línea de stock, el nivel va ahí (ResolveStock) y no se repite acá.
            if (stockText == null && up.MaxLevel > 1)
            {
                string level = FormatUpgradeLevel(up);
                text = string.IsNullOrEmpty(text) ? level : text + System.Environment.NewLine + level;
            }

            return text;
        }
        return "";
    }

    private string ResolveStock(ItemDataSO item)
    {
        // Una mejora no se acumula en el cooler: lo que "se tiene" es su nivel.
        if (item is UpgradeSO up) return FormatUpgradeLevel(up);

        int count = shop.Cooler != null ? shop.Cooler.GetCount(item) : 0;
        return string.Format(item is CoalSO ? coalStockFormat : stockFormat, count);
    }

    private string FormatUpgradeLevel(UpgradeSO up)
        => string.Format(upgradeLevelFormat, up.CurrentLevel, up.MaxLevel);
}