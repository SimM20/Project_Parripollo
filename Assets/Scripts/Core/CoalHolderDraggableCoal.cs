using UnityEngine;

public class CoalHolderDraggableCoal : MonoBehaviour
{
    private CoalSO coalData;
    private CoalTransferBuffer transferBuffer;
    private int transferEntryId = -1;

    private Vector3 dragOffset;
    private Vector3 startPosition;
    private Transform startParent;
    private int startSortingOrder;
    private SpriteRenderer selfRenderer;
    private bool isDragging;

    public void SetCoalData(CoalSO setupCoal) => coalData = setupCoal;
    public void SetTransferBuffer(CoalTransferBuffer setupBuffer) => transferBuffer = setupBuffer;
    public void SetTransferEntryId(int setupId) => transferEntryId = setupId;

    void Awake()
    {
        selfRenderer = GetComponent<SpriteRenderer>();
    }

    void OnMouseDown()
    {
        if (coalData == null || transferBuffer == null) return;

        isDragging = true;
        startPosition = transform.position;
        startParent = transform.parent;

        if (selfRenderer != null)
        {
            startSortingOrder = selfRenderer.sortingOrder;
            selfRenderer.sortingOrder = 6000;
        }

        dragOffset = transform.position - GetMouseWorldPosition();

        UpdateHover(transform.position);
    }

    void OnMouseDrag()
    {
        if (!isDragging) return;
        transform.position = GetMouseWorldPosition() + dragOffset;
        UpdateHover(transform.position);
    }

    void OnMouseUp()
    {
        isDragging = false;

        if (transferBuffer != null) transferBuffer.ClearCoalHolderHover();

        if (selfRenderer != null)
            selfRenderer.sortingOrder = startSortingOrder;

        if (transferBuffer != null && transferEntryId != -1)
        {
            bool dropped = transferBuffer.TryDropFromCoalHolderById(transferEntryId, transform.position);

            if (!dropped) RestoreStartTransform();
        }
        else
            RestoreStartTransform();
    }

    private void UpdateHover(Vector3 worldPoint)
    {
        if (transferBuffer != null && coalData != null)
            transferBuffer.UpdateCoalHolderHover(coalData, worldPoint);
    }

    private void RestoreStartTransform()
    {
        if (startParent != null) transform.SetParent(startParent, true);
        transform.position = startPosition;
    }

    private Vector3 GetMouseWorldPosition()
    {
        Camera cam = Camera.main;
        Vector3 pos = Input.mousePosition;
        pos.z = Mathf.Abs(transform.position.z - cam.transform.position.z);
        Vector3 world = cam.ScreenToWorldPoint(pos);
        world.z = transform.position.z;
        return world;
    }
}