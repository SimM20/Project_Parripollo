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

    // El carrito lleva una parada que deja la run sin los mínimos: el primer click arma el aviso
    // ("Confirmar: perdés la partida") y recién el segundo paga.
    private bool riskConfirmArmed;

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
                if (shop.CartRisksRun && !riskConfirmArmed)
                {
                    riskConfirmArmed = true;
                    GamepadHaptics.Play(HapticEvent.ActionRefused);
                    Refresh();
                    return;
                }

                riskConfirmArmed = false;
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
        shop.SetTab(shop.GetNextTab());
    }

    /// <summary>Spec punto 7: no se puede empezar el día sin cumplir ambos mínimos.</summary>
    private bool CanStartNextDay()
    {
        if (shop == null) return false;
        if (!shop.EnforceRunMinimums) return true;

        return shop.GetRunStatus().MeetsMinimums;
    }

    private void Refresh()
    {
        if (shop == null) return;

        bool isLastTab = shop.CurrentTab == ShopTabType.Toppings;
        bool payCart = isLastTab && shop.HasCartItems;
        bool blocked = isLastTab && (payCart ? !shop.CanConfirmCart() : !CanStartNextDay());
        bool risky = payCart && !blocked && shop.CartRisksRun;

        // Salir de la tab, vaciar el carrito o dejar de estar en riesgo desarma la confirmación.
        if (!risky) riskConfirmArmed = false;

        if (button != null) button.interactable = !blocked;

        if (label == null) return;

        switch (shop.CurrentTab)
        {
            default:
                // "Siguiente: <la pestaña que viene>".
                switch (shop.GetNextTab())
                {
                    case ShopTabType.Meat:     label.text = Loc.Get("shop.next.meat"); break;
                    case ShopTabType.Upgrades: label.text = Loc.Get("shop.next.upgrades"); break;
                    case ShopTabType.Route:    label.text = Loc.Get("shop.next.route"); break;
                    default:                   label.text = Loc.Get("shop.next.toppings"); break;
                }
                break;
            case ShopTabType.Toppings:
                if (payCart)
                {
                    string key = blocked ? "shop.next.cart_blocked"
                        : !risky ? "shop.next.buy_cart"
                        : riskConfirmArmed ? "shop.next.risk_confirm" : "shop.next.buy_cart_risk";
                    label.text = Loc.Get(key);
                }
                else label.text = Loc.Get(blocked ? "shop.next.blocked" : "shop.next.start_day");
                break;
        }
    }
}
