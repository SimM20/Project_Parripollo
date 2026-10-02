using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Un cartel del tutorial: el ícono del control (tecla, botón o gesto del mouse) y un texto corto,
/// con una flecha que apunta a lo que señala. Lo crea y lo recicla <see cref="TutorialHintLayer"/>.
///
/// Cada frame se reubica junto a su objetivo: un objeto del mundo (se mide su Renderer o su
/// Collider2D y se proyecta con la cámara, que es en perspectiva) o un elemento de UI. No se sale
/// de la pantalla: si hay que correrlo, la flecha se corre sobre su borde para seguir apuntando.
/// No recibe clicks, anima con tiempo sin escalar y, si el objetivo se destruye, se va solo.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class TutorialHintView : MonoBehaviour
{
    [Header("Partes")]
    [SerializeField] private LayoutElement glyphSlot;
    [Tooltip("Dibujo entero del control (LetraQ, el mouse).")]
    [SerializeField] private Image glyphImage;
    [Tooltip("Tecla en blanco para los controles sin dibujo propio; el nombre va en keyLabel.")]
    [SerializeField] private Image keyCap;
    [SerializeField] private TMP_Text keyLabel;
    [SerializeField] private TMP_Text label;
    [Tooltip("Triángulo que apunta hacia abajo, con el pivot en el centro de su base. Se rota según el lado.")]
    [SerializeField] private RectTransform arrow;

    [Header("Medidas (px de la resolución de referencia)")]
    [SerializeField] private float glyphHeight = 40f;
    [Tooltip("Margen a cada lado del nombre, en la tecla en blanco.")]
    [SerializeField] private float keyCapPadding = 10f;
    [Tooltip("Distancia entre la punta de la flecha y lo que señala.")]
    [SerializeField] private float gap = 6f;
    [SerializeField] private float screenMargin = 12f;
    [Tooltip("Cuánto se mete la base de la flecha en el borde del cartel, para que no quede una línea entre los dos.")]
    [SerializeField] private float arrowOverlap = 2f;
    [Tooltip("La flecha no se corre hasta las esquinas redondeadas.")]
    [SerializeField] private float arrowCornerInset = 10f;

    [Header("Animación (tiempo sin escalar)")]
    [SerializeField] [Min(0.01f)] private float showDuration = 0.28f;
    [SerializeField] [Min(0.01f)] private float hideDuration = 0.16f;
    [Tooltip("Vaivén hacia el objetivo, en px. 0 = quieto.")]
    [SerializeField] private float bobAmplitude = 3f;
    [SerializeField] private float bobFrequency = 1.2f;

    private enum Phase { Hidden, Showing, Visible, Hiding }

    private static readonly Vector3[] RectCorners = new Vector3[4];

    private TutorialHintLayer layer;
    private RectTransform rect;
    private CanvasGroup group;

    private HintPrompt prompt;
    private string textKey;
    private HintPlacement placement;
    private Vector2 offset;

    private Transform target;
    private RectTransform targetRect;
    private Canvas targetCanvas;
    private Renderer targetRenderer;
    private Collider2D targetCollider;

    private Phase phase = Phase.Hidden;
    private float phaseTime;
    private float bobTime;
    private bool leavingCompleted;
    private bool listeningTexts;

    /// <summary>Cambia cada vez que el cartel se reusa: sirve para no retirar uno nuevo por uno viejo.</summary>
    public int Generation { get; private set; }

    /// <summary>Entrando o a la vista: todavía no se lo mandó a retirar.</summary>
    public bool IsShowing => phase == Phase.Showing || phase == Phase.Visible;

    /// <summary>En uso: todavía no volvió al pool (puede estar retirándose).</summary>
    public bool IsInUse => phase != Phase.Hidden;

    public Transform Target => target;

    private void Awake()
    {
        rect = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    private void OnDisable() => StopListeningTexts();

    // ── Ciclo de vida (lo maneja TutorialHintLayer) ─────────────────────────

    internal void Show(TutorialHintLayer owner, HintPrompt hintPrompt, string hintTextKey,
                       Transform hintTarget, HintPlacement hintPlacement, Vector2 hintOffset)
    {
        layer = owner;
        prompt = hintPrompt;
        textKey = hintTextKey;
        placement = hintPlacement;
        offset = hintOffset;
        SetTarget(hintTarget);

        Generation++;
        phase = Phase.Showing;
        phaseTime = 0f;
        bobTime = 0f;
        leavingCompleted = false;

        gameObject.SetActive(true);
        ApplyPlacement();
        Refresh();
        StartListeningTexts();

        // Ubicado y a escala de entrada en este mismo frame: sin un frame en el lugar equivocado.
        Animate(0f);
        Place();
    }

    /// <summary>
    /// Retira el cartel. <paramref name="completed"/> = el jugador hizo lo que pedía: sale con un
    /// pop. Si no (cambió el contexto, se destruyó el objetivo), se desvanece.
    /// </summary>
    public void Dismiss(bool completed)
    {
        if (!IsShowing)
            return;

        phase = Phase.Hiding;
        phaseTime = 0f;
        leavingCompleted = completed;
    }

    private void Recycle()
    {
        phase = Phase.Hidden;
        SetTarget(null);
        StopListeningTexts();
        group.alpha = 0f;
        gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (phase == Phase.Hidden)
            return;

        // El objetivo se destruyó (la carne pasó al plato, el cliente se fue): el cartel se va solo.
        if (target == null && IsShowing)
            Dismiss(false);

        Animate(Time.unscaledDeltaTime);

        if (phase != Phase.Hidden)
            Place();
    }

    // ── Contenido ───────────────────────────────────────────────────────────

    /// <summary>Ícono y texto para el control en uso y el idioma actual. Corre también con Loc.OnTextsChanged.</summary>
    private void Refresh()
    {
        HintGlyph glyph = layer != null && layer.Glyphs != null ? layer.Glyphs.Resolve(prompt) : default;
        bool drawn = glyph.Sprite != null;
        bool keyed = !drawn && !string.IsNullOrEmpty(glyph.Label);

        glyphImage.gameObject.SetActive(drawn);
        keyCap.gameObject.SetActive(keyed);
        glyphSlot.gameObject.SetActive(drawn || keyed);

        float glyphWidth = 0f;

        if (drawn)
        {
            glyphImage.sprite = glyph.Sprite;
            Rect spriteRect = glyph.Sprite.rect;
            glyphWidth = spriteRect.height > 0f ? glyphHeight * spriteRect.width / spriteRect.height : glyphHeight;
        }
        else if (keyed)
        {
            if (glyph.Blank != null)
                keyCap.sprite = glyph.Blank;

            keyLabel.text = glyph.Label.ToUpperInvariant();
            glyphWidth = Mathf.Max(glyphHeight, keyLabel.GetPreferredValues(keyLabel.text).x + keyCapPadding * 2f);
        }

        glyphSlot.preferredWidth = glyphWidth;
        glyphSlot.preferredHeight = glyphHeight;

        string text = string.IsNullOrEmpty(textKey) ? null : Loc.Get(textKey);
        label.text = text;
        label.gameObject.SetActive(!string.IsNullOrEmpty(text));

        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    private void StartListeningTexts()
    {
        if (listeningTexts)
            return;

        Loc.OnTextsChanged += Refresh;
        listeningTexts = true;
    }

    private void StopListeningTexts()
    {
        if (!listeningTexts)
            return;

        Loc.OnTextsChanged -= Refresh;
        listeningTexts = false;
    }

    // ── Animación ───────────────────────────────────────────────────────────

    private void Animate(float deltaTime)
    {
        phaseTime += deltaTime;
        float alpha = 1f;
        float scale = 1f;

        switch (phase)
        {
            case Phase.Showing:
            {
                float k = Mathf.Clamp01(phaseTime / showDuration);
                alpha = Mathf.Clamp01(k * 2f);
                scale = Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(k));
                if (k >= 1f)
                    phase = Phase.Visible;
                break;
            }

            case Phase.Hiding:
            {
                float k = Mathf.Clamp01(phaseTime / hideDuration);
                alpha = 1f - k;
                scale = leavingCompleted ? Mathf.Lerp(1f, 1.18f, k) : Mathf.Lerp(1f, 0.92f, k);
                if (k >= 1f)
                {
                    Recycle();
                    return;
                }
                break;
            }
        }

        if (phase != Phase.Hiding)
            bobTime += deltaTime;

        group.alpha = alpha;
        rect.localScale = new Vector3(scale, scale, 1f);
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    // ── Ubicación ───────────────────────────────────────────────────────────

    /// <summary>Pivot del cartel del lado del objetivo (crece desde la flecha) y flecha en ese borde.</summary>
    private void ApplyPlacement()
    {
        Vector2 pivot;
        float arrowAngle;

        switch (placement)
        {
            case HintPlacement.Right: pivot = new Vector2(0f, 0.5f); arrowAngle = -90f; break;
            case HintPlacement.Left:  pivot = new Vector2(1f, 0.5f); arrowAngle = 90f; break;
            case HintPlacement.Above: pivot = new Vector2(0.5f, 0f); arrowAngle = 0f; break;
            default:                  pivot = new Vector2(0.5f, 1f); arrowAngle = 180f; break;
        }

        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = pivot;

        arrow.anchorMin = arrow.anchorMax = pivot;
        arrow.pivot = new Vector2(0.5f, 1f);
        arrow.localEulerAngles = new Vector3(0f, 0f, arrowAngle);
    }

    private void Place()
    {
        RectTransform parent = rect.parent as RectTransform;
        if (target == null || parent == null)
            return;

        if (!TryGetTargetScreenRect(out Vector2 screenMin, out Vector2 screenMax))
        {
            // Detrás de la cámara: este frame no se dibuja.
            group.alpha = 0f;
            return;
        }

        Camera uiCamera = layer != null ? layer.UiCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenMin, uiCamera, out Vector2 min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenMax, uiCamera, out Vector2 max);
        Vector2 center = (min + max) * 0.5f;

        float reach = gap + arrow.rect.height;
        Vector2 anchor;
        Vector2 towardTarget;

        switch (placement)
        {
            case HintPlacement.Right: anchor = new Vector2(max.x + reach, center.y); towardTarget = Vector2.left; break;
            case HintPlacement.Left:  anchor = new Vector2(min.x - reach, center.y); towardTarget = Vector2.right; break;
            case HintPlacement.Above: anchor = new Vector2(center.x, max.y + reach); towardTarget = Vector2.down; break;
            default:                  anchor = new Vector2(center.x, min.y - reach); towardTarget = Vector2.up; break;
        }

        anchor += offset;

        // No salirse de la pantalla: se corre el cartel y la flecha compensa sobre su borde.
        Vector2 size = rect.rect.size;
        Rect bounds = parent.rect;
        Vector2 lowerLeft = anchor - Vector2.Scale(rect.pivot, size);
        Vector2 shift = new Vector2(
            ClampShift(lowerLeft.x, size.x, bounds.xMin + screenMargin, bounds.xMax - screenMargin),
            ClampShift(lowerLeft.y, size.y, bounds.yMin + screenMargin, bounds.yMax - screenMargin));
        anchor += shift;

        float bob = bobAmplitude > 0f
            ? (1f - Mathf.Cos(bobTime * bobFrequency * 2f * Mathf.PI)) * 0.5f * bobAmplitude
            : 0f;

        // Las anclas del cartel están en el centro del padre.
        rect.anchoredPosition = anchor + towardTarget * bob - bounds.center;
        PlaceArrow(-shift, size);
    }

    private void PlaceArrow(Vector2 compensation, Vector2 size)
    {
        bool onSideEdge = placement == HintPlacement.Right || placement == HintPlacement.Left;
        float edgeLength = onSideEdge ? size.y : size.x;
        float limit = Mathf.Max(0f, edgeLength * 0.5f - arrow.rect.width * 0.5f - arrowCornerInset);
        float along = Mathf.Clamp(onSideEdge ? compensation.y : compensation.x, -limit, limit);

        switch (placement)
        {
            case HintPlacement.Right: arrow.anchoredPosition = new Vector2(arrowOverlap, along); break;
            case HintPlacement.Left:  arrow.anchoredPosition = new Vector2(-arrowOverlap, along); break;
            case HintPlacement.Above: arrow.anchoredPosition = new Vector2(along, arrowOverlap); break;
            default:                  arrow.anchoredPosition = new Vector2(along, -arrowOverlap); break;
        }
    }

    /// <summary>Corrimiento para que [start, start + length] entre en [min, max]. Si no entra, se centra.</summary>
    private static float ClampShift(float start, float length, float min, float max)
    {
        if (length > max - min)
            return (min + max) * 0.5f - (start + length * 0.5f);
        if (start < min)
            return min - start;
        if (start + length > max)
            return max - (start + length);
        return 0f;
    }

    // ── Objetivo ────────────────────────────────────────────────────────────

    private void SetTarget(Transform newTarget)
    {
        target = newTarget;
        targetRect = null;
        targetCanvas = null;
        targetRenderer = null;
        targetCollider = null;

        if (newTarget == null)
            return;

        // Elemento de UI: se mide su RectTransform. Un RectTransform sin Canvas (un TextMeshPro
        // del mundo) se mide como cualquier objeto del mundo.
        if (newTarget is RectTransform uiTarget)
        {
            Canvas canvas = uiTarget.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                targetRect = uiTarget;
                targetCanvas = canvas.rootCanvas;
                return;
            }
        }

        targetRenderer = newTarget.GetComponent<Renderer>();
        targetCollider = newTarget.GetComponent<Collider2D>();
        if (targetRenderer == null && targetCollider == null)
            targetRenderer = newTarget.GetComponentInChildren<Renderer>();
    }

    /// <summary>Rectángulo del objetivo en píxeles de pantalla. False si está detrás de la cámara.</summary>
    private bool TryGetTargetScreenRect(out Vector2 min, out Vector2 max)
    {
        min = max = Vector2.zero;

        if (targetRect != null)
        {
            Camera canvasCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : (targetCanvas.worldCamera != null ? targetCanvas.worldCamera : Camera.main);

            targetRect.GetWorldCorners(RectCorners);
            for (int i = 0; i < RectCorners.Length; i++)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(canvasCamera, RectCorners[i]);
                min = i == 0 ? screen : Vector2.Min(min, screen);
                max = i == 0 ? screen : Vector2.Max(max, screen);
            }
            return true;
        }

        Camera cam = Camera.main;
        if (cam == null)
            return false;

        Bounds bounds;
        if (targetRenderer != null && targetRenderer.enabled)
            bounds = targetRenderer.bounds;
        else if (targetCollider != null && targetCollider.enabled)
            bounds = targetCollider.bounds;
        else
            bounds = new Bounds(target.position, Vector3.zero);

        // Con la burbuja de la carne abierta sobre esta pieza (mouse encima), el cartel se pone por
        // fuera de las dos: si no, tapa la barra de cocción. Se sigue apuntando a la carne y no a la
        // burbuja para que el cartel no desaparezca y vuelva a entrar con cada hover.
        if (MeatHoverBubble.Instance != null && MeatHoverBubble.Instance.TryGetPanelBounds(target, out Bounds bubble))
            bounds.Encapsulate(bubble);

        // Cámara en perspectiva: se proyectan las 8 esquinas, no el centro con un tamaño.
        Vector3 c = bounds.center;
        Vector3 e = bounds.extents;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                c.x + ((i & 1) == 0 ? -e.x : e.x),
                c.y + ((i & 2) == 0 ? -e.y : e.y),
                c.z + ((i & 4) == 0 ? -e.z : e.z));

            Vector3 screen = cam.WorldToScreenPoint(corner);
            if (screen.z <= 0f)
                return false;

            min = i == 0 ? (Vector2)screen : Vector2.Min(min, screen);
            max = i == 0 ? (Vector2)screen : Vector2.Max(max, screen);
        }
        return true;
    }
}
