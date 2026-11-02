using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class HudContainer: MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private HudContainers type;
    [SerializeField] private HudDatabaseSO data;

    private Color baseTextColor = Color.white;
    private bool baseColorCaptured;

    private void Awake()
    {
        CaptureBaseColor();

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

    /// <summary>Tiñe el texto (null = color original del prefab). Solo presentación.</summary>
    public void SetTextTint(Color? tint)
    {
        if (text == null) return;
        CaptureBaseColor();
        text.color = tint ?? baseTextColor;
    }

    private void CaptureBaseColor()
    {
        if (baseColorCaptured || text == null) return;
        baseTextColor = text.color;
        baseColorCaptured = true;
    }
}

public enum HudContainers
{
    None,
    Day,
    Money,
    Customers,
    /// <summary>Reloj de la jornada: la hora que manda <see cref="DayClock"/>.</summary>
    Time
}