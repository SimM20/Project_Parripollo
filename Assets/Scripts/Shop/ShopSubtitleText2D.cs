using TMPro;
using UnityEngine;

public class ShopSubtitleText2D : MonoBehaviour
{
    [SerializeField] private ShopSystem shop;
    [SerializeField] private TextMeshPro title;
    [SerializeField] private TextMeshPro subtitle;

    [Header("Textos por tab")]
    [SerializeField] private string coalTitle = "Comprar Carbón";
    [SerializeField] private string meatTitle = "Comprar Cortes de Carne";
    [SerializeField] private string upgradesTitle = "Mejoras para la Parrilla";
    [SerializeField] private string toppingsTitle = "Comprar Toppings";
    [SerializeField] private string coalSubtitle = "Comprar Carbón";
    [SerializeField] private string meatSubtitle = "Comprar Cortes de Carne";
    [SerializeField] private string upgradesSubtitle = "Mejoras para la Parrilla";
    [SerializeField] private string toppingsSubtitle = "Comprar Toppings";

    private bool started;

    void OnEnable()
    {
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
        if (shop != null) shop.OnTabChanged -= Refresh;
    }

    private void Refresh()
    {
        if (shop == null || title == null || subtitle == null) return;

        switch (shop.CurrentTab)
        {
            case ShopTabType.Coal:     
                title.text = coalTitle; 
                subtitle.text = coalSubtitle;
                break;
            case ShopTabType.Meat:     
                title.text = meatTitle;
                subtitle.text = meatSubtitle;
                break;
            case ShopTabType.Upgrades: 
                title.text = upgradesTitle;
                subtitle.text = upgradesSubtitle;
                break;
            case ShopTabType.Toppings: 
                title.text = toppingsTitle;
                subtitle.text = toppingsSubtitle;
                break;
        }
    }
}