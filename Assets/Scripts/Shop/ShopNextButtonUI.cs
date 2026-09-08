using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class ShopNextButtonUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShopSystem shop;
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI label;

    [Header("Textos por tab actual")]
    [SerializeField] private string fromCoalText = "Siguiente — Cortes de Carne →";
    [SerializeField] private string fromMeatText = "Siguiente — Mejoras →";
    [SerializeField] private string fromUpgradesText = "Siguiente — Toppings →";
    [SerializeField] private string fromToppingsText = "Arrancar próximo día →";

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "GameScene";
    
    [SerializeField] private ShopConfigSO config;

    private bool started;

    void Awake()
    {
        if (button != null) button.onClick.AddListener(OnClicked);
    }

    void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OnClicked);
    }

    void OnEnable()
    {
        if (shop != null)
        {
            shop.OnTabChanged += Refresh;
            if (shop.Wallet != null) shop.Wallet.OnMoneyChanged += _ => Refresh();
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
        if (shop != null) shop.OnTabChanged -= Refresh;
    }

    private void OnClicked()
    {
        if (shop == null) return;

        if (shop.CurrentTab == ShopTabType.Toppings)
        {
            // Antes de arrancar el próximo día, verificar mínimos
            if (!CanStartNextDay())
            {
                Debug.Log("[ShopNextButton] No se cumplen los mínimos, no puede arrancar el día.");
                // Opcional: mostrar feedback visual (shake, mensaje, etc.)
                return;
            }

            // Avanzar contador de día antes de cargar la escena
            DayCounter.Instance?.AdvanceDay();
            SceneManager.LoadScene(gameSceneName);
            return;
        }

        ShopTabType next = GetNextTab(shop.CurrentTab);
        shop.SetTab(next);
    }

    private bool CanStartNextDay()
    {
        if (config == null || DayCounter.Instance == null || shop == null) return true;

        int nextDay = DayCounter.Instance.CurrentDay + 1;
        int meatRequired = config.GetMeatMinimumForDay(nextDay);
        int coalRequired = config.GetCoalMinimumForDay(nextDay);

        return shop.GetTotalMeatUnits() >= meatRequired
               && shop.GetTotalCoalUnits() >= coalRequired;
    }

    private static ShopTabType GetNextTab(ShopTabType current)
    {
        switch (current)
        {
            case ShopTabType.Coal:     return ShopTabType.Meat;
            case ShopTabType.Meat:     return ShopTabType.Upgrades;
            case ShopTabType.Upgrades: return ShopTabType.Toppings;
            default:                   return ShopTabType.Coal;
        }
    }

    private void Refresh()
    {
        if (shop == null || label == null) return;

        switch (shop.CurrentTab)
        {
            case ShopTabType.Coal:     label.text = fromCoalText; break;
            case ShopTabType.Meat:     label.text = fromMeatText; break;
            case ShopTabType.Upgrades: label.text = fromUpgradesText; break;
            case ShopTabType.Toppings: label.text = fromToppingsText; break;
        }

        // Si es el último tab, deshabilitar si no cumple mínimos
        if (button != null)
        {
            bool canProceed = shop.CurrentTab != ShopTabType.Toppings || CanStartNextDay();
            button.interactable = canProceed;
        }
    }
}