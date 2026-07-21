using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class ShopTabButtonUI : MonoBehaviour
{
    [SerializeField] private ShopTabType tab;
    [SerializeField] private Image background;
    [SerializeField] private TextMeshProUGUI label;

    [Header("Visual States")]
    [SerializeField] private Color activeBgColor = new Color(0.8f, 0.4f, 0.15f);
    [SerializeField] private Color inactiveBgColor = new Color(0.3f, 0.3f, 0.3f);
    [SerializeField] private Color activeTextColor = Color.white;
    [SerializeField] private Color inactiveTextColor = new Color(0.7f, 0.7f, 0.7f);

    public ShopTabType Tab => tab;
    public System.Action<ShopTabType> OnTabClicked;

    private Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(HandleClick);
    }

    void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(HandleClick);
    }

    public void SetActiveState(bool isActive)
    {
        if (background != null)
            background.color = isActive ? activeBgColor : inactiveBgColor;
        if (label != null)
            label.color = isActive ? activeTextColor : inactiveTextColor;
    }

    private void HandleClick()
    {
        OnTabClicked?.Invoke(tab);
    }
}