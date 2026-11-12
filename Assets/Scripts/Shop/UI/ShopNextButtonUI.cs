using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopNextButtonUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShopSystem shop;
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI label;

    // Textos por tab actual: claves shop.next.* de las tablas de Loc.

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "GameScene";

    [Tooltip("Después de pagar el carrito el botón pasa a \"Arrancar el día\": un doble click no tiene que saltear ese paso.")]
    [SerializeField] private float startDayLockAfterPurchase = 0.4f;

    private bool started;
    private float startDayUnlockTime;

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
        Loc.OnTextsChanged += Refresh;
        if (shop != null)
        {
            shop.OnTabChanged += Refresh;
            shop.OnCartChanged += Refresh;

            // Una compra cambia plata y stock, o sea que puede cumplir (o romper) el mínimo:
            // sin esto el botón seguiría bloqueado hasta cambiar de tab.
            if (shop.Wallet != null) shop.Wallet.OnMoneyChanged += OnMoneyChanged;
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
            shop.OnCartChanged -= Refresh;
            if (shop.Wallet != null) shop.Wallet.OnMoneyChanged -= OnMoneyChanged;
            if (shop.Cooler != null) shop.Cooler.OnInventoryChanged -= Refresh;
        }
    }

    private void OnMoneyChanged(float _) => Refresh();

    private void OnClicked()
    {
        if (shop == null) return;

        if (shop.CurrentTab == ShopTabType.Toppings)
        {
            // Paso intermedio: con algo en el carrito, el botón primero lo paga.
            if (shop.HasCartItems)
            {
                if (shop.TryConfirmPurchase(out _))
                    startDayUnlockTime = Time.unscaledTime + startDayLockAfterPurchase;
                return;
            }

            // Último tab: arrancar próximo día. El botón ya debería estar deshabilitado,
            // pero la regla se vuelve a chequear acá por si algo lo dispara igual.
            if (Time.unscaledTime < startDayUnlockTime || !CanStartNextDay()) return;

            SceneManagementUtils.LoadSceneByName(gameSceneName);
            return;
        }

        // Avanzar al siguiente tab
        ShopTabType next = GetNextTab(shop.CurrentTab);
        shop.SetTab(next);
    }

    /// <summary>Spec punto 7: no se puede empezar el día sin cumplir ambos mínimos.</summary>
    private bool CanStartNextDay()
    {
        if (shop == null) return false;
        if (!shop.EnforceRunMinimums) return true;

        return shop.GetRunStatus().MeetsMinimums;
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
        if (shop == null) return;

        bool isLastTab = shop.CurrentTab == ShopTabType.Toppings;
        bool payCart = isLastTab && shop.HasCartItems;
        bool blocked = isLastTab && (payCart ? !shop.CanConfirmCart() : !CanStartNextDay());

        if (button != null) button.interactable = !blocked;

        if (label == null) return;

        switch (shop.CurrentTab)
        {
            case ShopTabType.Coal:     label.text = Loc.Get("shop.next.meat"); break;
            case ShopTabType.Meat:     label.text = Loc.Get("shop.next.upgrades"); break;
            case ShopTabType.Upgrades: label.text = Loc.Get("shop.next.toppings"); break;
            case ShopTabType.Toppings:
                if (payCart) label.text = Loc.Get(blocked ? "shop.next.cart_blocked" : "shop.next.buy_cart");
                else label.text = Loc.Get(blocked ? "shop.next.blocked" : "shop.next.start_day");
                break;
        }
    }
}
