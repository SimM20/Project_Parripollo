using System.Reflection;
using UnityEngine;

public class CoolerDraggableMeat : MonoBehaviour
{
    private MeatCutSO cut;
    private CoolerSystem coolerSystem;
    private MonoBehaviour meatTransferBuffer;
    private SpriteRenderer toGrillDropArea;

    private Vector3 dragOffset;
    private Vector3 startPosition;
    private Transform startParent;
    private int startSortingOrder;
    private SpriteRenderer selfRenderer;

    public void SetCut(MeatCutSO setupCut)
    {
        cut = setupCut;
    }

    public void SetCoolerSystem(CoolerSystem setupCoolerSystem)
    {
        coolerSystem = setupCoolerSystem;
    }

    public void SetTransferBuffer(MonoBehaviour setupTransferBuffer)
    {
        meatTransferBuffer = setupTransferBuffer;
    }

    public void SetToGrillDropArea(SpriteRenderer setupToGrillDropArea)
    {
        toGrillDropArea = setupToGrillDropArea;
    }

    void Awake()
    {
        if (selfRenderer == null)
            selfRenderer = GetComponent<SpriteRenderer>();

        EnsureCollider2D();
    }

    void OnMouseDown()
    {
        if (cut == null || coolerSystem == null || meatTransferBuffer == null)
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
        if (cut == null || coolerSystem == null || meatTransferBuffer == null)
            return;

        transform.position = GetMouseWorldPosition() + dragOffset;
    }

    void OnMouseUp()
    {
        if (selfRenderer != null)
            selfRenderer.sortingOrder = startSortingOrder;

        if (cut == null || coolerSystem == null || meatTransferBuffer == null)
        {
            RestoreStartTransform();
            return;
        }

        Vector3 dropWorldPoint = transform.position;

        bool droppedOnToGrill = IsOverToGrill(dropWorldPoint);
        if (!droppedOnToGrill)
        {
            RestoreStartTransform();
            return;
        }

        if (!coolerSystem.TryTake(cut, 1))
        {
            RestoreStartTransform();
            return;
        }

        if (!TryQueueToGrill(dropWorldPoint))
        {
            coolerSystem.Add(cut, 1);
            RestoreStartTransform();
            return;
        }

        string cutName = cut != null ? cut.cutName : "Sin corte";
        Debug.Log("Arrastraste a ToGrill: " + cutName + " | " + coolerSystem.GetDebugStockString());
    }

    private bool IsOverToGrill(Vector3 worldPoint)
    {
        if (toGrillDropArea == null)
            return false;

        Vector3 point = worldPoint;
        point.z = toGrillDropArea.bounds.center.z;

        return toGrillDropArea.bounds.Contains(point);
    }

    private bool TryQueueToGrill(Vector3 dropWorldPoint)
    {
        try
        {
            MethodInfo enqueueAtPoint = meatTransferBuffer.GetType().GetMethod(
                "EnqueueToGrillAtPoint",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (enqueueAtPoint != null)
            {
                object result = enqueueAtPoint.Invoke(meatTransferBuffer, new object[] { cut, dropWorldPoint });

                if (enqueueAtPoint.ReturnType == typeof(bool))
                    return result is bool ok && ok;

                return true;
            }

            meatTransferBuffer.SendMessage("EnqueueToGrill", cut, SendMessageOptions.DontRequireReceiver);
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("No se pudo encolar carne en ToGrill: " + ex.Message);
            return false;
        }
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
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null)
            box = gameObject.AddComponent<BoxCollider2D>();

        box.size = Vector2.one;
    }
}
