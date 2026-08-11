using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Componente UI para un globo de notificación de cocción individual.
/// Muestra el corte de carne, su progreso de cocción (relleno), y el color según su estado.
/// </summary>
public class GrillNotificationBubbleUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image backgroundFrame;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image cutIconImage;
    [SerializeField] private TextMeshProUGUI stateText;
    [SerializeField] private Button actionButton;

    [Header("State Colors")]
    [SerializeField] private Color crudoColor = new Color(0.9f, 0.45f, 0.45f, 1f);      // #E57373
    [SerializeField] private Color jugosoColor = new Color(1f, 0.6f, 0f, 1f);         // #FF9800
    [SerializeField] private Color hechoColor = new Color(0.3f, 0.75f, 0.3f, 1f);      // #4CAF50
    [SerializeField] private Color muyHechoColor = new Color(1f, 0.75f, 0.05f, 1f);   // #FFC107
    [SerializeField] private Color pasadoColor = new Color(0.95f, 0.35f, 0.15f, 1f);   // #FF5722
    [SerializeField] private Color quemadoColor = new Color(0.25f, 0.25f, 0.25f, 1f);   // #424242

    private Meat targetMeat;
    private MeatStates lastState = (MeatStates)(-1);
    private Coroutine pulseCoroutine;

    public Meat TargetMeat => targetMeat;

    private void Awake()
    {
        if (actionButton == null)
            actionButton = GetComponent<Button>();

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(OnBubbleClicked);
        }
    }

    public void Initialize(Meat meat)
    {
        targetMeat = meat;
        lastState = (MeatStates)(-1);

        if (targetMeat == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        UpdateVisuals(forceStateUpdate: true);
    }

    private void Update()
    {
        if (targetMeat == null || !targetMeat.IsOnGrill)
        {
            gameObject.SetActive(false);
            return;
        }

        UpdateVisuals(forceStateUpdate: false);
    }

    private void UpdateVisuals(bool forceStateUpdate)
    {
        if (targetMeat == null) return;

        // 1. Progreso de cocción de la cara activa (0.0 a 1.0)
        float progress = Mathf.Clamp01(targetMeat.ActiveSideProgress01);
        if (fillImage != null)
        {
            fillImage.fillAmount = progress;
        }

        // 2. Estado de cocción actual
        MeatStates currentState = targetMeat.ActiveSideState;

        if (currentState != lastState || forceStateUpdate)
        {
            lastState = currentState;

            Color stateColor = GetColorForState(currentState);
            if (fillImage != null)
                fillImage.color = stateColor;

            if (backgroundFrame != null)
                backgroundFrame.color = new Color(stateColor.r * 0.4f, stateColor.g * 0.4f, stateColor.b * 0.4f, 0.9f);

            if (stateText != null)
            {
                stateText.text = MeatHoverText.GetStateDisplayName(currentState);
                stateText.color = stateColor;
            }

            // 3. Icono/Sprite del corte
            if (cutIconImage != null && targetMeat.cut != null)
            {
                Sprite cutSprite = targetMeat.cut.GetSpriteForState(currentState, targetMeat.isSideA);
                if (cutSprite == null)
                    cutSprite = targetMeat.cut.GetDefaultSprite();

                if (cutSprite != null)
                {
                    cutIconImage.sprite = cutSprite;
                    cutIconImage.enabled = true;
                }
            }

            // Animación de pulso cuando cambia el estado
            if (!forceStateUpdate)
            {
                TriggerPulseEffect();
            }
        }
    }

    public Color GetColorForState(MeatStates state)
    {
        switch (state)
        {
            case MeatStates.Crudo: return crudoColor;
            case MeatStates.Jugoso: return jugosoColor;
            case MeatStates.Hecho: return hechoColor;
            case MeatStates.Muy_Hecho: return muyHechoColor;
            case MeatStates.Pasado: return pasadoColor;
            case MeatStates.Quemado: return quemadoColor;
            default: return Color.white;
        }
    }

    private void OnBubbleClicked()
    {
        ViewManager viewMgr = FindFirstObjectByType<ViewManager>();
        if (viewMgr != null)
        {
            viewMgr.Show(ViewType.Grill);
        }
    }

    private void TriggerPulseEffect()
    {
        if (pulseCoroutine != null)
            StopCoroutine(pulseCoroutine);

        pulseCoroutine = StartCoroutine(PulseRoutine());
    }

    private IEnumerator PulseRoutine()
    {
        Vector3 baseScale = Vector3.one;
        Vector3 targetScale = Vector3.one * 1.25f;
        float duration = 0.15f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(baseScale, targetScale, elapsed / duration);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(targetScale, baseScale, elapsed / duration);
            yield return null;
        }

        transform.localScale = baseScale;
        pulseCoroutine = null;
    }

    /// <summary>
    /// Construye proceduralmente un Globo UI funcional si se crea sin prefab.
    /// </summary>
    public static GrillNotificationBubbleUI CreateProceduralBubble(Transform parent, Sprite defaultCircleSprite)
    {
        GameObject bubbleObj = new GameObject("GrillNotificationBubble", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button), typeof(LayoutElement));
        bubbleObj.transform.SetParent(parent, false);

        RectTransform rect = bubbleObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(80f, 80f);

        LayoutElement layoutElement = bubbleObj.GetComponent<LayoutElement>();
        layoutElement.minWidth = 80f;
        layoutElement.minHeight = 80f;
        layoutElement.preferredWidth = 80f;
        layoutElement.preferredHeight = 80f;
        layoutElement.flexibleWidth = 0f;
        layoutElement.flexibleHeight = 0f;

        // Fondo oscuro circular
        Image bgImg = bubbleObj.GetComponent<Image>();
        if (defaultCircleSprite != null)
            bgImg.sprite = defaultCircleSprite;
        bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        bgImg.type = Image.Type.Simple;

        // Relleno circular de progreso
        GameObject fillObj = new GameObject("FillProgress", typeof(RectTransform), typeof(Image));
        fillObj.transform.SetParent(bubbleObj.transform, false);
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;

        Image fillImg = fillObj.GetComponent<Image>();
        if (defaultCircleSprite != null)
            fillImg.sprite = defaultCircleSprite;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Radial360;
        fillImg.fillOrigin = (int)Image.Origin360.Top;
        fillImg.fillClockwise = true;
        fillImg.fillAmount = 0.5f;

        // Máscara interna para la foto
        GameObject iconObj = new GameObject("CutIcon", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(bubbleObj.transform, false);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.15f, 0.15f);
        iconRect.anchorMax = new Vector2(0.85f, 0.85f);
        iconRect.sizeDelta = Vector2.zero;

        Image iconImg = iconObj.GetComponent<Image>();
        iconImg.preserveAspect = true;

        // Texto de Estado (debajo o sobre el globo)
        GameObject textObj = new GameObject("StateText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(bubbleObj.transform, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, -0.3f);
        textRect.anchorMax = new Vector2(1f, 0.05f);
        textRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 11f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        tmp.raycastTarget = false;

        GrillNotificationBubbleUI component = bubbleObj.AddComponent<GrillNotificationBubbleUI>();
        component.backgroundFrame = bgImg;
        component.fillImage = fillImg;
        component.cutIconImage = iconImg;
        component.stateText = tmp;
        component.actionButton = bubbleObj.GetComponent<Button>();

        return component;
    }
}
