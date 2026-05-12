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

    private static void SetSlotVisuals(GridSlot slot, bool visible)
    {
        SpriteRenderer sr = slot.GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = visible;

        Collider2D col = slot.GetComponent<Collider2D>();
        if (col != null) col.enabled = visible;

        if (slot.currentItem != null)
            SetGameObjectVisuals(slot.currentItem, visible);

        if (slot.stackedCoals != null)
        {
            foreach (var coal in slot.stackedCoals)
            {
                if (coal != null)
                    SetGameObjectVisuals(coal.gameObject, visible);
            }
        }
    }

    private static void SetGameObjectVisuals(GameObject obj, bool visible)
    {
        if (obj == null) return;

        SpriteRenderer[] renderers = obj.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = visible;

        Collider2D[] colliders = obj.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = visible;
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
