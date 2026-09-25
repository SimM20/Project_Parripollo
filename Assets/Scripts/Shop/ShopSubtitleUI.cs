using UnityEngine;
using TMPro;

public class ShopSubtitleUI : MonoBehaviour
{
    [SerializeField] private ShopSystem shop;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI detailText;

    [Header("Textos por tab")]
    [SerializeField] private string coalTitle = "CONSUMO PROMEDIO:";
    [SerializeField] private string coalDetailFormat = "Venís usando {0} unidades de carbón por jornada.";
    [SerializeField] private string coalFirstNightDetail = "Primera jornada: todavía no hay datos de consumo.";
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
        if (shop == null) return;

        // El título es opcional: si la tab activa ya dice en qué sección estás, alcanza con el detalle.
        switch (shop.CurrentTab)
        {
            case ShopTabType.Coal:
                SetTexts(coalTitle, BuildCoalDetail());
                break;
            case ShopTabType.Meat:
                SetTexts(meatTitle, meatDetail);
                break;
            case ShopTabType.Upgrades:
                SetTexts(upgradesTitle, upgradesDetail);
                break;
            case ShopTabType.Toppings:
                SetTexts(toppingsTitle, toppingsDetail);
                break;
        }
    }

    private void SetTexts(string title, string detail)
    {
        if (titleText != null) titleText.text = title;
        if (detailText != null) detailText.text = detail;
    }

    private string BuildCoalDetail()
    {
        var tracker = CoalConsumptionTracker.Instance;
        if (tracker == null || tracker.DaysPlayed == 0)
            return coalFirstNightDetail;

        int avg = Mathf.RoundToInt(tracker.AverageCoalPerDay);
        return string.Format(coalDetailFormat, avg);
    }
}