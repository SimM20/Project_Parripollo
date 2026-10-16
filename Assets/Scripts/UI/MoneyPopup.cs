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
/// Estilo del popup de propina: hereda fuente, contorno y vuelo del popup de plata y suma lo
/// propio. Los niveles (normal / buena / excepcional) los decide <see cref="TipRulesSO"/>.
/// </summary>
[System.Serializable]
public class TipPopupStyle
{
    [Tooltip("Ámbar cálido: se distingue del verde del cobro sin parecer una alerta.")]
    public Color color = new Color(0.98f, 0.75f, 0.14f);          // #FBBF24
    [Tooltip("Color de la propina excepcional.")]
    public Color greatColor = new Color(1f, 0.87f, 0.35f);        // dorado más claro
    [Tooltip("Tamaño relativo al popup de plata, por nivel: normal, buena, excepcional.")]
    public Vector3 tierScale = new Vector3(0.65f, 0.75f, 0.85f);
    [Tooltip("Aparece cuando el cobro ya arrancó hacia el HUD (pop-in + hold del cobro): " +
             "primero la comida, después el premio, en el mismo lugar y sin pisarse.")]
    public float delaySeconds = 0.5f;
    [Tooltip("Desplazamiento respecto del lugar donde nació el cobro.")]
    public Vector3 offsetFromPayment = new Vector3(0f, 0.1f, 0f);
    [Tooltip("Hold extra del popup excepcional, para que se lea con calma.")]
    public float greatExtraHoldSeconds = 0.25f;
    [Tooltip("Propinas en pantalla a la vez. Las que pasan el tope se cobran igual, sin popup.")]
    [Min(1)] public int maxOnScreen = 3;
}

/// <summary>
/// "+$N" que nace sobre el cliente al cobrar, hace pop, y vuela hasta el contador de plata
/// del HUD. Al llegar avisa a HudManager (punch + conteo). Se crea con Spawn: cero setup de
/// escena. Sin HudManager el popup igual se muestra y se desvanece en el lugar.
/// La propina sale con <see cref="SpawnTip"/>: mismo recorrido, en ámbar y un poco después.
/// </summary>
public class MoneyPopup : MonoBehaviour
{
    /// <summary>Popups vivos. HudManager lo usa para saber si debe esperar al aterrizaje antes de contar.</summary>
    public static int InFlight { get; private set; }

    /// <summary>Plata que todavía no aterrizó: el HUD cuenta hasta el total menos esto.</summary>
    public static float InFlightAmount { get; private set; }

    private static int tipsOnScreen;

