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

    // Textos: claves shop.cell.* de las tablas de Loc. Stock y nivel van en mayúsculas porque
    // la tienda los dibuja en Bungee (titulares); la bolsa de carbón va en Nunito (cuerpo).

    [Header("Buttons")]
    [Tooltip("Contenedor de −/cantidad/+. Se oculta en los items que se compran de a uno (mejoras).")]
    [SerializeField] private GameObject stepperRoot;
    [SerializeField] private Button minusButton;
    [SerializeField] private Button plusButton;
    [SerializeField] private Button buyButton;

    [Tooltip("Texto del botón: \"Agregar\", o \"En el carrito\" cuando ya está el máximo (mejoras, toppings).")]
    [SerializeField] private LocalizedText buyLabel;

    [Header("Colors")]
    [SerializeField] private Color normalIconColor = Color.white;
    [SerializeField] private Color lockedIconColor = new Color(1f, 1f, 1f, 0.35f);

    private ItemDataSO item;
    private ToppingSO toppingItem;
    private ShopSystem shop;
    private int pendingQty = 1;

    private int MaxQty
    {
        get
        {
            if (shop == null) return ShopSystem.UnlimitedQty;
            if (toppingItem != null) return shop.GetMaxPurchaseQty(toppingItem);
            return item != null ? shop.GetMaxPurchaseQty(item) : ShopSystem.UnlimitedQty;
        }
    }

    public Button BuyButton => buyButton;

    /// <summary>El Agregar está habilitado: el carrito con esto se puede pagar y no rompe los mínimos del próximo día.</summary>
    public bool CanBuy { get; private set; }

    /// <summary>Se puede pagar, pero sumarlo al carrito dejaría los mínimos del próximo día fuera de alcance.</summary>
    public bool BlockedByMinimums { get; private set; }

    void Awake()
    {
        if (minusButton != null) minusButton.onClick.AddListener(OnMinus);
        if (plusButton != null) plusButton.onClick.AddListener(OnPlus);
        if (buyButton != null) buyButton.onClick.AddListener(OnAdd);
    }

    void OnDestroy()
    {
        if (minusButton != null) minusButton.onClick.RemoveListener(OnMinus);
        if (plusButton != null) plusButton.onClick.RemoveListener(OnPlus);
        if (buyButton != null) buyButton.onClick.RemoveListener(OnAdd);
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
            // Floor y no Round: un frasco al 99,6% no puede decir 100% con el Comprar habilitado.
            int fillPercent = shop.Toppings != null ? Mathf.FloorToInt(shop.Toppings.GetFillRatio(toppingItem) * 100f) : 0;
            stock = Loc.Format("shop.cell.sauce_fill", fillPercent);
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
        if (subtotalText != null) subtotalText.text = pendingQty > 1 ? Loc.Format("shop.cell.subtotal", price * pendingQty) : "";
        if (stepperRoot != null) stepperRoot.SetActive(MaxQty > 1);

        // Spec punto 7: no se permite gastar la plata que hace falta para el mínimo del próximo
        // día. Plata y mínimos se miden contra el carrito entero; el guard real vive en ShopSystem.
        bool blockedByMinimums;
        CanBuy = toppingItem != null
            ? shop.CanAddToCart(toppingItem, pendingQty, out blockedByMinimums)
            : shop.CanAddToCart(item, pendingQty, out blockedByMinimums);
        BlockedByMinimums = blockedByMinimums;

        int inCart = toppingItem != null ? shop.GetToppingCartQty(toppingItem) : shop.GetCartQty(item);
        if (buyLabel != null)
        {
            string key = inCart >= MaxQty ? "shop.cell.in_cart" : "shop.cell.add";
            if (buyLabel.Key != key) buyLabel.Key = key;
        }

        SetInteractable(minusButton, purchasable && pendingQty > 1);
        SetInteractable(plusButton, purchasable && pendingQty < MaxQty);
        SetInteractable(buyButton, CanBuy);
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
        int before = pendingQty;
        pendingQty = Mathf.Max(1, pendingQty - 1);
        RefreshVisuals();
        PunchIfChanged(qtyText, before != pendingQty, 0.18f);
    }

    private void OnPlus()
    {
        int before = pendingQty;
        pendingQty = Mathf.Min(pendingQty + 1, MaxQty);
        RefreshVisuals();
        PunchIfChanged(qtyText, before != pendingQty, 0.18f);
    }

    private void OnAdd()
    {
        if (shop == null) return;

        bool added = false;
        if (toppingItem != null)
            added = shop.TryAddToCart(toppingItem, pendingQty);
        else if (item != null)
            added = shop.TryAddToCart(item, pendingQty);

        pendingQty = 1;
        RefreshVisuals();

        // Solo si entró al carrito: el ícono salta (el stock cambia recién al pagar el carrito).
        PunchIfChanged(iconImage, added, 0.16f);
    }

    /// <summary>Golpe de escala de presentación (tiempo sin escalar). Textos e íconos sin raycast propio.</summary>
    private static void PunchIfChanged(Component target, bool changed, float amount)
    {
        if (changed && target != null)
            ScalePunch.Punch(target.transform, amount, 0.2f, true);
    }

    /// <summary>Icono de la tarjeta. Lo reusa la línea del carrito.</summary>
    public static Sprite ResolveIcon(ItemDataSO item)
    {
        if (item is CoalSO coal) return coal.coalSprite;
        if (item is MeatCutSO cut) return cut.GetDefaultSprite();
        if (item is UpgradeSO up) return up.icon;
        return null;
    }

    /// <summary>Nombre de la tarjeta (el carbón con el tamaño de bolsa). Lo reusa la línea del carrito.</summary>
    public static string ResolveName(ItemDataSO item)
    {
        if (item == null) return "";
        if (item is MeatCutSO cut) return cut.cutName;
        if (item is CoalSO coal && coal.unitsPerBag > 1)
            return Loc.Format("shop.cell.coal_name", coal.DisplayName, coal.unitsPerBag);
        return item.DisplayName;
    }

    private string ResolveDescription(ItemDataSO item)
    {
        // Una compra de carbón suma unitsPerBag unidades al cooler, no una.
        if (item is CoalSO coal)
            return coal.unitsPerBag > 1 ? Loc.Format("shop.cell.coal_bag", coal.unitsPerBag) : "";

        if (item is UpgradeSO up)
        {
            string text = up.DisplayDescription ?? "";

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
        return Loc.Format(item is CoalSO ? "shop.cell.coal_stock" : "shop.cell.stock", count);
    }

    private string FormatUpgradeLevel(UpgradeSO up)
        => Loc.Format("shop.cell.level", up.CurrentLevel, up.MaxLevel);
}