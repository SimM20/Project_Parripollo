using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Estilo del popup de plata. Vive serializado en HudManager (escena) para poder ajustarlo
/// sin tocar código; MoneyPopup se crea en runtime y no tiene inspector propio.
/// </summary>
[System.Serializable]
public class MoneyPopupStyle
{
    public TMP_FontAsset font;                       // null → TMP_Settings.defaultFontAsset
    public float fontSize = 5f;
    public Color color = new Color(0.29f, 0.87f, 0.5f);   // #4ADE80, mismo verde del feedback
    public Color outlineColor = new Color(0f, 0f, 0f, 0.8f);
    [Range(0f, 1f)] public float outlineWidth = 0.25f;
    public int sortingOrder = 6000;

    [Header("Timing")]
    public float popInSeconds = 0.15f;
    public float holdSeconds = 0.35f;
    public float flySeconds = 0.45f;
    [Tooltip("Cuánto sube (unidades de mundo) durante el hold.")]
    public float holdRise = 0.4f;
    public Vector3 spawnOffset = new Vector3(0f, 0.9f, -0.5f);
}

/// <summary>
/// "+$N" que nace sobre el cliente al cobrar, hace pop, y vuela hasta el contador de plata
/// del HUD. Al llegar avisa a HudManager (punch + conteo). Se crea con Spawn: cero setup de
/// escena. Sin HudManager el popup igual se muestra y se desvanece en el lugar.
/// </summary>
public class MoneyPopup : MonoBehaviour
{
    /// <summary>Popups vivos. HudManager lo usa para saber si debe esperar al aterrizaje antes de contar.</summary>
    public static int InFlight { get; private set; }

    private TextMeshPro text;
    private MoneyPopupStyle style;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState() => InFlight = 0;

    public static void Spawn(Vector3 worldFrom, float amount)
    {
        if (amount <= 0f) return;

        MoneyPopupStyle style = HudManager.Instance != null ? HudManager.Instance.PopupStyle : new MoneyPopupStyle();

        // Se crea en la raíz y activo: TextMeshPro necesita su Awake antes de asignar la fuente
        // (mismo gotcha que CoalStackCounter).
        var go = new GameObject("MoneyPopup");
        go.transform.position = worldFrom + style.spawnOffset;

        var tmp = go.AddComponent<TextMeshPro>();
        var popup = go.AddComponent<MoneyPopup>();
        popup.text = tmp;
        popup.style = style;

        TMP_FontAsset font = style.font != null ? style.font : TMP_Settings.defaultFontAsset;
        if (font != null) tmp.font = font;

        tmp.text = "+$" + (int)amount;
        tmp.fontSize = style.fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = style.color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.outlineColor = style.outlineColor;
        tmp.outlineWidth = style.outlineWidth;
        tmp.rectTransform.sizeDelta = new Vector2(4f, 1.5f);

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sortingOrder = style.sortingOrder;

        InFlight++;
        popup.StartCoroutine(popup.Animate());
    }

    private void OnDestroy()
    {
        InFlight = Mathf.Max(0, InFlight - 1);
    }

    private IEnumerator Animate()
    {
        Vector3 start = transform.position;

        // Pop-in: 0 → 1.2 → 1.
        float t = 0f;
        while (t < style.popInSeconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, style.popInSeconds));
            float s = k < 0.7f ? Mathf.Lerp(0f, 1.2f, k / 0.7f) : Mathf.Lerp(1.2f, 1f, (k - 0.7f) / 0.3f);
            transform.localScale = Vector3.one * s;
            yield return null;
        }
        transform.localScale = Vector3.one;

        // Hold: sube despacio.
        t = 0f;
        while (t < style.holdSeconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, style.holdSeconds));
            transform.position = start + Vector3.up * (style.holdRise * k);
            yield return null;
        }

        // Fly: hacia el contador del HUD, proyectado al plano z del popup (cámara en
        // perspectiva: no se puede usar la posición del HUD a secas, ver nota 22 del doc).
        Vector3 from = transform.position;
        bool hasTarget = TryGetHudTarget(from.z, out Vector3 target);

        t = 0f;
        while (t < style.flySeconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, style.flySeconds));
            float ease = k * k;   // ease-in: arranca lento y acelera hacia el HUD

            if (hasTarget)
            {
                transform.position = Vector3.Lerp(from, target, ease);
                transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, ease);
            }
            else
            {
                transform.position = from + Vector3.up * (0.6f * k);
                Color c = style.color; c.a = 1f - k;
                text.color = c;
            }

            yield return null;
        }

        if (hasTarget)
            HudManager.Instance?.OnMoneyPopupArrived();

        Destroy(gameObject);
    }

    private static bool TryGetHudTarget(float worldZ, out Vector3 target)
    {
        target = default;

        Transform anchor = HudManager.Instance != null ? HudManager.Instance.GetMoneyAnchor() : null;
        Camera cam = Camera.main;
        if (anchor == null || cam == null) return false;

        Vector3 screen = cam.WorldToScreenPoint(anchor.position);
        screen.z = Mathf.Abs(worldZ - cam.transform.position.z);
        target = cam.ScreenToWorldPoint(screen);
        target.z = worldZ;
        return true;
    }
}
