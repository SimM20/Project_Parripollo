using System.Collections.Generic;
using UnityEngine;

public class ShopGridUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShopSystem shop;
    [SerializeField] private Transform contentTransform;   // el Content del ScrollView
    [SerializeField] private GameObject cellPrefab;

    private readonly List<ShopItemCellUI> cells = new List<ShopItemCellUI>();
    private bool started;
    
    [SerializeField] private UnityEngine.UI.ScrollRect scrollRect;

    void OnEnable()
    {
        if (shop != null)
        {
            shop.OnTabChanged += RebuildAll;
            if (shop.Cooler != null) shop.Cooler.OnInventoryChanged += RefreshAllCells;
            if (shop.Toppings != null) shop.Toppings.OnStockChanged += RefreshAllCells;
            if (shop.Wallet != null) shop.Wallet.OnMoneyChanged += OnMoneyChanged;
        }
        if (started) RebuildAll();
    }

    void Start()
    {
        started = true;
        RebuildAll();
    }

    void OnDisable()
    {
        if (shop != null)
        {
            shop.OnTabChanged -= RebuildAll;
            if (shop.Cooler != null) shop.Cooler.OnInventoryChanged -= RefreshAllCells;
            if (shop.Toppings != null) shop.Toppings.OnStockChanged -= RefreshAllCells;
            if (shop.Wallet != null) shop.Wallet.OnMoneyChanged -= OnMoneyChanged;
        }
    }

    private void OnMoneyChanged(float _) => RefreshAllCells();

    private void RefreshAllCells()
    {
        for (int i = 0; i < cells.Count; i++)
            if (cells[i] != null) cells[i].RefreshVisuals();
    }

    private void RebuildAll()
    {
        Debug.Log($"[ShopGridUI] RebuildAll() llamado. Tab: {shop?.CurrentTab}");
    
        if (shop == null) { Debug.Log("[ShopGridUI] shop es null"); return; }
        if (cellPrefab == null) { Debug.Log("[ShopGridUI] cellPrefab es null"); return; }
        if (contentTransform == null) { Debug.Log("[ShopGridUI] contentTransform es null"); return; }
        if (shop.Cooler == null) { Debug.Log("[ShopGridUI] shop.Cooler es null"); return; }
        if (shop.Wallet == null) { Debug.Log("[ShopGridUI] shop.Wallet es null"); return; }

        if (shop.CurrentTab == ShopTabType.Toppings)
        {
            Debug.Log("[ShopGridUI] Entrando a RebuildToppingsTab");
            RebuildToppingsTab();
        }
        else
        {
            Debug.Log("[ShopGridUI] Entrando a RebuildItemsTab");
            RebuildItemsTab();
        }
    }

    private void RebuildItemsTab()
    {
        var items = shop.GetItemsForCurrentTab();
        Debug.Log($"[ShopGridUI] GetItemsForCurrentTab devolvió {items?.Count ?? -1} items");
        AdjustCellCount(items.Count);

        for (int i = 0; i < items.Count; i++)
            cells[i].Bind(items[i], shop);
    }

    private void RebuildToppingsTab()
    {
        var toppings = shop.GetToppings();
        AdjustCellCount(toppings.Count);

        for (int i = 0; i < toppings.Count; i++)
            cells[i].Bind(toppings[i], shop);
    }

    private void AdjustCellCount(int target)
    {
        while (cells.Count < target)
        {
            GameObject go = Instantiate(cellPrefab, contentTransform);
            cells.Add(go.GetComponent<ShopItemCellUI>());
        }
        while (cells.Count > target)
        {
            int last = cells.Count - 1;
            if (cells[last] != null) Destroy(cells[last].gameObject);
            cells.RemoveAt(last);
        }
    }
}