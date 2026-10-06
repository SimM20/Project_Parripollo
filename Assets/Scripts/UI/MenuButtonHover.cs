using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Juice mínimo para botones de menú: escala suave al pasar el mouse y un pequeño
/// hundimiento al apretar. Complementa el ColorTint del Button, no lo reemplaza.
/// Usa tiempo unscaled para funcionar también con el juego pausado.
/// La UI se dibuja a 480×270 (ver PixelGrid): el tamaño del botón cambia de a 2 px enteros (uno por
/// lado, así no se descentra). Con una escala continua, la cola del SmoothDamp (1,005 → 1,0001) movía
/// bordes y letras de a un píxel en momentos distintos y el botón temblaba al achicarse.
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

        if (current == target)
            return;

        current = Mathf.SmoothDamp(current, target, ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        if (Mathf.Abs(current - target) < 0.0005f)
        {
            // Termina justo en la escala de reposo: un 1,0001 que queda dando vueltas ya cambia píxeles.
            current = target;
            velocity = 0f;
        }

        rect.localScale = SnappedScale(current);
    }

    /// <summary>Escala que lleva el botón al tamaño en píxeles enteros más cercano a <paramref name="s"/>.</summary>
    private Vector3 SnappedScale(float s)
    {
        Vector2 sizePx = RestSizeInPixels();
        if (sizePx.x <= 0f || sizePx.y <= 0f)
            return baseScale * s;

        return new Vector3(baseScale.x * SnapAxis(sizePx.x, s), baseScale.y * SnapAxis(sizePx.y, s), baseScale.z * s);
    }

    /// <summary>Crece o se achica de a 2 px: en reposo (s = 1) da exactamente 1.</summary>
    private static float SnapAxis(float sizePx, float s)
    {
        float delta = Mathf.Round(sizePx * (s - 1f) * 0.5f) * 2f;
        return 1f + delta / sizePx;
    }

    /// <summary>Tamaño del botón en reposo, en píxeles de la render texture de 480×270.</summary>
    private Vector2 RestSizeInPixels()
    {
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        if (canvas == null)
            return Vector2.zero;

        var canvasRect = (RectTransform)canvas.rootCanvas.transform;
        float canvasHeight = canvasRect.rect.height;
        float canvasScale = canvasRect.lossyScale.y;
        if (canvasHeight <= 0f || Mathf.Approximately(canvasScale, 0f))
            return Vector2.zero;

        // Unidades del canvas → píxeles, y la escala de los padres del botón relativa al canvas.
        float pixelsPerUnit = PixelGrid.ReferenceHeight / canvasHeight;
        Vector3 parentScale = rect.parent != null ? rect.parent.lossyScale / canvasScale : Vector3.one;
        return new Vector2(
            rect.rect.width * Mathf.Abs(baseScale.x * parentScale.x),
            rect.rect.height * Mathf.Abs(baseScale.y * parentScale.y)) * pixelsPerUnit;
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData eventData) => pressed = true;
    public void OnPointerUp(PointerEventData eventData) => pressed = false;
}
