using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manager central de notificaciones de la parrilla.
/// Muestra notificaciones en el lateral izquierdo cuando el jugador no está en la vista de la Parrilla (ViewType.Grill)
/// y hay carnes cocinándose en la parrilla. Agrupa las carnes por tipo de corte (MeatCutSO) en notificaciones apiladas.
/// </summary>
public class GrillNotificationManager : MonoBehaviour
{
    public static GrillNotificationManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private RectTransform notificationContainer;
    [SerializeField] private GameObject groupPrefab;
    [SerializeField] private GameObject bubblePrefab;
    [SerializeField] private Sprite circleSprite;

    [Header("Layout Configuration")]
    [SerializeField] private Vector2 bubbleSize = new Vector2(80f, 80f);
    [SerializeField] private float spacing = 15f;
    [SerializeField] private Vector2 leftMarginOffset = new Vector2(25f, 0f);

    private readonly List<GrillNotificationGroupUI> activeGroups = new List<GrillNotificationGroupUI>();
    private readonly List<GrillNotificationGroupUI> groupPool = new List<GrillNotificationGroupUI>();
    private ViewManager viewManager;

    public Sprite CircleSprite
    {
        get
        {
            if (circleSprite == null)
                circleSprite = CreateRuntimeCircleSprite();
            return circleSprite;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance == null && FindFirstObjectByType<GrillNotificationManager>() == null)
        {
            GameObject go = new GameObject("GrillNotificationManager");
            go.AddComponent<GrillNotificationManager>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        viewManager = FindFirstObjectByType<ViewManager>();
        if (viewManager != null)
        {
            viewManager.OnViewChanged += HandleViewChanged;
        }

        EnsureUIHierarchy();
    }

    private void OnDestroy()
    {
        if (viewManager != null)
        {
            viewManager.OnViewChanged -= HandleViewChanged;
        }
    }

    private void Update()
    {
        if (viewManager == null)
            viewManager = FindFirstObjectByType<ViewManager>();

        ViewType currentView = viewManager != null ? viewManager.CurrentView : ViewType.Grill;

        // Si estamos en la vista de la parrilla, ocultamos el contenedor
        if (currentView == ViewType.Grill)
        {
            if (notificationContainer != null && notificationContainer.gameObject.activeSelf)
            {
                notificationContainer.gameObject.SetActive(false);
            }
            return;
        }

        // Si estamos en otra vista (Cooler, Build, Shop, etc.), activamos y actualizamos las notificaciones
        if (notificationContainer != null && !notificationContainer.gameObject.activeSelf)
        {
            notificationContainer.gameObject.SetActive(true);
        }

        RefreshNotifications();
    }

    private void HandleViewChanged(ViewType newView)
    {
        if (notificationContainer == null) return;

        bool showNotifications = (newView != ViewType.Grill);
        notificationContainer.gameObject.SetActive(showNotifications);

        if (showNotifications)
        {
            RefreshNotifications();
        }
    }

    private void RefreshNotifications()
    {
        EnsureUIHierarchy();

        // 1. Obtener todas las carnes colocadas en la parrilla y agruparlas por MeatCutSO
        Meat[] allMeats = FindObjectsByType<Meat>(FindObjectsSortMode.None);
        Dictionary<MeatCutSO, List<Meat>> groupedMeats = new Dictionary<MeatCutSO, List<Meat>>();

        for (int i = 0; i < allMeats.Length; i++)
        {
            Meat m = allMeats[i];
            if (m != null && m.IsOnGrill && m.cut != null)
            {
                if (!groupedMeats.ContainsKey(m.cut))
                {
                    groupedMeats[m.cut] = new List<Meat>();
                }
                groupedMeats[m.cut].Add(m);
            }
        }

        // 2. Limpiar o desactivar grupos que ya no tienen carnes en la parrilla
        for (int i = activeGroups.Count - 1; i >= 0; i--)
        {
            GrillNotificationGroupUI group = activeGroups[i];
            if (group == null || group.GroupCut == null || !groupedMeats.ContainsKey(group.GroupCut))
            {
                if (group != null) group.gameObject.SetActive(false);
                activeGroups.RemoveAt(i);
                if (group != null) groupPool.Add(group);
            }
        }

        // 3. Crear o actualizar grupos para cada tipo de corte en la parrilla
        foreach (var kvp in groupedMeats)
        {
            MeatCutSO cut = kvp.Key;
            List<Meat> meats = kvp.Value;

            GrillNotificationGroupUI existingGroup = activeGroups.Find(g => g != null && g.GroupCut == cut);

            if (existingGroup != null)
            {
                existingGroup.UpdateMeats(meats);
            }
            else
            {
                GrillNotificationGroupUI newGroup = GetOrCreateGroup();
                newGroup.transform.SetParent(notificationContainer, false);
                newGroup.Initialize(cut, meats, CircleSprite);
                activeGroups.Add(newGroup);
            }
        }
    }

    private GrillNotificationGroupUI GetOrCreateGroup()
    {
        for (int i = groupPool.Count - 1; i >= 0; i--)
        {
            GrillNotificationGroupUI g = groupPool[i];
            groupPool.RemoveAt(i);

            if (g != null)
            {
                g.gameObject.SetActive(true);
                return g;
            }
        }

        if (groupPrefab != null)
        {
            GameObject obj = Instantiate(groupPrefab, notificationContainer);
            return obj.GetComponent<GrillNotificationGroupUI>();
        }

        return GrillNotificationGroupUI.CreateProceduralGroup(notificationContainer);
    }

    private void EnsureUIHierarchy()
    {
        if (notificationContainer != null) return;

        Canvas targetCanvas = FindFirstObjectByType<Canvas>();

        if (targetCanvas == null)
        {
            GameObject canvasObj = new GameObject("GrillNotificationCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            targetCanvas = canvasObj.GetComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        GameObject containerObj = new GameObject("GrillNotificationContainer", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        containerObj.transform.SetParent(targetCanvas.transform, false);

        notificationContainer = containerObj.GetComponent<RectTransform>();
        notificationContainer.anchorMin = new Vector2(0f, 0.5f);
        notificationContainer.anchorMax = new Vector2(0f, 0.5f);
        notificationContainer.pivot = new Vector2(0f, 0.5f);
        notificationContainer.anchoredPosition = leftMarginOffset;

        ContentSizeFitter sizeFitter = containerObj.GetComponent<ContentSizeFitter>();
        sizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        VerticalLayoutGroup layout = containerObj.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = spacing;
    }

    private static Sprite CreateRuntimeCircleSprite()
    {
        int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float radius = size * 0.5f;
        Vector2 center = new Vector2(radius, radius);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= radius)
                {
                    float alpha = Mathf.Clamp01((radius - dist) + 0.5f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                else
                {
                    texture.SetPixel(x, y, Color.clear);
                }
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f, 0, SpriteMeshType.FullRect, new Vector4(16, 16, 16, 16));
    }
}
