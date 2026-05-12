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
        if (grid != null && item != null) grid.SelectItem(item);
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