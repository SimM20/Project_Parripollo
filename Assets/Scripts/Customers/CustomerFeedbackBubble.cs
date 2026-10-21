using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Cartel de resultado que se muestra sobre el cliente al resolver una entrega, un abandono
/// (paciencia 0) o un cambio por faltante: reacción del cliente + lo que pagó y la propina.
/// Implementa la secuencia en 4 etapas del spec doc.
///
/// Usa el mismo diseño que la burbuja de pedido (<see cref="CustomerOrderBubble"/>) y la de estado
/// de la carne (<see cref="MeatHoverBubble"/>): panel crema 9-sliced con borde, ícono a la izquierda,
/// título en negrita y una línea de detalle (<see cref="WorldBubbleStyle"/>). Aparece donde estaba la
/// burbuja de pedido (que se apaga durante el feedback), así el pedido "se convierte" en el resultado.
/// El color de la categoría (verde / amarillo / rojo de <see cref="CustomerFeedbackConfigSO"/>) tiñe
/// el borde y los montos.
/// </summary>
public class CustomerFeedbackBubble : MonoBehaviour
{
    [Header("Posición (unidades de mundo)")]
    [Tooltip("Centro del cartel respecto de la burbuja de pedido del cliente. Por defecto el borde de arriba " +
             "queda a la altura del de la burbuja de pedido base (no tapa la cara) y crece hacia abajo.")]
    [SerializeField] private Vector3 cardOffset = new Vector3(0f, -0.04f, -0.13f);
    [Tooltip("Los clientes están a 2.6 entre sí: más de ~2.3 de ancho y los carteles vecinos se tocan.")]
    [SerializeField] private Vector2 cardSize = new Vector2(2.25f, 1.3f);
    [Tooltip("Orden del borde; panel, ícono y textos van encima. Por encima de los paneles deslizantes (90): " +
             "el resultado de una entrega no queda tapado.")]
    [SerializeField] private int baseSortingOrder = 600;

    [Header("Colores (los de CustomerOrderBubble / MeatHoverBubble)")]
    [SerializeField] private Color panelColor = WorldBubbleStyle.PanelColor;
    [SerializeField] private Color borderColor = WorldBubbleStyle.BorderColor;
    [SerializeField] private Color titleColor = WorldBubbleStyle.TitleColor;
    [SerializeField] private Color detailColor = WorldBubbleStyle.DetailColor;
    [Tooltip("Cuánto toma el borde el color de la categoría (CustomerFeedbackConfigSO).")]
    [Range(0f, 1f)] [SerializeField] private float borderCategoryTint = 0.75f;
    [Tooltip("Cuánto toma el panel el color de la categoría. Poco: el texto tiene que seguir leyéndose.")]
    [Range(0f, 1f)] [SerializeField] private float panelCategoryTint = 0.08f;

    [Header("Animación")]
    [Tooltip("Escala de arranque de la entrada (crece con un rebote corto).")]
    [Range(0.1f, 1f)] [SerializeField] private float entranceFromScale = 0.6f;
    [Tooltip("Escala final de la salida (se achica mientras se desvanece).")]
    [Range(0.1f, 1f)] [SerializeField] private float exitToScale = 0.75f;

    [Tooltip("Jerarquía vieja del prefab (fondo oscuro con borde de color, LiberationSans). Se oculta: el " +
             "cartel se arma por código para verse igual en todos los clientes.")]
    [FormerlySerializedAs("contentRoot")]
    [SerializeField] private Transform legacyRoot;

    private const float BorderThickness = 0.09f;
    private const float IconSize = 0.56f;
    private const float Padding = 0.12f;
    private const float TopRowHeight = 0.58f;   // ícono + frase
    // Los tiempos de la secuencia no cambian: el lugar del cliente queda ocupado exactamente
    // feedbackDuration (CustomerFeedbackConfigSO) y de eso dependen el spawn y el fin de la noche.
    private const float EntranceDuration = 0.15f;
    private const float EconomicPopDuration = 0.12f;

