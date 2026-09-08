using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Componente visual de feedback contextual que se muestra sobre el cliente
/// al resolver una entrega, un abandono (paciencia 0) o un cambio por faltante.
/// Implementa la secuencia en 4 etapas y código de colores según el spec doc.
/// </summary>
public class CustomerFeedbackBubble : MonoBehaviour
{
    [Header("Positioning")]
    [SerializeField] private Vector3 offsetAboveCustomer = new Vector3(0f, 1.85f, -0.5f);
    [SerializeField] private int baseSortingOrder = 600;

    [Header("Visual Elements (Auto-generados si no están asignados)")]
    [SerializeField] private Transform contentRoot;
    [SerializeField] private SpriteRenderer backgroundRenderer;
    [SerializeField] private SpriteRenderer borderRenderer;
    [SerializeField] private SpriteRenderer reactionSpriteRenderer;
    [SerializeField] private TextMeshPro reactionEmojiText;
    [SerializeField] private TextMeshPro phraseText;
    [SerializeField] private Transform economicContainer;
    [SerializeField] private TextMeshPro paymentText;
    [SerializeField] private TextMeshPro tipText;

    private Coroutine sequenceCoroutine;
    private static Sprite roundedBoxSprite;
    private static Sprite borderBoxSprite;

    private void Awake()
    {
        EnsureVisualHierarchy();
        HideImmediate();
    }

    /// <summary>
    /// Construye la jerarquía visual procedimentalmente si no viene preconfigurada en un prefab.
    /// Garantiza funcionamiento out-of-the-box sin requerir setup manual en el editor.
    /// </summary>
    public void EnsureVisualHierarchy()
    {
        if (contentRoot != null) return;

        // Raíz de contenido escalable para animaciones
        var rootGo = new GameObject("ContentRoot");
        rootGo.transform.SetParent(transform, false);
        rootGo.transform.localPosition = offsetAboveCustomer;
        contentRoot = rootGo.transform;

        EnsureProceduralSprites();

        // 1. Sombra / Borde exterior de color
        var borderGo = new GameObject("Border");
        borderGo.transform.SetParent(contentRoot, false);
        borderGo.transform.localPosition = Vector3.zero;
        borderGo.transform.localScale = new Vector3(2.55f, 1.35f, 1f);
        borderRenderer = borderGo.AddComponent<SpriteRenderer>();
        borderRenderer.sprite = borderBoxSprite;
        borderRenderer.sortingOrder = baseSortingOrder;

        // 2. Fondo del panel
        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(contentRoot, false);
        bgGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        bgGo.transform.localScale = new Vector3(2.45f, 1.25f, 1f);
        backgroundRenderer = bgGo.AddComponent<SpriteRenderer>();
        backgroundRenderer.sprite = roundedBoxSprite;
        backgroundRenderer.color = new Color(0.12f, 0.12f, 0.14f, 0.96f);
        backgroundRenderer.sortingOrder = baseSortingOrder + 1;

        // 3. Fila superior: Sprite de reacción
        var spriteGo = new GameObject("ReactionSprite");
        spriteGo.transform.SetParent(contentRoot, false);
        spriteGo.transform.localPosition = new Vector3(-0.85f, 0.28f, -0.02f);
        spriteGo.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
        reactionSpriteRenderer = spriteGo.AddComponent<SpriteRenderer>();
        reactionSpriteRenderer.sortingOrder = baseSortingOrder + 3;

        // 4. Fila superior: Emoji fallback (si no hay sprite dibujado)
        var emojiGo = new GameObject("ReactionEmoji");
        emojiGo.transform.SetParent(contentRoot, false);
        emojiGo.transform.localPosition = new Vector3(-0.85f, 0.28f, -0.02f);
        reactionEmojiText = emojiGo.AddComponent<TextMeshPro>();
        reactionEmojiText.fontSize = 5.2f;
        reactionEmojiText.alignment = TextAlignmentOptions.Center;
        reactionEmojiText.sortingOrder = baseSortingOrder + 3;

        // 5. Fila superior: Frase seleccionada
        var phraseGo = new GameObject("PhraseText");
        phraseGo.transform.SetParent(contentRoot, false);
        phraseGo.transform.localPosition = new Vector3(0.18f, 0.28f, -0.02f);
        phraseText = phraseGo.AddComponent<TextMeshPro>();
        phraseText.fontSize = 3.3f;
        phraseText.fontStyle = FontStyles.Bold;
        phraseText.alignment = TextAlignmentOptions.Left;
        phraseText.rectTransform.sizeDelta = new Vector2(1.7f, 0.6f);
        phraseText.enableWordWrapping = true;
        phraseText.sortingOrder = baseSortingOrder + 3;

        // 6. Contenedor económico (Nivel inferior)
        var econGo = new GameObject("EconomicContainer");
        econGo.transform.SetParent(contentRoot, false);
        econGo.transform.localPosition = new Vector3(0f, -0.25f, -0.02f);
        economicContainer = econGo.transform;

        // 6a. Texto de pago
        var payGo = new GameObject("PaymentText");
        payGo.transform.SetParent(economicContainer, false);
        payGo.transform.localPosition = new Vector3(-0.45f, 0f, 0f);
        paymentText = payGo.AddComponent<TextMeshPro>();
        paymentText.fontSize = 2.8f;
        paymentText.alignment = TextAlignmentOptions.Center;
        paymentText.rectTransform.sizeDelta = new Vector2(1.3f, 0.4f);
        paymentText.sortingOrder = baseSortingOrder + 4;

        // 6b. Texto de propina
        var tipGo = new GameObject("TipText");
        tipGo.transform.SetParent(economicContainer, false);
        tipGo.transform.localPosition = new Vector3(0.55f, 0f, 0f);
        tipText = tipGo.AddComponent<TextMeshPro>();
        tipText.fontSize = 2.8f;
        tipText.alignment = TextAlignmentOptions.Center;
        tipText.rectTransform.sizeDelta = new Vector2(1.3f, 0.4f);
        tipText.sortingOrder = baseSortingOrder + 4;
    }

