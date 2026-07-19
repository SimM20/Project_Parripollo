using TMPro;
using UnityEngine;

public class ShopBreadcrumbBar2D : MonoBehaviour
{
    [SerializeField] private ShopSystem shop;
    [SerializeField] private ShopTabButton2D[] tabButtons;

    [Header("Next Button")]
    [SerializeField] private ShopButton2D nextButton;
    [SerializeField] private TextMeshPro nextButtonLabel;

    private bool started;

    void OnEnable()
    {
        SubscribeButtons();
        if (shop != null) shop.OnTabChanged += Refresh;
        if (nextButton != null) nextButton.OnClicked += OnNextClicked;
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
        if (nextButton != null) nextButton.OnClicked -= OnNextClicked;
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

    private void HandleTab(ShopTabType tab) => shop?.SetTab(tab);

    private void OnNextClicked()
    {
        if (shop == null) return;
        ShopTabType next = GetNextTab(shop.CurrentTab);
        shop.SetTab(next);
    }

    private static ShopTabType GetNextTab(ShopTabType current)
    {
        switch (current)
        {
            case ShopTabType.Coal: return ShopTabType.Meat;
            case ShopTabType.Meat: return ShopTabType.Upgrades;
            case ShopTabType.Upgrades: return ShopTabType.Toppings;
            default: return ShopTabType.Coal;
        }
    }

    private static string GetNextLabel(ShopTabType current)
    {
        switch (current)
        {
            case ShopTabType.Coal: return "Siguiente — Cortes de Carne ->";
            case ShopTabType.Meat: return "Siguiente — Mejoras ->";
            case ShopTabType.Upgrades: return "Siguiente — Toppings ->";
            default: return "Comenzar Dia ->";
        }
    }

    private void Refresh()
    {
        if (shop == null) return;

        if (tabButtons != null)
            for (int i = 0; i < tabButtons.Length; i++)
                if (tabButtons[i] != null)
                    tabButtons[i].SetActive(tabButtons[i].Tab == shop.CurrentTab);

        if (nextButtonLabel != null)
            nextButtonLabel.text = GetNextLabel(shop.CurrentTab);

        bool isLastTab = shop.CurrentTab == ShopTabType.Toppings;
        if (nextButton != null) nextButton.SetInteractable(!isLastTab);
    }
}