    private Transform cardRoot;
    private SpriteRenderer borderRenderer;
    private SpriteRenderer panelRenderer;
    private SpriteRenderer iconRenderer;
    private TextMeshPro phraseText;
    private TextMeshPro economyText;
    private bool built;

    private Color shownBorderColor;
    private Color shownPanelColor;
    private Coroutine sequenceCoroutine;

    private void Awake()
    {
        EnsureVisualHierarchy();
        HideImmediate();
    }

    /// <summary>Arma el cartel por código (una sola vez) y oculta la jerarquía vieja del prefab.</summary>
    public void EnsureVisualHierarchy()
    {
        if (built) return;

        if (legacyRoot != null)
            legacyRoot.gameObject.SetActive(false);

        var rootGo = new GameObject("FeedbackCard");
        rootGo.transform.SetParent(transform, false);
        cardRoot = rootGo.transform;

        borderRenderer = WorldBubbleStyle.CreatePanel(cardRoot, "Border", borderColor, cardSize + new Vector2(BorderThickness, BorderThickness));
        panelRenderer = WorldBubbleStyle.CreatePanel(cardRoot, "Panel", panelColor, cardSize);

        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(cardRoot, false);
        iconRenderer = iconGo.AddComponent<SpriteRenderer>();

        // La frase puede ocupar dos renglones; el detalle es uno solo (pago y propina).
        phraseText = WorldBubbleStyle.CreateText(cardRoot, "Phrase", 2.4f, FontStyles.Bold, TextAlignmentOptions.Left);
        economyText = WorldBubbleStyle.CreateText(cardRoot, "Economy", 1.9f, FontStyles.Normal, TextAlignmentOptions.Left);
        economyText.enableWordWrapping = false;

        // Todo en el mismo Z (cámara perspectiva): el orden lo resuelve sortingOrder.
        borderRenderer.sortingOrder = baseSortingOrder;
        panelRenderer.sortingOrder = baseSortingOrder + 1;
        iconRenderer.sortingOrder = baseSortingOrder + 2;
        phraseText.sortingOrder = baseSortingOrder + 3;
        economyText.sortingOrder = baseSortingOrder + 3;

        built = true;
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
        Action onComplete,
        bool burnedVariant = false)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        enabled = true;

