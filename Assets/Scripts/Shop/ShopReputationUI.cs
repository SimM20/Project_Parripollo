using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Reputación en la tienda (abajo, entre los requisitos de mañana y "Siguiente"): nivel, barra hacia el
/// próximo nivel, lo que se ganó o perdió en la jornada que acaba de terminar y lo que da hoy la
/// reputación (más clientes, más plata por corte). En el tope del local la barra queda llena y avisa que
/// no crece más: el cartel del header vende el local mejor.
///
/// Mismo patrón que el resto de <c>Shop/*UI.cs</c>: OnEnable suscribe, Start marca <c>started</c> y
/// refresca, OnDisable desuscribe. <see cref="ReputationSystem"/> es estático: no hay nada que asignar.
/// </summary>
public class ShopReputationUI : MonoBehaviour
{
    [Header("Text")]
    [Tooltip("\"REPUTACIÓN\".")]
    [SerializeField] private TextMeshProUGUI captionText;

    [Tooltip("\"NV 3\".")]
    [SerializeField] private TextMeshProUGUI levelText;

    [Tooltip("\"120/400\", o el aviso de tope del local / nivel máximo.")]
    [SerializeField] private TextMeshProUGUI progressText;

    [Tooltip("\"HOY +85\": balance de la jornada que terminó.")]
    [SerializeField] private TextMeshProUGUI todayText;

    [Tooltip("\"CLIENTES +20%  PLATA +16%\": lo que da la reputación actual.")]
    [SerializeField] private TextMeshProUGUI bonusText;

    [Header("Barra")]
    [Tooltip("Relleno: se estira desde la izquierda del fondo de la barra (anchorMax.x = avance).")]
    [SerializeField] private RectTransform barFill;
    [SerializeField] private Image barFillImage;

    // Textos: claves reputation.* (Gameplay.csv, las comparte el HUD) y shop.reputation.* (Shop.csv).

    [Header("Colors")]
    [SerializeField] private Color fillColor = new Color(0.949f, 0.608f, 0.165f);
    [SerializeField] private Color cappedFillColor = new Color(0.62f, 0.56f, 0.45f);
    [SerializeField] private Color gainColor = new Color(0.45f, 0.85f, 0.4f);
    [SerializeField] private Color lossColor = new Color(0.9f, 0.35f, 0.3f);
    [SerializeField] private Color neutralColor = new Color(0.6f, 0.64f, 0.65f);

    private bool started;

    void OnEnable()
    {
        Loc.OnTextsChanged += Refresh;
        ReputationSystem.OnChanged += OnReputationChanged;
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
    }

    private void OnReputationChanged(ReputationChange _) => Refresh();

    private void Refresh()
    {
        if (captionText != null) captionText.text = Loc.Get("reputation.title");
        if (levelText != null) levelText.text = Loc.Format("reputation.level", ReputationSystem.Level);
        if (progressText != null) progressText.text = ReputationSystem.FormatProgress();

        // En el tope la barra se ve llena: se completó lo que da este local, falta el próximo.
        float fill = ReputationSystem.IsCapped ? 1f : ReputationSystem.LevelProgress01;
        if (barFill != null)
        {
            barFill.anchorMin = Vector2.zero;
            barFill.anchorMax = new Vector2(fill, 1f);
            barFill.offsetMin = Vector2.zero;
            barFill.offsetMax = Vector2.zero;
        }
        if (barFillImage != null)
            barFillImage.color = ReputationSystem.IsBlockedByVenue ? cappedFillColor : fillColor;

        if (todayText != null)
        {
            int today = ReputationSystem.XpToday;
            todayText.text = Loc.Format("shop.reputation.today", ReputationSystem.FormatSigned(today));
            todayText.color = today > 0 ? gainColor : today < 0 ? lossColor : neutralColor;
        }

        if (bonusText != null)
        {
            bonusText.text = Loc.Format(
                "shop.reputation.bonus",
                ToPercent(ReputationSystem.CustomersMultiplier),
                ToPercent(ReputationSystem.MoneyMultiplier));
        }
    }

    /// <summary>1.2 → "+20". Siempre con signo: con reputación 0 dice "+0".</summary>
    private static string ToPercent(float multiplier)
        => "+" + Mathf.RoundToInt(Mathf.Max(0f, multiplier - 1f) * 100f);
}
