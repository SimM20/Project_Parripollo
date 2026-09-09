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
    private bool isDragging;

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

    void OnMouseDrag()
    {
        if (!isDragging) return;
        transform.position = GetFoodItemMouseWorldPos();
    }

    void OnMouseUp()
    {
        if (!isDragging) return;
        isDragging = false;
        GamePause.OnPaused -= CancelDrag;

        if (!HasExactlyOneData())
        {
            Debug.LogWarning("[BuildDraggableFoodItem] " + gameObject.name +
                " must have exactly one SO assigned (BreadSO, SideSO, or ToppingSO).");
        }
        else
            BuildFoodDropZone.TryAcceptAt(GetFoodItemMouseWorldPos(), this);

        // Always return to source position — item is reusable, not consumed
        transform.position = startPosition;

        if (selfRenderer != null)
            selfRenderer.sortingOrder = startSortingOrder;
    }

    void OnDisable() => CancelDrag();

    /// <summary>Aborta el arrastre y devuelve el ítem a su origen sin intentar el drop. Lo dispara GamePause al pausar.</summary>
    private void CancelDrag()
    {
        if (!isDragging) return;
        isDragging = false;
        GamePause.OnPaused -= CancelDrag;

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
