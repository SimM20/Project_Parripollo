using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [SerializeField] private HudManager hudManager;
    [SerializeField] private GameObject pauseCanvasPrefab;
    private GameObject pauseCanvasInstance;

    private int servedCustomers;
    private int arrivedCustomers;
    private int actualMoney;
    private int actualDay;
    private string dayTime = "";

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

    public bool IsPaused => GamePause.IsMenuPaused;

    public void PauseGame()
    {
        if (!pauseCanvasInstance)
            pauseCanvasInstance = Instantiate(pauseCanvasPrefab);

        pauseCanvasInstance.SetActive(true);
        GamePause.SetMenuPaused(true);
    }

    public void UnPauseGame()
    {
        if (pauseCanvasInstance)
            pauseCanvasInstance.SetActive(false);

        GamePause.SetMenuPaused(false);
    }

    public void SetActualDay(int newDay)
    {
        actualDay = newDay;
        if (hudManager != null)
            hudManager.UpdateDayText(actualDay);
    }

    /// <summary>Hora de la jornada tal como la muestra el HUD ("06:30", "CERRADO"). La escribe <see cref="DayClock"/>.</summary>
    public void SetDayTime(string newTime)
    {
        dayTime = newTime;
        if (hudManager != null)
            hudManager.UpdateTimeText(dayTime);
    }

    /// <summary>Clientes que aparecieron en lo que va del día: es el número que muestra el HUD.</summary>
    public void SetArrivedCustomers(int newCustomers)
    {
        arrivedCustomers = newCustomers;
        if (hudManager != null)
            hudManager.UpdateCustomersText(arrivedCustomers);
    }

    /// <summary>
    /// Clientes que se fueron con su pedido entregado. No se muestra en el HUD
    /// (que cuenta los que aparecieron), queda para el resumen de la jornada.
    /// </summary>
    public void SetServedCustomers(int newCustomers)
    {
        servedCustomers = newCustomers;
    }

    public void SetActualMoney(float newMoney)
    {
        actualMoney = (int)newMoney;
        if (hudManager != null)
            hudManager.UpdateMoneyText(actualMoney);
    }

    public int GetActualDay() { return actualDay; }
    public int GetActualMoney() { return actualMoney; }
    public string GetDayTime() { return dayTime; }
    public int GetArrivedCustomers() { return arrivedCustomers; }
    public int GetServedCustomers() { return servedCustomers; }
}