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
    [SerializeField] [Min(0.01f)] private float showDuration = 0.35f;
    [SerializeField] [Min(0.01f)] private float hideDuration = 0.16f;
    [Tooltip("Vaivén hacia el objetivo, en px. 0 = quieto.")]
    [SerializeField] private float bobAmplitude = 6f;
    [SerializeField] private float bobFrequency = 1.4f;

    [Header("Llamar la atención")]
    [Tooltip("Fondo del cartel: su borde parpadea y lleva el contorno brillante. Vacío = el hijo 'Background'.")]
    [SerializeField] private Image background;
    [Tooltip("Imagen de la flecha: parpadea y se estira hacia el objetivo. Vacío = la Image de 'arrow'.")]
    [SerializeField] private Image arrowImage;
    [Tooltip("Color del parpadeo, del contorno y del recuadro alrededor del objetivo.")]
    [SerializeField] private Color flashColor = new Color(1f, 0.72f, 0.1f, 1f);
    [Tooltip("Parpadeos por segundo. 0 = sin parpadeo.")]
    [SerializeField] [Min(0f)] private float flashFrequency = 1.2f;
    [Tooltip("Grosor máximo del contorno brillante, en px. 0 = sin contorno.")]
    [SerializeField] [Min(0f)] private float outlineDistance = 3f;
    [Tooltip("Cuánto late el tamaño del cartel (0.04 = ±4 %).")]
    [SerializeField] [Min(0f)] private float pulseAmount = 0.04f;
    [SerializeField] [Min(0f)] private float pulseFrequency = 1.2f;
    [Tooltip("Cada cuánto el cartel da un salto (crece y vuelve), en segundos. 0 = nunca.")]
    [SerializeField] [Min(0f)] private float burstInterval = 3.5f;
    [SerializeField] [Min(0.01f)] private float burstDuration = 0.4f;
    [Tooltip("Cuánto crece en el salto (0.1 = +10 %).")]
    [SerializeField] [Min(0f)] private float burstScale = 0.1f;
    [Tooltip("Cuánto se estira la flecha hacia el objetivo con cada parpadeo (0.2 = +20 %).")]
    [SerializeField] [Min(0f)] private float arrowStretch = 0.2f;

    [Header("Insistencia (si el jugador lo ignora)")]
    [Tooltip("Segundos a la vista antes de empezar a insistir más.")]
    [SerializeField] [Min(0f)] private float escalateAfter = 6f;
    [Tooltip("Segundos que tarda en llegar a la insistencia máxima.")]
    [SerializeField] [Min(0.01f)] private float escalateDuration = 10f;
    [Tooltip("Multiplicador de pulso, saltos y recuadro a la insistencia máxima.")]
    [SerializeField] [Min(1f)] private float maxEscalation = 1.35f;

    [Header("Recuadro alrededor del objetivo")]
    [SerializeField] private bool showFocusFrame = true;
    [Tooltip("Distancia mínima entre el objetivo y las esquinas del recuadro, en px.")]
    [SerializeField] private float focusPadding = 8f;
    [Tooltip("Cuánto se abre y se cierra el recuadro, en px.")]
    [SerializeField] [Min(0f)] private float focusBreath = 6f;
    [SerializeField] [Min(1f)] private float focusThickness = 4f;
    [SerializeField] [Min(1f)] private float focusCornerLength = 24f;

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

    // Llamar la atención
    private Outline outline;
    private Color backgroundBaseColor = Color.white;
    private Color arrowBaseColor = Color.white;
    private float effectTime;
    private float aliveTime;
    private float burstClock;
    private float burstTime = float.MaxValue;
    private float flash;
    private float escalation = 1f;

    private RectTransform focusFrame;
    private CanvasGroup focusGroup;
    private readonly Image[] focusBars = new Image[8];

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

        if (background == null)
        {
            Transform child = transform.Find("Background");
            if (child != null)
                background = child.GetComponent<Image>();
        }
        if (arrowImage == null && arrow != null)
            arrowImage = arrow.GetComponent<Image>();

        if (background != null)
        {
            backgroundBaseColor = background.color;
            if (outlineDistance > 0f)
            {
                outline = background.GetComponent<Outline>();
                if (outline == null)
                    outline = background.gameObject.AddComponent<Outline>();
                outline.useGraphicAlpha = true;
            }
        }
        if (arrowImage != null)
            arrowBaseColor = arrowImage.color;
    }

    private void OnDisable() => StopListeningTexts();

    private void OnDestroy()
    {
        if (focusFrame != null)
            Destroy(focusFrame.gameObject);
    }

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
        effectTime = 0f;
        aliveTime = 0f;
        escalation = 1f;
        // El primer sacudón sale apenas termina de entrar.
        burstClock = burstInterval > 0f ? Mathf.Max(0f, burstInterval - showDuration) : 0f;
        burstTime = float.MaxValue;

        gameObject.SetActive(true);
        EnsureFocusFrame();
        if (focusFrame != null)
        {
            focusFrame.gameObject.SetActive(true);
            // Debajo del cartel: el recuadro nunca le tapa el texto.
            focusFrame.SetSiblingIndex(rect.GetSiblingIndex());
        }
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
        ResetEffects();
        if (focusFrame != null)
            focusFrame.gameObject.SetActive(false);
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
                // Entra con un rebote marcado: que se note que apareció algo.
                float k = Mathf.Clamp01(phaseTime / showDuration);
                alpha = Mathf.Clamp01(k * 2.5f);
                scale = Mathf.LerpUnclamped(0.45f, 1f, EaseOutBack(k, 2.2f));
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
        {
            bobTime += deltaTime * escalation;
            effectTime += deltaTime;
            aliveTime += deltaTime;
            UpdateEscalation();

            // Late todo el tiempo y, cada tanto, un salto.
            scale *= 1f + Mathf.Sin(effectTime * pulseFrequency * 2f * Mathf.PI) * pulseAmount * escalation;
            UpdateBurst(deltaTime, ref scale);
            UpdateFlash();
        }
        else
        {
            flash = Mathf.MoveTowards(flash, 0f, deltaTime * 8f);
            ApplyFlash();
        }

        group.alpha = alpha;
        rect.localScale = new Vector3(scale, scale, 1f);
    }

    /// <summary>Si el jugador lo ignora, el cartel insiste cada vez más.</summary>
    private void UpdateEscalation()
    {
        float k = Mathf.Clamp01((aliveTime - escalateAfter) / escalateDuration);
        escalation = Mathf.Lerp(1f, maxEscalation, k * k * (3f - 2f * k));
    }

    private void UpdateBurst(float deltaTime, ref float scale)
    {
        if (burstInterval <= 0f || phase != Phase.Visible)
            return;

        // Con más insistencia, los saltos vienen más seguido.
        burstClock += deltaTime * escalation;
        if (burstClock >= burstInterval)
        {
            burstClock -= burstInterval;
            burstTime = 0f;
        }

        if (burstTime >= burstDuration)
            return;

        burstTime += deltaTime;
        float p = Mathf.Clamp01(burstTime / burstDuration);
        scale *= 1f + Mathf.Sin(p * Mathf.PI) * burstScale * escalation;
    }

    private void UpdateFlash()
    {
        if (flashFrequency <= 0f)
        {
            flash = 0f;
        }
        else
        {
            // Onda suave: el color va y viene sin cortes bruscos.
            float s = Mathf.Sin(effectTime * flashFrequency * 2f * Mathf.PI);
            flash = 0.5f + s * 0.5f;
        }

        ApplyFlash();
    }

    private void ApplyFlash()
    {
        if (background != null)
            background.color = Color.Lerp(backgroundBaseColor, backgroundBaseColor * flashColor, flash);

        if (outline != null)
        {
            Color glow = flashColor;
            glow.a = flash;
            outline.effectColor = glow;
            float d = outlineDistance * Mathf.Lerp(0.5f, 1f, flash) * escalation;
            outline.effectDistance = new Vector2(d, -d);
        }

        if (arrowImage != null)
            arrowImage.color = Color.Lerp(arrowBaseColor, arrowBaseColor * flashColor, flash);

        if (arrow != null)
        {
            // El pivot está en la base: estirarla en Y la alarga hacia el objetivo.
            float stretch = 1f + flash * arrowStretch * escalation;
            arrow.localScale = new Vector3(1f + flash * arrowStretch * 0.4f, stretch, 1f);
        }
    }

    private void ResetEffects()
    {
        flash = 0f;
        escalation = 1f;
        ApplyFlash();
        if (outline != null)
            outline.effectColor = Color.clear;
        if (arrow != null)
            arrow.localScale = Vector3.one;
    }

    private static float EaseOutBack(float t, float overshoot = 1.70158f)
    {
        float c3 = overshoot + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + overshoot * u * u;
    }

    // ── Recuadro alrededor del objetivo ─────────────────────────────────────

    /// <summary>
    /// Cuatro esquinas en L alrededor de lo que señala el cartel, en la misma capa y debajo de él.
    /// Se crean por código (Image sin sprite) para no depender de arte.
    /// </summary>
    private void EnsureFocusFrame()
    {
        if (!showFocusFrame || focusFrame != null || rect.parent == null)
            return;

        GameObject root = new GameObject("TutorialFocus", typeof(RectTransform), typeof(CanvasGroup));
        root.layer = gameObject.layer;
        focusFrame = (RectTransform)root.transform;
        focusFrame.SetParent(rect.parent, false);
        focusFrame.anchorMin = focusFrame.anchorMax = new Vector2(0.5f, 0.5f);
        focusFrame.pivot = new Vector2(0.5f, 0.5f);

        focusGroup = root.GetComponent<CanvasGroup>();
        focusGroup.blocksRaycasts = false;
        focusGroup.interactable = false;
        focusGroup.alpha = 0f;

        for (int corner = 0; corner < 4; corner++)
        {
            Vector2 anchor = new Vector2(corner & 1, corner >> 1);
            for (int bar = 0; bar < 2; bar++)
            {
                GameObject go = new GameObject(bar == 0 ? "H" : "V", typeof(RectTransform), typeof(Image));
                go.layer = root.layer;
                RectTransform r = (RectTransform)go.transform;
                r.SetParent(focusFrame, false);
                r.anchorMin = r.anchorMax = anchor;
                r.pivot = anchor;
                r.anchoredPosition = Vector2.zero;
                r.sizeDelta = bar == 0
                    ? new Vector2(focusCornerLength, focusThickness)
                    : new Vector2(focusThickness, focusCornerLength);

                Image image = go.GetComponent<Image>();
                image.raycastTarget = false;
                image.color = flashColor;
                focusBars[corner * 2 + bar] = image;
            }
        }
    }

    private void PlaceFocusFrame(Vector2 min, Vector2 max, Vector2 parentCenter, bool visible)
    {
        if (focusFrame == null)
            return;

        if (!visible)
        {
            focusGroup.alpha = 0f;
            return;
        }

        // Se abre y se cierra sobre el objetivo, más rápido y más amplio si el jugador lo ignora.
        float breath = (0.5f - 0.5f * Mathf.Cos(effectTime * pulseFrequency * 1.5f * 2f * Mathf.PI)) * focusBreath * escalation;
        float pad = focusPadding + breath;

        focusFrame.anchoredPosition = (min + max) * 0.5f - parentCenter;
        focusFrame.sizeDelta = new Vector2(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y)) + Vector2.one * (pad * 2f);
        focusGroup.alpha = group.alpha * Mathf.Lerp(0.55f, 1f, flash);

        Color color = Color.Lerp(flashColor, Color.white, flash * 0.6f);
        for (int i = 0; i < focusBars.Length; i++)
        {
            if (focusBars[i] != null)
                focusBars[i].color = color;
        }
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
        {
            PlaceFocusFrame(Vector2.zero, Vector2.zero, Vector2.zero, false);
            return;
        }

        if (!TryGetTargetScreenRect(out Vector2 screenMin, out Vector2 screenMax))
        {
            // Detrás de la cámara: este frame no se dibuja.
            group.alpha = 0f;
            PlaceFocusFrame(Vector2.zero, Vector2.zero, Vector2.zero, false);
            return;
        }

        Camera uiCamera = layer != null ? layer.UiCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenMin, uiCamera, out Vector2 min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenMax, uiCamera, out Vector2 max);
        Vector2 center = (min + max) * 0.5f;
        PlaceFocusFrame(min, max, parent.rect.center, true);

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
