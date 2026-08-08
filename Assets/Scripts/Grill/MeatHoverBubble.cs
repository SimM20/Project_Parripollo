using UnityEngine;
using TMPro;

public class MeatHoverBubble : MonoBehaviour
{
    public static MeatHoverBubble Instance { get; private set; }

    [SerializeField] private SpriteRenderer circleRenderer;
    [SerializeField] private TMP_Text text;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.2f, 0f);

    private Transform followTarget;
    private Meat source;
    private MeatStates lastState;

    void Awake()
    {
        Instance = this;
        Hide();
    }

    void LateUpdate()
    {
        if (followTarget != null)
            transform.position = followTarget.position + worldOffset;

        if (source == null) return;

        // Refresco en vivo: el texto se reconstruye solo cuando cambia el estado de la cara activa.
        MeatStates current = source.ActiveSideState;
        if (current != lastState)
        {
            lastState = current;
            if (text != null) text.text = source.ToHoverString();
        }
    }

    public void Show(string message, Transform target)
    {
        source = null;
        followTarget = target;
        if (text != null) text.text = message;
        gameObject.SetActive(true);
    }

    /// <summary>Muestra la burbuja siguiendo a la pieza y actualizando el estado mientras se cocina.</summary>
    public void Show(Meat meat)
    {
        if (meat == null) return;

        Show(meat.ToHoverString(), meat.transform);
        source = meat;
        lastState = meat.ActiveSideState;
    }

    public void Hide()
    {
        followTarget = null;
        source = null;
        gameObject.SetActive(false);
    }
}
