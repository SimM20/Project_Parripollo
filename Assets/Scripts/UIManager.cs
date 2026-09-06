using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [SerializeField] private HudManager hudManager;
    [SerializeField] private GameObject pauseCanvasPrefab;
    private GameObject pauseCanvasInstance;

    private int actualCustomers;
    private int totalCustomers;
    private int actualMoney;
    private int actualDay;
    
    [Header("Strikes HUD")]
    [SerializeField] private TMPro.TextMeshProUGUI strikesText;
    [SerializeField] private string strikesFormat = "Strikes: {0}/{1}";

    [Header("Strikes Colors")]
    [SerializeField] private Color strikesNormalColor = Color.white;
    [SerializeField] private Color strikesWarningColor = new Color(1f, 0.6f, 0.2f);   // ~2/3
    [SerializeField] private Color strikesMaxColor = Color.red;                        // 3/3

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        if (PlayerWallet.Instance != null)
        {
            PlayerWallet.Instance.OnMoneyChanged += SetActualMoney;
            SetActualMoney(PlayerWallet.Instance.Money);
        }
    }

    private void OnDisable()
    {
        if (PlayerWallet.Instance != null)
            PlayerWallet.Instance.OnMoneyChanged -= SetActualMoney;
    }

    public bool IsPaused => pauseCanvasInstance != null && pauseCanvasInstance.activeSelf;

    public void PauseGame()
    {
        if (!pauseCanvasInstance)
            pauseCanvasInstance = Instantiate(pauseCanvasPrefab);

        pauseCanvasInstance.SetActive(true);
    }

    public void UnPauseGame()
    {
        if (!pauseCanvasInstance) return;

        pauseCanvasInstance.SetActive(false);
    }

    public void SetActualDay(int newDay)
    {
        actualDay = newDay;
        if (hudManager != null)
            hudManager.UpdateDayText(actualDay);
    }

    public void SetTotalCustomers(int newCustomers)
    {
        totalCustomers = newCustomers;
        if (hudManager != null)
            hudManager.UpdateCustomersText(actualCustomers, totalCustomers);
    }

    public void SetActualCustomers(int newCustomers)
    {
        actualCustomers = newCustomers;
        if (hudManager != null)
            hudManager.UpdateCustomersText(actualCustomers, totalCustomers);
    }

    public void SetActualMoney(float newMoney)
    {
        actualMoney = (int)newMoney;
        if (hudManager != null)
            hudManager.UpdateMoneyText(actualMoney);
    }
    
    public void SetStrikes(int current, int max)
    {
        if (strikesText == null) return;

        strikesText.text = string.Format(strikesFormat, current, max);

        // Color escala según proximidad al máximo
        if (current >= max)
            strikesText.color = strikesMaxColor;
        else if (max > 0 && current >= max - 1)
            strikesText.color = strikesWarningColor;
        else
            strikesText.color = strikesNormalColor;
    }    
    public void TriggerStrikeShake()
    {
        if (strikesText == null) return;
        StartCoroutine(ShakeText(strikesText.transform));
    }

    private System.Collections.IEnumerator ShakeText(Transform target)
    {
        Vector3 originalPos = target.localPosition;
        float duration = 0.3f;
        float magnitude = 6f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float x = UnityEngine.Random.Range(-1f, 1f) * magnitude;
            float y = UnityEngine.Random.Range(-1f, 1f) * magnitude;
            target.localPosition = originalPos + new Vector3(x, y, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        target.localPosition = originalPos;
    }
    public int GetActualDay() { return actualDay; }
    public int GetActualMoney() { return actualMoney; }
    public int GetTotalCustomersPerDay() { return totalCustomers; }
    public int GetActualCustomers() { return actualCustomers; }
}