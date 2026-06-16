using TMPro;
using UnityEngine;

public class WalletDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI moneyText;

    private void Start()
    {
        if (PlayerWallet.Instance != null)
        {
            PlayerWallet.Instance.OnMoneyChanged += OnMoneyChanged;
            SetText(PlayerWallet.Instance.Money);
        }
        else if (moneyText != null)
        {
            moneyText.text = "$ 0";
        }
    }

    private void OnDestroy()
    {
        if (PlayerWallet.Instance != null)
            PlayerWallet.Instance.OnMoneyChanged -= OnMoneyChanged;
    }

    private void OnMoneyChanged(float amount) => SetText(amount);

    private void SetText(float amount)
    {
        if (moneyText != null)
            moneyText.text = $"$ {(int)amount}";
    }
}
