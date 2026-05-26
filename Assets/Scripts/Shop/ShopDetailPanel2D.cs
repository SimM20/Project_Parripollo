using TMPro;
using UnityEngine;

public class ShopDetailPanel2D : MonoBehaviour
{
    [SerializeField] private ShopSystem shop;

    [Header("Display")]
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private TextMeshPro nameText;
    [SerializeField] private TextMeshPro priceText;
    [SerializeField] private TextMeshPro stockText;
    [SerializeField] private TextMeshPro descriptionText;
    [SerializeField] private GameObject lockedBadgeRoot;

    [Header("Stepper")]
    [SerializeField] private ShopButton2D minusButton;
    [SerializeField] private ShopButton2D plusButton;
    [SerializeField] private TextMeshPro qtyText;

    [Header("Empty State")]
    [SerializeField] private GameObject contentRoot;
    [SerializeField] private GameObject emptyRoot;

    private ItemDataSO currentItem;
    
    private ToppingSO currentTopping;


    void Awake()
    {
        if (minusButton != null) minusButton.OnClicked += OnMinusClicked;
        if (plusButton != null) plusButton.OnClicked += OnPlusClicked;
        ShowEmpty();
    }

    void OnDestroy()
    {
        if (minusButton != null) minusButton.OnClicked -= OnMinusClicked;
        if (plusButton != null) plusButton.OnClicked -= OnPlusClicked;
    }

    void OnEnable()
    {
        if (shop != null)
        {
            shop.OnCartChanged += RefreshQty;
            if (shop.Cooler != null) shop.Cooler.OnInventoryChanged += RefreshStock;
            if (shop.Toppings != null) shop.Toppings.OnStockChanged += RefreshStock;
        }
    }

    void OnDisable()
    {
        if (shop != null)
        {
            shop.OnCartChanged -= RefreshQty;
            if (shop.Cooler != null) shop.Cooler.OnInventoryChanged -= RefreshStock;
            if (shop.Toppings != null) shop.Toppings.OnStockChanged -= RefreshStock;
        }
    }
    public void Show(ItemDataSO item)
    {
        currentTopping = null;
        currentItem = item;
        if (item == null) { ShowEmpty(); return; }
        
        

        if (contentRoot != null) contentRoot.SetActive(true);
        if (emptyRoot != null) emptyRoot.SetActive(false);

        bool purchasable = shop.IsPurchasable(item);
        if (lockedBadgeRoot != null) lockedBadgeRoot.SetActive(!purchasable);

        if (item is CoalSO coal)
        {
            if (iconRenderer != null) iconRenderer.sprite = coal.coalSprite;
            if (nameText != null) nameText.text = coal.itemName;
            if (priceText != null) priceText.text = "$" + coal.basePrice.ToString("F0") + " / bolsa";
            if (descriptionText != null)
                descriptionText.text = "Trae " + coal.unitsPerBag + " unidades de carbón.";
        }
        else if (item is MeatCutSO cut)
        {
            if (iconRenderer != null) iconRenderer.sprite = cut.GetDefaultSprite();
            if (nameText != null) nameText.text = cut.cutName;
            if (priceText != null) priceText.text = "$" + cut.basePrice.ToString("F0");
            if (descriptionText != null)
                descriptionText.text = cut.isUnlocked ? "" : "Corte bloqueado.";
        }

        RefreshStock();
        RefreshQty();

        if (minusButton != null) minusButton.SetInteractable(purchasable);
        if (plusButton != null) plusButton.SetInteractable(purchasable);
    }

    private void ShowEmpty()
    {
        currentItem = null;
        currentTopping = null;
        if (contentRoot != null) contentRoot.SetActive(false);
        if (emptyRoot != null) emptyRoot.SetActive(true);
    }
    
    private void OnMinusClicked()
    {
        if (currentTopping != null) shop.IncrementToppingQty(currentTopping, -1);
        else if (currentItem != null) shop.IncrementQty(currentItem, -1);
    }

    private void OnPlusClicked()
    {
        if (currentTopping != null) shop.IncrementToppingQty(currentTopping, +1);
        else if (currentItem != null) shop.IncrementQty(currentItem, +1);
    }
    private void RefreshQty()
    {
        if (qtyText == null) return;

        if (currentTopping != null)
            qtyText.text = shop.GetToppingCartQty(currentTopping).ToString();
        else if (currentItem != null)
            qtyText.text = shop.GetCartQty(currentItem).ToString();
        else
            qtyText.text = "0";
    }

    private void RefreshStock()
    {
        if (stockText == null) return;
        if (shop == null) return;

        int stock = 0;
        if (currentTopping != null && shop.Toppings != null)
            stock = shop.Toppings.GetCount(currentTopping);
        else if (currentItem != null && shop.Cooler != null)
            stock = shop.Cooler.GetCount(currentItem);

        stockText.text = "Stock actual: " + stock;
    }
    
    public void ShowTopping(ToppingSO topping)
    {
        currentItem = null;
        currentTopping = topping;

        if (topping == null) { ShowEmpty(); return; }

        if (contentRoot != null) contentRoot.SetActive(true);
        if (emptyRoot != null) emptyRoot.SetActive(false);

        bool purchasable = shop.IsToppingPurchasable(topping);
        if (lockedBadgeRoot != null) lockedBadgeRoot.SetActive(!purchasable);

        if (iconRenderer != null) iconRenderer.sprite = topping.toppingSprite;
        if (nameText != null) nameText.text = topping.toppingName;
        if (priceText != null) priceText.text = "$" + topping.purchasePrice.ToString("F0");
        if (descriptionText != null) descriptionText.text = "";

        RefreshStock();
        RefreshQty();

        if (minusButton != null) minusButton.SetInteractable(purchasable);
        if (plusButton != null) plusButton.SetInteractable(purchasable);
    }
    
}