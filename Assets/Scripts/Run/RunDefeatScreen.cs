using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Pantalla de Derrota Total de la Run (spec punto 4 y "Pantalla — Derrota Total / Game Over").
///
/// Vive en EndScene y decide en <c>Awake</c>, antes que corra ningún <c>Start</c>, si la run
/// sigue. Si está perdida apaga la tienda y el popup de strikes y se muestra ella: el jugador
/// no llega a entrar a comprar algo que ya no puede pagar.
///
/// Mismo patrón de modal que <see cref="StrikeEndPopup"/>: canvas raíz activo con sorting
/// order alto y un hijo <c>root</c> inactivo con fondo opaco que bloquea los clicks de atrás.
/// </summary>
public class RunDefeatScreen : MonoBehaviour
{
    [Header("Lógica")]
    [SerializeField] private ShopSystem shop;

    [Tooltip("Solo se usa para mostrar los números del requisito. La evaluación la hace ShopSystem.")]
    [SerializeField] private RunDefeatConfigSO runDefeatConfig;

    [Header("Roots")]
    [Tooltip("Panel de la derrota. Arranca inactivo en la escena.")]
    [SerializeField] private GameObject root;

    [Tooltip("ShopCanvas: se apaga si la run está perdida (no se pasa por la tienda).")]
    [SerializeField] private GameObject shopCanvas;

    [Tooltip("StrikeEndPopupCanvas: se apaga si la run está perdida, su aviso ya no aporta.")]
    [SerializeField] private GameObject strikePopupCanvas;

    [Header("Text")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text reasonText;
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private TMP_Text meatLineText;
    [SerializeField] private TMP_Text coalLineText;
    [SerializeField] private TMP_Text deficitText;

    [Header("Buttons")]
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button retryButton;

    // Textos: claves defeat.* de las tablas de Loc.

    /// <summary>Motivo resuelto en Awake. <c>None</c> = la run sigue y esta pantalla no se mostró.</summary>
    public RunDefeatReason Reason { get; private set; }

    private void Awake()
    {
        if (root == null) root = gameObject;

        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(GoToMainMenu);
        if (retryButton != null) retryButton.onClick.AddListener(RestartRun);

        // Red de seguridad: si ninguna tienda llegó a abrirse todavía, este es el último
        // momento en que las mejoras siguen en su valor original.
        if (shop != null) RunStateReset.CaptureBaseline(shop.Catalog);

        RunEconomyStatus status;
        Reason = ResolveReason(out status);

        if (Reason == RunDefeatReason.None)
        {
            root.SetActive(false);
            return;
        }

        Show(status);
    }

    private void OnDestroy()
    {
        if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(GoToMainMenu);
        if (retryButton != null) retryButton.onClick.RemoveListener(RestartRun);
    }

    private RunDefeatReason ResolveReason(out RunEconomyStatus status)
    {
        status = default;

        // La racha manda: si se perdió por el cartel, ese es el motivo aunque la plata alcance.
        if (StrikeSystem.IsStrikeStreakDefeat)
        {
            if (CanEvaluateEconomy()) status = shop.GetRunStatus();
            return RunDefeatReason.StrikeStreak;
        }

        if (!CanEvaluateEconomy()) return RunDefeatReason.None;

        status = shop.GetRunStatus();

        return status.CanContinue ? RunDefeatReason.None : RunDefeatReason.InsufficientResources;
    }

    /// <summary>
    /// Sin tienda, sin mínimos activos o sin los DDOL de stock y plata no hay con qué juzgar.
    /// Pasa al abrir EndScene sola en el Editor: ahí no se muestra derrota.
    /// </summary>
    private bool CanEvaluateEconomy()
        => shop != null && shop.EnforceRunMinimums && shop.Cooler != null && shop.Wallet != null;

    private void Show(RunEconomyStatus status)
    {
        if (shopCanvas != null) shopCanvas.SetActive(false);
        if (strikePopupCanvas != null) strikePopupCanvas.SetActive(false);

        root.SetActive(true);

        Paint(status);

        Debug.Log("[RunDefeat] Run terminada. Motivo: " + Reason);
    }

    private void Paint(RunEconomyStatus status)
    {
        if (titleText != null) titleText.text = Loc.Get("defeat.title");

        if (reasonText != null)
        {
            reasonText.text = Reason == RunDefeatReason.StrikeStreak
                ? Loc.Format("defeat.reason.strike_streak", StrikeSystem.ConsecutiveStrikeNights)
                : Loc.Get("defeat.reason.resources");
        }

        // Día alcanzado = jornadas completadas. CurrentNight ya apunta a la que no se va a jugar.
        int daysPlayed = CoalConsumptionTracker.Instance != null
            ? CoalConsumptionTracker.Instance.DaysPlayed
            : 0;

        if (dayText != null) dayText.text = Loc.Format("defeat.day", daysPlayed);

        float money = shop != null && shop.Wallet != null ? shop.Wallet.Money : status.money;
        if (moneyText != null) moneyText.text = Loc.Format("defeat.money", money);

        if (meatLineText != null)
            meatLineText.text = Loc.Format("defeat.meat", status.meatStock, status.meatRequired, status.meatDeficit);

        if (coalLineText != null)
            coalLineText.text = Loc.Format("defeat.coal", status.coalStock, status.coalRequired, status.coalDeficit);

        if (deficitText != null)
        {
            // Si se perdió por la racha y la economía estaba sana, hablar de plata faltante
            // confunde el motivo real.
            bool economyIsFine = Reason == RunDefeatReason.StrikeStreak && status.MeetsMinimums;

            deficitText.gameObject.SetActive(!economyIsFine);

            if (!economyIsFine)
            {
                deficitText.text = status.HasBlockingGap
                    ? Loc.Get("defeat.unreachable")
                    : Loc.Format("defeat.deficit", Mathf.Max(0f, status.totalCost - status.money));
            }
        }
    }

    public void GoToMainMenu() => SceneManagementUtils.ReturnToMainMenu();

    public void RestartRun()
        => SceneManagementUtils.RestartRun(shop != null ? shop.Catalog : null);

    // ---- QA: forzar cada pantalla desde el inspector en Play Mode ----

    [ContextMenu("QA/Forzar derrota por recursos")]
    private void DebugForceResourceDefeat()
    {
        Reason = RunDefeatReason.InsufficientResources;
        Show(shop != null ? shop.GetRunStatus() : default);
    }

    [ContextMenu("QA/Forzar derrota por racha de strikes")]
    private void DebugForceStrikeDefeat()
    {
        Reason = RunDefeatReason.StrikeStreak;
        Show(shop != null ? shop.GetRunStatus() : default);
    }
}