    private TextMeshPro text;
    private MoneyPopupStyle style;
    private float amount;
    private float delay;
    private float extraHold;
    private float scale = 1f;
    private TipTier tipTier = TipTier.None;
    private bool landed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        InFlight = 0;
        InFlightAmount = 0f;
        tipsOnScreen = 0;
    }

    public static void Spawn(Vector3 worldFrom, float amount)
    {
        if (amount <= 0f) return;

        MoneyPopupStyle style = HudManager.Instance != null ? HudManager.Instance.PopupStyle : new MoneyPopupStyle();
        MoneyPopup popup = Create(worldFrom + style.spawnOffset, "+$" + (int)amount, style.color, style);
        popup.amount = amount;
        popup.Launch();
    }

    /// <summary>
    /// Popup de propina. Se crea al instante (para que el HUD sepa que hay plata en vuelo) pero
    /// aparece <c>delaySeconds</c> después, encima del cobro. Pasado el tope de pantalla no se
    /// muestra: la plata ya está en la billetera y el HUD la cuenta igual.
    /// </summary>
    public static void SpawnTip(Vector3 worldFrom, float tip, TipTier tier)
    {
        if (tip <= 0f || tier == TipTier.None) return;

        MoneyPopupStyle style = HudManager.Instance != null ? HudManager.Instance.PopupStyle : new MoneyPopupStyle();
        TipPopupStyle tipStyle = HudManager.Instance != null ? HudManager.Instance.TipStyle : new TipPopupStyle();

        if (tipsOnScreen >= tipStyle.maxOnScreen) return;

        string key = tier == TipTier.Great ? "popup.tip.great" : tier == TipTier.Good ? "popup.tip.good" : "popup.tip";
        Color color = tier == TipTier.Great ? tipStyle.greatColor : tipStyle.color;
        Vector3 position = worldFrom + style.spawnOffset + tipStyle.offsetFromPayment;

        MoneyPopup popup = Create(position, Loc.Format(key, (int)tip), color, style);
        popup.amount = tip;
        popup.tipTier = tier;
        popup.delay = tipStyle.delaySeconds;
        popup.extraHold = tier == TipTier.Great ? tipStyle.greatExtraHoldSeconds : 0f;
        popup.scale = tier == TipTier.Great ? tipStyle.tierScale.z : tier == TipTier.Good ? tipStyle.tierScale.y : tipStyle.tierScale.x;
        tipsOnScreen++;
        popup.Launch();
    }

    private static MoneyPopup Create(Vector3 position, string label, Color color, MoneyPopupStyle style)
    {
        // Se crea en la raíz y activo: TextMeshPro necesita su Awake antes de asignar la fuente
        // (mismo gotcha que CoalStackCounter).
        var go = new GameObject("MoneyPopup");
        go.transform.position = position;

        var tmp = go.AddComponent<TextMeshPro>();
        var popup = go.AddComponent<MoneyPopup>();
        popup.text = tmp;
        popup.style = style;

        TMP_FontAsset font = style.font != null ? style.font : TMP_Settings.defaultFontAsset;
        if (font != null) tmp.font = font;

        tmp.text = label;
        tmp.fontSize = style.fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.outlineColor = style.outlineColor;
        tmp.outlineWidth = style.outlineWidth;
        tmp.rectTransform.sizeDelta = new Vector2(4f, 1.5f);

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sortingOrder = style.sortingOrder;

        return popup;
    }

    private void Launch()
    {
        InFlight++;
        InFlightAmount += amount;
        transform.localScale = Vector3.zero;
        StartCoroutine(Animate());
    }

    private void OnDestroy()
    {
        InFlight = Mathf.Max(0, InFlight - 1);
        if (!landed) InFlightAmount = Mathf.Max(0f, InFlightAmount - amount);
        if (tipTier != TipTier.None) tipsOnScreen = Mathf.Max(0, tipsOnScreen - 1);
    }

    private IEnumerator Animate()
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        Vector3 start = transform.position;

        // Pop-in: 0 → 1.2 → 1.
        float t = 0f;
        while (t < style.popInSeconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, style.popInSeconds));
            float s = k < 0.7f ? Mathf.Lerp(0f, 1.2f, k / 0.7f) : Mathf.Lerp(1.2f, 1f, (k - 0.7f) / 0.3f);
            transform.localScale = Vector3.one * (s * scale);
            yield return null;
        }
        transform.localScale = Vector3.one * scale;

        // Hold: sube despacio. La propina excepcional late un poco mientras se lee.
        t = 0f;
        float hold = style.holdSeconds + extraHold;
        while (t < hold)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, hold));
            transform.position = start + Vector3.up * (style.holdRise * k);
            if (tipTier == TipTier.Great)
                transform.localScale = Vector3.one * (scale * (1f + 0.06f * Mathf.Sin(t * 18f)));
            yield return null;
        }

        // Fly: hacia el contador del HUD, proyectado al plano z del popup (cámara en
        // perspectiva: no se puede usar la posición del HUD a secas, ver nota 22 del doc).
        Vector3 from = transform.position;
        bool hasTarget = TryGetHudTarget(from.z, out Vector3 target);
        Color baseColor = text.color;

        t = 0f;
        while (t < style.flySeconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, style.flySeconds));
            float ease = k * k;   // ease-in: arranca lento y acelera hacia el HUD

            if (hasTarget)
            {
                transform.position = Vector3.Lerp(from, target, ease);
                transform.localScale = Vector3.one * (scale * Mathf.Lerp(1f, 0.6f, ease));
            }
            else
            {
                transform.position = from + Vector3.up * (0.6f * k);
                Color c = baseColor; c.a = 1f - k;
                text.color = c;
            }

            yield return null;
        }

        landed = true;
        InFlightAmount = Mathf.Max(0f, InFlightAmount - amount);

        if (hasTarget)
            HudManager.Instance?.OnMoneyPopupArrived();

        if (tipTier != TipTier.None)
            AudioManager.Instance?.PlayTip(tipTier);

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
