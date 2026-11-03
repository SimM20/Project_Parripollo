using UnityEngine;

public class ShopBreadcrumbUI : MonoBehaviour
{
    [SerializeField] private ShopSystem shop;
    [SerializeField] private ShopTabButtonUI[] tabButtons;

    private bool started;

    void OnEnable()
    {
        SubscribeButtons();
        if (shop != null) shop.OnTabChanged += Refresh;
        if (started) Refresh();
    }

    void Start()
    {
        started = true;
        Refresh();
    }

    void OnDisable()
    {
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