using System.Collections.Generic;
using UnityEngine;

public class ToppingDraggable : MonoBehaviour
{
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
    [SerializeField] private Color sauceColor = new Color(0.18f, 0.54f, 0.14f, 1f);

    [Tooltip("Width of the sauce thread at the top (pour origin).")]
    [SerializeField] private float sauceWidth = 0.06f;

    [Tooltip("Maximum length of the sauce thread downward from the pour origin.")]
    [SerializeField] private float sauceLength = 2f;

    [Tooltip("Optional child Transform at the tip of the container. " +
             "If null, defaults to this transform's position.")]
    [SerializeField] private Transform pourOrigin;

    [Header("Wobbly Stream")]
    [Tooltip("Number of points in the sauce stream. More = smoother curve.")]
    [SerializeField] private int streamSegments = 10;

    [Tooltip("How fast each point chases the one above it. Lower = more delay/trailing.")]
    [SerializeField] private float streamChaseSpeed = 12f;

    [Tooltip("Horizontal wobble intensity from Perlin noise.")]
    [SerializeField] private float wobbleAmplitude = 0.04f;

    [Tooltip("Speed of the Perlin noise scroll. Higher = faster wobble.")]
    [SerializeField] private float wobbleSpeed = 4f;

    [Tooltip("How fast the stream grows to full length when pouring starts.")]
    [SerializeField] private float streamGrowSpeed = 3f;

    [Header("Sauce Splatters")]
    [Tooltip("Seconds between each splatter spawn. Lower = denser sauce.")]
    [SerializeField] private float splatterInterval = 0.05f;

    [Tooltip("Base size of each splatter in world units.")]
    [SerializeField] private float splatterSize = 0.15f;

    [Tooltip("± random variation added to the base size.")]
    [SerializeField] private float splatterSizeVariation = 0.05f;

    [Tooltip("± horizontal spread from the impact point.")]
    [SerializeField] private float splatterSpread = 0.1f;

    [Tooltip("Optional custom sprite for splatters. If null, a circle is generated.")]
    [SerializeField] private Sprite splatterSprite;

    [Tooltip("Sorting order for splatters (should be above the food sprite).")]
    [SerializeField] private int splatterSortingOrder = 5500;

    [Tooltip("Parent transform for splatters (e.g. the food GameObject). " +
             "If null, splatters are spawned in world space.")]
    [SerializeField] private Transform splatterParent;

    [Header("Fallback Visual")]
    [Tooltip("Color used to render a square placeholder when no sprite is assigned.")]
    [SerializeField] private Color placeholderColor = new Color(0.25f, 0.6f, 0.2f, 1f);

    [Header("Sauce Bar")]
    [Tooltip("Height of the sauce bar in world units.")]
    [SerializeField] private float barHeight = 0.08f;

    [Tooltip("Width of the sauce bar in world units.")]
    [SerializeField] private float barWidth = 0.6f;

    [Tooltip("Vertical offset of the bar above the container's center.")]
    [SerializeField] private float barOffsetY = 0.55f;

