using UnityEngine;

/// <summary>
/// Golpe de escala de presentación sobre un transform SIN collider (íconos, textos, visuales del
/// plato, contenedores del HUD). Nunca se usa sobre algo que se agarra o recibe clicks: escalarlo
/// cambiaría su área de interacción.
///
/// Se puede reiniciar en cualquier momento sin acumular: vuelve primero a la escala de reposo.
/// Si otro script reescribe la escala mientras dura (un layout que reacomoda la fila), esa pasa
/// a ser la nueva escala de reposo. Al terminar, al apagarse o al destruirse deja el transform
/// exactamente en reposo y se apaga solo (no corre LateUpdate fuera de un golpe).
/// </summary>
[DisallowMultipleComponent]
public class ScalePunch : MonoBehaviour
{
    private enum Mode { Punch, PopIn }

    private Mode mode;
    private Vector3 restScale = Vector3.one;
    private Vector3 lastWritten;
    private float amount;
    private float duration;
    private float elapsed;
    private bool unscaledTime;
    private bool playing;

    /// <summary>Crece <paramref name="amount"/> (0.15 = 15 %) y vuelve a su tamaño.</summary>
    public static void Punch(Transform target, float amount = 0.15f, float duration = 0.2f, bool unscaledTime = false)
    {
        ScalePunch punch = Resolve(target);
        if (punch != null)
            punch.Play(Mode.Punch, amount, duration, unscaledTime);
    }

    /// <summary>Aparece desde <paramref name="fromScale"/> de su tamaño con un rebote chico.</summary>
    public static void PopIn(Transform target, float fromScale = 0.45f, float duration = 0.22f, bool unscaledTime = false)
    {
        ScalePunch punch = Resolve(target);
        if (punch != null)
            punch.Play(Mode.PopIn, fromScale, duration, unscaledTime);
    }

    private static ScalePunch Resolve(Transform target)
    {
        if (target == null || !Application.isPlaying || !target.gameObject.activeInHierarchy)
            return null;

        ScalePunch punch = target.GetComponent<ScalePunch>();
        return punch != null ? punch : target.gameObject.AddComponent<ScalePunch>();
    }

    private void Play(Mode newMode, float newAmount, float newDuration, bool useUnscaledTime)
    {
        // Un golpe nuevo corta el anterior desde el reposo: nunca se apila escala sobre escala.
        if (playing && transform.localScale == lastWritten)
            transform.localScale = restScale;

        restScale = transform.localScale;
        mode = newMode;
        amount = newAmount;
        duration = Mathf.Max(0.01f, newDuration);
        unscaledTime = useUnscaledTime;
        elapsed = 0f;
        playing = true;
        enabled = true;
        Apply(0f);
    }

    private void LateUpdate()
    {
        if (!playing)
        {
            enabled = false;
            return;
        }

        // Alguien más escribió la escala (layout, normalización): esa es la nueva base.
        if (transform.localScale != lastWritten)
            restScale = transform.localScale;

        elapsed += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float t = elapsed / duration;

        if (t >= 1f)
        {
            Finish();
            return;
        }

        Apply(t);
    }

    private void Apply(float t)
    {
        float factor = mode == Mode.Punch
            ? 1f + amount * FeelEase.Bump(t)
            : Mathf.LerpUnclamped(amount, 1f, FeelEase.OutBack(t, 1.4f));

        transform.localScale = restScale * factor;
        lastWritten = transform.localScale;
    }

    private void Finish()
    {
        if (playing && transform.localScale == lastWritten)
            transform.localScale = restScale;

        playing = false;
        enabled = false;
    }

    private void OnDisable()
    {
        if (playing)
            Finish();
    }
}
