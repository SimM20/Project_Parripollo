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

    [Header("Reject Reaction")]
    [Tooltip("Cuanto se corre el cuerpo hacia los costados al rechazar un plato (niega con la cabeza), " +
             "en unidades locales del cliente.")]
    [SerializeField] private float rejectShakeAmplitude = 0.08f;
    [Tooltip("Idas y vueltas del sacudon de rechazo.")]
    [SerializeField] private float rejectShakeCycles = 3f;
    [SerializeField] private float rejectShakeSeconds = 0.45f;
    [Tooltip("Tinte del cuerpo al rechazar. Arranca en este color y vuelve al normal durante el sacudon.")]
    [SerializeField] private Color rejectTint = new Color(1f, 0.45f, 0.45f, 1f);

    [Header("Arrival / Slot Move / Leave")]
    [Tooltip("Segundos de la llegada: cae a su lugar desde un poco mas arriba y aparece. Es solo el dibujo: " +
             "el cliente ya cuenta paciencia y recibe entregas desde el primer frame.")]
    [SerializeField] [Min(0.01f)] private float arrivalSeconds = 0.35f;
    [Tooltip("Desde cuanto mas arriba cae al llegar (unidades de mundo). Desde arriba y no desde abajo: los pies " +
             "estan justo sobre el borde de la parrilla y, subiendo, las piernas la pisaban.")]
    [SerializeField] [Min(0f)] private float arrivalDrop = 0.18f;
    [Tooltip("Segundos del corrimiento del dibujo al pasar a otro lugar de la fila. El lugar real cambia al instante.")]
    [SerializeField] [Min(0.01f)] private float slotMoveSeconds = 0.3f;
    [Tooltip("Saltito al correrse de lugar, como un paso.")]
    [SerializeField] [Min(0f)] private float slotMoveHop = 0.08f;
    [Tooltip("Segundos de la salida. Es un fantasma del cuerpo: el cliente real ya se fue.")]
    [SerializeField] [Min(0.01f)] private float leaveSeconds = 0.35f;
    [Tooltip("Cuanto se corre hacia el costado al irse (unidades de mundo). Negativo = a la izquierda, " +
             "lejos de la fila que se corre a ocupar su lugar desde la derecha.")]
    [SerializeField] private float leaveDrift = -0.45f;

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

    // Reaccion al rechazo de un plato: sacudon lateral + tinte rojo.
    private Vector3 skinBasePosition;
    private Color skinBaseColor = Color.white;
    private Coroutine rejectRoutine;

    // Raiz visual: todos los hijos (cuerpo, barra, burbujas) cuelgan de aca. El collider de pick
    // vive en la raiz del cliente, asi que mover el dibujo nunca mueve el area de interaccion.
    private Transform visualRoot;
    private readonly SettleOffset visualMove = new SettleOffset();
    private float arrivalT = 1f;
    private static readonly List<Transform> ChildBuffer = new List<Transform>();

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

        PlayArrival();
    }

    // ── Llegada, cambio de lugar y salida (solo presentacion) ───────────────

    /// <summary>Pasa todos los hijos a una raiz visual propia, sin cambiar ninguna posicion local.</summary>
    private void BuildVisualRoot()
    {
        if (visualRoot != null) return;

        ChildBuffer.Clear();
        for (int i = 0; i < transform.childCount; i++)
            ChildBuffer.Add(transform.GetChild(i));

        visualRoot = new GameObject("Visual").transform;
        visualRoot.SetParent(transform, false);

        for (int i = 0; i < ChildBuffer.Count; i++)
            ChildBuffer[i].SetParent(visualRoot, false);

        ChildBuffer.Clear();
    }

    /// <summary>Cae a su lugar en el mostrador y el cuerpo aparece. No demora nada: el cliente ya esta activo.</summary>
    private void PlayArrival()
    {
        if (visualRoot == null) return;

        arrivalT = 0f;
        visualMove.Begin(new Vector3(0f, arrivalDrop, 0f), arrivalSeconds, 0f);
        ApplyVisualMotion();
    }

    /// <summary>
    /// Cambia de lugar en la fila: la posicion real (collider, area de pick, recuadro) cambia al
    /// instante y el dibujo camina hasta ahi.
    /// </summary>
    public void MoveToSlot(Vector3 worldPosition)
    {
        Vector3 visualFrom = visualRoot != null ? visualRoot.position : transform.position;
        transform.position = worldPosition;

        if (visualRoot == null) return;

        visualRoot.localPosition = Vector3.zero;
        visualMove.Begin(visualFrom - worldPosition, slotMoveSeconds, slotMoveHop);
        ApplyVisualMotion();
    }

    /// <summary>
    /// Salida: un fantasma del cuerpo se corre y se desvanece. Lo llama CustomerSystem justo antes
    /// de destruir al cliente; no cambia cuando se libera el lugar.
    /// </summary>
    public void PlayLeave()
    {
        if (skinRenderer == null || !isActiveAndEnabled) return;

        StopRejectReaction();
        FinishArrival();

        SpriteGhost.Spawn(skinRenderer, new SpriteGhost.Motion
        {
            duration = leaveSeconds,
            scaleFrom = 1f,
            scaleTo = 0.94f,
            alphaFrom = 1f,
            alphaTo = 0f,
            moveBy = new Vector3(leaveDrift, 0f, 0f),
        });
    }

    private void UpdateVisualMotion()
    {
        if (visualRoot == null) return;

        if (visualMove.Active || arrivalT < 1f)
        {
            if (visualMove.Active)
                visualMove.Tick(Time.deltaTime);

            bool arriving = arrivalT < 1f;
            if (arriving)
                arrivalT = Mathf.Min(1f, arrivalT + Time.deltaTime / arrivalSeconds);

            ApplyVisualMotion();

            // Ultimo frame de la llegada: el cuerpo queda exactamente con su color base.
            if (arriving && arrivalT >= 1f && skinRenderer != null && rejectRoutine == null)
                skinRenderer.color = skinBaseColor;
        }
        else if (visualRoot.localPosition != Vector3.zero)
        {
            visualRoot.localPosition = Vector3.zero;
        }
    }

    private void ApplyVisualMotion()
    {
        Vector3 worldOffset = visualMove.Evaluate();
        visualRoot.localPosition = worldOffset == Vector3.zero ? Vector3.zero : transform.InverseTransformVector(worldOffset);

        // El cuerpo aparece en el primer tercio de la llegada (la barra y la burbuja ya se ven
        // enteras: un cuerpo transparente mucho rato detras de ellas se leia como un fantasma).
        if (skinRenderer != null && rejectRoutine == null && arrivalT < 1f)
        {
            Color c = skinBaseColor;
            c.a *= FeelEase.OutCubic(arrivalT * 3f);
            skinRenderer.color = c;
        }
    }

    /// <summary>Termina la llegada de golpe (antes de una reaccion que maneja el color del cuerpo).</summary>
    private void FinishArrival()
    {
        if (arrivalT >= 1f) return;

        arrivalT = 1f;
        if (skinRenderer != null)
            skinRenderer.color = skinBaseColor;
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
        {
            skinBaseScale = skinRenderer.transform.localScale;
            skinBasePosition = skinRenderer.transform.localPosition;
            skinBaseColor = skinRenderer.color;
        }

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

        // Al final: incluye las burbujas creadas arriba.
        BuildVisualRoot();
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
        StopRejectReaction();
    }

    /// <summary>
    /// El cliente rechaza el plato que le soltaron: niega con el cuerpo (sacudon lateral que se
    /// apaga) y se tine de rojo un instante. Solo visual: no toca paciencia ni estado.
    /// </summary>
    public void PlayRejectReaction()
    {
        if (skinRenderer == null || !isActiveAndEnabled) return;

        FinishArrival();
        StopRejectReaction();
        rejectRoutine = StartCoroutine(RejectRoutine());
    }

    private IEnumerator RejectRoutine()
    {
        Transform skin = skinRenderer.transform;
        // Si el cuerpo es la raiz del cliente no se lo corre: pelearia con el posicionado por slot.
        bool canMove = skin != transform;

        for (float e = 0f; e < rejectShakeSeconds; e += Time.deltaTime)
        {
            float t = e / rejectShakeSeconds;
            float decay = 1f - t;

            if (canMove)
            {
                float offset = Mathf.Sin(t * rejectShakeCycles * Mathf.PI * 2f) * rejectShakeAmplitude * decay;
                skin.localPosition = skinBasePosition + new Vector3(offset, 0f, 0f);
            }

            skinRenderer.color = Color.Lerp(skinBaseColor, rejectTint * skinBaseColor, decay);
            yield return null;
        }

        rejectRoutine = null;
        ResetRejectVisuals();
    }

    private void StopRejectReaction()
    {
        if (rejectRoutine == null) return;

        StopCoroutine(rejectRoutine);
        rejectRoutine = null;
        ResetRejectVisuals();
    }

    private void ResetRejectVisuals()
    {
        if (skinRenderer == null) return;

        if (skinRenderer.transform != transform)
            skinRenderer.transform.localPosition = skinBasePosition;
        skinRenderer.color = skinBaseColor;
    }

    void Update()
    {
        UpdateVisualMotion();

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
        {
            system.SelectCustomer(customer);
            TutorialSignals.Raise(TutorialSignal.CustomerClicked, target: transform);
        }
    }

    void OnWorldPointerEnter()
    {
        if (customer != null && customer.IsInFeedback) return;

        isHovered = true;

        if (customer?.order == null) return;

        // La burbuja no se crea ni se mueve: la que ya está sobre el pecho se agranda.
        SetOrderBubbleExpanded(true);
        TutorialSignals.Raise(TutorialSignal.CustomerHovered, target: transform);
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

        FinishArrival();

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