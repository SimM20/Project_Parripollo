using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Cartel "¿Cómo te llamás?" que abre cada run del evento. Lo usan el botón Jugar del menú
/// y Reintentar de la derrota: en un evento la run siguiente suele ser de otra persona.
///
/// Se arma entero por código sobre su propio canvas, así no toca escenas ni prefabs de
/// la rama principal. Enter confirma, Esc vuelve. Textos: claves event.name.* de Menus.csv.
/// </summary>
public class EventNamePrompt : MonoBehaviour
{
    private const string FontResource = "ParripolloPixel SDF";

    private static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.78f);
    private static readonly Color PanelColor = new Color32(0x2B, 0x1A, 0x12, 0xFF);
    private static readonly Color BorderColor = new Color32(0xE0, 0x70, 0x2A, 0xFF);
    private static readonly Color FieldColor = new Color32(0xF6, 0xE8, 0xD6, 0xFF);
    private static readonly Color FieldTextColor = new Color32(0x2B, 0x1A, 0x12, 0xFF);
    private static readonly Color ConfirmColor = new Color32(0xE0, 0x70, 0x2A, 0xFF);
    private static readonly Color CancelColor = new Color32(0x5A, 0x3E, 0x2E, 0xFF);
    private static readonly Color ErrorColor = new Color32(0xFF, 0x8A, 0x6B, 0xFF);

    public static bool IsOpen => current != null;

    private static EventNamePrompt current;

    private TMP_InputField input;
    private TMP_Text errorText;
    private Action<string> onConfirm;
    private Action onCancel;
    private TMP_FontAsset font;

    /// <summary>
    /// Pide el nombre. <paramref name="onConfirm"/> recibe el nombre ya limpio
    /// (<see cref="EventRun.SanitizeName"/>) y nunca vacío.
    /// </summary>
    public static void Show(Action<string> onConfirm, Action onCancel = null)
    {
        if (current != null) return;

        var go = new GameObject("EventNamePrompt");
        go.SetActive(false); // armar todo antes de que corra el OnEnable del input
        current = go.AddComponent<EventNamePrompt>();
        current.onConfirm = onConfirm;
        current.onCancel = onCancel;
        current.Build();
        go.SetActive(true);
    }

    private void Start() => StartCoroutine(FocusInput());

    private void Update()
    {
        if (InputManager.WasPressed(GameAction.Pause)) Cancel();
    }

    private void OnDestroy()
    {
        if (current == this) current = null;
    }

    private IEnumerator FocusInput()
    {
        yield return null; // el EventSystem ignora la selección en el mismo frame del alta

        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(input.gameObject);
        input.ActivateInputField();
    }

    private void Confirm()
    {
        string playerName = EventRun.SanitizeName(input.text);
        if (playerName.Length == 0)
        {
            errorText.text = Loc.Get("event.name.empty");
            input.ActivateInputField();
            return;
        }

        Action<string> callback = onConfirm;
        Destroy(gameObject);
        callback?.Invoke(playerName);
    }

    private void Cancel()
    {
        Action callback = onCancel;
        Destroy(gameObject);
        callback?.Invoke();
    }

    // ---- Armado ----

    private void Build()
    {
        font = Resources.Load<TMP_FontAsset>(FontResource);

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        // Fondo que tapa (y bloquea los clicks de) lo de atrás.
        RectTransform backdrop = CreateRect("Backdrop", transform);
        Stretch(backdrop);
        backdrop.gameObject.AddComponent<Image>().color = Backdrop;

        RectTransform panel = CreateRect("Panel", backdrop);
        panel.sizeDelta = new Vector2(1240f, 640f);
        panel.gameObject.AddComponent<Image>().color = BorderColor;

        RectTransform inner = CreateRect("Inner", panel);
        Stretch(inner, 8f);
        inner.gameObject.AddComponent<Image>().color = PanelColor;

        var layout = inner.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(64, 64, 48, 48);
        layout.spacing = 24f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateText("Title", inner, Loc.Get("event.name.title"), 64f, Color.white, 72f);
        CreateText("Body", inner, Loc.Format("event.name.body", EventRun.MaxDays), 30f, FieldColor, 120f);
        input = CreateInput(inner);
        errorText = CreateText("Error", inner, string.Empty, 28f, ErrorColor, 36f);

        RectTransform buttons = CreateRect("Buttons", inner);
        var row = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 32f;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = true;
        buttons.gameObject.AddComponent<LayoutElement>().preferredHeight = 88f;

        CreateButton("Cancel", buttons, Loc.Get("event.name.cancel"), CancelColor, Cancel);
        CreateButton("Confirm", buttons, Loc.Get("event.name.confirm"), ConfirmColor, Confirm);

        UIPopIn.Ensure(panel);
    }

    private TMP_InputField CreateInput(Transform parent)
    {
        RectTransform rect = CreateRect("NameInput", parent);
        rect.gameObject.AddComponent<Image>().color = FieldColor;
        rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 96f;

        RectTransform area = CreateRect("Text Area", rect);
        Stretch(area, 20f);
        area.gameObject.AddComponent<RectMask2D>();

        TMP_Text placeholder = CreateText("Placeholder", area, Loc.Get("event.name.placeholder"), 48f,
            new Color(FieldTextColor.r, FieldTextColor.g, FieldTextColor.b, 0.4f), 0f);
        TMP_Text text = CreateText("Text", area, string.Empty, 48f, FieldTextColor, 0f);
        foreach (TMP_Text t in new[] { placeholder, text })
        {
            Stretch(t.rectTransform);
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = false;
        }

        var field = rect.gameObject.AddComponent<TMP_InputField>();
        field.textViewport = area;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.characterLimit = EventRun.MaxNameLength;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.onFocusSelectAll = true;
        field.caretColor = FieldTextColor;
        field.customCaretColor = true;
        field.caretWidth = 3;
        field.text = EventRun.LastPlayerName ?? string.Empty;
        field.onSubmit.AddListener(_ => Confirm());
        field.onValueChanged.AddListener(_ => errorText.text = string.Empty);
        return field;
    }

    private void CreateButton(string name, Transform parent, string label, Color color, Action onClick)
    {
        RectTransform rect = CreateRect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => onClick());

        TMP_Text text = CreateText("Label", rect, label, 40f, Color.white, 0f);
        Stretch(text.rectTransform);

        MenuButtonHover.Ensure(button);
    }

    private TMP_Text CreateText(string name, Transform parent, string value, float size, Color color, float height)
    {
        RectTransform rect = CreateRect(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true; // creado inactivo, TMP no llega a aplicar su default
        text.raycastTarget = false;
        if (height > 0f) rect.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        return text;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }
}
