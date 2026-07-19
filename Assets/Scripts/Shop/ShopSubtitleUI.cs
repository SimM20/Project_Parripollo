using UnityEngine;
using TMPro;

public class ShopSubtitleUI : MonoBehaviour
{
    [SerializeField] private ShopSystem shop;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI detailText;

    [Header("Textos por tab")]
    [SerializeField] private string coalTitle = "CONSUMO PROMEDIO:";
    [SerializeField] private string meatTitle = "CORTES DE CARNE";
    [SerializeField] private string meatDetail = "Elegí los cortes para la noche.";
    [SerializeField] private string upgradesTitle = "MEJORAS PARA LA PARRILLA";
    [SerializeField] private string upgradesDetail = "Invertí en mejoras permanentes.";
    [SerializeField] private string toppingsTitle = "TOPPINGS Y SALSAS";
    [SerializeField] private string toppingsDetail = "Comprá condimentos para tus platos.";

    private bool started;

    void OnEnable()
    {
        if (shop != null)
        {
            shop.OnTabChanged += Refresh;
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
            if (shop.Cooler != null) shop.Cooler.OnInventoryChanged -= Refresh;
        }
    }

    private void Refresh()
    {
        if (shop == null || titleText == null || detailText == null) return;

        switch (shop.CurrentTab)
        {
            case ShopTabType.Coal:
                titleText.text = coalTitle;
                detailText.text = BuildCoalDetail();
                break;
            case ShopTabType.Meat:
                titleText.text = meatTitle;
                detailText.text = meatDetail;
                break;
            case ShopTabType.Upgrades:
                titleText.text = upgradesTitle;
                detailText.text = upgradesDetail;
                break;
            case ShopTabType.Toppings:
                titleText.text = toppingsTitle;
                detailText.text = toppingsDetail;
                break;
        }
    }

    private string BuildCoalDetail()
    {
        var tracker = CoalConsumptionTracker.Instance;
        if (tracker == null || tracker.DaysPlayed == 0)
            return "PRIMERA NOCHE — SIN DATOS DE CONSUMO";

        int avg = Mathf.RoundToInt(tracker.AverageCoalPerDay);
        return $"USASTE {avg} UNIDADES DE CARBÓN";
    }
}