    [Tooltip("Background color of the sauce bar.")]
    [SerializeField] private Color barBackgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);

    [Tooltip("Color of the sauce bar when full.")]
    [SerializeField] private Color barFullColor = new Color(0.2f, 0.85f, 0.3f, 1f);

    [Tooltip("Color of the sauce bar when empty.")]
    [SerializeField] private Color barEmptyColor = new Color(0.9f, 0.2f, 0.15f, 1f);

    [Tooltip("Sorting order for the sauce bar visuals.")]
    [SerializeField] private int barSortingOrder = 6100;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private int startSortingOrder;
    private SpriteRenderer selfRenderer;

    private bool isDragging;
    private bool isPouring;
    private bool toppingRegistered; 

    private LineRenderer sauceThread;
    private Vector3[] streamPoints;
    private float streamCurrentLength;
    private float perlinSeed;

    private float lastSplatterTime;
    private readonly List<GameObject> activeSplatters = new List<GameObject>();
    private Sprite generatedCircleSprite;

    private float currentSauceAmount;
    private bool sauceEmpty;

    private GameObject barRoot;
    private SpriteRenderer barBackground;
    private SpriteRenderer barFill;
    private static Sprite cachedPixelSprite;

    private static readonly List<ToppingDraggable> ActiveInstances = new List<ToppingDraggable>();

    void OnEnable()
    {
        if (!ActiveInstances.Contains(this))
            ActiveInstances.Add(this);
    }

    void OnDestroy()
    {
        ActiveInstances.Remove(this);
    }

    void Awake()
    {
        selfRenderer = GetComponent<SpriteRenderer>();
        EnsureCollider();
        EnsurePlaceholderVisual();
        InitializeSauceAmount();
        CreateSauceBar();
    }

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

        transform.position = GetMouseWorldPos();

        bool insideZone = IsInsideRotationZone();

        float currentZ = NormalizeAngle(transform.eulerAngles.z);

        if (insideZone)
        {
            float newZ = Mathf.MoveTowardsAngle(currentZ, 180f, rotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f, newZ);
        }
        else
        {
            float newZ = Mathf.MoveTowardsAngle(currentZ, 0f, rotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f, newZ);

            if (isPouring)
                StopPouring();
        }

        float absAngle = Mathf.Abs(NormalizeAngle(transform.eulerAngles.z));
        bool fullyInverted = absAngle >= 175f;

        if (fullyInverted && insideZone && !isPouring && !sauceEmpty)
        {
            StartPouring();
        }

        if (isPouring)
        {
            DrainSauce();

            if (sauceThread != null)
                UpdateSauceThread();
            if (Time.time - lastSplatterTime >= splatterInterval)
            {
                SpawnSplatter();
                lastSplatterTime = Time.time;
            }
        }

        UpdateSauceBar();
    }

    void OnMouseUp()
    {
        if (!isDragging) return;
        isDragging = false;

        if (isPouring || toppingRegistered)
        {
            if (!toppingRegistered)
                RegisterTopping();
        }

        StopPouring();

        transform.position = startPosition;
        transform.rotation = startRotation;

        if (selfRenderer != null)
            selfRenderer.sortingOrder = startSortingOrder;
    }

    private void StartPouring()
    {
        isPouring = true;
        lastSplatterTime = Time.time;
        streamCurrentLength = 0f;
        perlinSeed = Random.Range(0f, 1000f);
        CreateSauceThread();

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

    private void InitializeSauceAmount()
    {
        float max = (toppingData != null) ? toppingData.maxSauceAmount : 100f;
        currentSauceAmount = max;
        sauceEmpty = false;
    }

    private void DrainSauce()
    {
        if (sauceEmpty) return;

        float rate = (toppingData != null) ? toppingData.sauceDrainRate : 10f;
        currentSauceAmount -= rate * Time.deltaTime;

        if (currentSauceAmount <= 0f)
        {
            currentSauceAmount = 0f;
            sauceEmpty = true;
            StopPouring();
        }
    }

    public float GetSauceRatio()
    {
        float max = (toppingData != null) ? toppingData.maxSauceAmount : 100f;
        if (max <= 0f) return 0f;
        return Mathf.Clamp01(currentSauceAmount / max);
    }

    public void RefillSauce()
    {
        float max = (toppingData != null) ? toppingData.maxSauceAmount : 100f;
        currentSauceAmount = max;
        sauceEmpty = false;
    }

    private void CreateSauceBar()
    {
        barRoot = new GameObject("SauceBar");
        barRoot.transform.SetParent(transform, false);
        barRoot.transform.localPosition = new Vector3(0f, barOffsetY, 0f);
        barRoot.transform.localRotation = Quaternion.identity;
        barRoot.transform.localScale = Vector3.one;

        Sprite pixel = GetPixelSprite();

        GameObject bgGo = new GameObject("BarBG");
        bgGo.transform.SetParent(barRoot.transform, false);
        bgGo.transform.localPosition = Vector3.zero;
        bgGo.transform.localScale = new Vector3(barWidth, barHeight, 1f);
        barBackground = bgGo.AddComponent<SpriteRenderer>();
        barBackground.sprite = pixel;
        barBackground.color = barBackgroundColor;
        barBackground.sortingOrder = barSortingOrder;

        GameObject fillGo = new GameObject("BarFill");
        fillGo.transform.SetParent(barRoot.transform, false);
        fillGo.transform.localPosition = Vector3.zero;
        fillGo.transform.localScale = new Vector3(barWidth, barHeight, 1f);
        barFill = fillGo.AddComponent<SpriteRenderer>();
        barFill.sprite = pixel;
        barFill.color = barFullColor;
        barFill.sortingOrder = barSortingOrder + 1;

        barRoot.SetActive(false);
    }

    private void UpdateSauceBar()
    {
        if (barRoot == null || barFill == null) return;

        bool shouldShow = isDragging;
        if (barRoot.activeSelf != shouldShow)
            barRoot.SetActive(shouldShow);

        if (!shouldShow) return;

        float ratio = GetSauceRatio();

        barRoot.transform.rotation = Quaternion.identity;
        barRoot.transform.position = transform.position + new Vector3(0f, barOffsetY, 0f);

        Vector3 fillScale = barFill.transform.localScale;
        fillScale.x = barWidth * ratio;
        barFill.transform.localScale = fillScale;

        float fillOffsetX = -(barWidth - barWidth * ratio) * 0.5f;
        barFill.transform.localPosition = new Vector3(fillOffsetX, 0f, 0f);

        barFill.color = Color.Lerp(barEmptyColor, barFullColor, ratio);
    }

    private static Sprite GetPixelSprite()
    {
        if (cachedPixelSprite != null) return cachedPixelSprite;

        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.filterMode = FilterMode.Point;

        cachedPixelSprite = Sprite.Create(
            tex,
            new Rect(0, 0, 1, 1),
            new Vector2(0.5f, 0.5f),
            1f
        );

        return cachedPixelSprite;
    }

    private void CreateSauceThread()
    {
        if (sauceThread != null) return;

        int segments = Mathf.Max(streamSegments, 2);

        GameObject go = new GameObject("SauceThread");
        sauceThread = go.AddComponent<LineRenderer>();

        sauceThread.positionCount = segments;
        sauceThread.startWidth = sauceWidth;
        sauceThread.endWidth = sauceWidth * 0.5f;

        sauceThread.material = new Material(Shader.Find("Sprites/Default"));
        sauceThread.startColor = sauceColor;
        sauceThread.endColor = new Color(sauceColor.r, sauceColor.g, sauceColor.b, sauceColor.a * 0.7f);

        sauceThread.sortingOrder = 5999;
        sauceThread.useWorldSpace = true;
        sauceThread.numCapVertices = 4;
        sauceThread.numCornerVertices = 4;

        streamPoints = new Vector3[segments];
        Vector3 origin = pourOrigin != null ? pourOrigin.position : transform.position;
        for (int i = 0; i < segments; i++)
            streamPoints[i] = origin;

        sauceThread.SetPositions(streamPoints);
    }

    private void UpdateSauceThread()
    {
        if (sauceThread == null || streamPoints == null) return;

        int segments = streamPoints.Length;
        Vector3 origin = pourOrigin != null ? pourOrigin.position : transform.position;

        streamCurrentLength = Mathf.MoveTowards(streamCurrentLength, sauceLength, streamGrowSpeed * Time.deltaTime);

        streamPoints[0] = origin;

        float segmentSpacing = streamCurrentLength / (segments - 1);
        float time = Time.time;

        for (int i = 1; i < segments; i++)
        {
            float t = (float)i / (segments - 1);

            Vector3 idealPos = origin + Vector3.down * (segmentSpacing * i);

            float noiseX = Mathf.PerlinNoise(
                perlinSeed + i * 0.5f,
                time * wobbleSpeed + i * 0.3f
            ) - 0.5f;
            idealPos.x += noiseX * wobbleAmplitude * (1f + t);

            float chaseMultiplier = 1f - t * 0.5f;
            float chase = streamChaseSpeed * chaseMultiplier * Time.deltaTime;
            streamPoints[i] = Vector3.Lerp(streamPoints[i], idealPos, Mathf.Clamp01(chase));

            streamPoints[i].z = origin.z;
        }

        sauceThread.SetPositions(streamPoints);
    }

    private void SpawnSplatter()
    {
        Vector3 origin = pourOrigin != null ? pourOrigin.position : transform.position;
        Vector3 impactPoint = origin + Vector3.down * sauceLength;
        float offsetX = Random.Range(-splatterSpread, splatterSpread);
        impactPoint.x += offsetX;

        GameObject splatterGo = new GameObject("SauceSplatter");
        splatterGo.transform.position = impactPoint;

        float size = splatterSize + Random.Range(-splatterSizeVariation, splatterSizeVariation);
        size = Mathf.Max(size, 0.01f);
        splatterGo.transform.localScale = new Vector3(size, size, 1f);

        splatterGo.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        if (splatterParent != null)
            splatterGo.transform.SetParent(splatterParent, true);

        SpriteRenderer sr = splatterGo.AddComponent<SpriteRenderer>();
        sr.sprite = GetSplatterSprite();
        sr.sortingOrder = splatterSortingOrder;

        Color c = sauceColor;
        c.a = Random.Range(0.7f, 1f);
        sr.color = c;

        activeSplatters.Add(splatterGo);
    }

    private Sprite GetSplatterSprite()
    {
        if (splatterSprite != null)
            return splatterSprite;

        if (generatedCircleSprite != null)
            return generatedCircleSprite;

        generatedCircleSprite = GenerateCircleSprite();
        return generatedCircleSprite;
    }

    private Sprite GenerateCircleSprite()
    {
        const int size = 32;
        const float radius = size * 0.5f;
        const float edgeSoftness = 1.5f;

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - radius + 0.5f;
                float dy = y - radius + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha;
                if (dist <= radius - edgeSoftness)
                    alpha = 1f;
                else if (dist >= radius)
                    alpha = 0f;
                else
                    alpha = 1f - (dist - (radius - edgeSoftness)) / edgeSoftness;

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            size
        );
    }

    public void ClearSplatters()
    {
        for (int i = 0; i < activeSplatters.Count; i++)
        {
            if (activeSplatters[i] != null)
                Destroy(activeSplatters[i]);
        }

        activeSplatters.Clear();
    }

    /// <summary>Destruye los splatters desde el índice indicado en adelante. Usado por el undo de una vertida.</summary>
    public void RemoveSplattersFrom(int startIndex)
    {
        if (startIndex < 0)
            startIndex = 0;

        for (int i = activeSplatters.Count - 1; i >= startIndex; i--)
        {
            if (activeSplatters[i] != null)
                Destroy(activeSplatters[i]);

            activeSplatters.RemoveAt(i);
        }
    }

    /// <summary>Restaura la cantidad de salsa del frasco. Usado por el undo de una vertida.</summary>
    public void SetSauceAmount(float amount)
    {
        float max = (toppingData != null) ? toppingData.maxSauceAmount : 100f;
        currentSauceAmount = Mathf.Clamp(amount, 0f, max);
        sauceEmpty = currentSauceAmount <= 0f;
    }

    public static void ClearAllSplatters()
    {
        for (int i = 0; i < ActiveInstances.Count; i++)
        {
            if (ActiveInstances[i] != null)
                ActiveInstances[i].ClearSplatters();
        }
    }

    private void RegisterTopping()
    {
        if (toppingData == null)
        {
            Debug.LogWarning("[ToppingDraggable] " + gameObject.name +
                " has no ToppingSO assigned — cannot register topping.");
            return;
        }

        toppingRegistered = true;

        BuildFoodDropZone zone = FindDropZone();

        if (zone == null)
        {
            BuildStationSystem station = FindFirstObjectByType<BuildStationSystem>();
            if (station != null)
            {
                station.AddTopping(toppingData);
                BuildUndoHistory.Instance?.Push(new AddToppingUndoAction(
                    station, null, toppingData, false, this, activeSplatters.Count, currentSauceAmount));
                Debug.Log("[ToppingDraggable] Topping registrado (fallback): " + toppingData.toppingName);
            }
            else
            {
                Debug.LogWarning("[ToppingDraggable] No BuildStationSystem found — topping not registered.");
            }
            return;
        }

        zone.BuildStation.AddTopping(toppingData);
        bool visualSpawned = zone.SpawnPlateVisual(selfRenderer != null ? selfRenderer.sprite : null);
        BuildUndoHistory.Instance?.Push(new AddToppingUndoAction(
            zone.BuildStation, zone, toppingData, visualSpawned, this, activeSplatters.Count, currentSauceAmount));
        Debug.Log("[ToppingDraggable] Topping registrado: " + toppingData.toppingName);
    }

    private BuildFoodDropZone FindDropZone()
    {
        return FindFirstObjectByType<BuildFoodDropZone>();
    }

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

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
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

    private void EnsurePlaceholderVisual()
    {
        if (selfRenderer == null)
            selfRenderer = gameObject.AddComponent<SpriteRenderer>();

        if (selfRenderer.sprite == null)
        {
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
    }
}