    /// <summary>
    /// Inicia la presentación completa del feedback siguiendo las 4 etapas del spec doc.
    /// </summary>
    public void Show(
        CustomerFeedbackState state,
        float payment,
        float tip,
        bool isMissingReplacement,
        CustomerFeedbackConfigSO config,
        Action onComplete)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        enabled = true;

        EnsureVisualHierarchy();

        if (contentRoot != null)
            contentRoot.gameObject.SetActive(true);

        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = null;
        }

        if (gameObject.activeInHierarchy)
        {
            sequenceCoroutine = StartCoroutine(
                FeedbackSequenceRoutine(state, payment, tip, isMissingReplacement, config, onComplete)
            );
        }
        else
        {
            onComplete?.Invoke();
        }
    }

    private IEnumerator FeedbackSequenceRoutine(
        CustomerFeedbackState state,
        float payment,
        float tip,
        bool isMissingReplacement,
        CustomerFeedbackConfigSO config,
        Action onComplete)
    {
        if (config == null)
            config = CustomerFeedbackConfigSO.Instance;

        if (contentRoot != null)
            contentRoot.gameObject.SetActive(true);

        // ── Configuración Visual por Estado y Categoría ──
        CustomerFeedbackCategory category = state.GetCategory();
        Color categoryColor = config.GetColor(category);

        if (borderRenderer != null)
            borderRenderer.color = categoryColor;

        if (backgroundRenderer != null)
        {
            // Tinte sutil oscuro en el fondo con el tono de la categoría
            Color bgTint = category == CustomerFeedbackCategory.NegativeSevere
                ? new Color(0.25f, 0.05f, 0.05f, 0.97f)
                : new Color(0.10f, 0.11f, 0.13f, 0.96f);
            backgroundRenderer.color = bgTint;
        }

        // Sprite o Emoji
        Sprite stateSprite = config.GetSprite(state);
        if (stateSprite != null)
        {
            if (reactionSpriteRenderer != null)
            {
                reactionSpriteRenderer.sprite = stateSprite;
                reactionSpriteRenderer.enabled = true;
            }
            if (reactionEmojiText != null)
                reactionEmojiText.text = string.Empty;
        }
        else
        {
            if (reactionSpriteRenderer != null)
                reactionSpriteRenderer.enabled = false;

            if (reactionEmojiText != null)
                reactionEmojiText.text = config.GetFallbackEmoji(state);
        }

        // Frase de reacción
        string phrase = config.GetRandomPhrase(state);
        if (phraseText != null)
        {
            phraseText.text = phrase;
            phraseText.color = category == CustomerFeedbackCategory.NegativeSevere
                ? new Color(1f, 0.6f, 0.6f, 1f)
                : Color.white;
        }

        // Preparar desglose económico (oculto en Etapa 1)
        if (economicContainer != null)
            economicContainer.gameObject.SetActive(false);

        FormatEconomicTexts(state, payment, tip, isMissingReplacement, categoryColor);

        // ── Etapa 1 — Reacción: Aparecen primero Sprite y Frase con rebote suave ──
        float elapsed = 0f;
        float entranceDuration = 0.15f;
        while (elapsed < entranceDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / entranceDuration);
            // Overshoot elástico (0 -> 1.15 -> 1)
            float s = Mathf.Sin(t * Mathf.PI * 0.5f) * 1.05f;
            if (contentRoot != null)
                contentRoot.localScale = new Vector3(s, s, 1f);
            yield return null;
        }

        if (contentRoot != null)
            contentRoot.localScale = Vector3.one;

        // Espera de la separación temporal hacia Etapa 2
        float delay = Mathf.Max(0.05f, config.EconomicFeedbackDelay - entranceDuration);
        yield return new WaitForSeconds(delay);

        // ── Etapa 2 — Resultado económico: Aparece inmediatamente después debajo ──
        if (economicContainer != null)
        {
            economicContainer.gameObject.SetActive(true);
            economicContainer.localScale = new Vector3(0.5f, 0.5f, 1f);
        }

        float econPopElapsed = 0f;
        float econPopDuration = 0.12f;
        while (econPopElapsed < econPopDuration)
        {
            econPopElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(econPopElapsed / econPopDuration);
            float s = Mathf.Lerp(0.5f, 1f, t);
            if (economicContainer != null)
                economicContainer.localScale = new Vector3(s, s, 1f);
            yield return null;
        }

        if (economicContainer != null)
            economicContainer.localScale = Vector3.one;

        // ── Etapa 3 — Permanencia: Permanece visible hasta completar los 4 segundos ──
        float timeSpentSoFar = entranceDuration + delay + econPopDuration;
        float remainingHoldTime = Mathf.Max(0.1f, config.FeedbackDuration - timeSpentSoFar - config.ExitAnimationDuration);
        yield return new WaitForSeconds(remainingHoldTime);

        // ── Etapa 4 — Salida: Desaparición suave y ejecución del callback ──
        float exitElapsed = 0f;
        float exitDuration = Mathf.Max(0.05f, config.ExitAnimationDuration);
        while (exitElapsed < exitDuration)
        {
            exitElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(exitElapsed / exitDuration);
            float s = Mathf.Lerp(1f, 0f, t * t); // ease in scale down
            if (contentRoot != null)
                contentRoot.localScale = new Vector3(s, s, 1f);
            yield return null;
        }

        HideImmediate();
        sequenceCoroutine = null;

        onComplete?.Invoke();
    }

    private void FormatEconomicTexts(
        CustomerFeedbackState state,
        float payment,
        float tip,
        bool isMissingReplacement,
        Color accentColor)
    {
        if (state == CustomerFeedbackState.CambioPorFaltante || isMissingReplacement)
        {
            if (paymentText != null)
                paymentText.text = "<color=#E0E0E0>Pedido:</color> <color=#FACC15>pendiente</color>";

            if (tipText != null)
                tipText.text = "<color=#E0E0E0>Propina:</color> <color=#EF4444>anulada</color>";
        }
        else if (state == CustomerFeedbackState.NoPagaSeVa)
        {
            if (paymentText != null)
                paymentText.text = "<color=#E0E0E0>Pedido:</color> <color=#EF4444><b>$0</b></color>";

            if (tipText != null)
                tipText.text = "<color=#E0E0E0>Propina:</color> <color=#EF4444><b>$0</b></color>";
        }
        else
        {
            if (paymentText != null)
                paymentText.text = $"<color=#E0E0E0>Pedido:</color> <color=#4ADE80>${(int)payment}</color>";

            if (tipText != null)
            {
                if (tip > 0)
                {
                    tipText.text = $"<color=#E0E0E0>Propina:</color> <color=#4ADE80>+${(int)tip}</color>";
                }
                else
                {
                    // "El valor: Propina: $0 debe tener una lectura visual negativa fuerte."
                    tipText.text = "<color=#E0E0E0>Propina:</color> <color=#EF4444><b>$0</b></color>";
                }
            }
        }
    }

    public void HideImmediate()
    {
        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = null;
        }

        if (contentRoot != null)
        {
            contentRoot.localScale = Vector3.zero;
            contentRoot.gameObject.SetActive(false);
        }
    }

    private static void EnsureProceduralSprites()
    {
        if (roundedBoxSprite != null && borderBoxSprite != null) return;

        int width = 96;
        int height = 54;
        int radius = 14;

        Texture2D bgTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Texture2D borderTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        bgTex.filterMode = FilterMode.Bilinear;
        borderTex.filterMode = FilterMode.Bilinear;

        int borderWidth = 3;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dist = GetDistanceToRoundedCorner(x, y, width, height, radius);
                float alpha = Mathf.Clamp01(radius - dist + 0.5f);

                bgTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));

                // Borde exterior
                float borderAlpha = alpha;
                if (dist < radius - borderWidth)
                {
                    float innerEdge = (radius - borderWidth) - dist;
                    borderAlpha = Mathf.Clamp01(1f - innerEdge);
                }

                borderTex.SetPixel(x, y, new Color(1f, 1f, 1f, borderAlpha));
            }
        }

        bgTex.Apply();
        borderTex.Apply();

        roundedBoxSprite = Sprite.Create(
            bgTex,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            100f
        );

        borderBoxSprite = Sprite.Create(
            borderTex,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            100f
        );
    }

    private static float GetDistanceToRoundedCorner(int x, int y, int w, int h, int r)
    {
        int cx = (x < r) ? r : ((x >= w - r) ? w - r - 1 : x);
        int cy = (y < r) ? r : ((y >= h - r) ? h - r - 1 : y);

        int dx = x - cx;
        int dy = y - cy;

        return Mathf.Sqrt(dx * dx + dy * dy);
    }
}
