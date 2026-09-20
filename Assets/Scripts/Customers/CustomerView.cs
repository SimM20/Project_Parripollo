using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomerView : MonoBehaviour
{
    [Header("Patience Bar (fill sprite)")]
    [SerializeField] private Transform patienceFill; // escalar en X (0..1)
    [SerializeField] private float fillFullX = 1f;

    [Header("Patience Urgency")]
    [Tooltip("Color del fill con la paciencia llena. Se interpola hacia Mid y Low a medida que baja.")]
    [SerializeField] private Color patienceHighColor = new Color(0.35f, 0.85f, 0.35f);
    [SerializeField] private Color patienceMidColor = new Color(0.95f, 0.8f, 0.2f);
    [SerializeField] private Color patienceLowColor = new Color(0.9f, 0.2f, 0.2f);
    [Tooltip("Paciencia (0..1) por debajo de la cual la barra tiembla.")]
    [Range(0f, 1f)]
    [SerializeField] private float urgentThreshold = 0.2f;
    [Tooltip("Amplitud del temblor en unidades locales del contenedor de la barra.")]
    [SerializeField] private float urgentShakeAmplitude = 0.03f;
    [SerializeField] private float urgentShakeFrequency = 22f;

    [Header("Selection Visual (optional)")]
    [SerializeField] private GameObject selectionHighlight;

    [Header("Feedback Bubble")]
    [SerializeField] private CustomerFeedbackBubble feedbackBubble;

    private Customer customer;
    private CustomerSystem system;

    private Collider2D pickCollider;
    private bool isHovered;

    // Area del collider relativa al transform, para saber si un panel abierto lo tapa.
    private Bounds pickLocalBounds;
    private bool hasPickBounds;

    // Barra de paciencia: renderer del fill (color) y contenedor (temblor).
    private SpriteRenderer patienceFillRenderer;
    private Transform patienceBarRoot;
    private Vector3 patienceBarBaseLocalPos;
    private bool patienceShaking;

    private static bool deliveryDragActive;
    private static event Action OnDeliveryDragActiveChanged;

    public Customer Customer => customer;

    // Con "Enter Play Mode" sin domain reload el estatico sobrevive entre sesiones de play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        deliveryDragActive = false;
        OnDeliveryDragActiveChanged = null;
    }

    /// <summary>
    /// Marca que hay un arrastre de plato en curso. Durante el arrastre los clientes
    /// vuelven a ser detectables aunque haya paneles abiertos, porque la entrega los
    /// busca con Physics2D y no con los eventos de mouse.
    /// </summary>
    public static void SetDeliveryDragActive(bool active)
    {
        if (deliveryDragActive == active) return;

        deliveryDragActive = active;
        OnDeliveryDragActiveChanged?.Invoke();
    }

    public void Init(Customer c, CustomerSystem owner)
    {
        customer = c;
        system = owner;
        RefreshSelection(false);
        RefreshPatience();
    }

    void Awake()
    {
        pickCollider = GetComponent<Collider2D>();
        CachePickBounds();

        if (patienceFill != null)
        {
            patienceFillRenderer = patienceFill.GetComponent<SpriteRenderer>();
            patienceBarRoot = patienceFill.parent;
            if (patienceBarRoot != null)
                patienceBarBaseLocalPos = patienceBarRoot.localPosition;
        }

        if (feedbackBubble == null)
            feedbackBubble = GetComponentInChildren<CustomerFeedbackBubble>(true);

        if (feedbackBubble == null)
        {
            var bubbleGo = new GameObject("FeedbackBubble");
            bubbleGo.transform.SetParent(transform, false);
            feedbackBubble = bubbleGo.AddComponent<CustomerFeedbackBubble>();
        }
    }

    void OnEnable()
    {
        SlidingPanel.OnAnyPanelOpenChanged += HandlePanelOpenChanged;
        OnDeliveryDragActiveChanged += ApplyPickingState;
        ApplyPickingState();
    }

    void OnDisable()
    {
        SlidingPanel.OnAnyPanelOpenChanged -= HandlePanelOpenChanged;
        OnDeliveryDragActiveChanged -= ApplyPickingState;
    }

    void Update()
    {
        if (customer == null) return;
        RefreshPatience();
    }

    private void HandlePanelOpenChanged(bool anyPanelOpen) => ApplyPickingState();

    /// <summary>
    /// Los paneles deslizantes se abren justo encima de los slots de clientes y sus
    /// colliders comparten el mismo z, asi que un cliente tapado por el panel le roba el
    /// OnMouseDown a las celdas (bloqueaba, por ejemplo, agarrar el carbon). Solo se apaga
    /// el pick de los clientes que quedan debajo de un panel desplegado: los que no se
    /// superponen siguen respondiendo al hover y muestran su burbuja de pedido.
    /// </summary>
    private void ApplyPickingState()
    {
        if (customer != null && customer.IsInFeedback)
        {
            if (pickCollider != null)
                pickCollider.enabled = false;
            return;
        }

        bool blocked = !deliveryDragActive && SlidingPanel.AnyPanelOpen && IsCoveredByOpenPanel();

        if (pickCollider != null)
            pickCollider.enabled = !blocked;

        if (!blocked || !isHovered) return;

        isHovered = false;
        RestoreBubbleAfterHover();
    }

    /// <summary>
    /// Reevalua el gateo por paneles. Hay que llamarlo cuando el cliente cambia de slot:
    /// moverse puede meterlo o sacarlo de debajo de un panel desplegado.
    /// </summary>
    public void RefreshPickingState() => ApplyPickingState();

    /// <summary>
    /// True si el area de pick queda debajo de algun panel desplegado. Sin area conocida
    /// se bloquea igual que antes, para no reabrir el softlock.
    /// </summary>
    private bool IsCoveredByOpenPanel()
    {
        Bounds area;
        if (!TryGetPickArea(out area))
            return true;

        return SlidingPanel.IsAreaCoveredByOpenPanel(area);
    }

    /// <summary>Area de pick en mundo, con el cliente en la posicion de su slot actual.</summary>
    private bool TryGetPickArea(out Bounds area)
    {
        if (!hasPickBounds)
            CachePickBounds();

        if (!hasPickBounds)
        {
            area = default(Bounds);
            return false;
        }

        area = new Bounds(pickLocalBounds.center + transform.position, pickLocalBounds.size);
        return true;
    }

    /// <summary>
    /// Guarda el area del collider relativa al transform. Con BoxCollider2D se lee de
    /// offset/size (los clientes no rotan) porque Collider2D.bounds no sirve con el collider
    /// apagado ni antes del primer sync de fisica; el resto cae al AABB del fisico.
    /// </summary>
    private void CachePickBounds()
    {
        hasPickBounds = false;

        if (pickCollider == null) return;

        Vector3 scale = transform.lossyScale;
        BoxCollider2D box = pickCollider as BoxCollider2D;

        if (box != null)
        {
            pickLocalBounds = new Bounds(
                new Vector3(box.offset.x * scale.x, box.offset.y * scale.y, 0f),
                new Vector3(Mathf.Abs(box.size.x * scale.x), Mathf.Abs(box.size.y * scale.y), 0f));
            hasPickBounds = true;
            return;
        }

        if (!pickCollider.enabled) return;

        Bounds world = pickCollider.bounds;
        if (world.size.sqrMagnitude <= 0f) return;

        pickLocalBounds = new Bounds(world.center - transform.position, world.size);
        hasPickBounds = true;
    }

    void OnMouseDown()
    {
        if (customer != null && customer.IsInFeedback) return;

        if (system != null && customer != null)
            system.SelectCustomer(customer);
    }

    void OnMouseEnter()
    {
        if (customer != null && customer.IsInFeedback) return;

        isHovered = true;

        if (customer?.order == null) return;
        if (CustomerHoverBubble.Instance == null) return;

        CustomerHoverBubble.Instance.Show(
            customer.order.ToHoverString(),
            transform,
            GetDishSprite());
    }

    /// <summary>
    /// Activa el feedback de reacción y resultado económico sobre este cliente.
    /// Durante la duración (4s), el cliente no responde a clicks, hover ni arrastre.
    /// </summary>
    public void ShowFeedback(
        CustomerFeedbackState state,
        float payment,
        float tip,
        bool isMissingReplacement,
        Action onComplete,
        CustomerFeedbackConfigSO config = null)
    {
        if (customer != null)
            customer.StartFeedback();

        if (config == null)
            config = CustomerFeedbackConfigSO.Instance;

        // Apagar selección, collider y burbuja previa
        RefreshSelection(false);
        if (pickCollider != null)
            pickCollider.enabled = false;

        if (isHovered)
        {
            isHovered = false;
            CustomerHoverBubble.Instance?.Hide();
        }

        // Ocultar barra de paciencia durante el feedback (y dejarla quieta por si vuelve)
        if (patienceBarRoot != null && patienceShaking)
        {
            patienceBarRoot.localPosition = patienceBarBaseLocalPos;
            patienceShaking = false;
        }
        if (patienceFill != null && patienceFill.parent != null)
            patienceFill.parent.gameObject.SetActive(false);

        // Feedback sonoro por categoría
        CustomerFeedbackCategory category = state.GetCategory();
        if (category == CustomerFeedbackCategory.Positive)
            AudioManager.Instance?.PlayPositiveFeedback();
        else if (category == CustomerFeedbackCategory.Intermediate)
            AudioManager.Instance?.PlayIntermediateFeedback();
        else
            AudioManager.Instance?.PlayNegativeFeedback();

        if (feedbackBubble == null)
            feedbackBubble = GetComponentInChildren<CustomerFeedbackBubble>(true);

        if (feedbackBubble == null)
        {
            var bubbleGo = new GameObject("FeedbackBubble");
            bubbleGo.transform.SetParent(transform, false);
            feedbackBubble = bubbleGo.AddComponent<CustomerFeedbackBubble>();
        }

        if (feedbackBubble != null)
        {
            if (!feedbackBubble.gameObject.activeSelf)
                feedbackBubble.gameObject.SetActive(true);
            feedbackBubble.enabled = true;

            feedbackBubble.Show(
                state,
                payment,
                tip,
                isMissingReplacement,
                config != null ? config : CustomerFeedbackConfigSO.Instance,
                () =>
                {
                    if (isMissingReplacement && customer != null)
                    {
                        customer.EndFeedback();
                        if (patienceFill != null && patienceFill.parent != null)
                            patienceFill.parent.gameObject.SetActive(true);
                        ApplyPickingState();
                    }

                    onComplete?.Invoke();
                }
            );
        }
        else
        {
            onComplete?.Invoke();
        }
    }

    /// <summary>
    /// Sprite del plato para el pedido actual, resuelto por corte + punto de cocción
    /// solicitado. Devuelve null si no hay pedido o variante en el catálogo.
    /// </summary>
    public Sprite GetDishSprite()
    {
        Order order = customer?.order;
        if (order == null || system == null || system.Catalog == null) return null;

        var variant = system.Catalog.GetVariantForOrder(order);
        if (variant == null) return null;

        return variant.GetSpriteForState(order.GetRequestedState(0));
    }
    void OnMouseExit()
    {
        isHovered = false;
        RestoreBubbleAfterHover();
    }

    /// <summary>
    /// En modo selección de entrega, restaura la burbuja del cliente seleccionado
    /// en vez de ocultarla; fuera del modo, la oculta como siempre.
    /// </summary>
    private void RestoreBubbleAfterHover()
    {
        if (system != null)
            system.ShowSelectedOrderBubble();
        else if (CustomerHoverBubble.Instance != null)
            CustomerHoverBubble.Instance.Hide();
    }

    public void RefreshSelection(bool selected)
    {
        if (selectionHighlight != null)
            selectionHighlight.SetActive(selected);
    }

    private void RefreshPatience()
    {
        if (patienceFill == null || customer == null || customer.IsInFeedback) return;

        float p = customer.Patience01;

        var s = patienceFill.localScale;
        s.x = fillFullX * p;
        patienceFill.localScale = s;

        // Verde → amarillo en la mitad superior, amarillo → rojo en la inferior.
        if (patienceFillRenderer != null)
        {
            patienceFillRenderer.color = p >= 0.5f
                ? Color.Lerp(patienceMidColor, patienceHighColor, (p - 0.5f) * 2f)
                : Color.Lerp(patienceLowColor, patienceMidColor, p * 2f);
        }

        // Temblor de la barra completa cuando queda poca paciencia. Se mueve el contenedor
        // y no el cliente, así el collider de pick no se corre bajo el mouse.
        if (patienceBarRoot == null) return;

        bool urgent = p > 0f && p < urgentThreshold;
        if (urgent)
        {
            float t = Time.time * urgentShakeFrequency;
            Vector3 offset = new Vector3(
                Mathf.Sin(t) * urgentShakeAmplitude,
                Mathf.Cos(t * 1.7f) * urgentShakeAmplitude * 0.5f,
                0f);
            patienceBarRoot.localPosition = patienceBarBaseLocalPos + offset;
            patienceShaking = true;
        }
        else if (patienceShaking)
        {
            patienceBarRoot.localPosition = patienceBarBaseLocalPos;
            patienceShaking = false;
        }
    }
}