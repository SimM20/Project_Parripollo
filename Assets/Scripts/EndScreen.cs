using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class EndScreen : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI moneyText;
    [Tooltip("Cartel con la cantidad total de clientes que pasaron por el local en la jornada.")]
    [SerializeField] private TextMeshProUGUI customersText;
    [SerializeField] private GameObject UI;
    [SerializeField] private GameObject ShopRoot;
    private float newMoney = 0;
    private void Start()
    {
        GetNewMoney();
        UpdateUI(newMoney);
        UpdateCustomersUI(DayStats.CustomersToday);
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
}
