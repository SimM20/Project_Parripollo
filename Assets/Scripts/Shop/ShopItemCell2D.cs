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
    [SerializeField] private TextMeshPro itemText;
    [SerializeField] private TextMeshPro stockText;
    [SerializeField] private TextMeshPro cartBadgeText;
    [SerializeField] private GameObject cartBadgeRoot;

    [Header("Colors")]
    [SerializeField] private Color normalIconColor = Color.white;
    [SerializeField] private Color lockedIconColor = new Color(1f, 1f, 1f, 0.35f);

    private ItemDataSO item;
    private ShopGrid2D grid;

    public ItemDataSO Item => item;
    
    private ToppingSO toppingItem;   // null si es item normal
    public ToppingSO ToppingItem => toppingItem;

    public void Bind(ToppingSO topping, ShopGrid2D grid, ShopSystem shop)
    {
        this.item = null;
        this.toppingItem = topping;
        this.grid = grid;

        if (iconRenderer != null)
            iconRenderer.sprite = topping != null ? topping.toppingSprite : null;

        if (itemText != null)
            itemText.text = topping != null ? topping.toppingName : "";

        int currentStock = (topping != null && shop.Toppings != null)
            ? shop.Toppings.GetCount(topping)
            : 0;
        if (stockText != null)
            stockText.text = currentStock.ToString();

        bool purchasable = shop.IsToppingPurchasable(topping);
        if (iconRenderer != null)
            iconRenderer.color = purchasable ? normalIconColor : lockedIconColor;
        if (lockedOverlay != null)
            lockedOverlay.enabled = !purchasable;

        int cartQty = shop.GetToppingCartQty(topping);
        if (cartBadgeRoot != null) cartBadgeRoot.SetActive(cartQty > 0);
        if (cartBadgeText != null) cartBadgeText.text = "x" + cartQty;
    }

    public void Bind(ItemDataSO item, ShopGrid2D grid, ShopSystem shop)
    {
        this.item = item;
        this.grid = grid;

        if (iconRenderer != null)
            iconRenderer.sprite = ResolveIcon(item);

        if (itemText != null)
            itemText.text = ResolveName(item);
        
        if (stockText != null)
            stockText.text = shop.Cooler.GetCount(item).ToString();

        bool purchasable = shop.IsPurchasable(item);
        if (iconRenderer != null)
            iconRenderer.color = purchasable ? normalIconColor : lockedIconColor;
        if (lockedOverlay != null)
            lockedOverlay.enabled = !purchasable;

        int cartQty = shop.GetCartQty(item);
        if (cartBadgeRoot != null) cartBadgeRoot.SetActive(cartQty > 0);
        if (cartBadgeText != null) cartBadgeText.text = "x" + cartQty;
    }

    public void SetSelected(bool selected)
    {
        if (selectionFrame != null) selectionFrame.enabled = selected;
    }

    void OnMouseUpAsButton()
    {
        if (grid == null) return;

        if (toppingItem != null) grid.SelectTopping(toppingItem);
        else if (item != null) grid.SelectItem(item);
    }
    private static Sprite ResolveIcon(ItemDataSO item)
    {
        if (item is CoalSO coal) return coal.coalSprite;
        if (item is MeatCutSO cut) return cut.GetDefaultSprite();
        return null;
    }
    
    private static string ResolveName(ItemDataSO item)
    {
        if (item == null) return "";
        if (item is MeatCutSO cut) return cut.cutName;
        return item.itemName;
    }
}