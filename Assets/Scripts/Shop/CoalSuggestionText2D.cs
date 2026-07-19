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

        var tracker = CoalConsumptionTracker.Instance;
        if (tracker == null || tracker.DaysPlayed == 0)
        {
            text.text = "Primera noche — sin datos de consumo";
            return;
        }

        // Promedio redondeado a entero
        int avg = Mathf.RoundToInt(tracker.AverageCoalPerDay);
        text.text = $"Usaste {avg} unidades de carbón ayer";
    }
}