using System.Collections.Generic;
using UnityEngine;

public class ShopGrid2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShopSystem shop;
    [SerializeField] private Transform cellsParent;
    [SerializeField] private GameObject cellPrefab;
    [SerializeField] private ShopDetailPanel2D detailPanel;

    [Header("Layout")]
    [SerializeField] [Min(1)] private int columns = 4;
    [SerializeField] private Vector2 cellSpacing = new Vector2(1.2f, 1.4f);
    [SerializeField] private Vector2 originOffset = Vector2.zero;

    private readonly List<ShopItemCell2D> cells = new List<ShopItemCell2D>();
    private ItemDataSO selected;
    private bool started = false;

    void OnEnable()
    {
        if (shop != null)
        {
            shop.OnCartChanged += RebuildAll;
            if (shop.Cooler != null) shop.Cooler.OnInventoryChanged += RebuildAll;
        }

        // Si Start ya corrió, hacé el rebuild ahora.
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
            shop.OnCartChanged -= RebuildAll;
            if (shop.Cooler != null) shop.Cooler.OnInventoryChanged -= RebuildAll;
        }
    }

    public void SelectItem(ItemDataSO item)
    {
        selected = item;

        for (int i = 0; i < cells.Count; i++)
            cells[i].SetSelected(cells[i].Item == item);

        if (detailPanel != null) detailPanel.Show(item);
    }

    private void RebuildAll()
    {
        if (shop == null || cellPrefab == null || cellsParent == null) return;

        // Defensa contra singletons no listos.
        if (shop.Cooler == null || shop.Wallet == null)
        {
            Debug.LogWarning("[ShopGrid2D] Singletons no listos, esperando...");
            return;
        }

        var items = shop.GetShopItems();

        while (cells.Count < items.Count)
        {
            GameObject go = Instantiate(cellPrefab, cellsParent);
            var cell = go.GetComponent<ShopItemCell2D>();
            cells.Add(cell);
        }
        while (cells.Count > items.Count)
        {
            int last = cells.Count - 1;
            if (cells[last] != null) Destroy(cells[last].gameObject);
            cells.RemoveAt(last);
        }

        for (int i = 0; i < items.Count; i++)
        {
            cells[i].Bind(items[i], this, shop);
            cells[i].SetSelected(items[i] == selected);
            LayoutCell(i, cells[i].transform, items.Count);
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