        EnsureVisualHierarchy();

        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = null;
        }

        if (gameObject.activeInHierarchy)
        {
            sequenceCoroutine = StartCoroutine(
                FeedbackSequenceRoutine(state, payment, tip, isMissingReplacement, config, onComplete, burnedVariant)
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
        Action onComplete,
        bool burnedVariant)
    {
        if (config == null)
            config = CustomerFeedbackConfigSO.Instance;

        // ── Contenido y colores por estado / categoría ──
        CustomerFeedbackCategory category = state.GetCategory();
        bool severe = category == CustomerFeedbackCategory.NegativeSevere;
        Color categoryColor = config.GetColor(category);
        categoryColor.a = 1f;

        shownBorderColor = Color.Lerp(borderColor, categoryColor, borderCategoryTint);
        shownBorderColor.a = borderColor.a;
        shownPanelColor = Color.Lerp(panelColor, categoryColor, panelCategoryTint);
        shownPanelColor.a = panelColor.a;

        // Ícono: el dibujo propio del estado si Arte lo cargó en la config; si no, el plato del
        // pedido, el mismo ícono de la burbuja de pedido.
        Sprite icon = config.GetSprite(state);
        if (icon == null)
        {
            CustomerView view = GetComponentInParent<CustomerView>();
            icon = view != null ? view.GetDishSprite() : null;
        }

        phraseText.text = config.GetRandomPhrase(state, burnedVariant);
        phraseText.color = severe ? Color.Lerp(titleColor, WorldBubbleStyle.MoneyBad, 0.7f) : titleColor;

        economyText.text = BuildEconomyLine(state, payment, tip, isMissingReplacement);
        economyText.color = detailColor;

        Layout(icon);
        PlaceCard();

        cardRoot.gameObject.SetActive(true);
        economyText.gameObject.SetActive(false);   // Etapa 2

        // ── Etapa 1 — Reacción: el cartel crece con un rebote corto y aparece ──
        float elapsed = 0f;
        while (elapsed < EntranceDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / EntranceDuration);
            cardRoot.localScale = Vector3.one * Mathf.LerpUnclamped(entranceFromScale, 1f, FeelEase.OutBack(t, 1.6f));
            SetAlpha(FeelEase.OutCubic(t * 1.5f));
            yield return null;
        }

        cardRoot.localScale = Vector3.one;
        SetAlpha(1f);

        // Espera de la separación temporal hacia Etapa 2
        float delay = Mathf.Max(0.05f, config.EconomicFeedbackDelay - EntranceDuration);
        yield return new WaitForSeconds(delay);

        // ── Etapa 2 — Resultado económico: aparece la línea de pago y propina con un pop ──
        economyText.gameObject.SetActive(true);
        Transform economy = economyText.transform;
        Vector3 economyScale = economy.localScale;

        float econElapsed = 0f;
        while (econElapsed < EconomicPopDuration)
        {
            econElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(econElapsed / EconomicPopDuration);
            economy.localScale = economyScale * Mathf.LerpUnclamped(0.5f, 1f, FeelEase.OutBack(t, 2f));
            yield return null;
        }

        economy.localScale = economyScale;

        // ── Etapa 3 — Permanencia: visible hasta completar la duración del feedback ──
        float timeSpentSoFar = EntranceDuration + delay + EconomicPopDuration;
        float remainingHoldTime = Mathf.Max(0.1f, config.FeedbackDuration - timeSpentSoFar - config.ExitAnimationDuration);
        yield return new WaitForSeconds(remainingHoldTime);

        // ── Etapa 4 — Salida: se achica y se desvanece; después el callback ──
        float exitElapsed = 0f;
        float exitDuration = Mathf.Max(0.05f, config.ExitAnimationDuration);
        while (exitElapsed < exitDuration)
        {
            exitElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(exitElapsed / exitDuration);
            cardRoot.localScale = Vector3.one * Mathf.Lerp(1f, exitToScale, FeelEase.InQuad(t));
            SetAlpha(1f - FeelEase.InQuad(t));
            yield return null;
        }

        HideImmediate();
        sequenceCoroutine = null;

        onComplete?.Invoke();
    }

    // ── Contenido ────────────────────────────────────────────────────────────

    private static string OrderLabel => Loc.Get("feedback.order");
    private static string TipLabel => Loc.Get("feedback.tip");

    /// <summary>
    /// Dos renglones, "Pedido: $X" y "Propina: +$Y", con los montos alineados en columna. Las
    /// etiquetas van en el color de detalle; los montos en negrita con la paleta de montos de
    /// <see cref="WorldBubbleStyle"/> (legible sobre crema).
    /// </summary>
    private static string BuildEconomyLine(CustomerFeedbackState state, float payment, float tip, bool isMissingReplacement)
    {
        string orderValue;
        string tipValue;

        if (state == CustomerFeedbackState.CambioPorFaltante || isMissingReplacement)
        {
            orderValue = Value(WorldBubbleStyle.MoneyWarnHex, Loc.Get("feedback.pending"));
            tipValue = Value(WorldBubbleStyle.MoneyBadHex, Loc.Get("feedback.voided"));
        }
        else if (state.GetCategory() == CustomerFeedbackCategory.NegativeSevere)
        {
            // Se fue sin pagar (paciencia 0 o entrega cruda/quemada): $0 en rojo, no en verde.
            orderValue = Value(WorldBubbleStyle.MoneyBadHex, "$0");
            tipValue = Value(WorldBubbleStyle.MoneyBadHex, "$0");
        }
        else
        {
            orderValue = Value(WorldBubbleStyle.MoneyGoodHex, "$" + (int)payment);

            // "El valor: Propina: $0 debe tener una lectura visual negativa fuerte."
            tipValue = tip > 0
                ? Value(WorldBubbleStyle.MoneyGoodHex, "+$" + (int)tip)
                : Value(WorldBubbleStyle.MoneyBadHex, "$0");
        }

        return OrderLabel + ValueColumn + orderValue + LineBreak + TipLabel + ValueColumn + tipValue;
    }

    // Columna de los montos: todos arrancan a la misma altura aunque "Pedido:" y "Propina:" midan distinto.
    private const string ValueColumn = "<pos=48%>";
    private const string LineBreak = "\n";

    private static string Value(string hex, string text) => "<b><color=" + hex + ">" + text + "</color></b>";

    // ── Layout ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Mismo esquema que la burbuja de pedido: arriba el ícono a la izquierda y la frase en negrita a
    /// la derecha; abajo, a todo el ancho, los dos renglones de montos.
    /// </summary>
    private void Layout(Sprite icon)
    {
        float halfW = cardSize.x * 0.5f;
        float halfH = cardSize.y * 0.5f;

        borderRenderer.size = cardSize + new Vector2(BorderThickness, BorderThickness);
        panelRenderer.size = cardSize;

        float left = -halfW + Padding + 0.04f;
        float right = halfW - Padding;
        float rowCenterY = halfH - Padding - TopRowHeight * 0.5f;

        bool hasIcon = icon != null;
        iconRenderer.sprite = icon;
        iconRenderer.enabled = hasIcon;

        float iconX = left + IconSize * 0.5f;
        if (hasIcon)
            WorldBubbleStyle.FitSprite(iconRenderer.transform, icon, IconSize, new Vector3(iconX, rowCenterY, -0.01f));

        // Sin ícono la frase ocupa todo el ancho en vez de dejar el hueco.
        float phraseLeft = hasIcon ? iconX + IconSize * 0.5f + 0.1f : left;
        float phraseWidth = Mathf.Max(0.2f, right - phraseLeft);
        phraseText.rectTransform.sizeDelta = new Vector2(phraseWidth, TopRowHeight);
        phraseText.transform.localPosition = new Vector3(phraseLeft + phraseWidth * 0.5f, rowCenterY, -0.02f);

        float economyTop = halfH - Padding - TopRowHeight - 0.04f;
        float economyBottom = -halfH + Padding;
        float economyWidth = right - left;
        economyText.rectTransform.sizeDelta = new Vector2(economyWidth, Mathf.Max(0.2f, economyTop - economyBottom));
        economyText.transform.localPosition = new Vector3((left + right) * 0.5f, (economyTop + economyBottom) * 0.5f, -0.02f);
    }

    /// <summary>
    /// Deja las medidas en unidades de mundo aunque el cliente venga escalado (los prefabs vienen
    /// en x2, igual que con la burbuja de pedido) y apoya el cartel donde está la burbuja de
    /// pedido del cliente (o en su origen si no tiene), corrido <see cref="cardOffset"/>.
    /// </summary>
    private void PlaceCard()
    {
        Transform parent = transform.parent;
        if (parent != null)
        {
            Vector3 p = parent.lossyScale;
            transform.localScale = new Vector3(
                Mathf.Approximately(p.x, 0f) ? 1f : 1f / p.x,
                Mathf.Approximately(p.y, 0f) ? 1f : 1f / p.y,
                Mathf.Approximately(p.z, 0f) ? 1f : 1f / p.z);
        }

        CustomerView view = GetComponentInParent<CustomerView>();
        Vector3 anchor = view != null && view.OrderBubble != null ? view.OrderBubble.transform.position
                       : parent != null ? parent.position
                       : transform.position;

        cardRoot.position = anchor + cardOffset;
    }

    private void SetAlpha(float alpha)
    {
        Color c = shownBorderColor;
        c.a *= alpha;
        borderRenderer.color = c;

        c = shownPanelColor;
        c.a *= alpha;
        panelRenderer.color = c;

        iconRenderer.color = new Color(1f, 1f, 1f, alpha);
        phraseText.alpha = alpha;
        economyText.alpha = alpha;
    }

    public void HideImmediate()
    {
        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = null;
        }

        if (cardRoot != null)
        {
            cardRoot.localScale = Vector3.one;
            cardRoot.gameObject.SetActive(false);
        }
    }
}
