using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StrikeClosurePopupUI : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private StrikeConfigSO config;

    [Header("References")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private TextMeshProUGUI buttonText;
    [SerializeField] private Button continueButton;

    void Awake()
    {
        Debug.Log($"[StrikeClosurePopupUI] Awake en {gameObject.name}");

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinueClicked);

        if (popupRoot != null) popupRoot.SetActive(false);
    }

    void Start()
    {
        Debug.Log($"[StrikeClosurePopupUI] Start ejecutado");

        var flag = StrikeSessionFlag.Instance;
        Debug.Log($"[StrikeClosurePopupUI] Flag instance: {(flag == null ? "NULL" : "existe")}");

        if (flag != null)
            Debug.Log($"[StrikeClosurePopupUI] ClosedByStrikes: {flag.ClosedByStrikes}");

        if (flag == null || !flag.ClosedByStrikes)
        {
            Debug.Log($"[StrikeClosurePopupUI] No corresponde mostrar popup, saliendo");
            return;
        }

        Debug.Log($"[StrikeClosurePopupUI] Va a mostrar popup");
        ShowPopup();
    }

    private void ShowPopup()
    {
        Debug.Log($"[StrikeClosurePopupUI] ShowPopup() llamado");

        if (config == null)
        {
            Debug.LogError("[StrikeClosurePopupUI] Falta StrikeConfigSO asignado.");
            return;
        }

        if (titleText != null) titleText.text = config.popupTitle;
        else Debug.LogWarning("[StrikeClosurePopupUI] titleText es null");

        if (bodyText != null) bodyText.text = config.popupBody;
        else Debug.LogWarning("[StrikeClosurePopupUI] bodyText es null");

        if (buttonText != null) buttonText.text = config.popupButtonText;
        else Debug.LogWarning("[StrikeClosurePopupUI] buttonText es null");

        if (popupRoot != null)
        {
            popupRoot.SetActive(true);
            Debug.Log($"[StrikeClosurePopupUI] popupRoot activado. ActiveSelf: {popupRoot.activeSelf}, ActiveInHierarchy: {popupRoot.activeInHierarchy}");
        }
        else Debug.LogError("[StrikeClosurePopupUI] popupRoot es null");
    }

    private void OnContinueClicked()
    {
        if (popupRoot != null) popupRoot.SetActive(false);
        StrikeSessionFlag.Instance?.Reset();
    }
}