using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomerView : MonoBehaviour
{
    // La barra de paciencia es sprites sueltos (Fondo + Completo) y la camara del juego es
    // perspectiva, asi que separarlos en Z para decidir quien dibuja encima deforma la barra:
    // el que queda mas cerca se ve mas grande y corrido hacia afuera del centro de la pantalla,
    // y cuanto mas alto o mas al costado esta el cliente, mas se nota. Los dos hijos van al
    // mismo Z y el orden de dibujo lo resuelve el sortingOrder del SpriteRenderer.
    [Header("Patience Bar (fill sprite)")]
    [SerializeField] private Transform patienceFill; // escalar en X (0..1)
    [Tooltip("Escala en X del fill con la paciencia llena. En 0 se toma la que trae el prefab, " +
             "que es la que esta encuadrada dentro del marco: si no coinciden, el verde se " +
             "desborda de la barra gris apenas arranca la partida.")]
    [SerializeField] private float fillFullX = 0f;

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
    [Tooltip("Color del riel (Fondo) detras del fill.")]
    [SerializeField] private Color patienceTrackColor = new Color(0.18f, 0.13f, 0.11f);
    [SerializeField] private Color patienceOutlineColor = new Color(0.05f, 0.03f, 0.02f, 0.9f);
    [Tooltip("Cuanto se aplasta el cuerpo del cliente al entrar en zona urgente.")]
    [SerializeField] private float urgentBodySquash = 0.12f;

    [Header("Skin")]
    [Tooltip("Renderer del cuerpo del cliente. Es el que recibe la imagen que le toca al spawnear, " +
             "elegida del pool de CustomerSystem. Si queda vacio se busca solo entre los hijos.")]
    [SerializeField] private SpriteRenderer skinRenderer;

    [Header("Selection Visual (optional)")]
    [SerializeField] private GameObject selectionHighlight;

    [Header("Feedback Bubble")]
    [SerializeField] private CustomerFeedbackBubble feedbackBubble;

    [Header("Order Bubble")]
    [Tooltip("Burbuja de pedido propia de este cliente: base al pecho siempre visible, " +
             "expandida al hover. Si queda vacia se crea sola.")]
    [SerializeField] private CustomerOrderBubble orderBubble;

    private Customer customer;
    private CustomerSystem system;

    private Collider2D pickCollider;
    private bool isHovered;

    // Area del collider relativa al transform, para saber si un panel abierto lo tapa.
    private Bounds pickLocalBounds;
    private bool hasPickBounds;

    private PatienceBar patienceBar;

    // Sacudon del cuerpo al entrar en zona urgente.
    private Vector3 skinBaseScale;
    private float bodySquash;

    private static bool deliveryDragActive;
    private static event Action OnDeliveryDragActiveChanged;

    public Customer Customer => customer;

    /// <summary>Collider de hover/entrega. Apagado mientras un panel lo tapa o el cliente está en feedback.</summary>
    public Collider2D PickCollider => pickCollider;

    /// <summary>Sistema dueño de este cliente. Lo usa la burbuja para llegar al catálogo.</summary>
    public CustomerSystem System => system;

    /// <summary>Burbuja de pedido de este cliente (base + expandida).</summary>
    public CustomerOrderBubble OrderBubble => orderBubble;

    /// <summary>Imagen que tiene puesta ahora mismo este cliente, o null si no hay renderer de cuerpo.</summary>
    public Sprite CurrentSkin => skinRenderer != null ? skinRenderer.sprite : null;

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
        patienceBar?.Show();

        if (orderBubble != null)
            orderBubble.Bind(customer, this);
    }

    /// <summary>
    /// Vuelve a leer el pedido en la burbuja. Hay que llamarla cuando el pedido cambia en
    /// caliente, como en el cambio por faltante.
    /// </summary>
    public void RefreshOrderBubble()
    {
        if (orderBubble != null)
            orderBubble.Refresh();
    }

    /// <summary>Abre o cierra la burbuja de este cliente. <paramref name="preview"/> = línea del arrastre.</summary>
    public void SetOrderBubbleExpanded(bool expandedState, string preview = null)
    {
        if (orderBubble != null)
            orderBubble.SetExpanded(expandedState, preview);
    }

    /// <summary>
    /// Pone la imagen del cuerpo. La pinta del cliente no depende de su tipo: el pool vive en
    /// <see cref="CustomerSystem"/> y sortea una al spawnear, asi que el mismo dibujo puede
    /// tocarle a un apurado o a un camionero. Con null se deja la del prefab.
    /// </summary>
    public void ApplySkin(Sprite skin)
    {
        if (skin == null) return;

        if (skinRenderer == null)
            skinRenderer = ResolveSkinRenderer();

        if (skinRenderer == null)
        {
            Debug.LogWarning(
                "[CustomerView] " + name + " no tiene un SpriteRenderer de cuerpo: " +
                "no se le puede aplicar la imagen '" + skin.name + "'."
            );

            return;
        }

        skinRenderer.sprite = skin;
    }

    /// <summary>
    /// Busca el renderer del cuerpo cuando el prefab no lo trae asignado: se descartan la barra
    /// de paciencia y la burbuja de feedback, y queda el primer SpriteRenderer suelto.
    /// </summary>
    private SpriteRenderer ResolveSkinRenderer()
    {
        var renderers = GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer candidate = renderers[i];

            if (patienceFill != null &&
                (candidate.transform == patienceFill ||
                 candidate.transform.IsChildOf(patienceFill.parent != null ? patienceFill.parent : patienceFill)))
                continue;

            if (candidate.GetComponentInParent<CustomerFeedbackBubble>(true) != null)
                continue;

            return candidate;
        }

        return null;
    }

    void Awake()
    {
        pickCollider = GetComponent<Collider2D>();
        CachePickBounds();

        if (skinRenderer == null)
            skinRenderer = ResolveSkinRenderer();

        if (skinRenderer != null)
            skinBaseScale = skinRenderer.transform.localScale;

        if (patienceFill != null && patienceFill.parent != null &&
            patienceFill.GetComponent<SpriteRenderer>() != null)
        {
            if (fillFullX <= 0f)
                fillFullX = patienceFill.localScale.x;

            patienceBar = new PatienceBar(patienceFill, fillFullX, patienceTrackColor, patienceOutlineColor)
            {
                highColor = patienceHighColor,
                midColor = patienceMidColor,
                lowColor = patienceLowColor,
                urgentThreshold = urgentThreshold,
                shakeAmplitude = urgentShakeAmplitude,
                shakeFrequency = urgentShakeFrequency,
            };
        }

        if (feedbackBubble == null)
            feedbackBubble = GetComponentInChildren<CustomerFeedbackBubble>(true);

        if (feedbackBubble == null)
        {
            var bubbleGo = new GameObject("FeedbackBubble");
            bubbleGo.transform.SetParent(transform, false);
            feedbackBubble = bubbleGo.AddComponent<CustomerFeedbackBubble>();
        }

        if (orderBubble == null)
            orderBubble = GetComponentInChildren<CustomerOrderBubble>(true);

        if (orderBubble == null)
        {
            var orderGo = new GameObject("OrderBubble");
            orderGo.transform.SetParent(transform, false);
            orderBubble = orderGo.AddComponent<CustomerOrderBubble>();
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
        if (customer == null || patienceBar == null) return;

        if (patienceBar.Tick(customer.Patience01))
            bodySquash = 1f;

        if (bodySquash > 0f && skinRenderer != null)
        {
            bodySquash = Mathf.MoveTowards(bodySquash, 0f, Time.deltaTime * 3f);
            // Resorte amortiguado: se estira, se aplasta y vuelve.
            float wobble = Mathf.Sin((1f - bodySquash) * Mathf.PI * 3f) * bodySquash * urgentBodySquash;
            skinRenderer.transform.localScale = Vector3.Scale(skinBaseScale,
                new Vector3(1f - wobble, 1f + wobble, 1f));
        }
    }

    private void HandlePanelOpenChanged(bool anyPanelOpen) => ApplyPickingState();

    /// <summary>
    /// Los paneles deslizantes se abren justo encima de los slots de clientes y sus
    /// colliders comparten el mismo z, asi que un cliente tapado por el panel le roba el
    /// OnWorldPointerDown a las celdas (bloqueaba, por ejemplo, agarrar el carbon). Solo se apaga
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

    void OnWorldPointerDown()
    {
        if (customer != null && customer.IsInFeedback) return;

        if (system != null && customer != null)
            system.SelectCustomer(customer);
    }

    void OnWorldPointerEnter()
    {
        if (customer != null && customer.IsInFeedback) return;

        isHovered = true;

        if (customer?.order == null) return;

        // La burbuja no se crea ni se mueve: la que ya está sobre el pecho se agranda.
        SetOrderBubbleExpanded(true);
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
        CustomerFeedbackConfigSO config = null,
        bool burnedVariant = false)
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
            isHovered = false;

        // Durante el feedback manda la burbuja de feedback: la de pedido se apaga entera.
        if (orderBubble != null)
            orderBubble.HideAll();

        // La barra se encoge y se apaga durante el feedback.
        patienceBar?.Hide();

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
                        patienceBar?.Show();

                        // El cambio por faltante deja al cliente en su slot con un pedido
                        // nuevo: la burbuja vuelve, ya releyendo el pedido nuevo.
                        if (orderBubble != null)
                            orderBubble.ShowBase();

                        ApplyPickingState();
                    }

                    onComplete?.Invoke();
                },
                burnedVariant
            );
        }
        else
        {
            onComplete?.Invoke();
        }
    }

    /// <summary>
    /// Sprite del plato para el pedido actual, resuelto por corte + punto de cocción
    /// solicitado. Devuelve null solo si no hay pedido, o si ni la variante ni el corte
    /// tienen dibujo.
    /// </summary>
    public Sprite GetDishSprite()
    {
        Order order = customer?.order;
        if (order == null) return null;

        MeatStates requestedState = order.GetRequestedState(0);

        if (system != null && system.Catalog != null)
        {
            var variant = system.Catalog.GetVariantForOrder(order);

            if (variant != null)
            {
                Sprite fromVariant = variant.GetSpriteForState(requestedState);
                if (fromVariant != null) return fromVariant;
            }
        }

        // Sin variante se cae al sprite del propio corte en el punto pedido. Hace falta para
        // el tutorial: `FoodCatalogTutorial` tiene 1 corte y CERO variantes, así que
        // `GetVariantForOrder` siempre devuelve null y la burbuja terminaba mostrando el
        // chorizo crudo pasara lo que pasara, con un punto que no era el del pedido.
        // `ChorizoTutorial` sí trae sus sprites por punto.
        MeatCutSO cut = order.PrimaryCut;
        return cut != null ? cut.GetSpriteForState(requestedState, true) : null;
    }
    void OnWorldPointerExit()
    {
        isHovered = false;
        RestoreBubbleAfterHover();
    }

    /// <summary>
    /// Al salir el mouse la burbuja vuelve a su tamaño base, salvo que este cliente sea el
    /// seleccionado en modo entrega: ahí se queda abierta.
    /// </summary>
    private void RestoreBubbleAfterHover()
    {
        SetOrderBubbleExpanded(false);

        if (system != null)
            system.ShowSelectedOrderBubble();
    }

    public void RefreshSelection(bool selected)
    {
        if (selectionHighlight != null)
            selectionHighlight.SetActive(selected);
    }
}