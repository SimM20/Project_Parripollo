using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Estilo de la etiqueta de punto. Vive serializado en MeatCookStateFeedback (prefab de la
/// carne) para ajustarlo sin tocar codigo; CookStateLabel se crea en runtime.
/// </summary>
[System.Serializable]
public class CookStateLabelStyle
{
    public TMP_FontAsset font;                       // null → TMP_Settings.defaultFontAsset
    public float fontSize = 3f;
    [Tooltip("Puntos entregables sin apuro (Jugoso, Hecho, Bien Hecho).")]
    public Color normalColor = new Color(1f, 0.95f, 0.84f);
    [Tooltip("Pasado: ultimo punto valido, hay que sacarlo ya.")]
    public Color warningColor = new Color(1f, 0.66f, 0.2f);
    [Tooltip("Quemado: cuesta un strike.")]
    public Color burnedColor = new Color(1f, 0.3f, 0.24f);
    public Color outlineColor = new Color(0.12f, 0.06f, 0.03f, 0.85f);
    [Range(0f, 1f)] public float outlineWidth = 0.28f;
    public int sortingOrder = 5000;

    [Header("Timing")]
    public float popInSeconds = 0.12f;
    public float holdSeconds = 0.55f;
    public float fadeSeconds = 0.3f;
    [Tooltip("Cuanto sube (unidades de mundo) en toda su vida.")]
    public float rise = 0.35f;
    [Tooltip("Posicion respecto del borde superior del corte (centrado en X). Queda por debajo de la burbuja y la barra de hover.")]
    public Vector3 offset = new Vector3(0f, 0.12f, 0f);

    public Color GetColor(MeatStates state)
    {
        switch (state)
        {
            case MeatStates.Pasado:  return warningColor;
            case MeatStates.Quemado: return burnedColor;
            default:                 return normalColor;
        }
    }
}

/// <summary>
/// Nombre del punto nuevo ("Jugoso", "Pasado", ...) que nace sobre el corte al cambiar de
/// punto, hace pop, sube un poco y se desvanece. Mismo patron que MoneyPopup: se crea con
/// Spawn, en la raiz (no hijo de la carne, que rota con R), cero setup de escena.
/// </summary>
public class CookStateLabel : MonoBehaviour
{
    private TextMeshPro text;
    private CookStateLabelStyle style;
    private Color baseColor;

    /// <param name="anchorWorldPosition">Borde superior del corte, centrado: cortes altos (tira) no quedan tapados.</param>
    public static CookStateLabel Spawn(Vector3 anchorWorldPosition, MeatStates state, CookStateLabelStyle style)
    {
        if (style == null) style = new CookStateLabelStyle();

        // Activo y en la raiz: TextMeshPro necesita su Awake antes de asignar la fuente
        // (mismo gotcha que CoalStackCounter / MoneyPopup). Misma z que la carne: con la
        // camara en perspectiva separar en z desalinea (nota 22); el orden va por sortingOrder.
        var go = new GameObject("CookStateLabel");
        go.transform.position = anchorWorldPosition + style.offset;

        var tmp = go.AddComponent<TextMeshPro>();
        var label = go.AddComponent<CookStateLabel>();
        label.text = tmp;
        label.style = style;
        label.baseColor = style.GetColor(state);

        TMP_FontAsset font = style.font != null ? style.font : TMP_Settings.defaultFontAsset;
        if (font != null) tmp.font = font;

        tmp.text = MeatHoverText.GetStateDisplayName(state);
        tmp.fontSize = style.fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = label.baseColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.outlineColor = style.outlineColor;
        tmp.outlineWidth = style.outlineWidth;
        tmp.enableWordWrapping = false;
        tmp.rectTransform.sizeDelta = new Vector2(3f, 0.8f);

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sortingOrder = style.sortingOrder;

        go.transform.localScale = Vector3.zero;
        label.StartCoroutine(label.Animate());
        return label;
    }

    private IEnumerator Animate()
    {
        Vector3 start = transform.position;
        float total = style.popInSeconds + style.holdSeconds + style.fadeSeconds;
        float t = 0f;

        while (t < total)
        {
            t += Time.deltaTime;

            // Pop-in 0 → 1.15 → 1, igual que MoneyPopup pero mas chico.
            float pop = Mathf.Clamp01(t / Mathf.Max(0.01f, style.popInSeconds));
            float s = pop < 0.7f ? Mathf.Lerp(0f, 1.15f, pop / 0.7f) : Mathf.Lerp(1.15f, 1f, (pop - 0.7f) / 0.3f);
            transform.localScale = Vector3.one * s;

            // Sube con ease-out durante toda la vida.
            float life = Mathf.Clamp01(t / total);
            float rise = 1f - (1f - life) * (1f - life);
            transform.position = start + Vector3.up * (style.rise * rise);

            float fadeStart = style.popInSeconds + style.holdSeconds;
            float fade = t <= fadeStart ? 1f : 1f - Mathf.Clamp01((t - fadeStart) / Mathf.Max(0.01f, style.fadeSeconds));
            Color c = baseColor; c.a = baseColor.a * fade;
            text.color = c;

            yield return null;
        }

        Destroy(gameObject);
    }
}
