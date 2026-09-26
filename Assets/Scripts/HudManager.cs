using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HudManager : MonoBehaviour
{
    public static HudManager Instance { get; private set; }

    [SerializeField] private HudContainer[] containers;

    [SerializeField] private Button pauseButton;

    [Header("Money Popup")]
    [SerializeField] private MoneyPopupStyle moneyPopupStyle = new MoneyPopupStyle();
    [Tooltip("Escala máxima del contenedor de plata cuando llega un popup.")]
    [SerializeField] private float moneyPunchScale = 1.25f;
    [SerializeField] private float moneyPunchSeconds = 0.25f;
    [Tooltip("Duración del conteo animado del valor mostrado hacia el real.")]
    [SerializeField] private float moneyCountSeconds = 0.35f;

    public MoneyPopupStyle PopupStyle => moneyPopupStyle;

    // Plata: el valor real llega al instante desde PlayerWallet, pero el HUD lo muestra
    // recién cuando el popup "aterriza" (punch + conteo). Al bajar (compras) no hay popup y
    // el texto se actualiza al toque.
    private int displayedMoney;
    private int targetMoney;
    private bool moneyCountPending;
    private Coroutine moneyCountRoutine;
    private Coroutine moneyPunchRoutine;
    private Vector3 moneyBaseScale = Vector3.one;

    private void Awake()
    {
        Instance = this;

        if (pauseButton != null)
            pauseButton.onClick.AddListener(() => UIManager.Instance?.PauseGame());

        HudContainer money = GetContainer(HudContainers.Money);
        if (money != null) moneyBaseScale = money.transform.localScale;
    }

    private void Start() => AutoUpdateTexts();

    private void OnEnable()
    {
        Loc.OnLanguageChanged += RefreshDayText;
        AutoUpdateTexts();
    }

    private void RefreshDayText()
    {
        if (UIManager.Instance != null) UpdateDayText(UIManager.Instance.GetActualDay());
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void AutoUpdateTexts()
    {
        string newText = "";
        foreach (var container in containers)
        {
            if (container != null)
            {
                switch (container.GetContainerType())
                {
                    case HudContainers.Day:
                        container.UpdateText(FormatDay(UIManager.Instance != null ? UIManager.Instance.GetActualDay() : 1));
                        break;
                    case HudContainers.Money:
                        int money = UIManager.Instance != null ? UIManager.Instance.GetActualMoney() : 0;
                        displayedMoney = money;
                        targetMoney = money;
                        container.UpdateText(FormatMoney(money));
                        break;
                    case HudContainers.Customers:
                        int customers = UIManager.Instance != null ? UIManager.Instance.GetArrivedCustomers() : 0;
                        container.UpdateText(customers.ToString());
                        break;
                    case HudContainers.Time:
                        newText = UIManager.Instance?.GetDayTime();
                        if (!string.IsNullOrEmpty(newText))
                            container.UpdateText(newText);
                        break;
                }
            }
        }
    }

    public void UpdateDayText(int value)
    {
        if (!gameObject.activeInHierarchy) return;

        foreach (var container in containers)
        {
            if (container != null && container.GetContainerType() == HudContainers.Day)
                container.UpdateText(FormatDay(value));
        }
    }

    /// <summary>Hora de la jornada ("06:30") o el cartel de cerrado. La manda <see cref="DayClock"/> vía UIManager.</summary>
    public void UpdateTimeText(string value)
    {
        if (!gameObject.activeInHierarchy) return;

        foreach (var container in containers)
        {
            if (container != null && container.GetContainerType() == HudContainers.Time)
                container.UpdateText(value);
        }
    }

    public void UpdateMoneyText(int value)
    {
        if (!gameObject.activeInHierarchy) return;

        targetMoney = value;

        if (value > displayedMoney && MoneyPopup.InFlight > 0)
        {
            // Subió y hay un popup volando: él dispara el conteo al llegar (OnMoneyPopupArrived).
            // Si por lo que sea no llega, el conteo arranca solo pasado el vuelo.
            moneyCountPending = true;
            float fallback = moneyPopupStyle.popInSeconds + moneyPopupStyle.holdSeconds + moneyPopupStyle.flySeconds + 0.2f;
            StartCoroutine(MoneyCountFallback(fallback));
            return;
        }

        // Bajó, igual, o subió sin popup (arranque de escena): sin animación.
        moneyCountPending = false;
        if (moneyCountRoutine != null) { StopCoroutine(moneyCountRoutine); moneyCountRoutine = null; }
        displayedMoney = value;
        SetMoneyText(value);
    }

    /// <summary>Transform del contenedor de plata, para que el popup sepa adónde volar.</summary>
    public Transform GetMoneyAnchor()
    {
        HudContainer money = GetContainer(HudContainers.Money);
        return money != null ? money.transform : null;
    }

    /// <summary>Llamado por MoneyPopup al aterrizar: punch del contenedor y conteo hasta el valor real.</summary>
    public void OnMoneyPopupArrived()
    {
        if (moneyPunchRoutine != null) StopCoroutine(moneyPunchRoutine);
        moneyPunchRoutine = StartCoroutine(PunchMoney());

        StartMoneyCount();
    }

    private void StartMoneyCount()
    {
        if (!moneyCountPending) return;
        moneyCountPending = false;

        if (moneyCountRoutine != null) StopCoroutine(moneyCountRoutine);
        moneyCountRoutine = StartCoroutine(CountMoney(displayedMoney, targetMoney));
    }

    private IEnumerator MoneyCountFallback(float delay)
    {
        yield return new WaitForSeconds(delay);
        StartMoneyCount();
    }

    private IEnumerator CountMoney(int from, int to)
    {
        float t = 0f;
        float duration = Mathf.Max(0.01f, moneyCountSeconds);

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            k = 1f - (1f - k) * (1f - k);   // ease-out
            displayedMoney = Mathf.RoundToInt(Mathf.Lerp(from, to, k));
            SetMoneyText(displayedMoney);
            yield return null;
        }

        displayedMoney = to;
        SetMoneyText(to);
        moneyCountRoutine = null;
    }

    private IEnumerator PunchMoney()
    {
        HudContainer money = GetContainer(HudContainers.Money);
        if (money == null) yield break;

        Transform target = money.transform;
        float t = 0f;
        float duration = Mathf.Max(0.01f, moneyPunchSeconds);

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float s = Mathf.Lerp(moneyPunchScale, 1f, k);   // arranca grande y vuelve
            target.localScale = moneyBaseScale * s;
            yield return null;
        }

        target.localScale = moneyBaseScale;
        moneyPunchRoutine = null;
    }

    private void SetMoneyText(int value)
    {
        foreach (var container in containers)
        {
            if (container != null && container.GetContainerType() == HudContainers.Money)
                container.UpdateText(FormatMoney(value));
        }
    }

    private static string FormatDay(int value) => Loc.Format("hud.day", value);

    private static string FormatMoney(int value) => value.ToString() + " $";

    private HudContainer GetContainer(HudContainers type)
    {
        if (containers == null) return null;

        foreach (var container in containers)
        {
            if (container != null && container.GetContainerType() == type)
                return container;
        }

        return null;
    }

    /// <summary>
    /// Clientes que aparecieron en lo que va del día: un contador que arranca en 0
    /// y suma 1 por cada cliente nuevo.
    /// </summary>
    public void UpdateCustomersText(int customers)
    {
        if (!gameObject.activeInHierarchy) return;

        foreach (var container in containers)
        {
            if (container != null && container.GetContainerType() == HudContainers.Customers)
                container.UpdateText(customers.ToString());
        }
    }

    private void OnDisable()
    {
        Loc.OnLanguageChanged -= RefreshDayText;

        if (pauseButton != null)
            pauseButton.onClick.RemoveAllListeners();
    }
}
