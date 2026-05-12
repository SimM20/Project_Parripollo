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
        }
    }

    void OnDisable()
    {
        if (shop != null)
        {
            shop.OnCartChanged -= RefreshQty;
            if (shop.Cooler != null) shop.Cooler.OnInventoryChanged -= RefreshStock;
        }
    }

    public void Show(ItemDataSO item)
    {
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
        if (contentRoot != null) contentRoot.SetActive(false);
        if (emptyRoot != null) emptyRoot.SetActive(true);
    }

    private void OnMinusClicked()
    {
        Debug.Log("Minus Button Clicked");
        if (currentItem == null) return;
        Debug.Log("Minus Button Entered");
        shop.IncrementQty(currentItem, -1);
    }

    private void OnPlusClicked()
    {
        Debug.Log("Plus Button Clicked");
        if (currentItem == null) return;
        Debug.Log("Plus Button Entered");
        shop.IncrementQty(currentItem, +1);
    }

    private void RefreshQty()
    {
        if (currentItem == null || qtyText == null) return;
        qtyText.text = shop.GetCartQty(currentItem).ToString();
    }

    private void RefreshStock()
    {
        if (currentItem == null || stockText == null) return;
        if (shop == null || shop.Cooler == null) return;
        stockText.text = "Stock actual: " + shop.Cooler.GetCount(currentItem);
    }
    
}