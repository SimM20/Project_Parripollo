using UnityEngine;
using TMPro;

public class CustomerHoverBubble : MonoBehaviour
{
    public static CustomerHoverBubble Instance { get; private set; }

    [SerializeField] private SpriteRenderer circleRenderer;
    [SerializeField] private TMP_Text text;
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

    public void Show(string message, Transform target)
    {
        followTarget = target;
        if (text != null) text.text = message;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        followTarget = null;
        gameObject.SetActive(false);
    }
}