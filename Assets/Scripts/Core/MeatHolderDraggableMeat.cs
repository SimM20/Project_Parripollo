using System.Reflection;
using UnityEngine;

public class MeatHolderDraggableMeat : MonoBehaviour
{
    private MeatCutSO cut;
    private MonoBehaviour transferBuffer;

    [Header("Grid Rotation")]
    [SerializeField] private bool rotatePreviewVisual = true;
    [SerializeField] private float rotatedPreviewAngleZ = 90f;

    private Vector3 dragOffset;
    private Vector3 startPosition;
    private Transform startParent;
    private int startSortingOrder;
    private SpriteRenderer selfRenderer;
    private MethodInfo dropMethod;
    private MethodInfo updateHoverMethod;
    private MethodInfo clearHoverMethod;
    private bool isDragging;
    private bool isGridRotated;

    public void SetCut(MeatCutSO setupCut)
    {
        cut = setupCut;
    }

    public void SetTransferBuffer(MonoBehaviour setupTransferBuffer)
    {
        transferBuffer = setupTransferBuffer;
        CacheTransferBufferMethods();
    }

    void Awake()
    {
        if (selfRenderer == null)
            selfRenderer = GetComponent<SpriteRenderer>();

        EnsureCollider2D();
    }

    void OnMouseDown()
    {
        if (cut == null || transferBuffer == null)
            return;

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
        UpdateHoverPreview(transform.position);
    }

    void OnMouseDrag()
    {
        if (cut == null || transferBuffer == null)
            return;

        transform.position = GetMouseWorldPosition() + dragOffset;
        UpdateHoverPreview(transform.position);
    }

    void OnMouseUp()
    {
        isDragging = false;
        ClearHoverPreview();

        if (selfRenderer != null)
            selfRenderer.sortingOrder = startSortingOrder;

        if (cut == null || transferBuffer == null)
        {
            RestoreStartTransform();
            return;
        }

        bool dropped = TryDropToGrill();
        if (!dropped)
            RestoreStartTransform();
    }

    private bool TryDropToGrill()
    {
        if (dropMethod == null)
            CacheTransferBufferMethods();

        if (dropMethod == null)
            return false;

        object result;
        if (dropMethod.GetParameters().Length == 3)
            result = dropMethod.Invoke(transferBuffer, new object[] { cut, GetMouseWorldPosition(), isGridRotated });
        else
            result = dropMethod.Invoke(transferBuffer, new object[] { cut, GetMouseWorldPosition() });

        return result is bool ok && ok;
    }

    private void UpdateHoverPreview(Vector3 worldPoint)
    {
        if (transferBuffer == null)
            return;

        if (updateHoverMethod == null)
            CacheTransferBufferMethods();

        if (updateHoverMethod == null)
            return;

        if (updateHoverMethod.GetParameters().Length == 3)
            updateHoverMethod.Invoke(transferBuffer, new object[] { cut, worldPoint, isGridRotated });
        else
            updateHoverMethod.Invoke(transferBuffer, new object[] { cut, worldPoint });
    }

    private void ClearHoverPreview()
    {
        if (transferBuffer == null)
            return;

        if (clearHoverMethod == null)
            CacheTransferBufferMethods();

        if (clearHoverMethod == null)
            return;

        clearHoverMethod.Invoke(transferBuffer, null);
    }

    private void CacheTransferBufferMethods()
    {
        if (transferBuffer == null)
            return;

        var type = transferBuffer.GetType();
        dropMethod = type.GetMethod(
            "TryDropFromMeatHolder",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new System.Type[] { typeof(MeatCutSO), typeof(Vector3), typeof(bool) },
            null);

        if (dropMethod == null)
        {
            dropMethod = type.GetMethod(
                "TryDropFromMeatHolder",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new System.Type[] { typeof(MeatCutSO), typeof(Vector3) },
                null);
        }

        updateHoverMethod = type.GetMethod(
            "UpdateMeatHolderHover",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new System.Type[] { typeof(MeatCutSO), typeof(Vector3), typeof(bool) },
            null);

        if (updateHoverMethod == null)
        {
            updateHoverMethod = type.GetMethod(
                "UpdateMeatHolderHover",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new System.Type[] { typeof(MeatCutSO), typeof(Vector3) },
                null);
        }

        clearHoverMethod = type.GetMethod("ClearMeatHolderHover", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }

    private void Update()
    {
        if (!isDragging || cut == null || transferBuffer == null)
            return;

        if (Input.GetKeyDown(KeyCode.R))
            ToggleGridRotation();
    }

    private void ToggleGridRotation()
    {
        isGridRotated = !isGridRotated;
        ApplyGridRotationPreview();
        UpdateHoverPreview(transform.position);
    }

    private void ApplyGridRotationPreview()
    {
        if (!rotatePreviewVisual)
            return;

        Vector3 euler = transform.localEulerAngles;
        euler.z = isGridRotated ? rotatedPreviewAngleZ : 0f;
        transform.localEulerAngles = euler;
    }

    private Vector3 GetMouseWorldPosition()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return transform.position;

        Vector3 pos = Input.mousePosition;
        pos.z = Mathf.Abs(transform.position.z - cam.transform.position.z);
        Vector3 world = cam.ScreenToWorldPoint(pos);
        world.z = transform.position.z;
        return world;
    }

    private void RestoreStartTransform()
    {
        if (startParent != null)
            transform.SetParent(startParent, true);

        transform.position = startPosition;
    }

    private void EnsureCollider2D()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null)
            box = gameObject.AddComponent<BoxCollider2D>();

        box.size = Vector2.one;
    }

    void OnDisable()
    {
        isDragging = false;
        ClearHoverPreview();
    }
}
