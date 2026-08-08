using UnityEngine;
using TMPro;

public class CustomerHoverBubble : MonoBehaviour
{
    public static CustomerHoverBubble Instance { get; private set; }

    [SerializeField] private SpriteRenderer circleRenderer;
    [SerializeField] private TMP_Text text;
    [SerializeField] private SpriteRenderer dishRenderer;    // NUEVO
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.2f, -1f);

    [Header("Dish Framing")]
    [Tooltip("Lado máximo (en unidades locales) que puede ocupar el sprite del plato. " +
             "Cada sprite se reescala para caber en este cuadro sin deformarse.")]
    [SerializeField] private float dishTargetSize = 0.47f;

    private Transform followTarget;
    private Vector3 dishBaseLocalPos;

    void Awake()
    {
        Instance = this;

        if (dishRenderer != null)
            dishBaseLocalPos = dishRenderer.transform.localPosition;

        Hide();
    }

    void LateUpdate()
    {
        if (followTarget != null)
            transform.position = followTarget.position + worldOffset;
    }

    public void Show(string message, Transform target, Sprite dishSprite = null)
    {
        followTarget = target;
        if (text != null) text.text = message;

        ApplyDishSprite(dishSprite);

        gameObject.SetActive(true);
    }

    /// <summary>
    /// Asigna el sprite normalizando escala y posición: los sprites del proyecto tienen
    /// distinto tamaño en unidades y algunos traen el pivot fuera del centro, así que se
    /// reescalan a 'dishTargetSize' y se recentran usando el centro real de sus bounds.
    /// </summary>
    private void ApplyDishSprite(Sprite dishSprite)
    {
        if (dishRenderer == null) return;

        dishRenderer.sprite = dishSprite;
        dishRenderer.enabled = dishSprite != null;

        if (dishSprite == null) return;

        Vector3 size = dishSprite.bounds.size;
        float largestSide = Mathf.Max(size.x, size.y);
        float scale = largestSide > 0f ? dishTargetSize / largestSide : 1f;

        Transform dish = dishRenderer.transform;
        dish.localScale = new Vector3(scale, scale, 1f);

        Vector3 center = dishSprite.bounds.center * scale;
        dish.localPosition = dishBaseLocalPos - new Vector3(center.x, center.y, 0f);
    }

    public void Hide()
    {
        followTarget = null;

        ApplyDishSprite(null);

        gameObject.SetActive(false);
    }
}