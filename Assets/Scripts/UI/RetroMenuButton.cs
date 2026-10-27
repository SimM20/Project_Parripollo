using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Botón de menú estilo cartel de ruta: en reposo muestra el cartel normal y, mientras está
/// seleccionado, el cartel amarillo titila lento entre dos sprites y late en escala al mismo
/// compás, a saltos y sin suavizado, como en los menús retro. Al apretar se hunde un toque.
///
/// La selección es la del EventSystem: el mouse (o el puntero del gamepad) al pasar por encima
/// selecciona el botón, así que hay un solo cartel titilando y el teclado sigue desde ahí.
/// Reemplaza a <see cref="MenuButtonHover"/> (los dos tocan la escala). El ColorTint del Button
/// queda para el apretado y el deshabilitado; el resto lo dibujan los sprites. Tiempo unscaled.
/// </summary>
[RequireComponent(typeof(Button))]
public class RetroMenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
                               ISelectHandler, IDeselectHandler, ISubmitHandler
{
    [Header("Sprites")]
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite selectedSprite;
    [Tooltip("Cuadro encendido del titileo. Vacío = solo late la escala.")]
    [SerializeField] private Sprite selectedLitSprite;

    [Header("Titileo")]
    [Tooltip("Segundos de un ciclo completo: mitad encendido (grande), mitad apagado (normal).")]
    [SerializeField] [Min(0.1f)] private float blinkPeriod = 1f;
    [Tooltip("Escala durante el medio ciclo encendido.")]
    [SerializeField] private float litScale = 1.06f;
    [SerializeField] private float pressedScale = 0.95f;
    [Tooltip("Segundos que se hunde al confirmarlo con teclado (Enter).")]
    [SerializeField] private float submitPressSeconds = 0.1f;

    [Tooltip("Al habilitarse se selecciona si no hay otra cosa seleccionada: el cursor inicial del menú.")]
    [SerializeField] private bool selectOnEnable;

    private Image image;
    private Button button;
    private RectTransform rect;
    private Vector3 baseScale;
    private bool selected;
    private bool pressed;
    private bool wasActive;
    private float blinkStart;
    private float submitPressUntil;

    private void Awake()
    {
        image = GetComponent<Image>();
        button = GetComponent<Button>();
        rect = (RectTransform)transform;
        baseScale = rect.localScale;
        if (normalSprite == null) normalSprite = image.sprite;
    }

    private void OnEnable()
    {
        EventSystem eventSystem = EventSystem.current;
        if (selectOnEnable && button.IsInteractable() && eventSystem != null && !eventSystem.alreadySelecting)
        {
            GameObject current = eventSystem.currentSelectedGameObject;
            if (current == null || !current.activeInHierarchy)
                eventSystem.SetSelectedGameObject(gameObject);
        }

        selected = eventSystem != null && eventSystem.currentSelectedGameObject == gameObject;
        Apply();
    }

    private void OnDisable()
    {
        selected = false;
        pressed = false;
        wasActive = false;
        if (rect != null) rect.localScale = baseScale;
        if (image != null && normalSprite != null) image.sprite = normalSprite;
    }

    private void Update() => Apply();

    private void Apply()
    {
        // Un botón deshabilitado no titila: invitaría a clickear algo que no hace nada.
        bool active = selected && button.IsInteractable();
        if (active && !wasActive)
            blinkStart = Time.unscaledTime;   // arranca encendido: la respuesta se ve en el acto
        wasActive = active;

        Sprite sprite = normalSprite;
        float scale = 1f;
        if (active)
        {
            sprite = selectedSprite != null ? selectedSprite : normalSprite;
            if (pressed || Time.unscaledTime < submitPressUntil)
            {
                scale = pressedScale;
            }
            else if (Mathf.Repeat(Time.unscaledTime - blinkStart, blinkPeriod) < blinkPeriod * 0.5f)
            {
                scale = litScale;
                if (selectedLitSprite != null) sprite = selectedLitSprite;
            }
        }

        if (sprite != null && image.sprite != sprite)
            image.sprite = sprite;
        rect.localScale = baseScale * scale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button.IsInteractable()) button.Select();
    }

    public void OnPointerExit(PointerEventData eventData) => pressed = false;
    public void OnPointerDown(PointerEventData eventData) => pressed = true;
    public void OnPointerUp(PointerEventData eventData) => pressed = false;
    public void OnSelect(BaseEventData eventData) => selected = true;
    public void OnDeselect(BaseEventData eventData) => selected = false;
    public void OnSubmit(BaseEventData eventData) => submitPressUntil = Time.unscaledTime + submitPressSeconds;
}
