using UnityEngine;

public class BuildDraggableFoodItem : MonoBehaviour
{
    [Header("Food Data (assign exactly one)")]
    [SerializeField] public BreadSO breadData;
    [SerializeField] public SideSO sideData;
    [SerializeField] public ToppingSO toppingData;

    [Header("Feedback")]
    [Tooltip("Escala del dibujo mientras se lo arrastra (1 = sin cambio). El collider se compensa: su area no cambia.")]
    [SerializeField] private float dragScale = 1.12f;
    [SerializeField] [Min(0.01f)] private float liftSeconds = 0.08f;
    [Tooltip("Segundos de la vuelta al panel cuando el plato no lo acepta. Aceptado vuelve al toque: el plato ya lo muestra.")]
    [SerializeField] [Min(0.01f)] private float rejectReturnSeconds = 0.16f;

    private Vector3 startPosition;
    private int startSortingOrder;
    private SpriteRenderer selfRenderer;
    private bool isDragging;

    // Presentacion: el dibujo crece y vuelve deslizando, pero el area de agarre queda fija
    // (BoxCollider2D compensado en tamano y offset).
    private BoxCollider2D grabBox;
    private Vector2 baseBoxSize;
    private Vector2 baseBoxOffset;
    private Vector3 baseScale = Vector3.one;
    private float lift;
    private Vector3 visualOffset;
    private readonly SettleOffset settle = new SettleOffset();

    public bool HasExactlyOneData()
    {
        int count = 0;
        if (breadData != null) count++;
        if (sideData != null) count++;
        if (toppingData != null) count++;
        return count == 1;
    }

    void Awake()
    {
        selfRenderer = GetComponent<SpriteRenderer>();
        EnsureFoodItemCollider();

        baseScale = transform.localScale;
        grabBox = GetComponent<BoxCollider2D>();
        if (grabBox != null)
        {
            baseBoxSize = grabBox.size;
            baseBoxOffset = grabBox.offset;
        }
    }

    void Update()
    {
        float liftTarget = isDragging ? 1f : 0f;
        bool lifting = !Mathf.Approximately(lift, liftTarget);
        if (!lifting && !settle.Active && visualOffset == Vector3.zero)
            return;

        if (lifting)
            lift = Mathf.MoveTowards(lift, liftTarget, Time.unscaledDeltaTime / liftSeconds);

        ApplyPresentation(settle.Active ? settle.Tick(Time.unscaledDeltaTime) : Vector3.zero);
    }

    /// <summary>
    /// Escala del levantado + corrimiento de la vuelta, con el collider compensado para que el
    /// area de agarre quede siempre en el lugar y del tamano originales.
    /// </summary>
    private void ApplyPresentation(Vector3 offset)
    {
        Vector3 rest = transform.position - visualOffset;
        visualOffset = offset;

        float factor = Mathf.LerpUnclamped(1f, dragScale, FeelEase.OutCubic(lift));
        transform.localScale = baseScale * factor;
        transform.position = rest + offset;

        if (grabBox != null && factor > 0.0001f)
        {
            Vector2 localOffset = offset == Vector3.zero ? Vector2.zero : (Vector2)transform.InverseTransformVector(offset);
            grabBox.size = baseBoxSize / factor;
            grabBox.offset = baseBoxOffset / factor - localOffset;
        }
    }

    /// <summary>Deja dibujo, escala y collider en reposo de inmediato.</summary>
    private void ResetPresentation()
    {
        settle.Stop();
        lift = 0f;
        ApplyPresentation(Vector3.zero);
    }

    void OnWorldPointerDown()
    {
        // Agarrado a mitad de la vuelta: el origen es su lugar real, sin el corrimiento.
        settle.Stop();
        ApplyPresentation(Vector3.zero);

        isDragging = true;
        GamePause.OnPaused += CancelDrag;
        startPosition = transform.position;
        AudioManager.Instance?.PlayOnUseTopping();

        if (selfRenderer != null)
        {
            startSortingOrder = selfRenderer.sortingOrder;
            selfRenderer.sortingOrder = 6000;
        }
    }

    void OnWorldPointerDrag()
    {
        if (!isDragging) return;
        transform.position = GetFoodItemMouseWorldPos();
    }

    void OnWorldPointerUp()
    {
        if (!isDragging) return;
        isDragging = false;
        GamePause.OnPaused -= CancelDrag;

        if (!HasExactlyOneData())
        {
            Debug.LogWarning("[BuildDraggableFoodItem] " + gameObject.name +
                " must have exactly one SO assigned (BreadSO, SideSO, or ToppingSO).");
        }
        Vector3 releaseVisual = transform.position;
        bool accepted = HasExactlyOneData() && BuildFoodDropZone.TryAcceptAt(GetFoodItemMouseWorldPos(), this);

        // Always return to source position — item is reusable, not consumed
        transform.position = startPosition;

        // Rechazado (fuera del plato o el plato no lo admite): se lo ve volver al panel.
        if (!accepted && settle.Begin(releaseVisual - startPosition, rejectReturnSeconds, 0f))
        {
            visualOffset = Vector3.zero;
            ApplyPresentation(settle.Evaluate());
        }

        if (selfRenderer != null)
            selfRenderer.sortingOrder = startSortingOrder;
    }

    void OnDisable()
    {
        CancelDrag();
        ResetPresentation();
    }

    /// <summary>Aborta el arrastre y devuelve el ítem a su origen sin intentar el drop. Lo dispara GamePause al pausar.</summary>
    private void CancelDrag()
    {
        if (!isDragging) return;
        isDragging = false;
        GamePause.OnPaused -= CancelDrag;

        transform.position = startPosition;
        visualOffset = Vector3.zero;

        if (selfRenderer != null)
            selfRenderer.sortingOrder = startSortingOrder;
    }

    private Vector3 GetFoodItemMouseWorldPos()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return transform.position;

        Vector3 pos = InputManager.PointerPosition;
        pos.z = Mathf.Abs(transform.position.z - cam.transform.position.z);
        Vector3 world = cam.ScreenToWorldPoint(pos);
        world.z = transform.position.z;
        return world;
    }

    private void EnsureFoodItemCollider()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null)
            box = gameObject.AddComponent<BoxCollider2D>();

        ResizeColliderToSprite(box);
    }

    private void ResizeColliderToSprite(BoxCollider2D box)
    {
        if (box == null) return;

        if (selfRenderer == null)
            selfRenderer = GetComponent<SpriteRenderer>();

        if (selfRenderer != null && selfRenderer.sprite != null)
        {
            box.size = (Vector2)selfRenderer.sprite.bounds.size * 1.2f;
            box.offset = selfRenderer.sprite.bounds.center;
        }
        else
            box.size = Vector2.one;
    }

    void OnValidate()
    {
        if (selfRenderer == null)
            selfRenderer = GetComponent<SpriteRenderer>();

        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
            ResizeColliderToSprite(box);
    }
}
