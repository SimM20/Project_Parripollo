using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Reputación en el HUD de la partida: nivel y una barra que se llena según cómo salen las entregas.
/// Cada cambio anima la barra y deja debajo cuánto se ganó o perdió (+20 / -15), que se desvanece. Al
/// subir de nivel el contenedor hace un punch y avisa en dorado; al bajar, en rojo. En el tope del local
/// la barra queda llena con un candado: lo que se gane no entra hasta comprar el próximo local.
///
/// Vive en HudCanvas/MainPanel como un contenedor más (world-space), igual que <see cref="StrikeHudView"/>.
/// Escucha a <see cref="ReputationSystem"/>, que es estático: no hay nada que asignar por fuera.
/// </summary>
public class ReputationHudView : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private TextMeshProUGUI levelText;

    [Tooltip("Relleno de la barra: se estira desde la izquierda del fondo (anchorMax.x = avance).")]
    [SerializeField] private RectTransform barFill;
    [SerializeField] private Image barFillImage;

    [Tooltip("\"120/400\", o el aviso de tope del local / nivel máximo.")]
    [SerializeField] private TextMeshProUGUI progressText;

    [Tooltip("Candado al final de la barra: aparece en el tope del local.")]
    [SerializeField] private Image lockIcon;

    [Tooltip("Lo ganado o perdido en cada cambio. Aparece debajo del contenedor y se desvanece bajando.")]
    [SerializeField] private TextMeshProUGUI deltaText;

    // Textos: claves reputation.* de Gameplay.csv.

    [Header("Colores")]
    [SerializeField] private Color levelColor = Color.white;
    [SerializeField] private Color fillColor = new Color(0.949f, 0.608f, 0.165f);
    [SerializeField] private Color cappedFillColor = new Color(0.62f, 0.56f, 0.45f);
    [SerializeField] private Color gainColor = new Color(0.29f, 0.87f, 0.5f);
    [SerializeField] private Color lossColor = new Color(0.94f, 0.27f, 0.27f);
    [SerializeField] private Color levelUpColor = new Color(1f, 0.82f, 0.25f);
    [SerializeField] private Color cappedColor = new Color(0.75f, 0.75f, 0.75f);

    [Header("Animación (ajuste UX)")]
    [Tooltip("Lo que tarda la barra en llegar al valor nuevo (por tramo, si cambia de nivel).")]
    [SerializeField] private float fillSeconds = 0.35f;
    [SerializeField] private float deltaSeconds = 1.3f;
    [Tooltip("Cuánto baja el texto de lo ganado/perdido mientras se desvanece, en unidades del canvas.")]
    [SerializeField] private float deltaTravel = 0.3f;
    [SerializeField] private float levelPunchScale = 1.25f;
    [SerializeField] private float levelPunchSeconds = 0.35f;

    private float displayedFill;
    private int displayedLevel;
    private Coroutine fillRoutine;
    private Coroutine deltaRoutine;
    private Coroutine punchRoutine;
    private Vector3 baseScale = Vector3.one;
    private Vector2 deltaBasePosition;

    private void Awake()
    {
        baseScale = transform.localScale;

        if (deltaText != null)
        {
            deltaBasePosition = deltaText.rectTransform.anchoredPosition;
            deltaText.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        ReputationSystem.OnChanged += HandleChanged;
        Loc.OnTextsChanged += RefreshTexts;
        SnapToCurrent();
    }

    private void OnDisable()
    {
        ReputationSystem.OnChanged -= HandleChanged;
        Loc.OnTextsChanged -= RefreshTexts;

        // Lo que estaba animando queda en el estado final: al volver a prenderse arranca limpio.
        StopAllCoroutines();
        fillRoutine = deltaRoutine = punchRoutine = null;
        transform.localScale = baseScale;
        if (deltaText != null) deltaText.gameObject.SetActive(false);
    }

    /// <summary>Lo que muestra la barra: en el tope (o al máximo) se ve llena.</summary>
    private static float TargetFill => ReputationSystem.IsCapped ? 1f : ReputationSystem.LevelProgress01;

    private void SnapToCurrent()
    {
        displayedLevel = ReputationSystem.Level;
        displayedFill = TargetFill;
        SetFill(displayedFill);
        RefreshTexts();
    }

    private void HandleChanged(ReputationChange change)
    {
        if (!isActiveAndEnabled) return;

        // Comprar un local o reiniciar la run no se anima: se acomoda y listo.
        if (change.reason == ReputationReason.VenuePurchased || change.reason == ReputationReason.RunReset)
        {
            StopFill();
            SnapToCurrent();
            return;
        }

        ShowDelta(change);

        StopFill();
        fillRoutine = StartCoroutine(AnimateFill(change.levelAfter, TargetFill));
    }

    private void StopFill()
    {
        if (fillRoutine == null) return;
        StopCoroutine(fillRoutine);
        fillRoutine = null;
    }

    /// <summary>
    /// Lleva la barra al valor nuevo. Si cambió el nivel la recorre entera: sube hasta llena, cambia el
    /// número con un punch y arranca de cero (al bajar, al revés).
    /// </summary>
    private IEnumerator AnimateFill(int targetLevel, float targetFill)
    {
        if (targetLevel != displayedLevel)
        {
            bool up = targetLevel > displayedLevel;

            yield return Tween(displayedFill, up ? 1f : 0f);

            displayedLevel = targetLevel;
            displayedFill = up ? 0f : 1f;
            SetFill(displayedFill);
            RefreshTexts();
            Punch(up);
        }

        yield return Tween(displayedFill, targetFill);

        displayedFill = targetFill;
        SetFill(displayedFill);
        RefreshTexts();
        fillRoutine = null;
    }

    private IEnumerator Tween(float from, float to)
    {
        float duration = Mathf.Max(0.01f, fillSeconds);
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            k = 1f - (1f - k) * (1f - k);   // ease-out
            displayedFill = Mathf.Lerp(from, to, k);
            SetFill(displayedFill);
            yield return null;
        }

        displayedFill = to;
        SetFill(to);
    }

    private void SetFill(float fill)
    {
        if (barFill == null) return;

        barFill.anchorMin = Vector2.zero;
        barFill.anchorMax = new Vector2(Mathf.Clamp01(fill), 1f);
        barFill.offsetMin = Vector2.zero;
        barFill.offsetMax = Vector2.zero;
    }

    /// <summary>Nivel, número de la barra, candado y color del relleno según el nivel que se está mostrando.</summary>
    private void RefreshTexts()
    {
        if (levelText != null)
        {
            levelText.text = Loc.Format("reputation.level", displayedLevel);
            if (punchRoutine == null) levelText.color = levelColor;
        }

        // Mientras la barra todavía recorre el nivel viejo, el número espera para no adelantarse.
        bool showingCurrentLevel = displayedLevel == ReputationSystem.Level;
        if (progressText != null && showingCurrentLevel)
            progressText.text = ReputationSystem.FormatProgress();

        bool blocked = showingCurrentLevel && ReputationSystem.IsBlockedByVenue;
        if (lockIcon != null) lockIcon.enabled = blocked;
        if (barFillImage != null) barFillImage.color = blocked ? cappedFillColor : fillColor;
    }

    private void ShowDelta(ReputationChange change)
    {
        if (deltaText == null) return;

        string text;
        Color color;

        if (change.LeveledUp)
        {
            text = Loc.Format("reputation.level_up", change.levelAfter);
            color = levelUpColor;
        }
        else if (change.LeveledDown)
        {
            text = Loc.Format("reputation.level_down", change.levelAfter);
            color = lossColor;
        }
        else if (change.applied != 0)
        {
            text = ReputationSystem.FormatSigned(change.applied);
            color = change.applied > 0 ? gainColor : lossColor;
        }
        else if (change.requested > 0)
        {
            // Una buena entrega en el tope del local: no suma, y conviene que se note por qué.
            text = Loc.Get(ReputationSystem.IsMaxLevel ? "reputation.max" : "reputation.capped");
            color = cappedColor;
        }
        else
        {
            return;
        }

        if (deltaRoutine != null) StopCoroutine(deltaRoutine);
        deltaRoutine = StartCoroutine(AnimateDelta(text, color));
    }

    private IEnumerator AnimateDelta(string text, Color color)
    {
        deltaText.gameObject.SetActive(true);
        deltaText.text = text;

        RectTransform rect = deltaText.rectTransform;
        float duration = Mathf.Max(0.01f, deltaSeconds);
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);

            // Entra de golpe, se queda un momento y se desvanece bajando.
            float fade = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            deltaText.color = new Color(color.r, color.g, color.b, color.a * fade);
            rect.anchoredPosition = deltaBasePosition + Vector2.down * (deltaTravel * k);
            rect.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, Mathf.Clamp01(k * 5f));
            yield return null;
        }

        deltaText.gameObject.SetActive(false);
        rect.anchoredPosition = deltaBasePosition;
        rect.localScale = Vector3.one;
        deltaRoutine = null;
    }

    private void Punch(bool leveledUp)
    {
        if (punchRoutine != null) StopCoroutine(punchRoutine);
        punchRoutine = StartCoroutine(PunchRoutine(leveledUp));
    }

    private IEnumerator PunchRoutine(bool leveledUp)
    {
        float duration = Mathf.Max(0.01f, levelPunchSeconds);
        float t = 0f;
        Color flash = leveledUp ? levelUpColor : lossColor;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            transform.localScale = baseScale * Mathf.Lerp(levelPunchScale, 1f, k);
            if (levelText != null) levelText.color = Color.Lerp(flash, levelColor, k);
            yield return null;
        }

        transform.localScale = baseScale;
        if (levelText != null) levelText.color = levelColor;
        punchRoutine = null;
    }

    [ContextMenu("QA/Sumar 50 de reputación")]
    private void DebugAdd() => ReputationSystem.AddXp(50, ReputationReason.Debug);

    [ContextMenu("QA/Restar 30 de reputación")]
    private void DebugRemove() => ReputationSystem.AddXp(-30, ReputationReason.Debug);
}
