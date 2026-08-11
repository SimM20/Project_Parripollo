using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Representa un grupo/stack de notificaciones de un mismo tipo de corte de carne (MeatCutSO).
/// En estado colapsado muestra el efecto de notificaciones apiladas (stack).
/// Al pasar el cursor (hover) o hacer clic, despliega horizontalmente hacia la derecha los globos individuales.
/// </summary>
public class GrillNotificationGroupUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("References")]
    [SerializeField] private RectTransform mainStackContainer;
    [SerializeField] private RectTransform expandedHorizontalContainer;
    [SerializeField] private TextMeshProUGUI countBadgeText;
    [SerializeField] private Button headerButton;

    [Header("Stack Visuals")]
    [SerializeField] private GameObject[] stackBackingLayers = new GameObject[3];
    [SerializeField] private float stackOffsetStepX = 8f;

    [Header("Layout Settings")]
    [SerializeField] private float horizontalSpacing = 12f;

    private MeatCutSO groupCut;
    private readonly List<Meat> targetMeats = new List<Meat>();
    private readonly List<GrillNotificationBubbleUI> expandedBubbles = new List<GrillNotificationBubbleUI>();
    private GrillNotificationBubbleUI headerBubble;

    private bool isExpanded = false;
    private float hoverExitTimer = 0f;
    private const float HOVER_EXIT_DELAY = 0.3f;
    private bool isPointerOver = false;

    public MeatCutSO GroupCut => groupCut;
    public int Count => targetMeats.Count;

    private void Awake()
    {
        EnsureHierarchy();

        if (headerButton != null)
        {
            headerButton.onClick.RemoveAllListeners();
            headerButton.onClick.AddListener(OnHeaderClicked);
        }
    }

    private void Update()
    {
        // Limpiar elementos nulos de la lista
        targetMeats.RemoveAll(m => m == null || !m.IsOnGrill);

        if (targetMeats.Count == 0)
        {
            gameObject.SetActive(false);
            return;
        }

        UpdateStackVisuals();
        UpdateExpandedItems();

        // Control de retardo al quitar el cursor
        if (!isPointerOver && isExpanded)
        {
            hoverExitTimer += Time.deltaTime;
            if (hoverExitTimer >= HOVER_EXIT_DELAY)
            {
                SetExpanded(false);
            }
        }
    }

    public void Initialize(MeatCutSO cut, List<Meat> meats, Sprite circleSprite)
    {
        groupCut = cut;
        targetMeats.Clear();
        if (meats != null)
            targetMeats.AddRange(meats);

        EnsureHierarchy();

        if (headerBubble == null)
            headerBubble = headerButton.GetComponent<GrillNotificationBubbleUI>();

        if (headerBubble == null && mainStackContainer != null)
        {
            headerBubble = GrillNotificationBubbleUI.CreateProceduralBubble(mainStackContainer, circleSprite);
            headerButton = headerBubble.GetComponent<Button>();
            if (headerButton != null)
            {
                headerButton.onClick.RemoveAllListeners();
                headerButton.onClick.AddListener(OnHeaderClicked);
            }
        }

        SetExpanded(false);
        gameObject.SetActive(true);
        UpdateStackVisuals();
    }

    public void UpdateMeats(List<Meat> meats)
    {
        targetMeats.Clear();
        if (meats != null)
            targetMeats.AddRange(meats);

        if (targetMeats.Count == 0)
        {
            gameObject.SetActive(false);
            return;
        }

        UpdateStackVisuals();
    }

    private void UpdateStackVisuals()
    {
        if (targetMeats.Count == 0) return;

        // Actualizar el globo principal de cabecera con la carne más avanzada o primera
        Meat primaryMeat = GetPrimaryMeat();
        if (headerBubble != null && primaryMeat != null)
        {
            headerBubble.Initialize(primaryMeat);
        }

        // Contador de cantidad (ej: x3)
        if (countBadgeText != null)
        {
            countBadgeText.text = targetMeats.Count > 1 ? $"x{targetMeats.Count}" : "";
            countBadgeText.gameObject.SetActive(targetMeats.Count > 1);
        }

        // Mostrar u ocultar las capas de sombra traslapadas (stack effect)
        int stackCount = Mathf.Min(targetMeats.Count - 1, stackBackingLayers.Length);
        for (int i = 0; i < stackBackingLayers.Length; i++)
        {
            if (stackBackingLayers[i] != null)
            {
                bool showLayer = (i < stackCount) && !isExpanded;
                stackBackingLayers[i].SetActive(showLayer);
            }
        }
    }

    private void UpdateExpandedItems()
    {
        if (!isExpanded || expandedHorizontalContainer == null) return;

        // Sincronizar burbujas individuales desplegadas en la fila horizontal
        for (int i = expandedBubbles.Count - 1; i >= 0; i--)
        {
            GrillNotificationBubbleUI bubble = expandedBubbles[i];
            if (bubble == null || bubble.TargetMeat == null || !targetMeats.Contains(bubble.TargetMeat))
            {
                if (bubble != null) bubble.gameObject.SetActive(false);
                expandedBubbles.RemoveAt(i);
            }
        }

        Sprite circleSprite = GrillNotificationManager.Instance != null ? GrillNotificationManager.Instance.CircleSprite : null;

        for (int i = 0; i < targetMeats.Count; i++)
        {
            Meat meat = targetMeats[i];
            GrillNotificationBubbleUI bubble = expandedBubbles.Find(b => b != null && b.TargetMeat == meat);

            if (bubble == null)
            {
                bubble = GrillNotificationBubbleUI.CreateProceduralBubble(expandedHorizontalContainer, circleSprite);
                bubble.Initialize(meat);
                expandedBubbles.Add(bubble);
            }
        }
    }

    private Meat GetPrimaryMeat()
    {
        if (targetMeats.Count == 0) return null;

        // Priorizar la carne con el estado de cocción más avanzado o más urgente (ej: Hecho / Quemado)
        Meat mostUrgent = targetMeats[0];
        for (int i = 1; i < targetMeats.Count; i++)
        {
            if (targetMeats[i].ActiveSideProgress01 > mostUrgent.ActiveSideProgress01)
            {
                mostUrgent = targetMeats[i];
            }
        }
        return mostUrgent;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerOver = true;
        hoverExitTimer = 0f;
        if (targetMeats.Count > 1)
        {
            SetExpanded(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerOver = false;
        hoverExitTimer = 0f;
    }

    private void OnHeaderClicked()
    {
        if (targetMeats.Count > 1)
        {
            SetExpanded(!isExpanded);
        }
        else if (targetMeats.Count == 1)
        {
            ViewManager viewMgr = FindFirstObjectByType<ViewManager>();
            if (viewMgr != null)
                viewMgr.Show(ViewType.Grill);
        }
    }

    public void SetExpanded(bool expanded)
    {
        isExpanded = expanded;
        if (expandedHorizontalContainer != null)
        {
            expandedHorizontalContainer.gameObject.SetActive(expanded);
        }

        if (mainStackContainer != null)
        {
            // En estado desplegado, ocultamos el apilado principal para mostrar la fila individual
            mainStackContainer.gameObject.SetActive(!expanded || targetMeats.Count <= 1);
        }

        if (isExpanded)
        {
            UpdateExpandedItems();
        }
    }

    private void EnsureHierarchy()
    {
        if (mainStackContainer != null && expandedHorizontalContainer != null) return;

        RectTransform groupRect = GetComponent<RectTransform>();
        if (groupRect == null)
            groupRect = gameObject.AddComponent<RectTransform>();

        groupRect.sizeDelta = new Vector2(80f, 80f);

        LayoutElement groupLayout = GetComponent<LayoutElement>();
        if (groupLayout == null)
            groupLayout = gameObject.AddComponent<LayoutElement>();

        groupLayout.minWidth = 80f;
        groupLayout.minHeight = 80f;
        groupLayout.preferredWidth = 80f;
        groupLayout.preferredHeight = 80f;

        // 1. Contenedor del Stack principal (colapsado)
        if (mainStackContainer == null)
        {
            GameObject stackObj = new GameObject("MainStackContainer", typeof(RectTransform));
            stackObj.transform.SetParent(transform, false);
            mainStackContainer = stackObj.GetComponent<RectTransform>();
            mainStackContainer.anchorMin = Vector2.zero;
            mainStackContainer.anchorMax = Vector2.one;
            mainStackContainer.sizeDelta = Vector2.zero;

            // Crear 3 capas traslapadas de fondo para el efecto de apilado (shadow stack rings)
            for (int i = 0; i < stackBackingLayers.Length; i++)
            {
                GameObject layerObj = new GameObject($"StackLayer_{i}", typeof(RectTransform), typeof(Image));
                layerObj.transform.SetParent(mainStackContainer, false);
                RectTransform layerRect = layerObj.GetComponent<RectTransform>();
                layerRect.anchorMin = Vector2.zero;
                layerRect.anchorMax = Vector2.one;

                // Desplazamiento horizontal progresivo hacia la derecha (efecto stack)
                float offsetX = (i + 1) * stackOffsetStepX;
                layerRect.anchoredPosition = new Vector2(offsetX, 0f);
                layerRect.sizeDelta = Vector2.zero;

                Image img = layerObj.GetComponent<Image>();
                img.color = new Color(0.15f, 0.15f, 0.15f, 0.75f - (i * 0.15f));
                img.type = Image.Type.Simple;

                stackBackingLayers[i] = layerObj;
                layerObj.SetActive(false);
            }

            // Texto de Badge (ej: x3)
            GameObject badgeObj = new GameObject("CountBadge", typeof(RectTransform), typeof(TextMeshProUGUI));
            badgeObj.transform.SetParent(mainStackContainer, false);
            RectTransform badgeRect = badgeObj.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.6f, 0.6f);
            badgeRect.anchorMax = new Vector2(1.1f, 1.1f);
            badgeRect.sizeDelta = Vector2.zero;

            countBadgeText = badgeObj.GetComponent<TextMeshProUGUI>();
            countBadgeText.fontSize = 13f;
            countBadgeText.alignment = TextAlignmentOptions.Center;
            countBadgeText.fontStyle = FontStyles.Bold;
            countBadgeText.color = Color.white;
            countBadgeText.raycastTarget = false;
        }

        // 2. Contenedor Horizontal Desplegable hacia la derecha
        if (expandedHorizontalContainer == null)
        {
            GameObject expandedObj = new GameObject("ExpandedHorizontalContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            expandedObj.transform.SetParent(transform, false);
            expandedHorizontalContainer = expandedObj.GetComponent<RectTransform>();
            expandedHorizontalContainer.anchorMin = new Vector2(0f, 0.5f);
            expandedHorizontalContainer.anchorMax = new Vector2(0f, 0.5f);
            expandedHorizontalContainer.pivot = new Vector2(0f, 0.5f);
            expandedHorizontalContainer.anchoredPosition = Vector2.zero;

            ContentSizeFitter fitter = expandedObj.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            HorizontalLayoutGroup horizontalLayout = expandedObj.GetComponent<HorizontalLayoutGroup>();
            horizontalLayout.childAlignment = TextAnchor.MiddleLeft;
            horizontalLayout.childControlWidth = true;
            horizontalLayout.childControlHeight = true;
            horizontalLayout.childForceExpandWidth = false;
            horizontalLayout.childForceExpandHeight = false;
            horizontalLayout.spacing = horizontalSpacing;

            expandedHorizontalContainer.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Construye proceduralmente un elemento de Grupo/Stack UI.
    /// </summary>
    public static GrillNotificationGroupUI CreateProceduralGroup(Transform parent)
    {
        GameObject groupObj = new GameObject("GrillNotificationGroupUI", typeof(RectTransform), typeof(CanvasGroup));
        groupObj.transform.SetParent(parent, false);

        GrillNotificationGroupUI groupUI = groupObj.AddComponent<GrillNotificationGroupUI>();
        groupUI.EnsureHierarchy();
        return groupUI;
    }
}
