using UnityEngine;
using TMPro;

public class CustomerHoverBubble : MonoBehaviour
{
    public static CustomerHoverBubble Instance { get; private set; }

    [SerializeField] private SpriteRenderer circleRenderer;
    [SerializeField] private TMP_Text text;
    [SerializeField] private SpriteRenderer dishRenderer;    // NUEVO
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.2f, -1f);

    private Transform followTarget;

    void Awake()
    {
        Instance = this;
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

        if (dishRenderer != null)
        {
            dishRenderer.sprite = dishSprite;
            dishRenderer.enabled = dishSprite != null;
        }

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        followTarget = null;

        if (dishRenderer != null)
        {
            dishRenderer.sprite = null;
            dishRenderer.enabled = false;
        }

        gameObject.SetActive(false);
    }
}