using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Muestra un sprite "hundido" mientras se aprieta el botón, con un mínimo de tiempo en pantalla
/// para que un toque rápido también se vea. Solo cambia el sprite: la escala sigue siendo de
/// <see cref="MenuButtonHover"/> y el deshabilitado del ColorTint del Button. Tiempo unscaled.
/// </summary>
[RequireComponent(typeof(Image))]
public class UIPressedSprite : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private Sprite pressedSprite;
    [Tooltip("Segundos mínimos que se ve hundido, aunque el toque dure menos.")]
    [SerializeField] [Min(0f)] private float minPressedSeconds = 0.1f;

    private Image image;
    private Selectable selectable;
    private Sprite normalSprite;
    private bool held;
    private bool showingPressed;
    private float releaseAt;

    private void Awake()
    {
        image = GetComponent<Image>();
        selectable = GetComponent<Selectable>();
        normalSprite = image.sprite;
    }

    private void OnDisable()
    {
        held = false;
        SetPressed(false);
    }

    private void Update()
    {
        if (showingPressed && !held && Time.unscaledTime >= releaseAt)
            SetPressed(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (pressedSprite == null || eventData.button != PointerEventData.InputButton.Left) return;
        if (selectable != null && !selectable.IsInteractable()) return;

        held = true;
        releaseAt = Time.unscaledTime + minPressedSeconds;
        SetPressed(true);
    }

    public void OnPointerUp(PointerEventData eventData) => held = false;
    public void OnPointerExit(PointerEventData eventData) => held = false;

    private void SetPressed(bool value)
    {
        showingPressed = value;
        if (image != null)
            image.sprite = value ? pressedSprite : normalSprite;
    }
}
