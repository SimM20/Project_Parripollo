using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Juice mínimo para botones de menú: escala suave al pasar el mouse y un pequeño
/// hundimiento al apretar. Complementa el ColorTint del Button, no lo reemplaza.
/// Usa tiempo unscaled para funcionar también con el juego pausado.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class MenuButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float hoverScale = 1.06f;
    [SerializeField] private float pressedScale = 0.97f;
    [SerializeField] private float smoothTime = 0.08f;

    private RectTransform rect;
    private Selectable selectable;
    private Vector3 baseScale;
    private float target = 1f;
    private float current = 1f;
    private float velocity;
    private bool hovered;
    private bool pressed;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        selectable = GetComponent<Selectable>();
        baseScale = rect.localScale;
    }

    private void OnDisable()
    {
        hovered = false;
        pressed = false;
        current = target = 1f;
        if (rect != null) rect.localScale = baseScale;
    }

    private void Update()
    {
        // Un botón deshabilitado no reacciona: agrandarlo invitaría a clickear algo que no hace nada.
        bool interactable = selectable == null || selectable.IsInteractable();
        target = !interactable ? 1f : pressed ? pressedScale : (hovered ? hoverScale : 1f);

        if (Mathf.Abs(current - target) < 0.0005f && Mathf.Abs(velocity) < 0.0005f)
            return;

        current = Mathf.SmoothDamp(current, target, ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        rect.localScale = baseScale * current;
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData eventData) => pressed = true;
    public void OnPointerUp(PointerEventData eventData) => pressed = false;
}
