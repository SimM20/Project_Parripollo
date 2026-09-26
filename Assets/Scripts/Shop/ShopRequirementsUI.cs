using UnityEngine;
using TMPro;

/// <summary>
/// Estado visual de los mínimos en la tienda (spec punto 7 / "Estado visual en Tienda").
/// Muestra de forma permanente stock actual vs. mínimo requerido de carne y carbón, cuánto
/// falta comprar, y la racha de noches seguidas cerradas por strikes.
///
/// Mismo patrón que el resto de <c>Shop/*UI.cs</c>: OnEnable suscribe, Start marca
/// <c>started</c> y refresca, OnDisable desuscribe.
/// </summary>
public class ShopRequirementsUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShopSystem shop;

    [Tooltip("Root opcional: se apaga entero si esta tienda no aplica mínimos (ShopTutorial).")]
    [SerializeField] private GameObject root;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI meatText;
    [SerializeField] private TextMeshProUGUI coalText;
    [SerializeField] private TextMeshProUGUI deficitText;
    [SerializeField] private TextMeshProUGUI strikeStreakText;

    // Textos: claves shop.req.* de las tablas de Loc (en mayúsculas: la tienda usa Bungee).

    [Header("Colors")]
    [SerializeField] private Color metColor = new Color(0.45f, 0.85f, 0.4f);
    [SerializeField] private Color unmetColor = new Color(0.9f, 0.35f, 0.3f);

    private bool started;

    void OnEnable()
    {
        Loc.OnTextsChanged += Refresh;
        if (shop != null)
        {
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
            if (shop.Wallet != null) shop.Wallet.OnMoneyChanged -= OnMoneyChanged;
            if (shop.Cooler != null) shop.Cooler.OnInventoryChanged -= Refresh;
        }
    }

    private void OnMoneyChanged(float _) => Refresh();

    private void Refresh()
    {
        if (shop == null) return;

        if (!shop.EnforceRunMinimums)
        {
            // Sin mínimos (ShopTutorial) la barra no tiene nada que decir.
            if (root != null) root.SetActive(false);
            return;
        }

        if (root != null) root.SetActive(true);

        RunEconomyStatus status = shop.GetRunStatus();

        if (meatText != null)
        {
            meatText.text = Loc.Format("shop.req.meat", status.meatStock, status.meatRequired);
            meatText.color = status.meatDeficit <= 0 ? metColor : unmetColor;
        }

        if (coalText != null)
        {
            coalText.text = Loc.Format("shop.req.coal", status.coalStock, status.coalRequired);
            coalText.color = status.coalDeficit <= 0 ? metColor : unmetColor;
        }

        if (deficitText != null)
        {
            if (status.MeetsMinimums)
            {
                deficitText.text = Loc.Get("shop.req.ready");
                deficitText.color = metColor;
            }
            else
            {
                deficitText.text = Loc.Format("shop.req.deficit", BuildDeficitText(status));
                deficitText.color = unmetColor;
            }
        }

        if (strikeStreakText != null)
        {
            int streak = StrikeSystem.ConsecutiveStrikeNights;
            int max = StrikeSystem.MaxConsecutiveStrikeNights;

            // Sin racha no hay nada que avisar: la línea aparece recién con la primera noche de strikes.
            strikeStreakText.gameObject.SetActive(streak > 0);
            strikeStreakText.text = Loc.Format("shop.req.streak", streak, max);
            strikeStreakText.color = unmetColor;
        }
    }

    private static string BuildDeficitText(RunEconomyStatus status)
    {
        string meat = Loc.Format(status.meatDeficit == 1 ? "shop.req.cuts.one" : "shop.req.cuts", status.meatDeficit);
        string coal = Loc.Format(status.coalDeficit == 1 ? "shop.req.coal_units.one" : "shop.req.coal_units", status.coalDeficit);

        if (status.meatDeficit > 0 && status.coalDeficit > 0) return meat + ", " + coal;
        if (status.meatDeficit > 0) return meat;
        return coal;
    }
}
