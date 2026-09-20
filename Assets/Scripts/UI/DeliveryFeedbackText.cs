using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Mensaje breve de feedback para el flujo de entrega en Build Station.
/// Singleton de escena, mismo patrón que CustomerHoverBubble.
/// Se muestra/oculta cambiando el texto (el GameObject queda activo bajo GrillView).
/// </summary>
public class DeliveryFeedbackText : MonoBehaviour
{
    public static DeliveryFeedbackText Instance { get; private set; }

    [SerializeField] private TMP_Text text;
    [SerializeField] private float showSeconds = 2f;

    private Coroutine hideRoutine;

    void Awake()
    {
        Instance = this;
        if (text != null) text.text = string.Empty;
    }

    public void Show(string message)
    {
        Show(message, showSeconds);
    }

    /// <summary>
    /// Igual que <see cref="Show(string)"/> pero con una duración propia, para mensajes que
    /// tienen que quedar más tiempo que el feedback de entrega (aviso de últimos clientes).
    /// </summary>
    public void Show(string message, float seconds)
    {
        if (text == null) return;

        text.text = message;

        if (hideRoutine != null) StopCoroutine(hideRoutine);
        hideRoutine = StartCoroutine(HideAfterDelay(Mathf.Max(0.1f, seconds)));
    }

    public void HideNow()
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        if (text != null) text.text = string.Empty;
    }

    void OnDisable()
    {
        // Al salir de Build View se corta la corutina: limpiar para no volver con texto viejo.
        hideRoutine = null;
        if (text != null) text.text = string.Empty;
    }

    private IEnumerator HideAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        hideRoutine = null;
        if (text != null) text.text = string.Empty;
    }
}
