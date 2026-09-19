using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomerView : MonoBehaviour
{
    [Header("Patience Bar (fill sprite)")]
    [SerializeField] private Transform patienceFill; // escalar en X (0..1)
    [SerializeField] private float fillFullX = 1f;

    [Header("Selection Visual (optional)")]
    [SerializeField] private GameObject selectionHighlight;

    [Header("Feedback Bubble")]
    [SerializeField] private CustomerFeedbackBubble feedbackBubble;

    private Customer customer;
    private CustomerSystem system;

    private Collider2D pickCollider;
    private bool isHovered;

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
    /// colliders comparten el mismo z, asi que el cliente le roba el OnMouseDown a las
    /// celdas del panel (bloqueaba, por ejemplo, agarrar el carbon). Mientras haya un
    /// panel desplegado el cliente deja de recibir hover y click, y la burbuja de pedido
    /// se retira.
    /// </summary>
    private void ApplyPickingState()
    {
        if (customer != null && customer.IsInFeedback)
        {
            if (pickCollider != null)
                pickCollider.enabled = false;
            return;
        }

        bool blocked = SlidingPanel.AnyPanelOpen && !deliveryDragActive;

        if (pickCollider != null)
            pickCollider.enabled = !blocked;

        if (!blocked || !isHovered) return;

        isHovered = false;
        RestoreBubbleAfterHover();
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

        // Ocultar barra de paciencia durante el feedback
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

        var s = patienceFill.localScale;
        s.x = fillFullX * customer.Patience01;
        patienceFill.localScale = s;
    }
}