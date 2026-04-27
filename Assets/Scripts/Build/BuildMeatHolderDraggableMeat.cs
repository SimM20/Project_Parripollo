using UnityEngine;

public class BuildMeatHolderDraggableMeat : MonoBehaviour
{
    private MeatCutSO cut;
    private MeatTransferBuffer buffer;
    private int entryId;

    private Vector3 startPosition;
    private Transform startParent;
    private int startSortingOrder;
    private SpriteRenderer selfRenderer;

    public void Setup(MeatCutSO setupCut, MeatTransferBuffer setupBuffer, int setupEntryId)
    {
        cut = setupCut;
        buffer = setupBuffer;
        entryId = setupEntryId;
    }

    void Awake()
    {
        if (selfRenderer == null)
            selfRenderer = GetComponent<SpriteRenderer>();

        EnsureCollider();
    }

    void OnMouseDown()
    {
        if (cut == null || buffer == null)
            return;

        startPosition = transform.position;
        startParent = transform.parent;

        if (selfRenderer != null)
        {
            startSortingOrder = selfRenderer.sortingOrder;
            selfRenderer.sortingOrder = 6000;
        }
    }

    void OnMouseDrag()
    {
        if (cut == null || buffer == null)
            return;

        transform.position = GetMouseWorldPos();
    }

    void OnMouseUp()
    {
        if (selfRenderer != null)
            selfRenderer.sortingOrder = startSortingOrder;

        if (cut == null || buffer == null)
        {
            RestoreStartTransform();
            return;
        }

        bool dropped = BuildFoodDropZone.TryAcceptMeatAt(GetMouseWorldPos(), cut);

        if (dropped)
            buffer.ConsumeBuildMeatEntry(entryId, gameObject);
        else
            RestoreStartTransform();
    }

    private void RestoreStartTransform()
    {
        if (startParent != null)
            transform.SetParent(startParent, true);

        transform.position = startPosition;
    }

    private Vector3 GetMouseWorldPos()
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

    private void EnsureCollider()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null)
            box = gameObject.AddComponent<BoxCollider2D>();

        if (selfRenderer != null && selfRenderer.sprite != null)
        {
            box.size = (Vector2)selfRenderer.sprite.bounds.size * 1.2f;
            box.offset = selfRenderer.sprite.bounds.center;
        }
        else
        {
            box.size = Vector2.one;
        }
    }
}
