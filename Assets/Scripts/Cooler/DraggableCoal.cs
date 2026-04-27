using System.Reflection;
using UnityEngine;

public class DraggableCoal : MonoBehaviour
{
    private CoalSO coal;
    private CoolerSystem coolerSystem;
    private SpriteRenderer toGrillDropArea;

    private Vector3 dragOffset;
    private Vector3 startPosition;
    private Transform startParent;
    private int startSortingOrder;
    private SpriteRenderer selfRenderer;

    void Awake()
    {
        if (selfRenderer == null)
            selfRenderer = GetComponent<SpriteRenderer>();

        EnsureCollider2D();
    }
    public void SetCoalData(CoalSO setupCoal) => coal = setupCoal;

    public void SetCoolerSystem(CoolerSystem setupCoolerSystem) => coolerSystem = setupCoolerSystem;

    public void SetToGrillDropArea(SpriteRenderer setupToGrillDropArea) => toGrillDropArea = setupToGrillDropArea;

    void OnMouseDown()
    {
        if (coal == null || coolerSystem == null)
            return;

        startPosition = transform.position;
        startParent = transform.parent;

        if (selfRenderer != null)
        {
            startSortingOrder = selfRenderer.sortingOrder;
            selfRenderer.sortingOrder = 5000;
        }

        Vector3 mouseWorld = GetMouseWorldPosition();
        dragOffset = transform.position - mouseWorld;
    }

    void OnMouseDrag()
    {
        if (coal == null || coolerSystem == null)
            return;

        transform.position = GetMouseWorldPosition() + dragOffset;
    }

    void OnMouseUp()
    {
        if (IsOverToGrill(transform.position))
        {
            if (coolerSystem.TryTake(coal, 1))
            {
                var buffer = FindFirstObjectByType<CoalTransferBuffer>();
                if (buffer != null)
                    buffer.EnqueueToGrill(coal);
            }
        }

        Destroy(gameObject);
    }

    private bool IsOverToGrill(Vector3 worldPoint)
    {
        if (toGrillDropArea == null)
            return false;

        Vector3 point = worldPoint;
        point.z = toGrillDropArea.bounds.center.z;

        return toGrillDropArea.bounds.Contains(point);
    }

    private void RestoreStartTransform()
    {
        if (startParent != null)
            transform.SetParent(startParent, true);

        transform.position = startPosition;
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

    private void EnsureCollider2D()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>() ?? gameObject.AddComponent<BoxCollider2D>();
        box.size = Vector2.one;
    }
}