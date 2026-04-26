using UnityEngine;

public class BuildDraggableFoodItem : MonoBehaviour
{
    [Header("Food Data (assign exactly one)")]
    [SerializeField] public BreadSO breadData;
    [SerializeField] public SideSO sideData;
    [SerializeField] public ToppingSO toppingData;

    private Vector3 startPosition;
    private int startSortingOrder;
    private SpriteRenderer selfRenderer;

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
    }

    void OnMouseDown()
    {
        startPosition = transform.position;

        if (selfRenderer != null)
        {
            startSortingOrder = selfRenderer.sortingOrder;
            selfRenderer.sortingOrder = 6000;
        }
    }

    void OnMouseDrag()
    {
        transform.position = GetFoodItemMouseWorldPos();
    }

    void OnMouseUp()
    {
        if (!HasExactlyOneData())
        {
            Debug.LogWarning("[BuildDraggableFoodItem] " + gameObject.name +
                " must have exactly one SO assigned (BreadSO, SideSO, or ToppingSO).");
        }
        else
        {
            BuildFoodDropZone.TryAcceptAt(GetFoodItemMouseWorldPos(), this);
        }

        // Always return to source position — item is reusable, not consumed
        transform.position = startPosition;

        if (selfRenderer != null)
            selfRenderer.sortingOrder = startSortingOrder;
    }

    private Vector3 GetFoodItemMouseWorldPos()
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
            // sprite.bounds.size is in local space (before transform scale),
            // matching BoxCollider2D.size which is also local space.
            // Multiply by 1.2 for a forgiving hit area slightly larger than the sprite.
            box.size = (Vector2)selfRenderer.sprite.bounds.size * 1.2f;
            box.offset = selfRenderer.sprite.bounds.center;
        }
        else
        {
            box.size = Vector2.one;
        }
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
