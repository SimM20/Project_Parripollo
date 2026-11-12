using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Panel del carrito de la tienda (columna derecha de <c>ShopCanvas.prefab</c>): una línea por item
/// agregado y el subtotal. La compra real la hace el botón grande ("Comprar carrito",
/// <see cref="ShopNextButtonUI"/>).
///
/// El <see cref="ShopSystem"/> se lee de <see cref="ShopGridUI.Shop"/> (como <c>ShopTabSwipeUI</c>):
/// así las instancias del prefab en cada escena no necesitan otro override.
/// Mismo patrón que el resto de <c>Shop/*UI.cs</c>: OnEnable suscribe, Start marca
/// <c>started</c> y refresca, OnDisable desuscribe.
/// </summary>
public class ShopCartUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShopGridUI grid;

    [Tooltip("Content del ScrollView de las líneas (VerticalLayoutGroup).")]
    [SerializeField] private Transform rowsParent;
    [SerializeField] private ShopCartRowUI rowPrefab;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI subtotalText;

    [Tooltip("Aviso de carrito vacío: se prende cuando no hay líneas.")]
    [SerializeField] private GameObject emptyState;

    // Las líneas se reciclan (se apagan, no se destruyen): agregar y sacar es frecuente.
    private readonly List<ShopCartRowUI> rows = new List<ShopCartRowUI>();
    private ShopSystem shop;
    private bool started;
    private float lastTotal;

    void OnEnable()
    {
        shop = grid != null ? grid.Shop : null;

        Loc.OnTextsChanged += Refresh;
        if (shop != null)
        {
            shop.OnCartChanged += Refresh;
            // Plata, stock y frascos cambian qué se puede sumar con el + de cada línea.
            if (shop.Wallet != null) shop.Wallet.OnMoneyChanged += OnMoneyChanged;
            if (shop.Cooler != null) shop.Cooler.OnInventoryChanged += Refresh;
            if (shop.Toppings != null) shop.Toppings.OnStockChanged += Refresh;
        }
        if (started) Refresh();
    }

    void Start()
    {
        started = true;
        Refresh();
    }

    void OnDisable()
    {
        Loc.OnTextsChanged -= Refresh;
        if (shop != null)
        {
            shop.OnCartChanged -= Refresh;
            if (shop.Wallet != null) shop.Wallet.OnMoneyChanged -= OnMoneyChanged;
            if (shop.Cooler != null) shop.Cooler.OnInventoryChanged -= Refresh;
            if (shop.Toppings != null) shop.Toppings.OnStockChanged -= Refresh;
        }
    }

    private void OnMoneyChanged(float _) => Refresh();

    private void Refresh()
    {
        if (shop == null) return;

        int used = 0;
        foreach (var kv in shop.EnumerateCart())
            if (kv.Key != null && kv.Value > 0) GetRow(used++).Bind(kv.Key, kv.Value, shop);

        foreach (var kv in shop.EnumerateToppingCart())
            if (kv.Key != null && kv.Value > 0) GetRow(used++).Bind(kv.Key, kv.Value, shop);

        for (int i = used; i < rows.Count; i++)
            if (rows[i].gameObject.activeSelf) rows[i].gameObject.SetActive(false);

        if (emptyState != null && emptyState.activeSelf != (used == 0)) emptyState.SetActive(used == 0);

        float total = shop.CartTotal();
        if (subtotalText != null)
        {
            subtotalText.text = $"${total:N0}";
            // Golpe solo cuando sube: se ve que lo agregado entró a la cuenta.
            if (started && total > lastTotal) ScalePunch.Punch(subtotalText.transform, 0.18f, 0.2f, true);
        }
        lastTotal = total;
    }

    private ShopCartRowUI GetRow(int index)
    {
        if (index < rows.Count)
        {
            ShopCartRowUI row = rows[index];
            if (!row.gameObject.activeSelf) row.gameObject.SetActive(true);
            return row;
        }

        ShopCartRowUI created = Instantiate(rowPrefab, rowsParent);
        rows.Add(created);
        return created;
    }
}
