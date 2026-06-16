using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class HudContainer: MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private HudContainers type;
    [SerializeField] private HudDatabaseSO data;

    private void Awake()
    {
        if (data == null || type == HudContainers.None)
            return;

        HudData myData = data.GetData(type);

        if (icon != null && myData.icon != null)
            icon.sprite = myData.icon;

        if (text != null && !string.IsNullOrEmpty(myData.defaultText))
            text.text = myData.defaultText;
    }

    public HudContainers GetContainerType() { return type; }
    public void UpdateText(string newText) { text.text = newText; }
}

public enum HudContainers
{
    None,
    Day,
    Money,
    Customers
}