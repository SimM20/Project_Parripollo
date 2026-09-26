using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Aviso de gameplay "¡Te clavaron el cartel!": aparece al registrar el último strike y se
/// va solo pasados <see cref="StrikeSystem.LimitNoticeSeconds"/>. No pausa, no bloquea
/// controles (el canvas no tiene GraphicRaycaster y ningún gráfico es raycastTarget) y, por
/// estar en un canvas overlay, queda por delante de los clientes. Su posición en la escena
/// evita la parrilla.
///
/// Va sobre el root del aviso (con CanvasGroup). Usa WaitForSeconds: se congela con la pausa.
/// </summary>
public class StrikeLimitNotice : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;

    // Textos (spec v0.1): claves strike.notice.* de las tablas de Loc.

    [Header("Animación")]
    [SerializeField] private float fadeInSeconds = 0.2f;
    [SerializeField] private float fadeOutSeconds = 0.4f;

    private Coroutine routine;
    private StrikeSystem subscribedTo;

    private void Awake()
    {
        if (group == null) group = GetComponent<CanvasGroup>();

        ApplyTexts();
        HideImmediate();
    }

    private void ApplyTexts()
    {
        if (titleText != null) titleText.text = Loc.Get("strike.notice.title");
        if (bodyText != null) bodyText.text = Loc.Get("strike.notice.body");
    }

    private void OnEnable()
    {
        Loc.OnTextsChanged += ApplyTexts;
        TrySubscribe();
    }

    private void Start() => TrySubscribe();

    private void OnDisable()
    {
        Loc.OnTextsChanged -= ApplyTexts;
        Unsubscribe();
        routine = null;
    }

    private void TrySubscribe()
    {
        StrikeSystem system = StrikeSystem.Instance;
        if (system == null || subscribedTo == system) return;

        Unsubscribe();

        system.OnLimitReached += Show;
        system.OnReset += HideImmediate;
        subscribedTo = system;
    }

    private void Unsubscribe()
    {
        if (subscribedTo == null) return;

        subscribedTo.OnLimitReached -= Show;
        subscribedTo.OnReset -= HideImmediate;
        subscribedTo = null;
    }

    public void Show()
    {
        if (group == null) return;

        float visibleSeconds = StrikeSystem.Instance != null ? StrikeSystem.Instance.LimitNoticeSeconds : 5f;

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ShowRoutine(visibleSeconds));
    }

    public void HideImmediate()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (group == null) return;

        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    private IEnumerator ShowRoutine(float visibleSeconds)
    {
        group.blocksRaycasts = false;
        group.interactable = false;

        yield return Fade(0f, 1f, fadeInSeconds);
        yield return new WaitForSeconds(visibleSeconds);
        yield return Fade(1f, 0f, fadeOutSeconds);

        routine = null;
    }

    private IEnumerator Fade(float from, float to, float seconds)
    {
        if (seconds <= 0f)
        {
            group.alpha = to;
            yield break;
        }

        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / seconds));
            yield return null;
        }

        group.alpha = to;
    }
}
