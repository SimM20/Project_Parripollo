using UnityEngine;

/// <summary>
/// Generic Papa's Freezeria-style sauce/topping behaviour.
///
/// Flow:
/// 1. Player clicks and holds → the topping follows the mouse.
/// 2. While inside the serialized rotationZone, the container rotates toward 180°.
/// 3. When fully inverted (180°), a sauce "thread" (LineRenderer) appears and
///    falls from pourOrigin downward, following the container freely.
/// 4. On mouse release the topping is registered in BuildStationSystem,
///    the visual is spawned on the plate, and the container returns home.
///
/// The script is generic: assign any ToppingSO (Chimichurri, Salsa Criolla, etc.)
/// and configure color / width per instance.
/// </summary>
public class ToppingDraggable : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("Food Data")]
    [Tooltip("The ToppingSO this container represents (Chimichurri, Salsa Criolla, etc.).")]
    [SerializeField] private ToppingSO toppingData;

    [Header("Rotation")]
    [Tooltip("Collider2D that defines where the container starts rotating. " +
             "Create a separate GameObject with a BoxCollider2D above the food.")]
    [SerializeField] private Collider2D rotationZone;

    [Tooltip("Degrees per second the container rotates when inside the zone.")]
    [SerializeField] private float rotationSpeed = 360f;

    [Header("Pour Visual")]
    [Tooltip("Color of the sauce thread. Used for the LineRenderer.")]
    [SerializeField] private Color sauceColor = new Color(0.18f, 0.54f, 0.14f, 1f); // chimichurri green

    [Tooltip("Width of the sauce thread in world units.")]
    [SerializeField] private float sauceWidth = 0.06f;

    [Tooltip("Maximum length of the sauce thread downward from the pour origin.")]
    [SerializeField] private float sauceLength = 2f;

    [Tooltip("Optional child Transform at the tip of the container. " +
             "If null, defaults to this transform's position.")]
    [SerializeField] private Transform pourOrigin;

    [Header("Fallback Visual")]
    [Tooltip("Color used to render a square placeholder when no sprite is assigned.")]
    [SerializeField] private Color placeholderColor = new Color(0.25f, 0.6f, 0.2f, 1f);

    // ── Runtime state ────────────────────────────────────────────────────────

    private Vector3 startPosition;
    private Quaternion startRotation;
    private int startSortingOrder;
    private SpriteRenderer selfRenderer;

    private bool isDragging;
    private bool isPouring;
    private bool toppingRegistered; // prevents double-registering on a single drag

    private LineRenderer sauceThread;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        selfRenderer = GetComponent<SpriteRenderer>();
        EnsureCollider();
        EnsurePlaceholderVisual();
    }

    // ── Mouse events ─────────────────────────────────────────────────────────

    void OnMouseDown()
    {
        isDragging = true;
        isPouring = false;
        toppingRegistered = false;

        startPosition = transform.position;
        startRotation = transform.rotation;

        if (selfRenderer != null)
        {
            startSortingOrder = selfRenderer.sortingOrder;
            selfRenderer.sortingOrder = 6000;
        }
    }

    void OnMouseDrag()
    {
        if (!isDragging) return;

        // ── Follow mouse ─────────────────────────────────────────────────────
        transform.position = GetMouseWorldPos();

        // ── Rotation ─────────────────────────────────────────────────────────
        bool insideZone = IsInsideRotationZone();

        float currentZ = NormalizeAngle(transform.eulerAngles.z);

        if (insideZone)
        {
            // Rotate toward 180°
            float newZ = Mathf.MoveTowardsAngle(currentZ, 180f, rotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f, newZ);
        }
        else
        {
            // Rotate back toward 0°
            float newZ = Mathf.MoveTowardsAngle(currentZ, 0f, rotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f, newZ);

            // If we left the zone, stop pouring
            if (isPouring)
                StopPouring();
        }

        // ── Pour check ───────────────────────────────────────────────────────
        float absAngle = Mathf.Abs(NormalizeAngle(transform.eulerAngles.z));
        bool fullyInverted = absAngle >= 175f; // small tolerance

        if (fullyInverted && insideZone && !isPouring)
        {
            StartPouring();
        }

        // ── Update sauce thread ──────────────────────────────────────────────
        if (isPouring && sauceThread != null)
        {
            UpdateSauceThread();
        }
    }

    void OnMouseUp()
    {
        if (!isDragging) return;
        isDragging = false;

        // If we were pouring, register the topping
        if (isPouring || toppingRegistered)
        {
            // Only register once per drag
            if (!toppingRegistered)
                RegisterTopping();
        }

        StopPouring();

        // Restore original state
        transform.position = startPosition;
        transform.rotation = startRotation;

        if (selfRenderer != null)
            selfRenderer.sortingOrder = startSortingOrder;
    }

    // ── Pour control ─────────────────────────────────────────────────────────

    private void StartPouring()
    {
        isPouring = true;
        CreateSauceThread();

        // Register topping immediately when pour starts
        if (!toppingRegistered)
            RegisterTopping();
    }

    private void StopPouring()
    {
        isPouring = false;

        if (sauceThread != null)
        {
            Destroy(sauceThread.gameObject);
            sauceThread = null;
        }
    }

    // ── Sauce thread (LineRenderer) ──────────────────────────────────────────

    private void CreateSauceThread()
    {
        if (sauceThread != null) return;

        GameObject go = new GameObject("SauceThread");
        // World space — not parented to the rotating container
        sauceThread = go.AddComponent<LineRenderer>();

        sauceThread.positionCount = 2;
        sauceThread.startWidth = sauceWidth;
        sauceThread.endWidth = sauceWidth * 0.6f; // slightly tapers

        // Use a simple unlit material
        sauceThread.material = new Material(Shader.Find("Sprites/Default"));
        sauceThread.startColor = sauceColor;
        sauceThread.endColor = sauceColor;

        sauceThread.sortingOrder = 5999;
        sauceThread.useWorldSpace = true;

        UpdateSauceThread();
    }

    private void UpdateSauceThread()
    {
        if (sauceThread == null) return;

        Vector3 origin = pourOrigin != null ? pourOrigin.position : transform.position;
        Vector3 end = origin + Vector3.down * sauceLength;

        sauceThread.SetPosition(0, origin);
        sauceThread.SetPosition(1, end);
    }

    // ── Topping registration ─────────────────────────────────────────────────

    private void RegisterTopping()
    {
        if (toppingData == null)
        {
            Debug.LogWarning("[ToppingDraggable] " + gameObject.name +
                " has no ToppingSO assigned — cannot register topping.");
            return;
        }

        toppingRegistered = true;

        // Find the active drop zone to register in the build system
        BuildFoodDropZone zone = FindDropZone();

        if (zone == null)
        {
            // Fallback: find BuildStationSystem directly
            BuildStationSystem station = FindFirstObjectByType<BuildStationSystem>();
            if (station != null)
            {
                station.AddTopping(toppingData);
                Debug.Log("[ToppingDraggable] Topping registrado (fallback): " + toppingData.toppingName);
            }
            else
            {
                Debug.LogWarning("[ToppingDraggable] No BuildStationSystem found — topping not registered.");
            }
            return;
        }

        // Register through the zone
        zone.BuildStation.AddTopping(toppingData);
        zone.SpawnPlateVisual(selfRenderer != null ? selfRenderer.sprite : null);
        Debug.Log("[ToppingDraggable] Topping registrado: " + toppingData.toppingName);
    }

    /// <summary>
    /// Finds the first active BuildFoodDropZone. 
    /// We don't need positional overlap here — the pour already happens in the rotation zone
    /// which is positioned above the food by design.
    /// </summary>
    private BuildFoodDropZone FindDropZone()
    {
        return FindFirstObjectByType<BuildFoodDropZone>();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool IsInsideRotationZone()
    {
        if (rotationZone == null) return false;
        return rotationZone.OverlapPoint(transform.position);
    }

    private Vector3 GetMouseWorldPos()
    {
        Camera cam = Camera.main;
        if (cam == null) return transform.position;

        Vector3 pos = Input.mousePosition;
        pos.z = Mathf.Abs(transform.position.z - cam.transform.position.z);
        Vector3 world = cam.ScreenToWorldPoint(pos);
        world.z = transform.position.z;
        return world;
    }

    /// <summary>Normalize angle to [-180, 180] range.</summary>
    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    /// <summary>Ensures a BoxCollider2D exists for mouse events.</summary>
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

    /// <summary>
    /// If no sprite is assigned, creates a simple colored square placeholder
    /// so the object is visible and clickable in the editor and at runtime.
    /// </summary>
    private void EnsurePlaceholderVisual()
    {
        if (selfRenderer == null)
            selfRenderer = gameObject.AddComponent<SpriteRenderer>();

        if (selfRenderer.sprite == null)
        {
            // Create a 1x1 white texture to use as placeholder
            Texture2D tex = new Texture2D(4, 4);
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();

            selfRenderer.sprite = Sprite.Create(
                tex,
                new Rect(0, 0, 4, 4),
                new Vector2(0.5f, 0.5f),
                4f
            );

            selfRenderer.color = placeholderColor;
        }
    }

    void OnValidate()
    {
        if (selfRenderer == null)
            selfRenderer = GetComponent<SpriteRenderer>();

        // Update placeholder color in editor
        if (selfRenderer != null && selfRenderer.sprite != null)
        {
            // Only apply placeholder color if using the generated placeholder
            // (real sprites keep their original color)
        }
    }
}
