using UnityEngine;

public class ShopTabBar2D : MonoBehaviour
{
    [SerializeField] private ShopSystem shop;
    [SerializeField] private ShopTabButton2D[] tabButtons;

    private bool started = false;

    void OnEnable()
    {
        SubscribeToButtons();
        if (shop != null) shop.OnTabChanged += RefreshVisuals;
        if (started) RefreshVisuals();
    }

    void Start()
    {
        started = true;
        RefreshVisuals();
    }

    void OnDisable()
    {
        UnsubscribeFromButtons();
        if (shop != null) shop.OnTabChanged -= RefreshVisuals;
    }

    private void SubscribeToButtons()
    {
        if (tabButtons == null) return;
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null) continue;
            tabButtons[i].OnTabClicked += HandleTabClicked;
        }
    }

    private void UnsubscribeFromButtons()
    {
        if (tabButtons == null) return;
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null) continue;
            tabButtons[i].OnTabClicked -= HandleTabClicked;
        }
    }

    private void HandleTabClicked(ShopTabType tab)
    {
        if (shop != null) shop.SetTab(tab);
    }

    private void RefreshVisuals()
    {
        if (shop == null || tabButtons == null) return;
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null) continue;
            tabButtons[i].SetActive(tabButtons[i].Tab == shop.CurrentTab);
        }
    }
}