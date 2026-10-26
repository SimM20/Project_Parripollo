using System.Collections.Generic;
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

    [Header("Sprite Toggle Settings")]
    [SerializeField] private SpriteRenderer targetToggleRenderer;
    [SerializeField] private Sprite buttonVariable1;
    [SerializeField] private Sprite buttonVariable2;

    [Header("State")]
    [SerializeField] private GrillLayer startLayer = GrillLayer.Meat;
    [SerializeField] [Range(0f, 1f)] private float inactiveAlpha = 0.3f;

    [Header("Feedback")]
    [Tooltip("Segundos del fundido de alfa entre capas. Los colliders cambian al instante: solo se suaviza el dibujo.")]
    [SerializeField] [Min(0f)] private float layerFadeSeconds = 0.12f;
    [Tooltip("Onda que se abre detras del boton al cambiar de capa.")]
    [SerializeField] private bool buttonEcho = true;

    public GrillLayer CurrentLayer { get; private set; }

    /// <summary>Toggle de la escena: el StockPanel pasa solo a la capa de lo que se toma (carbon o carne).</summary>
    public static GrillLayerToggle Current => instance;

    private static GrillLayerToggle instance;
    private static readonly List<SpriteRenderer> RendererBuffer = new List<SpriteRenderer>();

    // 0 = capa carne al frente, 1 = capa carbon al frente. Solo presentacion (alfa).
    private float layerBlend;

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
        if (targetToggleRenderer == null)
            targetToggleRenderer = GetComponent<SpriteRenderer>();

        if (targetToggleRenderer != null && buttonVariable1 != null)
            targetToggleRenderer.sprite = buttonVariable1;

        layerBlend = BlendFor(startLayer);
        ShowLayer(startLayer);
    }

    void Update()
    {
        float target = BlendFor(CurrentLayer);
        if (Mathf.Approximately(layerBlend, target)) return;

        layerBlend = layerFadeSeconds > 0f
            ? Mathf.MoveTowards(layerBlend, target, Time.unscaledDeltaTime / layerFadeSeconds)
            : target;

        ApplyLayerAlpha();
    }

    private static float BlendFor(GrillLayer layer) => layer == GrillLayer.Coal ? 1f : 0f;

    /// <summary>Alfa actual de los items de cada tipo segun el fundido en curso.</summary>
    private float AlphaFor(ItemType type)
    {
        float k = FeelEase.OutCubic(type == ItemType.Coal ? layerBlend : 1f - layerBlend);
        return Mathf.Lerp(inactiveAlpha, 1f, k);
    }

    void OnWorldPointerDown()
    {
        Toggle();
    }

    public void Toggle()
    {
        GrillLayer nextLayer = CurrentLayer == GrillLayer.Meat ? GrillLayer.Coal : GrillLayer.Meat;
        if (!TutorialManager.CheckGrillLayerToggleAllowed(nextLayer))
        {
            Debug.Log($"[GrillLayerToggle] Cambio de capa a {nextLayer} bloqueado por el tutorial.");
            return;
        }

        ShowLayer(nextLayer);
        ToggleSprite();
        GamepadHaptics.Play(HapticEvent.GrillLayerToggled);

        if (buttonEcho && targetToggleRenderer != null)
            SpriteGhost.Spawn(targetToggleRenderer, SpriteGhost.Motion.Echo(1.12f, 0.4f, 0.2f));
    }

    private void ToggleSprite()
    {
        if (targetToggleRenderer == null) return;

        if (targetToggleRenderer.sprite == buttonVariable1)
        {
            if (buttonVariable2 != null)
                targetToggleRenderer.sprite = buttonVariable2;
        }
        else
        {
            if (buttonVariable1 != null)
                targetToggleRenderer.sprite = buttonVariable1;
        }
    }

    public void ShowLayer(GrillLayer layer)
    {
        CurrentLayer = layer;
        ApplyVisibility();
        UpdateButtonIcon();
        TutorialSignals.Raise(TutorialSignal.GrillLayerChanged, layer: layer);
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

            SetSlotVisuals(slot, shouldShow, AlphaFor(slot.acceptsType));
        }
    }

    /// <summary>Durante el fundido solo cambia el alfa: colliders y renderers ya quedaron como corresponde.</summary>
    private void ApplyLayerAlpha()
    {
        if (grillSystem == null) return;

        foreach (var slot in grillSystem.slots)
        {
            if (slot == null) continue;

            float alpha = AlphaFor(slot.acceptsType);

            if (slot.currentItem != null)
                SetGameObjectAlpha(slot.currentItem, alpha);

            if (slot.stackedCoals != null)
            {
                foreach (var coal in slot.stackedCoals)
                {
                    if (coal != null)
                        SetGameObjectAlpha(coal.gameObject, alpha);
                }
            }
        }
    }

    private static void SetGameObjectAlpha(GameObject obj, float alpha)
    {
        obj.GetComponentsInChildren(true, RendererBuffer);
        for (int i = 0; i < RendererBuffer.Count; i++)
            SetAlpha(RendererBuffer[i], alpha);
        RendererBuffer.Clear();
    }

    private void SetSlotVisuals(GridSlot slot, bool active, float alpha)
    {
        SpriteRenderer sr = slot.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Color c = sr.color;
            c.a = 0f;
            sr.color = c;
        }

        Collider2D col = slot.GetComponent<Collider2D>();
        if (col != null) col.enabled = active;

        if (slot.currentItem != null)
            SetGameObjectVisuals(slot.currentItem, active, alpha);

        if (slot.stackedCoals != null)
        {
            foreach (var coal in slot.stackedCoals)
            {
                if (coal != null)
                    SetGameObjectVisuals(coal.gameObject, active, alpha);
            }
        }
    }

    private void SetGameObjectVisuals(GameObject obj, bool active, float alpha)
    {
        if (obj == null) return;

        obj.GetComponentsInChildren(true, RendererBuffer);
        for (int i = 0; i < RendererBuffer.Count; i++)
        {
            RendererBuffer[i].enabled = true;
            SetAlpha(RendererBuffer[i], alpha);
        }
        RendererBuffer.Clear();

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
