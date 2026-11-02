using TMPro;
using UnityEngine;

public class CoalSuggestionText2D : MonoBehaviour
{
    [SerializeField] private ShopSystem shop;
    [SerializeField] private TextMeshPro text;
    [SerializeField] private GameObject root;        // se prende solo en tab Coal

    private bool started = false;

    void OnEnable()
    {
        if (shop != null)
        {
            shop.OnTabChanged += Refresh;
            shop.OnCartChanged += Refresh;
            if (shop.Cooler != null) shop.Cooler.OnInventoryChanged += Refresh;
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
        if (shop != null)
        {
            shop.OnTabChanged -= Refresh;
            shop.OnCartChanged -= Refresh;
            if (shop.Cooler != null) shop.Cooler.OnInventoryChanged -= Refresh;
        }
    }

    private void Refresh()
    {
        if (shop == null) return;
        bool isCoalTab = shop.CurrentTab == ShopTabType.Coal;
        if (root != null) root.SetActive(isCoalTab);
        if (!isCoalTab) return;

        if (text == null) return;

        int units = shop.GetSuggestedCoalUnits();
        int bags = shop.GetSuggestedCoalBags();

        if (units <= 0)
            text.text = "Stock suficiente para el consumo promedio.";
        else
            text.text = $"Sugerido: comprar {bags} bolsa(s) ({units} u. faltantes)";
    }
}