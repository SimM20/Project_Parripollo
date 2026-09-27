using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Mensaje breve de feedback para el flujo de entrega (rechazos y avisos de cobro).
/// Singleton de escena, mismo patrón que MeatHoverBubble.
/// Se muestra/oculta cambiando el texto (el GameObject queda activo bajo GrillView).
/// Detrás del texto dibuja un cartel semitransparente con bordes redondeados que se crea solo
/// (no hace falta armarlo en la escena): entra con un "pop" y sale con un fundido.
/// </summary>
public class DeliveryFeedbackText : MonoBehaviour
{
    public static DeliveryFeedbackText Instance { get; private set; }

    [SerializeField] private TMP_Text text;
    [SerializeField] private float showSeconds = 2f;

    [Header("Cartel de fondo")]
    [SerializeField] private Color backgroundColor = new Color(0.08f, 0.05f, 0.04f, 0.72f);
    [Tooltip("Margen del cartel alrededor del texto, en unidades locales del texto (X horizontal, Y vertical).")]
    [SerializeField] private Vector2 backgroundPadding = new Vector2(0.35f, 0.18f);
    [Tooltip("Radio de las esquinas redondeadas, en unidades locales del texto.")]
    [SerializeField] private float cornerRadius = 0.18f;

    [Header("Animación")]
    [Tooltip("Duración del pop de entrada.")]
    [SerializeField] private float popSeconds = 0.18f;
    [Tooltip("Escala inicial del pop (1 = sin pop).")]
    [SerializeField] private float popStartScale = 0.8f;
    [Tooltip("Duración del fundido de salida. Se descuenta de showSeconds.")]
    [SerializeField] private float fadeSeconds = 0.35f;

    // Textura de 64 px con esquinas de 16 px: el sprite se estira en 9 partes (Sliced).
    private const int TexSize = 64;
    private const int TexCorner = 16;
    private static Sprite roundedSprite;

    private SpriteRenderer background;
    private Vector3 baseScale;
    private Color baseTextColor;
    private Coroutine showRoutine;

    void Awake()
    {
        Instance = this;
        if (text == null) return;

        baseScale = text.transform.localScale;
        baseTextColor = text.color;
        text.text = string.Empty;
        CreateBackground();
        SetBackgroundVisible(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show(string message)
    {
        if (text == null || string.IsNullOrEmpty(message)) return;

        if (showRoutine != null) StopCoroutine(showRoutine);

        text.text = message;
        text.ForceMeshUpdate();
        FitBackgroundToText();
        SetAlpha(1f);
        SetBackgroundVisible(true);

        showRoutine = StartCoroutine(ShowRoutine());
    }

    public void HideNow()
    {
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }

        Clear();
    }

    void OnDisable()
    {
        // Al salir de la vista se corta la corutina: limpiar para no volver con texto viejo.
        showRoutine = null;
        Clear();
    }

    private IEnumerator ShowRoutine()
    {
        Transform t = text.transform;

        // Pop: arranca chico, se pasa un poco y se asienta (easeOutBack).
        for (float e = 0f; e < popSeconds; e += Time.deltaTime)
        {
            float k = EaseOutBack(e / popSeconds);
            t.localScale = baseScale * Mathf.LerpUnclamped(popStartScale, 1f, k);
            yield return null;
        }
        t.localScale = baseScale;

        float hold = Mathf.Max(0f, showSeconds - popSeconds - fadeSeconds);
        yield return new WaitForSeconds(hold);

        for (float e = 0f; e < fadeSeconds; e += Time.deltaTime)
        {
            SetAlpha(1f - e / fadeSeconds);
            yield return null;
        }

        showRoutine = null;
        Clear();
    }

    private void Clear()
    {
        if (text == null) return;

        text.text = string.Empty;
        text.transform.localScale = baseScale;
        SetAlpha(1f);
        SetBackgroundVisible(false);
    }

    private void SetAlpha(float a)
    {
        Color c = baseTextColor;
        c.a *= a;
        text.color = c;

        if (background != null)
        {
            Color b = backgroundColor;
            b.a *= a;
            background.color = b;
        }
    }

    private void SetBackgroundVisible(bool visible)
    {
        if (background != null) background.enabled = visible;
    }

    private void CreateBackground()
    {
        var go = new GameObject("Background");
        go.transform.SetParent(text.transform, false);

        background = go.AddComponent<SpriteRenderer>();
        background.sprite = GetRoundedSprite();
        background.drawMode = SpriteDrawMode.Sliced;
        background.color = backgroundColor;

        // Justo detrás del texto, en su misma capa de orden.
        Renderer textRenderer = text.GetComponent<Renderer>();
        if (textRenderer != null)
        {
            background.sortingLayerID = textRenderer.sortingLayerID;
            background.sortingOrder = textRenderer.sortingOrder - 1;
        }
    }

    /// <summary>Ajusta el cartel al texto actual (textBounds está en el espacio local del texto).</summary>
    private void FitBackgroundToText()
    {
        if (background == null) return;

        Bounds b = text.textBounds;
        background.transform.localPosition = new Vector3(b.center.x, b.center.y, 0f);

        // El sprite mide TexSize px con esquinas de TexCorner px; se elige la escala para que
        // la esquina mida cornerRadius, y el tamaño se compensa para quedar en unidades del texto.
        float cornerScale = cornerRadius / (TexCorner / 100f);
        background.transform.localScale = new Vector3(cornerScale, cornerScale, 1f);

        Vector2 size = new Vector2(b.size.x + backgroundPadding.x * 2f, b.size.y + backgroundPadding.y * 2f);
        float minSide = cornerRadius * 2f;
        size.x = Mathf.Max(size.x, minSide);
        size.y = Mathf.Max(size.y, minSide);
        background.size = size / cornerScale;
    }

    private static Sprite GetRoundedSprite()
    {
        if (roundedSprite != null) return roundedSprite;

        var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false)
        {
            name = "DeliveryFeedbackRounded",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        var pixels = new Color32[TexSize * TexSize];
        float r = TexCorner;
        for (int y = 0; y < TexSize; y++)
        {
            for (int x = 0; x < TexSize; x++)
            {
                // Distancia al centro de la esquina más cercana (0 dentro del área recta).
                float cx = Mathf.Clamp(x + 0.5f, r, TexSize - r);
                float cy = Mathf.Clamp(y + 0.5f, r, TexSize - r);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                byte a = (byte)(Mathf.Clamp01(r - d + 0.5f) * 255f);
                pixels[y * TexSize + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        roundedSprite = Sprite.Create(tex, new Rect(0, 0, TexSize, TexSize), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(TexCorner, TexCorner, TexCorner, TexCorner));
        roundedSprite.name = "DeliveryFeedbackRounded";
        return roundedSprite;
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = x - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}
