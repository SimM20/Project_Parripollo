using System;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ShopButton2D : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite hoverSprite;
    [SerializeField] private Sprite disabledSprite;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = new Color(1f, 1f, 0.85f);
    [SerializeField] private Color disabledColor = new Color(1f, 1f, 1f, 0.4f);

    public event Action OnClicked;
    public bool Interactable { get; private set; } = true;

    private Collider2D col;
    private bool isHovered;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        if (iconRenderer == null) iconRenderer = GetComponent<SpriteRenderer>();
        ApplyVisual();
    }

    public void SetInteractable(bool value)
    {
        if (Interactable == value) return;
        Interactable = value;
        if (col != null) col.enabled = value;
        if (!value) isHovered = false;
        ApplyVisual();
    }

    void OnMouseEnter()
    {
        if (!Interactable) return;
        isHovered = true;
        ApplyVisual();
    }

    void OnMouseExit()
    {
        isHovered = false;
        ApplyVisual();
    }

    void OnMouseUpAsButton()
    {
        if (Interactable) OnClicked?.Invoke();
    }

    void OnDisable()
    {
        isHovered = false;
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        if (iconRenderer == null) return;

        if (!Interactable)
        {
            if (disabledSprite != null) iconRenderer.sprite = disabledSprite;
            else if (normalSprite != null) iconRenderer.sprite = normalSprite;
            iconRenderer.color = disabledColor;
            return;
        }

        if (isHovered && hoverSprite != null) iconRenderer.sprite = hoverSprite;
        else if (normalSprite != null) iconRenderer.sprite = normalSprite;

        iconRenderer.color = isHovered ? hoverColor : normalColor;
    }
}