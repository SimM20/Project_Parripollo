using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class EndScreen : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI moneyText;
    private float newMoney = 0;
    private void Start()
    {
        GetNewMoney();
        UpdateUI(newMoney);
    }
    public void GoToMainMenu() => SceneManagementUtils.LoadSceneByName("MainMenuScene");

    public void RetryGame() => SceneManagementUtils.LoadSceneByName("GameScene");

    public void GetNewMoney()
    {

    }

    public void UpdateUI(float newMoney) { moneyText.text = newMoney.ToString(); }
}
