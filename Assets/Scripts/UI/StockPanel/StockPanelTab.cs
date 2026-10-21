using UnityEngine;

/// <summary>
/// Pestaña lateral que abre y cierra un panel deslizante al hacer clic.
/// Sirve para cualquier SlidingPanel (stock, items de armado).
/// </summary>
public class StockPanelTab : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SlidingPanel controller;

    [Header("Feedback")]
    [Tooltip("Cuanto se aclara la pestaña con el puntero encima (0 = nada, 1 = blanco).")]
    [Range(0f, 1f)] [SerializeField] private float hoverLighten = 0.22f;
    [Tooltip("Aclarado extra en el instante del click; se apaga en clickFlashSeconds.")]
    [Range(0f, 1f)] [SerializeField] private float clickLighten = 0.45f;
    [SerializeField] [Min(0.01f)] private float clickFlashSeconds = 0.18f;
    [SerializeField] [Min(0.01f)] private float colorSmoothTime = 0.05f;

    /// <summary>Panel que abre esta pestaña.</summary>
    public SlidingPanel Controller => controller;

    // Solo color: el collider y el transform de la pestaña no cambian.
    private SpriteRenderer tabRenderer;
    private Color baseColor = Color.white;
    private bool pointerOver;
    private float hover;
    private float hoverVelocity;
    private float flash;

    void Awake()
    {
        EnsureCollider2D();

        tabRenderer = GetComponent<SpriteRenderer>();
        if (tabRenderer != null)
            baseColor = tabRenderer.color;
    }

    void Start()
    {
        if (controller == null)
            Debug.LogWarning("[StockPanelTab] Falta la referencia 'controller'. La pestaña no abre ningún panel.");
    }

    void Update()
    {
        if (tabRenderer == null) return;

        float target = pointerOver ? 1f : 0f;
        if (flash <= 0f && Mathf.Abs(hover - target) < 0.001f && Mathf.Abs(hoverVelocity) < 0.001f)
            return;

        hover = Mathf.SmoothDamp(hover, target, ref hoverVelocity, colorSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        flash = Mathf.Max(0f, flash - Time.unscaledDeltaTime / clickFlashSeconds);

        float lighten = Mathf.Clamp01(hover * hoverLighten + flash * clickLighten);
        Color c = Color.Lerp(baseColor, Color.white, lighten);
        c.a = baseColor.a;
        tabRenderer.color = c;
    }

    void OnDisable()
    {
        pointerOver = false;
        hover = 0f;
        hoverVelocity = 0f;
        flash = 0f;
        if (tabRenderer != null)
            tabRenderer.color = baseColor;
    }

    void OnWorldPointerEnter() => pointerOver = true;

    void OnWorldPointerExit() => pointerOver = false;

    void OnWorldPointerDown()
    {
        if (controller == null)
            return;

        // El gate del tutorial vive en SlidingPanel.CanOpen(): Toggle() se rechaza solo.
        bool wasOpen = controller.IsOpen;
        controller.Toggle();

        // El destello confirma el cambio: si el tutorial rechazó la apertura no hay destello.
        if (controller.IsOpen != wasOpen)
            flash = 1f;
    }

    private void EnsureCollider2D()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
            return;

        box = gameObject.AddComponent<BoxCollider2D>();
        box.size = Vector2.one;
    }
}
