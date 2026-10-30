using System.Collections;
using UnityEngine;
using TMPro;

public class ShopHeaderUI : MonoBehaviour
{
    [SerializeField] private ShopSystem shop;
    [SerializeField] private TextMeshProUGUI shopNameText;
    [SerializeField] private TextMeshProUGUI moneyText;
    [SerializeField] private TextMeshProUGUI totalCoalText;   // ← NUEVO
    [Tooltip("Cartel con los clientes que pasaron por el local en la jornada que acaba de terminar.")]
    [SerializeField] private TextMeshProUGUI customersTodayText;
    [Tooltip("Nombre del local: es la marca, no se traduce.")]
    [SerializeField] private string shopName = "LA PARRILLA DE DON COCO";

    [Header("Compra: animación de la plata")]
    [Tooltip("Lo que late al comprar (el cartel de la plata). Vacío = el texto. Con pivot al centro, crece parejo.")]
    [SerializeField] private Transform moneyPulseTarget;
    [Tooltip("Segundos que tarda el contador en bajar del monto anterior al nuevo, número por número.")]
    [SerializeField] [Min(0.01f)] private float spendCountSeconds = 0.6f;
    [Tooltip("Rojo cálido del texto mientras baja.")]
    [SerializeField] private Color spendTint = new Color(1f, 0.48f, 0.32f, 1f);
    [Tooltip("Segundos que tarda en volver a su color después de llegar al monto nuevo.")]
    [SerializeField] [Min(0.01f)] private float tintFadeSeconds = 0.25f;
    [Tooltip("Cuánto crece en el primer golpe del latido (0.1 = 10 %). El segundo es más chico.")]
    [SerializeField] private float beatScale = 0.1f;
    [Tooltip("Segundos de cada golpe del latido.")]
    [SerializeField] [Min(0.01f)] private float beatSeconds = 0.16f;

    private bool started;

    // Plata que muestra el cartel: durante el conteo va por detrás de la billetera.
    private float shownMoney;
    private Coroutine spendRoutine;
    private Color moneyBaseColor = Color.white;
    private Vector3 pulseBaseScale = Vector3.one;

    void Awake()
    {
        if (moneyPulseTarget == null && moneyText != null) moneyPulseTarget = moneyText.transform;
        if (moneyText != null) moneyBaseColor = moneyText.color;
        if (moneyPulseTarget != null) pulseBaseScale = moneyPulseTarget.localScale;
    }

    void OnEnable()
    {
        Loc.OnTextsChanged += Refresh;
        if (shop != null)
        {
            if (shop.Wallet != null) shop.Wallet.OnMoneyChanged += OnMoneyChanged;
            if (shop.Cooler != null) shop.Cooler.OnInventoryChanged += Refresh;   // ← NUEVO
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
            if (shop.Cooler != null) shop.Cooler.OnInventoryChanged -= Refresh;   // ← NUEVO
        }

        // La corrutina muere con el componente: el cartel queda en reposo y con la plata real.
        spendRoutine = null;
        if (moneyText != null) moneyText.color = moneyBaseColor;
        if (moneyPulseTarget != null) moneyPulseTarget.localScale = pulseBaseScale;
    }

    private void OnMoneyChanged(float money)
    {
        // Gastó (una compra): late, se pone rojo y baja contando. Si sube, se actualiza al toque.
        if (money < shownMoney && moneyText != null && isActiveAndEnabled)
        {
            if (spendRoutine != null) StopCoroutine(spendRoutine);
            spendRoutine = StartCoroutine(SpendAnimation(shownMoney, money));
        }

        Refresh();
    }

    private void Refresh()
    {
        if (shopNameText != null) shopNameText.text = shopName;

        if (moneyText != null && shop != null && shop.Wallet != null)
        {
            if (spendRoutine == null) shownMoney = shop.Wallet.Money;
            SetMoneyText(shownMoney);
        }

        if (totalCoalText != null && shop != null)
            totalCoalText.text = Loc.Format("shop.header.coal", shop.GetTotalCoalUnits());

        // Clientes de la jornada que acaba de terminar (lo dejó CustomerSystem en DayStats).
        if (customersTodayText != null)
        {
            int customers = DayStats.CustomersToday;
            customersTodayText.text = Loc.Format(customers == 1 ? "shop.header.customers.one" : "shop.header.customers", customers);
        }
    }

    private void SetMoneyText(float money) => moneyText.text = $"${Mathf.Round(money):N0}";

    /// <summary>
    /// Conteo del monto anterior al nuevo (frena al llegar, así los últimos números se leen de a
    /// uno), rojo cálido mientras baja y un latido de dos golpes. Una compra encima de otra
    /// arranca desde lo que se estaba mostrando. Tiempo sin escalar, como el resto de la UI.
    /// </summary>
    private IEnumerator SpendAnimation(float from, float to)
    {
        float t = 0f;
        float total = Mathf.Max(spendCountSeconds + tintFadeSeconds, beatSeconds * 2.2f);

        while (t < total)
        {
            // Tope por frame: un tirón (la compra refresca todas las tarjetas) no se come la animación.
            t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);

            shownMoney = Mathf.Lerp(from, to, FeelEase.OutCubic(t / spendCountSeconds));
            SetMoneyText(shownMoney);

            float fade = Mathf.Clamp01((t - spendCountSeconds) / tintFadeSeconds);
            moneyText.color = Color.Lerp(spendTint, moneyBaseColor, fade);

            if (moneyPulseTarget != null)
                moneyPulseTarget.localScale = pulseBaseScale * (1f + Heartbeat(t));

            yield return null;
        }

        shownMoney = to;
        SetMoneyText(to);
        moneyText.color = moneyBaseColor;
        if (moneyPulseTarget != null) moneyPulseTarget.localScale = pulseBaseScale;
        spendRoutine = null;
    }

    /// <summary>Latido "pum-pum": un golpe y enseguida otro más chico. 0 fuera de los golpes.</summary>
    private float Heartbeat(float t)
    {
        float first = FeelEase.Bump(t / beatSeconds);
        float second = FeelEase.Bump((t - beatSeconds * 1.15f) / beatSeconds) * 0.55f;
        return beatScale * (first + second);
    }
}
