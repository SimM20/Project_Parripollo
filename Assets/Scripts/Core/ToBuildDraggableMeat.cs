using UnityEngine;

/// <summary>
/// Arrastre de un corte que esta en la bandeja (cortes devueltos del plato).
/// Se puede soltar sobre el plato para volver a montarlo o sobre la parrilla para seguir cocinandolo.
/// </summary>
public class ToBuildDraggableMeat : MonoBehaviour
{
    /// <summary>Margen de agarre alrededor de la silueta, en unidades locales del sprite (100 px = 1 unidad).</summary>
    private const float TrayGrabPadding = 0.04f;

    private MeatCutSO cut;
    private MeatTransferBuffer buffer;
    private int entryId = -1;

    [Header("Grid Rotation")]
    [SerializeField] private bool rotatePreviewVisual = true;
    [SerializeField] private float rotatedPreviewAngleZ = 90f;

    private Vector3 dragOffset;
    private Vector3 startPosition;
    private Transform startParent;
    private int startSortingOrder;
    private SpriteRenderer selfRenderer;
    private bool isDragging;
    private bool isGridRotated;

    public void Setup(MeatCutSO setupCut, MeatTransferBuffer setupBuffer, int setupEntryId, bool setupGridRotation = false)
    {
        cut = setupCut;
        buffer = setupBuffer;
        entryId = setupEntryId;
        isGridRotated = setupGridRotation;
        ApplyGridRotationPreview();

        // El sprite ya lo puso RebuildStack; el corte cambia de visual entre estados de
        // coccion y entre entradas recicladas, asi que el collider se vuelve a medir aca.
        RefreshCollider();
    }

    void Awake()
    {
        if (selfRenderer == null)
            selfRenderer = GetComponent<SpriteRenderer>();

        RefreshCollider();
    }

    void OnMouseDown()
    {
        if (cut == null || buffer == null) return;

        isDragging = true;
        GamePause.OnPaused += CancelDrag;
        startPosition = transform.position;
        startParent = transform.parent;

        if (selfRenderer != null)
        {
            startSortingOrder = selfRenderer.sortingOrder;
            selfRenderer.sortingOrder = 6000;
        }

        Vector3 mouseWorld = GetMouseWorldPosition();
        dragOffset = transform.position - mouseWorld;

        ApplyGridRotationPreview();
        buffer.UpdateMeatHolderHover(cut, transform.position, isGridRotated);
    }

    void OnMouseDrag()
    {
        if (!isDragging || cut == null || buffer == null) return;

        transform.position = GetMouseWorldPosition() + dragOffset;
        buffer.UpdateMeatHolderHover(cut, transform.position, isGridRotated);
    }

    void OnMouseUp()
    {
        if (!isDragging) return;
        isDragging = false;
        GamePause.OnPaused -= CancelDrag;

        if (buffer != null)
            buffer.ClearMeatHolderHover();

        if (selfRenderer != null)
            selfRenderer.sortingOrder = startSortingOrder;

        if (cut == null || buffer == null)
        {
            RestoreStartTransform();
            return;
        }

        Vector3 dropPoint = GetMouseWorldPosition();

        // El plato tiene prioridad: es un area chica y superpuesta al borde de la grilla.
        // Y admite un solo corte: si esta ocupado, el corte vuelve a la bandeja en vez de
        // caer en los slots de la grilla que el plato tapa.
        bool dropped = buffer.TryPlateFromTrayById(entryId, dropPoint)
                       || (!BuildFoodDropZone.IsPlateOccupiedAt(dropPoint)
                           && buffer.TryDropFromTrayById(entryId, dropPoint, isGridRotated));

        if (!dropped)
            RestoreStartTransform();
    }

    private void Update()
    {
        if (!isDragging || cut == null || buffer == null) return;

        if (Input.GetKeyDown(KeyCode.R))
        {
            isGridRotated = !isGridRotated;
            ApplyGridRotationPreview();
            buffer.UpdateMeatHolderHover(cut, transform.position, isGridRotated);
        }
    }

    private void ApplyGridRotationPreview()
    {
        if (!rotatePreviewVisual) return;
        Vector3 euler = transform.localEulerAngles;
        euler.z = isGridRotated ? rotatedPreviewAngleZ : 0f;
        transform.localEulerAngles = euler;
    }

    private Vector3 GetMouseWorldPosition()
    {
        Camera cam = Camera.main;
        if (cam == null) return transform.position;
        Vector3 pos = Input.mousePosition;
        pos.z = Mathf.Abs(transform.position.z - cam.transform.position.z);
        Vector3 world = cam.ScreenToWorldPoint(pos);
        world.z = transform.position.z;
        return world;
    }

    private void RestoreStartTransform()
    {
        if (startParent != null) transform.SetParent(startParent, true);
        transform.position = startPosition;
    }

    /// <summary>
    /// Ajusta el collider a la silueta del corte que se esta mostrando. Los visuales salen del
    /// prefab generico 'StockPrefab' y todos los cortes se dibujan sobre un lienzo de 100x100 px,
    /// asi que medir por sprite.bounds daba la misma caja de 1x1 para un chorizo que para un paty.
    /// </summary>
    private void RefreshCollider()
    {
        if (selfRenderer == null)
            selfRenderer = GetComponent<SpriteRenderer>();

        SpriteColliderFitter.Fit(gameObject, selfRenderer, TrayGrabPadding);
    }

    void OnDisable() => CancelDrag();

    /// <summary>Aborta el arrastre y devuelve el corte a la bandeja. Lo dispara GamePause al pausar.</summary>
    private void CancelDrag()
    {
        if (!isDragging) return;

        isDragging = false;
        GamePause.OnPaused -= CancelDrag;
        if (selfRenderer != null)
            selfRenderer.sortingOrder = startSortingOrder;

        if (buffer != null)
            buffer.ClearMeatHolderHover();

        RestoreStartTransform();
    }
}
