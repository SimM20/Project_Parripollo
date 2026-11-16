using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Info de lo que está bajo el puntero, en dos partes:
///  • Un cartelito junto al puntero con el nombre ("Carbón").
///  • Una barra abajo de la pantalla con el nombre, el estado y las acciones posibles, cada una con
///    el ícono de su control (click izquierdo, click derecho, R...). Con joystick salen sus botones.
///
/// Va en un canvas en pantalla sin GraphicRaycaster: nunca le saca un click a nada. Arma sus partes
/// por código en Awake, así el prefab solo lleva el estilo. Qué decir de cada cosa lo decide
/// <see cref="HoverInfoResolver"/>. Con el juego en pausa no se dibuja.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class HoverInfoLayer : MonoBehaviour
{
    [Header("Estilo")]
    [SerializeField] private InputGlyphSetSO glyphs;
    [SerializeField] private TMP_FontAsset font;
    [Tooltip("Fondo de la barra y del cartelito. Se usa Sliced.")]
    [SerializeField] private Sprite panelSprite;
    [Tooltip("Escala de los bordes del sprite del panel (más chico = borde más fino).")]
    [SerializeField] [Min(0.05f)] private float panelPixelsPerUnitMultiplier = 1.6f;
    [SerializeField] private Color textColor = new Color(1f, 0.965f, 0.902f, 1f);
    [SerializeField] private Color statusColor = new Color(0.86f, 0.8f, 0.7f, 1f);
    [SerializeField] private Color warningColor = new Color(1f, 0.58f, 0.3f, 1f);
    [SerializeField] private Color idleColor = new Color(1f, 0.965f, 0.902f, 0.45f);

    [Header("Barra (px de la resolución de referencia)")]
    [SerializeField] private float barHeight = 58f;
    [SerializeField] private float barSideMargin = 8f;
    [SerializeField] private float barBottomMargin = 6f;
    [SerializeField] private float barPadding = 22f;
    [Tooltip("Dónde empieza el texto, desde el borde izquierdo de la barra. Sirve para dejar libre algo " +
             "que quede encima de la barra; si es menor que el padding, se usa el padding.")]
    [SerializeField] private float contentLeftInset = 0f;
    [SerializeField] private float barFontSize = 24f;
    [SerializeField] private float actionFontSize = 21f;
    [SerializeField] private float glyphHeight = 34f;
    [SerializeField] private float glyphTextSpacing = 8f;
    [SerializeField] private float actionSpacing = 30f;
    [Tooltip("Espacio mínimo entre el texto de la izquierda y las acciones.")]
    [SerializeField] private float columnGap = 28f;
    [Tooltip("Sin nada bajo el puntero, la barra queda con un texto de ayuda. Apagado: se oculta.")]
    [SerializeField] private bool showIdleText = true;

    [Header("Cartelito junto al puntero")]
    [SerializeField] private float labelFontSize = 22f;
    [SerializeField] private Vector2 labelPadding = new Vector2(14f, 7f);
    [Tooltip("Desde la punta del puntero hasta la esquina del cartelito.")]
    [SerializeField] private Vector2 labelOffset = new Vector2(24f, -30f);
    [Tooltip("Segundos con el puntero quieto sobre lo mismo antes de mostrarlo.")]
    [SerializeField] [Min(0f)] private float labelDelay = 0.12f;
    [SerializeField] private float screenMargin = 8f;

    [Header("Ritmo")]
    [Tooltip("Cada cuánto se vuelve a leer el estado de lo que está bajo el puntero (segundos sin escalar).")]
    [SerializeField] [Min(0.02f)] private float refreshInterval = 0.1f;
    [SerializeField] [Min(0.01f)] private float fadeDuration = 0.12f;

    private class ActionEntry
    {
        public RectTransform root;
        public LayoutElement glyphSlot;
        public Image glyphImage;
        public Image keyCap;
        public TMP_Text keyLabel;
        public TMP_Text text;
    }

    private readonly HoverInfo resolved = new HoverInfo();
    private readonly HoverInfo shown = new HoverInfo();
    private readonly ActionEntry[] entries = new ActionEntry[HoverInfo.MaxActions];

    private Canvas canvas;
    private RectTransform root;

    private RectTransform bar;
    private CanvasGroup barGroup;
    private RectTransform barContent;
    private CanvasGroup barContentGroup;
    private TMP_Text barText;
    private RectTransform actionsRow;

    private RectTransform label;
    private CanvasGroup labelGroup;
    private TMP_Text labelText;

    private GameObject lastHovered;
    private GameObject lastPressed;
    private float nextRefresh;
    private float labelTimer;
    private bool forceRebuild = true;
    private bool hasInfo;

    // ── Ciclo de vida ───────────────────────────────────────────────────────

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        root = (RectTransform)transform;
        BuildBar();
        BuildLabel();
    }

    private void OnEnable()
    {
        InputManager.OnSchemeChanged += HandleSchemeChanged;
        InputManager.OnGamepadFamilyChanged += HandleFamilyChanged;
        Loc.OnTextsChanged += HandleTextsChanged;
        forceRebuild = true;
    }

    private void OnDisable()
    {
        InputManager.OnSchemeChanged -= HandleSchemeChanged;
        InputManager.OnGamepadFamilyChanged -= HandleFamilyChanged;
        Loc.OnTextsChanged -= HandleTextsChanged;
    }

    private void HandleSchemeChanged(InputScheme scheme) => forceRebuild = true;
    private void HandleFamilyChanged(GamepadFamily family) => forceRebuild = true;
    private void HandleTextsChanged() => forceRebuild = true;

    private void Update()
    {
        // Toda pausa pasa por GamePause: ni la barra ni el cartelito quedan sobre el menú.
        bool visible = !GamePause.IsPaused;
        if (canvas.enabled != visible)
            canvas.enabled = visible;
        if (!visible)
            return;

        float dt = Time.unscaledDeltaTime;
        GameObject hovered = InputManager.HoveredObject;
        GameObject pressed = InputManager.PressedObject;

        bool targetChanged = hovered != lastHovered || pressed != lastPressed;
        if (targetChanged)
        {
            lastHovered = hovered;
            lastPressed = pressed;
            labelTimer = 0f;
        }

        if (targetChanged || forceRebuild || Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + refreshInterval;
            hasInfo = HoverInfoResolver.TryResolve(hovered, pressed, resolved);
            if (!hasInfo)
                resolved.Clear();

            if (forceRebuild || !resolved.SameAs(shown))
            {
                bool contentChanged = resolved.Title != shown.Title;
                shown.CopyFrom(resolved);
                RebuildBar(contentChanged || forceRebuild);
                RebuildLabel();
                forceRebuild = false;
            }
        }

        AnimateBar(dt);
        UpdateLabel(dt);
    }

    // ── Barra ───────────────────────────────────────────────────────────────

    private void BuildBar()
    {
        bar = NewRect("HoverInfoBar", root);
        bar.anchorMin = new Vector2(0f, 0f);
        bar.anchorMax = new Vector2(1f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.offsetMin = new Vector2(barSideMargin, barBottomMargin);
        bar.offsetMax = new Vector2(-barSideMargin, barBottomMargin + barHeight);
        AddPanel(bar.gameObject);
        barGroup = bar.gameObject.AddComponent<CanvasGroup>();
        barGroup.blocksRaycasts = false;
        barGroup.interactable = false;
        barGroup.alpha = showIdleText ? 1f : 0f;

        barContent = NewRect("Content", bar);
        Stretch(barContent);
        barContentGroup = barContent.gameObject.AddComponent<CanvasGroup>();

        barText = NewText("Info", barContent, barFontSize, TextAlignmentOptions.Left);
        RectTransform textRect = barText.rectTransform;
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(Mathf.Max(barPadding, contentLeftInset), 0f);
        textRect.offsetMax = new Vector2(-barPadding, 0f);
        barText.overflowMode = TextOverflowModes.Ellipsis;
        barText.enableWordWrapping = false;

        actionsRow = NewRect("Actions", barContent);
        actionsRow.anchorMin = actionsRow.anchorMax = new Vector2(1f, 0.5f);
        actionsRow.pivot = new Vector2(1f, 0.5f);
        actionsRow.anchoredPosition = new Vector2(-barPadding, 0f);
        HorizontalLayoutGroup row = actionsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = actionSpacing;
        row.childAlignment = TextAnchor.MiddleRight;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        ContentSizeFitter fitter = actionsRow.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        for (int i = 0; i < entries.Length; i++)
            entries[i] = BuildEntry(actionsRow, i);
    }

    private ActionEntry BuildEntry(RectTransform parent, int index)
    {
        ActionEntry entry = new ActionEntry();
        entry.root = NewRect("Action" + index, parent);
        HorizontalLayoutGroup row = entry.root.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = glyphTextSpacing;
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;

        RectTransform slot = NewRect("Glyph", entry.root);
        entry.glyphSlot = slot.gameObject.AddComponent<LayoutElement>();

        RectTransform image = NewRect("Image", slot);
        Stretch(image);
        entry.glyphImage = image.gameObject.AddComponent<Image>();
        entry.glyphImage.preserveAspect = true;
        entry.glyphImage.raycastTarget = false;

        RectTransform cap = NewRect("KeyCap", slot);
        Stretch(cap);
        entry.keyCap = cap.gameObject.AddComponent<Image>();
        entry.keyCap.type = Image.Type.Sliced;
        entry.keyCap.raycastTarget = false;
        entry.keyLabel = NewText("KeyLabel", cap, actionFontSize * 0.85f, TextAlignmentOptions.Center);
        entry.keyLabel.color = new Color(0.16f, 0.12f, 0.09f, 1f);
        Stretch(entry.keyLabel.rectTransform);

        entry.text = NewText("Text", entry.root, actionFontSize, TextAlignmentOptions.Left);
        entry.text.enableWordWrapping = false;

        entry.root.gameObject.SetActive(false);
        return entry;
    }

    private void RebuildBar(bool fade)
    {
        bool empty = shown.IsEmpty;

        if (empty)
        {
            barText.text = showIdleText ? Loc.Get("hover.empty") : string.Empty;
            barText.color = idleColor;
        }
        else
        {
            barText.color = textColor;
            string status = string.IsNullOrEmpty(shown.Status)
                ? string.Empty
                : $"   <color=#{ColorUtility.ToHtmlStringRGBA(shown.Warning ? warningColor : statusColor)}>{shown.Status}</color>";
            barText.text = $"<b>{shown.Title}</b>{status}";
        }

        for (int i = 0; i < entries.Length; i++)
        {
            bool used = !empty && i < shown.Actions.Count;
            entries[i].root.gameObject.SetActive(used);
            if (used)
                FillEntry(entries[i], shown.Actions[i]);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(actionsRow);

        // El texto de la izquierda llega hasta las acciones; si no entra, termina en "...".
        float actionsWidth = empty ? 0f : actionsRow.rect.width;
        RectTransform textRect = barText.rectTransform;
        textRect.offsetMax = new Vector2(-(barPadding + (actionsWidth > 0f ? actionsWidth + columnGap : 0f)), 0f);

        if (fade)
            barContentGroup.alpha = 0f;
    }

    private void FillEntry(ActionEntry entry, HoverInfo.Action action)
    {
        HintGlyph glyph = glyphs != null ? glyphs.Resolve(action.Prompt) : default;
        bool drawn = glyph.Sprite != null;
        bool keyed = !drawn && !string.IsNullOrEmpty(glyph.Label);

        entry.glyphImage.gameObject.SetActive(drawn);
        entry.keyCap.gameObject.SetActive(keyed);
        entry.glyphSlot.gameObject.SetActive(drawn || keyed);

        float width = 0f;
        if (drawn)
        {
            entry.glyphImage.sprite = glyph.Sprite;
            Rect r = glyph.Sprite.rect;
            width = r.height > 0f ? glyphHeight * r.width / r.height : glyphHeight;
        }
        else if (keyed)
        {
            if (glyph.Blank != null)
                entry.keyCap.sprite = glyph.Blank;
            entry.keyLabel.text = glyph.Label.ToUpperInvariant();
            width = Mathf.Max(glyphHeight, entry.keyLabel.GetPreferredValues(entry.keyLabel.text).x + 16f);
        }

        entry.glyphSlot.preferredWidth = width;
        entry.glyphSlot.preferredHeight = glyphHeight;
        entry.text.text = action.Text;
        entry.text.color = textColor;
    }

    private void AnimateBar(float dt)
    {
        float target = showIdleText || !shown.IsEmpty ? 1f : 0f;
        barGroup.alpha = Mathf.MoveTowards(barGroup.alpha, target, dt / fadeDuration);
        barContentGroup.alpha = Mathf.MoveTowards(barContentGroup.alpha, 1f, dt / fadeDuration);
    }

    // ── Cartelito junto al puntero ──────────────────────────────────────────

    private void BuildLabel()
    {
        label = NewRect("HoverLabel", root);
        label.anchorMin = label.anchorMax = new Vector2(0f, 0f);
        label.pivot = new Vector2(0f, 1f);
        AddPanel(label.gameObject);
        labelGroup = label.gameObject.AddComponent<CanvasGroup>();
        labelGroup.blocksRaycasts = false;
        labelGroup.interactable = false;
        labelGroup.alpha = 0f;

        HorizontalLayoutGroup layout = label.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(
            Mathf.RoundToInt(labelPadding.x), Mathf.RoundToInt(labelPadding.x),
            Mathf.RoundToInt(labelPadding.y), Mathf.RoundToInt(labelPadding.y));
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = label.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        labelText = NewText("Text", label, labelFontSize, TextAlignmentOptions.Center);
        labelText.enableWordWrapping = false;
        labelText.fontStyle = FontStyles.Bold;
        labelText.color = textColor;
    }

    private void RebuildLabel()
    {
        if (shown.IsEmpty)
            return;

        labelText.text = shown.Title;
        LayoutRebuilder.ForceRebuildLayoutImmediate(label);
    }

    private void UpdateLabel(float dt)
    {
        bool wanted = !shown.IsEmpty && !shown.HideCursorLabel;
        if (wanted)
            labelTimer += dt;

        bool show = wanted && labelTimer >= labelDelay;
        labelGroup.alpha = Mathf.MoveTowards(labelGroup.alpha, show ? 1f : 0f, dt / fadeDuration);
        if (labelGroup.alpha <= 0f)
            return;

        // Overlay: la posición de pantalla se pasa a la capa sin cámara.
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            root, InputManager.PointerPosition, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out Vector2 local);
        Vector2 pointer = local - root.rect.min;

        // Se da vuelta contra los bordes para no salirse de la pantalla.
        Vector2 size = label.rect.size;
        Vector2 area = root.rect.size;
        bool flipX = pointer.x + labelOffset.x + size.x > area.x - screenMargin;
        bool flipY = pointer.y + labelOffset.y - size.y < screenMargin;

        label.pivot = new Vector2(flipX ? 1f : 0f, flipY ? 0f : 1f);
        Vector2 offset = new Vector2(flipX ? -labelOffset.x * 0.5f : labelOffset.x, flipY ? -labelOffset.y : labelOffset.y);
        label.anchoredPosition = pointer + offset;
    }

    // ── Helpers de UI ───────────────────────────────────────────────────────

    private RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private void AddPanel(GameObject go)
    {
        Image image = go.AddComponent<Image>();
        image.sprite = panelSprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = panelPixelsPerUnitMultiplier;
        image.raycastTarget = false;
        if (panelSprite == null)
            image.color = new Color(0.12f, 0.1f, 0.09f, 0.94f);
    }

    private TMP_Text NewText(string name, Transform parent, float size, TextAlignmentOptions alignment)
    {
        RectTransform rect = NewRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.richText = true;
        text.color = textColor;
        return text;
    }
}
