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

    [Header("Formatos")]
    // En mayúsculas: la tienda usa Bungee, que es una tipografía de titulares.
    [SerializeField] private string meatFormat = "CARNE   {0} / {1}";
    [SerializeField] private string coalFormat = "CARBÓN  {0} / {1}";
    [SerializeField] private string deficitFormat = "FALTA: {0}";
    [SerializeField] private string requirementsMetText = "LISTO PARA ARRANCAR";
    [SerializeField] private string strikeStreakFormat = "NOCHES SEGUIDAS: {0} / {1}";

    [Header("Colors")]
    [SerializeField] private Color metColor = new Color(0.45f, 0.85f, 0.4f);
    [SerializeField] private Color unmetColor = new Color(0.9f, 0.35f, 0.3f);

    private bool started;

    void OnEnable()
    {
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
            meatText.text = string.Format(meatFormat, status.meatStock, status.meatRequired);
            meatText.color = status.meatDeficit <= 0 ? metColor : unmetColor;
        }

        if (coalText != null)
        {
            coalText.text = string.Format(coalFormat, status.coalStock, status.coalRequired);
            coalText.color = status.coalDeficit <= 0 ? metColor : unmetColor;
        }

        if (deficitText != null)
        {
            if (status.MeetsMinimums)
            {
                deficitText.text = requirementsMetText;
                deficitText.color = metColor;
            }
            else
            {
                deficitText.text = string.Format(deficitFormat, BuildDeficitText(status));
                deficitText.color = unmetColor;
            }
        }

        if (strikeStreakText != null)
        {
            int streak = StrikeSystem.ConsecutiveStrikeNights;
            int max = StrikeSystem.MaxConsecutiveStrikeNights;

            strikeStreakText.text = string.Format(strikeStreakFormat, streak, max);
            strikeStreakText.color = streak > 0 ? unmetColor : metColor;
        }
    }

    private static string BuildDeficitText(RunEconomyStatus status)
    {
        string meat = status.meatDeficit + (status.meatDeficit == 1 ? " CORTE" : " CORTES");
        string coal = status.coalDeficit + (status.coalDeficit == 1 ? " CARBÓN" : " CARBONES");

        if (status.meatDeficit > 0 && status.coalDeficit > 0) return meat + ", " + coal;
        if (status.meatDeficit > 0) return meat;
        return coal;
    }
}
