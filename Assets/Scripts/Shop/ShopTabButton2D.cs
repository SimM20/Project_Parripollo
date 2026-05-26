using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ShopTabButton2D : MonoBehaviour
{
    [SerializeField] private ShopTabType tab;
    [SerializeField] private SpriteRenderer background;
    [SerializeField] private TextMeshPro label;

    [Header("Visual States")]
    [SerializeField] private Color activeColor = new Color(1f, 0.9f, 0.7f);
    [SerializeField] private Color inactiveColor = new Color(0.5f, 0.4f, 0.3f);
    [SerializeField] private Color activeTextColor = Color.white;
    [SerializeField] private Color inactiveTextColor = new Color(0.8f, 0.7f, 0.6f);

    public ShopTabType Tab => tab;
    public System.Action<ShopTabType> OnTabClicked;

    public void SetActive(bool isActive)
    {
        if (background != null)
            background.color = isActive ? activeColor : inactiveColor;
        if (label != null)
            label.color = isActive ? activeTextColor : inactiveTextColor;
    }

    void OnMouseUpAsButton()
    {
        OnTabClicked?.Invoke(tab);
    }
}