using System.Collections.Generic;
using UnityEngine;

public class ShopBreadcrumbUI : MonoBehaviour
{
    [SerializeField] private ShopSystem shop;
    [SerializeField] private ShopTabButtonUI[] tabButtons;

    [Tooltip("Aire entre pestañas cuando se reparten de nuevo para hacerle lugar a la de la ruta.")]
    [SerializeField] private float tabGap = 12f;

    private bool started;

    void Awake()
    {
        EnsureRouteTab();
    }

    /// <summary>
    /// La pestaña de la ruta no está en el prefab: se crea acá clonando la de Mejoras (misma chapa,
    /// fuente y comportamiento) y las pestañas se reparten de nuevo en el mismo ancho. Si un día
    /// se agrega al prefab con <c>tab = Route</c>, esto no hace nada.
    /// </summary>
    private void EnsureRouteTab()
    {
        if (shop == null || !shop.HasRouteTab || tabButtons == null) return;

        ShopTabButtonUI source = null;
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null) continue;
            if (tabButtons[i].Tab == ShopTabType.Route) return;
            if (tabButtons[i].Tab == ShopTabType.Upgrades) source = tabButtons[i];
        }
        if (source == null) return;

        // El ancho que ocupaban las pestañas originales es el que se reparte.
        float left = float.MaxValue, right = float.MinValue;
        var ordered = new List<ShopTabButtonUI>();
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null) continue;
            RectTransform rect = (RectTransform)tabButtons[i].transform;
            left = Mathf.Min(left, rect.anchoredPosition.x - rect.sizeDelta.x * 0.5f);
            right = Mathf.Max(right, rect.anchoredPosition.x + rect.sizeDelta.x * 0.5f);
            ordered.Add(tabButtons[i]);
        }

        ShopTabButtonUI routeTab = Instantiate(source, source.transform.parent);
        routeTab.name = "TabRoute";
        routeTab.Configure(ShopTabType.Route, "shop.tab.route");
        ordered.Add(routeTab);
        ordered.Sort((a, b) => ShopTabs.OrderOf(a.Tab).CompareTo(ShopTabs.OrderOf(b.Tab)));

        float width = (right - left - tabGap * (ordered.Count - 1)) / ordered.Count;
        for (int i = 0; i < ordered.Count; i++)
        {
            RectTransform rect = (RectTransform)ordered[i].transform;
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
            rect.anchoredPosition = new Vector2(left + width * 0.5f + i * (width + tabGap), rect.anchoredPosition.y);
            rect.SetSiblingIndex(i);
        }

        tabButtons = ordered.ToArray();
    }

    /// <summary>
    /// Todas las pestañas con el mismo tamaño de letra: el más grande con el que entra la de
    /// texto más largo. Con auto-size por separado, "4. RUTA" quedaba enorme al lado de
    /// "5. TOPPINGS". Se repite al cambiar el idioma (cambian los largos).
    /// </summary>
    private void UnifyTabFontSize()
    {
        if (tabButtons == null) return;

        float size = float.MaxValue;
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null) continue;
            float fitting = tabButtons[i].MeasureFittingFontSize();
            if (fitting > 0f) size = Mathf.Min(size, fitting);
        }
        if (size == float.MaxValue) return;

        for (int i = 0; i < tabButtons.Length; i++)
            if (tabButtons[i] != null) tabButtons[i].SetFontSize(size);
    }

    void OnEnable()
    {
        Loc.OnTextsChanged += UnifyTabFontSize;
        SubscribeButtons();
        if (shop != null) shop.OnTabChanged += Refresh;
        if (started) Refresh();
    }

    void Start()
    {
        started = true;
        UnifyTabFontSize();
        Refresh();
    }

    void OnDisable()
    {
        Loc.OnTextsChanged -= UnifyTabFontSize;
        UnsubscribeButtons();
        if (shop != null) shop.OnTabChanged -= Refresh;
    }

    private void SubscribeButtons()
    {
        if (tabButtons == null) return;
        for (int i = 0; i < tabButtons.Length; i++)
            if (tabButtons[i] != null) tabButtons[i].OnTabClicked += HandleTab;
    }

    private void UnsubscribeButtons()
    {
        if (tabButtons == null) return;
        for (int i = 0; i < tabButtons.Length; i++)
            if (tabButtons[i] != null) tabButtons[i].OnTabClicked -= HandleTab;
    }

    private void HandleTab(ShopTabType tab)
    {
        if (shop != null) shop.SetTab(tab);
    }

    private void Refresh()
    {
        if (shop == null || tabButtons == null) return;
        for (int i = 0; i < tabButtons.Length; i++)
            if (tabButtons[i] != null)
                tabButtons[i].SetActiveState(tabButtons[i].Tab == shop.CurrentTab);
    }
}