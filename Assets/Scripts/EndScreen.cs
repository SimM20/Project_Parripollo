using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class EndScreen : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI moneyText;
    [SerializeField] private GameObject UI;
    [SerializeField] private GameObject ShopRoot;
    private float newMoney = 0;
    private void Start()
    {
        GetNewMoney();
        UpdateUI(newMoney);
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
}
