using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class EndScreen : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI moneyText;
    [Tooltip("Cartel con la cantidad total de clientes que pasaron por el local en la jornada.")]
    [SerializeField] private TextMeshProUGUI customersText;
    [Tooltip("Lo cobrado por la comida en la jornada (sin propinas). Opcional.")]
    [SerializeField] private TextMeshProUGUI salesText;
    [Tooltip("Propinas de la jornada y qué parte de lo cobrado representan. Opcional.")]
    [SerializeField] private TextMeshProUGUI tipsText;
    [SerializeField] private GameObject UI;
    [SerializeField] private GameObject ShopRoot;
    private float newMoney = 0;
    private void Start()
    {
        GetNewMoney();
        UpdateUI(newMoney);
        UpdateCustomersUI(DayStats.CustomersToday);
        UpdateIncomeUI(DayStats.SalesToday, DayStats.TipsToday);
    }
    public void GoToMainMenu() => SceneManagementUtils.ReturnToMainMenu();

    public void RetryGame() => SceneManagementUtils.LoadSceneByName("GameScene");

    public void GoShopping()
    {
        UI.SetActive(false);
        ShopRoot.SetActive(true);
    }

    public void GetNewMoney()
    {
        newMoney = PlayerWallet.Instance != null ? PlayerWallet.Instance.Money : 0f;
    }

    public void UpdateUI(float newMoney) { moneyText.text = newMoney.ToString(); }

    /// <summary>Cartel de cierre: cuántos clientes hubo en la jornada que acaba de terminar.</summary>
    public void UpdateCustomersUI(int customers)
    {
        if (customersText != null)
            customersText.text = customers.ToString();
    }

    /// <summary>Desglose del cobro: cuánto fue comida y cuánto propina.</summary>
    public void UpdateIncomeUI(float sales, float tips)
    {
        if (salesText != null)
            salesText.text = "$" + (int)sales;

        if (tipsText != null)
        {
            float income = sales + tips;
            int share = income > 0f ? Mathf.RoundToInt(tips / income * 100f) : 0;
            tipsText.text = Loc.Format("endscreen.tips.detail", (int)tips, share);
        }
    }
}
