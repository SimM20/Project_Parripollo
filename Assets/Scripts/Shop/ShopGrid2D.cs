using System.Collections.Generic;
using UnityEngine;

public class ShopGrid2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShopSystem shop;
    [SerializeField] private Transform cellsParent;
    [SerializeField] private GameObject cellPrefab;

    [Header("Layout")]
    [SerializeField] [Min(1)] private int columns = 4;
    [SerializeField] private Vector2 cellSpacing = new Vector2(1.2f, 1.4f);
    [SerializeField] private Vector2 originOffset = Vector2.zero;
    [SerializeField] private ShopDetailPanel2D detailPanel;

    private readonly List<ShopItemCell2D> cells = new List<ShopItemCell2D>();
    private bool started = false;
    private ItemDataSO selectedItem;
    private ToppingSO selectedTopping;


    void Start()
    {
        started = true;
        RebuildAll();
    }

    void OnEnable()
    {
        if (shop != null)
        {
            shop.OnCartChanged += RefreshAllCells;
            shop.OnTabChanged += OnTabChanged;
            if (shop.Cooler != null) shop.Cooler.OnInventoryChanged += RefreshAllCells;
            if (shop.Toppings != null) shop.Toppings.OnStockChanged += RefreshAllCells;
            if (shop.Wallet != null) shop.Wallet.OnMoneyChanged += OnMoneyChanged;
        }
        if (started) RebuildAll();
    }

    void OnDisable()
    {
        if (shop != null)
        {
            shop.OnCartChanged -= RefreshAllCells;
            shop.OnTabChanged -= OnTabChanged;
            if (shop.Cooler != null) shop.Cooler.OnInventoryChanged -= RefreshAllCells;
            if (shop.Toppings != null) shop.Toppings.OnStockChanged -= RefreshAllCells;
            if (shop.Wallet != null) shop.Wallet.OnMoneyChanged -= OnMoneyChanged;
        }
    }

    private void OnTabChanged()
    {
        selectedItem = null;
        selectedTopping = null;

        if (detailPanel != null) detailPanel.Show(null);
        RebuildAll();
    }

    private void OnMoneyChanged(float _)
        => RefreshAllCells();

    private void RefreshAllCells()
    {
        for (int i = 0; i < cells.Count; i++)
            if (cells[i] != null) cells[i].RefreshVisuals();
    }

    public void SelectTopping(ToppingSO topping)
    {
        selectedItem = null;
        selectedTopping = topping;

        for (int i = 0; i < cells.Count; i++)
            if (cells[i] != null) cells[i].SetSelected(cells[i].ToppingItem == topping);

        if (detailPanel != null) detailPanel.ShowTopping(topping);
    }

    public void SelectItem(ItemDataSO item)
    {
        selectedTopping = null;
        selectedItem = item;

        for (int i = 0; i < cells.Count; i++)
            if (cells[i] != null) cells[i].SetSelected(cells[i].Item == item);

        if (detailPanel != null) detailPanel.Show(item);
    }

    private void RebuildAll()
    {
        if (shop == null || cellPrefab == null || cellsParent == null) return;

        if (shop.CurrentTab == ShopTabType.Toppings)
            RebuildToppingsTab();
        else
            RebuildItemsTab();
    }

    private void RebuildItemsTab()
    {
        var items = shop.GetItemsForCurrentTab();
        AdjustCellCount(items.Count);

        for (int i = 0; i < items.Count; i++)
        {
            cells[i].Bind(items[i], this, shop);
            cells[i].SetSelected(items[i] == selectedItem);
            LayoutCell(i, cells[i].transform, items.Count);
        }
    }

    private void RebuildToppingsTab()
    {
        var toppings = shop.GetToppings();
        AdjustCellCount(toppings.Count);

        for (int i = 0; i < toppings.Count; i++)
        {
            cells[i].Bind(toppings[i], this, shop);
            cells[i].SetSelected(toppings[i] == selectedTopping);
            LayoutCell(i, cells[i].transform, toppings.Count);
        }
    }

    private void AdjustCellCount(int target)
    {
        while (cells.Count < target)
        {
            GameObject go = Instantiate(cellPrefab, cellsParent);
            cells.Add(go.GetComponent<ShopItemCell2D>());
        }
        while (cells.Count > target)
        {
            int last = cells.Count - 1;
            if (cells[last] != null) Destroy(cells[last].gameObject);
            cells.RemoveAt(last);
        }
    }

    private void LayoutCell(int index, Transform cellTransform, int totalCells)
    {
        int row = index / columns;
        int col = index % columns;

        cellTransform.localPosition = new Vector3(
            originOffset.x + col * cellSpacing.x,
            originOffset.y - row * cellSpacing.y,
            0f);
        cellTransform.localRotation = Quaternion.identity;
    }
}