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

    public int GetActualDay() { return actualDay; }
    public int GetActualMoney() { return actualMoney; }
    public int GetTotalCustomersPerDay() { return totalCustomers; }
    public int GetActualCustomers() { return actualCustomers; }
}