using UnityEngine;

public class GrillLayerToggle : MonoBehaviour
{
    public enum GrillLayer { Meat, Coal }

    [Header("Grill Reference")]
    [SerializeField] private GrillSystem grillSystem;

    [Header("Switch Button Visuals")]
    [SerializeField] private SpriteRenderer switchButtonRenderer;
    [SerializeField] private Sprite meatIcon;
    [SerializeField] private Sprite coalIcon;

    [Header("State")]
    [SerializeField] private GrillLayer startLayer = GrillLayer.Meat;
    [SerializeField] [Range(0f, 1f)] private float inactiveAlpha = 0.3f;

    public GrillLayer CurrentLayer { get; private set; }

    private static GrillLayerToggle instance;

    public static bool IsItemTypeAllowed(ItemType type)
    {
        if (instance == null) return true;

        if (type == ItemType.Meat)
            return instance.CurrentLayer == GrillLayer.Meat;
        if (type == ItemType.Coal)
            return instance.CurrentLayer == GrillLayer.Coal;

        return true;
    }

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        ShowLayer(startLayer);
    }

    void OnMouseDown()
    {
        Toggle();
    }

    public void Toggle()
    {
        ShowLayer(CurrentLayer == GrillLayer.Meat ? GrillLayer.Coal : GrillLayer.Meat);
    }

    public void ShowLayer(GrillLayer layer)
    {
        CurrentLayer = layer;
        ApplyVisibility();
        UpdateButtonIcon();
    }

    public void RefreshVisibility()
    {
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        if (grillSystem == null) return;

        foreach (var slot in grillSystem.slots)
        {
            if (slot == null) continue;

            bool shouldShow = (CurrentLayer == GrillLayer.Meat && slot.acceptsType == ItemType.Meat)
                           || (CurrentLayer == GrillLayer.Coal && slot.acceptsType == ItemType.Coal);

            SetSlotVisuals(slot, shouldShow);
        }
    }

    private void SetSlotVisuals(GridSlot slot, bool active)
    {
        SpriteRenderer sr = slot.GetComponent<SpriteRenderer>();
        if (sr != null) SetAlpha(sr, active ? 1f : inactiveAlpha);

        Collider2D col = slot.GetComponent<Collider2D>();
        if (col != null) col.enabled = active;

        if (slot.currentItem != null)
            SetGameObjectVisuals(slot.currentItem, active);

        if (slot.stackedCoals != null)
        {
            foreach (var coal in slot.stackedCoals)
            {
                if (coal != null)
                    SetGameObjectVisuals(coal.gameObject, active);
            }
        }
    }

    private void SetGameObjectVisuals(GameObject obj, bool active)
    {
        if (obj == null) return;

        SpriteRenderer[] renderers = obj.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = true;
            SetAlpha(renderers[i], active ? 1f : inactiveAlpha);
        }

        Collider2D[] colliders = obj.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = active;
    }

    private static void SetAlpha(SpriteRenderer sr, float alpha)
    {
        Color c = sr.color;
        c.a = alpha;
        sr.color = c;
    }

    private void UpdateButtonIcon()
    {
        if (switchButtonRenderer == null) return;

        if (CurrentLayer == GrillLayer.Meat && coalIcon != null)
            switchButtonRenderer.sprite = coalIcon;
        else if (CurrentLayer == GrillLayer.Coal && meatIcon != null)
            switchButtonRenderer.sprite = meatIcon;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
