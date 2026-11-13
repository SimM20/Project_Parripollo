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

    [Tooltip("Opcionales: si están, la tab cambia de chapa en vez de teñirla (los colores de fondo se ignoran).")]
    [SerializeField] private Sprite activeSprite;
    [SerializeField] private Sprite inactiveSprite;

    public ShopTabType Tab => tab;
    public System.Action<ShopTabType> OnTabClicked;

    private Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(HandleClick);
        Debug.Log($"[ShopTabButtonUI] Awake en {gameObject.name}, tab={tab}, button.interactable={button.interactable}");
    }
    
    void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(HandleClick);
    }

    /// <summary>
    /// Convierte este botón (un clon de otra pestaña) en el de <paramref name="newTab"/>, con su
    /// texto por clave de Loc. Lo usa <see cref="ShopBreadcrumbUI"/> para la pestaña de la ruta.
    /// </summary>
    public void Configure(ShopTabType newTab, string labelKey)
    {
        tab = newTab;
        if (label == null) return;

        if (!label.TryGetComponent(out LocalizedText localized))
            localized = label.gameObject.AddComponent<LocalizedText>();
        localized.Key = labelKey;
    }

    // Tamaño máximo del texto tal como viene del prefab: el techo al medir.
    private float authoredFontSize = -1f;

    /// <summary>
    /// El tamaño de letra más grande con el que el texto actual entra en la pestaña. Deja el
    /// auto-size prendido; <see cref="SetFontSize"/> lo apaga con el tamaño elegido.
    /// </summary>
    public float MeasureFittingFontSize()
    {
        if (label == null) return 0f;

        if (authoredFontSize < 0f)
            authoredFontSize = label.enableAutoSizing ? label.fontSizeMax : label.fontSize;

        if (label.TryGetComponent(out LocalizedText localized)) localized.Refresh();

        label.fontSizeMax = authoredFontSize;
        label.enableAutoSizing = true;
        label.ForceMeshUpdate(true);
        return label.fontSize;
    }

    /// <summary>Fija el tamaño de letra (sin auto-size), para que todas las pestañas usen el mismo.</summary>
    public void SetFontSize(float size)
    {
        if (label == null || size <= 0f) return;

        label.enableAutoSizing = false;
        label.fontSize = size;
    }

    public void SetActiveState(bool isActive)
    {
        // Con cartel de menú la tab activa es el cartel encendido ("estás acá"), no un botón:
        // no se selecciona ni titila, y volver a clickearla no reconstruye la grilla.
        RetroMenuButton retro = GetComponent<RetroMenuButton>();
        if (retro != null && button != null)
            button.interactable = !isActive;

        if (background != null)
        {
            Sprite sprite = isActive ? activeSprite : inactiveSprite;
            if (sprite != null && retro != null)
            {
                retro.SetNormalSprite(sprite);
                background.color = Color.white;
            }
            else if (sprite != null)
            {
                background.sprite = sprite;
                background.color = Color.white;
            }
            else
            {
                background.color = isActive ? activeBgColor : inactiveBgColor;
            }
        }
        if (label != null)
            label.color = isActive ? activeTextColor : inactiveTextColor;
    }

    private void HandleClick()
    {
        Debug.Log($"[ShopTabButtonUI] Click en tab: {tab}, GameObject: {gameObject.name}");
        OnTabClicked?.Invoke(tab);
    }
}