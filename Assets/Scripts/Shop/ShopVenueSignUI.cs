using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Cartel del header de la tienda para mudarse a un local mejor (el próximo escenario de
/// <see cref="ReputationSystem"/>). Muestra precio y estado: mientras la reputación no alcanza el botón
/// queda apagado con un candado y la leyenda dice qué nivel pide; si no, por qué no se puede pagar.
/// Comprarlo levanta el tope de la reputación; por ahora no hay escenarios nuevos, así que no cambia
/// nada visual. Con todos los locales comprados lo dice y esconde el botón.
///
/// Mismo patrón que el resto de <c>Shop/*UI.cs</c>: OnEnable suscribe, Start marca <c>started</c> y
/// refresca, OnDisable desuscribe.
/// </summary>
public class ShopVenueSignUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShopSystem shop;

    [Header("Text")]
    [Tooltip("\"LOCAL MEJOR  $3.000\".")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Tooltip("Estado o motivo del bloqueo: la reputación que pide, que falta plata, qué hace la compra.")]
    [SerializeField] private TextMeshProUGUI detailText;

    [Header("Bloqueo")]
    [Tooltip("Candado sobre el botón (hijo del botón). Aparece mientras la reputación no alcanza.")]
    [SerializeField] private Image lockIcon;

    [Header("Button")]
    [SerializeField] private Button buyButton;
    [SerializeField] private TextMeshProUGUI buyLabel;

    // Textos: claves shop.venue.* (Shop.csv); el botón usa shop.cell.buy, el mismo "Comprar" de las tarjetas.

    [Header("Colors")]
    [SerializeField] private Color priceColor = new Color(0.949f, 0.608f, 0.165f);
    [SerializeField] private Color lockedColor = new Color(0.98f, 0.76f, 0.3f);
    [SerializeField] private Color blockedColor = new Color(0.9f, 0.35f, 0.3f);
    [SerializeField] private Color readyColor = new Color(0.79f, 0.81f, 0.82f);
    [SerializeField] private Color doneColor = new Color(0.45f, 0.85f, 0.4f);

    [Header("Compra")]
    [Tooltip("Escala máxima del punch del cartel al comprar.")]
    [SerializeField] private float purchasePunchScale = 1.12f;
    [SerializeField] private float purchasePunchSeconds = 0.3f;

    private bool started;
    private Vector3 baseScale = Vector3.one;
    private Coroutine punchRoutine;

    /// <summary>El botón de compra, para los carteles del tutorial.</summary>
    public Button BuyButton => buyButton;

    void Awake()
    {
        baseScale = transform.localScale;
        if (buyButton != null) buyButton.onClick.AddListener(OnBuy);
    }

    void OnDestroy()
    {
        if (buyButton != null) buyButton.onClick.RemoveListener(OnBuy);
    }

    void OnEnable()
    {
        Loc.OnTextsChanged += Refresh;
        ReputationSystem.OnChanged += OnReputationChanged;
        if (shop != null)
        {
            // Plata y stock mueven los mínimos del próximo día, que también gatean la compra.
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
        ReputationSystem.OnChanged -= OnReputationChanged;
        if (shop != null)
        {
            if (shop.Wallet != null) shop.Wallet.OnMoneyChanged -= OnMoneyChanged;
            if (shop.Cooler != null) shop.Cooler.OnInventoryChanged -= Refresh;
        }

        if (punchRoutine != null)
        {
            StopCoroutine(punchRoutine);
            punchRoutine = null;
        }
        transform.localScale = baseScale;
    }

    private void OnMoneyChanged(float _) => Refresh();

    private void OnReputationChanged(ReputationChange _) => Refresh();

    private void Refresh()
    {
        if (shop == null) return;

        ReputationConfigSO.Venue next = ReputationSystem.NextVenue;
        ShopSystem.VenueBlock block = shop.GetNextVenueBlock();

        bool allOwned = next == null;
        bool locked = block == ShopSystem.VenueBlock.Reputation;

        if (titleText != null)
        {
            titleText.text = allOwned
                ? Loc.Get("shop.venue.all_owned.title")
                : Loc.Get("shop.venue.title") + "  <color=#" + ColorUtility.ToHtmlStringRGB(priceColor) + ">$" + next.price.ToString("N0") + "</color>";
        }

        if (detailText != null)
        {
            switch (block)
            {
                case ShopSystem.VenueBlock.AllOwned:
                    SetDetail(Loc.Get("shop.venue.all_owned"), doneColor);
                    break;
                case ShopSystem.VenueBlock.Reputation:
                    SetDetail(Loc.Format("shop.venue.requires", next.requiredLevel), lockedColor);
                    break;
                case ShopSystem.VenueBlock.Money:
                    SetDetail(Loc.Get("shop.venue.no_money"), blockedColor);
                    break;
                case ShopSystem.VenueBlock.Minimums:
                    SetDetail(Loc.Get("shop.venue.minimums"), blockedColor);
                    break;
                default:
                    SetDetail(Loc.Get("shop.venue.ready"), readyColor);
                    break;
            }
        }

        if (lockIcon != null) lockIcon.enabled = locked;

        if (buyButton != null)
        {
            buyButton.gameObject.SetActive(!allOwned);
            buyButton.interactable = block == ShopSystem.VenueBlock.None;
        }

        if (buyLabel != null)
        {
            // Con candado el botón no dice nada: el candado ocupa su lugar.
            buyLabel.enabled = !locked;
            buyLabel.text = Loc.Get("shop.cell.buy");
            // Mismo criterio que las tarjetas: el texto del botón apagado también se apaga.
            buyLabel.alpha = block == ShopSystem.VenueBlock.None ? 1f : 0.45f;
        }
    }

    private void SetDetail(string text, Color color)
    {
        detailText.text = text;
        detailText.color = color;
    }

    private void OnBuy()
    {
        if (shop == null) return;

        if (shop.TryBuyNextVenue(out _))
        {
            if (punchRoutine != null) StopCoroutine(punchRoutine);
            punchRoutine = StartCoroutine(Punch());
        }

        Refresh();
    }

    private IEnumerator Punch()
    {
        float duration = Mathf.Max(0.01f, purchasePunchSeconds);
        float t = 0f;

        while (t < duration)
        {
            // Unscaled: la tienda no pausa, pero un popup modal podría.
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            transform.localScale = baseScale * Mathf.Lerp(purchasePunchScale, 1f, k);
            yield return null;
        }

        transform.localScale = baseScale;
        punchRoutine = null;
    }
}
