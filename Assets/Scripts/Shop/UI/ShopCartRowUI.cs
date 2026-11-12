using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Una línea del carrito de la tienda: icono, nombre, cantidad con −/+, subtotal de la línea y
/// una X que la saca entera. Solo presentación: todo cambio pasa por <see cref="ShopSystem"/>,
/// que dispara <c>OnCartChanged</c> y <see cref="ShopCartUI"/> vuelve a bindear las líneas.
/// </summary>
public class ShopCartRowUI : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Image iconImage;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI qtyText;
    [SerializeField] private TextMeshProUGUI subtotalText;

    [Header("Buttons")]
    [Tooltip("Contenedor de −/cantidad/+. Se oculta en lo que se compra de a uno (mejoras, toppings).")]
    [SerializeField] private GameObject stepperRoot;
    [SerializeField] private Button minusButton;
    [SerializeField] private Button plusButton;
    [SerializeField] private Button removeButton;

    private ShopSystem shop;
    private ItemDataSO item;
    private ToppingSO topping;

    void Awake()
    {
        if (minusButton != null) minusButton.onClick.AddListener(OnMinus);
        if (plusButton != null) plusButton.onClick.AddListener(OnPlus);
        if (removeButton != null) removeButton.onClick.AddListener(OnRemove);
    }

    void OnDestroy()
    {
        if (minusButton != null) minusButton.onClick.RemoveListener(OnMinus);
        if (plusButton != null) plusButton.onClick.RemoveListener(OnPlus);
        if (removeButton != null) removeButton.onClick.RemoveListener(OnRemove);
    }

    public void Bind(ItemDataSO data, int qty, ShopSystem shop)
    {
        this.shop = shop;
        item = data;
        topping = null;

        bool canAddOne = shop.CanAddToCart(data, 1, out _);
        Apply(ShopItemCellUI.ResolveIcon(data), ShopItemCellUI.ResolveName(data), data.basePrice, qty,
              shop.GetMaxPurchaseQty(data), canAddOne);
    }

    public void Bind(ToppingSO data, int qty, ShopSystem shop)
    {
        this.shop = shop;
        item = null;
        topping = data;

        bool canAddOne = shop.CanAddToCart(data, 1, out _);
        Apply(data.toppingSprite, data.toppingName, data.purchasePrice, qty,
              shop.GetMaxPurchaseQty(data), canAddOne);
    }

    private void Apply(Sprite icon, string displayName, float price, int qty, int maxQty, bool canAddOne)
    {
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            // Sin sprite, un Image dibuja un cuadrado blanco: mejor no mostrar nada.
            iconImage.enabled = icon != null;
        }
        if (nameText != null) nameText.text = displayName;
        if (qtyText != null) qtyText.text = qty.ToString();
        if (subtotalText != null) subtotalText.text = $"${price * qty:N0}";

        // Igual que la tarjeta: lo que se compra de a uno no tiene −/+, solo la X.
        if (stepperRoot != null) stepperRoot.SetActive(maxQty > 1);
        SetInteractable(minusButton, qty > 1);
        SetInteractable(plusButton, canAddOne);
    }

    // Mismo criterio que ShopItemCellUI: el texto del botón deshabilitado también se apaga.
    private static void SetInteractable(Button button, bool interactable)
    {
        if (button == null) return;
        button.interactable = interactable;

        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.alpha = interactable ? 1f : 0.45f;
    }

    private void OnMinus()
    {
        if (shop == null) return;
        if (topping != null) shop.IncrementToppingQty(topping, -1);
        else if (item != null) shop.IncrementQty(item, -1);
    }

    private void OnPlus()
    {
        if (shop == null) return;
        if (topping != null) shop.TryAddToCart(topping, 1);
        else if (item != null) shop.TryAddToCart(item, 1);
    }

    private void OnRemove()
    {
        if (shop == null) return;
        if (topping != null) shop.RemoveFromCart(topping);
        else if (item != null) shop.RemoveFromCart(item);
    }
}
