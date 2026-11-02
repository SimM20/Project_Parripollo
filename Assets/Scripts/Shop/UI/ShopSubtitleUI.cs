using UnityEngine;
using TMPro;

public class ShopSubtitleUI : MonoBehaviour
{
    [SerializeField] private ShopSystem shop;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI detailText;

    // Textos por tab: claves shop.help.* de las tablas de Loc.

    private bool started;

    void OnEnable()
    {
        Loc.OnTextsChanged += Refresh;
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
        Loc.OnTextsChanged -= Refresh;
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
                SetTexts(Loc.Get("shop.help.coal.title"), BuildCoalDetail());
                break;
            case ShopTabType.Meat:
                SetTexts(Loc.Get("shop.help.meat.title"), Loc.Get("shop.help.meat"));
                break;
            case ShopTabType.Upgrades:
                SetTexts(Loc.Get("shop.help.upgrades.title"), Loc.Get("shop.help.upgrades"));
                break;
            case ShopTabType.Toppings:
                SetTexts(Loc.Get("shop.help.toppings.title"), Loc.Get("shop.help.toppings"));
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
            return Loc.Get("shop.help.coal.first_night");

        int avg = Mathf.RoundToInt(tracker.AverageCoalPerDay);
        return Loc.Format("shop.help.coal", avg);
    }
}