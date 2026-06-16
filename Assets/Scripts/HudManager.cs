using UnityEngine;
using UnityEngine.UI;

public class HudManager : MonoBehaviour
{
    [SerializeField] private HudContainer[] containers;

    [SerializeField] private Button pauseButton;

    private void Awake() => pauseButton?.onClick.AddListener(UIManager.Instance.PauseGame);
    private void Start() => AutoUpdateTexts();
    private void OnEnable() => AutoUpdateTexts();

    private void AutoUpdateTexts()
    {
        string newText = "";
        foreach (var container in containers)
        {
            if (container != null)
            {
                switch (container.GetContainerType())
                {
                    case HudContainers.Day:
                        newText = UIManager.Instance?.GetActualDay().ToString();
                        container.UpdateText(newText);
                        break;
                    case HudContainers.Money:
                        newText = UIManager.Instance?.GetActualMoney().ToString();
                        container.UpdateText(newText);
                        break;
                    case HudContainers.Customers:
                        newText = UIManager.Instance?.GetActualCustomers().ToString() + "/" + UIManager.Instance?.GetTotalCustomersPerDay().ToString();
                        container.UpdateText(newText);
                        break;
                }
            }
        }
    }

    public void UpdateDayText(int value)
    {
        if (!gameObject.activeInHierarchy) return;

        foreach (var container in containers)
        {
            if (container != null && container.GetContainerType() == HudContainers.Day)
                container.UpdateText("DÍA " + value.ToString());
        }
    }

    public void UpdateMoneyText(int value)
    {
        if (!gameObject.activeInHierarchy) return;

        foreach (var container in containers)
        {
            if (container != null && container.GetContainerType() == HudContainers.Money)
                container.UpdateText(value.ToString() + " $");
        }
    }

    public void UpdateCustomersText(int value1, int value2)
    {
        if (!gameObject.activeInHierarchy) return;

        foreach (var container in containers)
        {
            if (container != null && container.GetContainerType() == HudContainers.Customers)
                container.UpdateText(value1.ToString() + "/" + value2.ToString());
        }
    }

    private void OnDisable() => pauseButton.onClick.RemoveAllListeners();
}