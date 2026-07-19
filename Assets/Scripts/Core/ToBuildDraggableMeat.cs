using UnityEngine;

public class ToBuildDraggableMeat : MonoBehaviour
{
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
    }

    void Awake()
    {
        if (selfRenderer == null)
            selfRenderer = GetComponent<SpriteRenderer>();

        EnsureCollider2D();
    }

    void OnMouseDown()
    {
        if (cut == null || buffer == null) return;

        isDragging = true;
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
        if (cut == null || buffer == null) return;

        transform.position = GetMouseWorldPosition() + dragOffset;
        buffer.UpdateMeatHolderHover(cut, transform.position, isGridRotated);
    }

    void OnMouseUp()
    {
        if (!isDragging) return;
        isDragging = false;

        if (buffer != null)
            buffer.ClearMeatHolderHover();

        if (selfRenderer != null)
            selfRenderer.sortingOrder = startSortingOrder;

        if (cut == null || buffer == null)
        {
            RestoreStartTransform();
            return;
        }

        bool dropped = buffer.TryDropFromToBuildById(entryId, GetMouseWorldPosition(), isGridRotated);
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

    private void EnsureCollider2D()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null)
            box = gameObject.AddComponent<BoxCollider2D>();

        if (selfRenderer != null && selfRenderer.sprite != null)
        {
            box.size = selfRenderer.sprite.bounds.size;
            box.offset = selfRenderer.sprite.bounds.center;
        }
        else
        {
            box.size = Vector2.one;
        }
    }

    void OnDisable()
    {
        if (!isDragging) return;

        isDragging = false;
        if (selfRenderer != null)
            selfRenderer.sortingOrder = startSortingOrder;

        if (buffer != null)
            buffer.ClearMeatHolderHover();

        RestoreStartTransform();
    }
}
