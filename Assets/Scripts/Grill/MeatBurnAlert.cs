using UnityEngine;

/// <summary>
/// Cartel "!" que avisa que un corte se esta por quemar. Aparece sobre la pieza cuando la cara
/// apoyada entra en la zona de aviso de Pasado (el mismo momento en que la barra de coccion empieza
/// a temblar, ver <see cref="MeatCookHoverBar"/>) y solo mientras el puntero NO esta encima.
///
/// Transicion con la burbuja de estado (<see cref="MeatHoverBubble"/>), que aparece en el mismo lugar:
///  • Puntero entra: el "!" crece y se desvanece por encima de la burbuja, que hace su pop desde abajo.
///  • Puntero sale: la burbuja se cierra y el "!" vuelve achicandose desde el tamanio de la burbuja.
///  • Primera vez (entra en la zona de aviso): pop desde la colita.
/// Desaparece al toque al quemarse, al sacar la pieza de la parrilla y durante el flip (al dar vuelta
/// la pieza la otra cara arranca de su propio punto).
///
/// NO es hijo de la carne: GrillLayerToggle, GrillSystem y ViewManager prenden y apagan todos los
/// SpriteRenderer hijos de cada pieza, y GamepadNavTargets los suma al area de seleccion. Lo crea
/// <see cref="Meat"/> desde burnAlertPrefab la primera vez que la pieza se cocina, lo destruye con
/// ella, y el cartel sigue la visibilidad del sprite de la pieza (<see cref="Meat.IsVisualShown"/>).
/// </summary>
public class MeatBurnAlert : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Sprite pixel art del cartel, con el pivote en la punta de la colita. Si queda vacio se busca en el objeto o sus hijos.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Cuando aparece")]
    [Tooltip("Parte del segmento Pasado desde la que avisa (0 = inicio de Pasado, 1 = al quemarse). " +
             "Mismo valor que burnWarningStart de la barra de coccion.")]
    [Range(0f, 1f)]
    [SerializeField] private float warningStart = 0.5f;
    [Tooltip("Segundos de anticipacion: el cartel aparece este tiempo antes de llegar a warningStart, " +
             "con el calor que recibe la pieza ahora. Da margen para reaccionar antes de que la barra tiemble.")]
    [Min(0f)] [SerializeField] private float leadSeconds = 1f;

    [Header("Posicion y tamanio (unidades de mundo)")]
    [Tooltip("Espacio entre el borde de arriba de la pieza (Meat.VisualTopOffset) y la punta de la colita. " +
             "El latido solo lo sube, nunca lo acerca a la pieza.")]
    [SerializeField] private float gapAboveMeat = 0.2f;
    [Tooltip("Alto del cartel.")]
    [Min(0.01f)] [SerializeField] private float height = 0.55f;

    [Header("Aparicion")]
    [Tooltip("Segundos del pop la primera vez que aparece. En tiempo sin escalar, igual que la burbuja de la carne.")]
    [SerializeField] private float popDuration = 0.16f;

    [Header("Transicion con la burbuja de estado")]
    [Tooltip("Segundos en que el '!' crece y se desvanece al abrirse la burbuja.")]
    [Min(0.01f)] [SerializeField] private float toBubbleDuration = 0.14f;
    [Tooltip("Segundos en que el '!' vuelve achicandose al cerrarse la burbuja.")]
    [Min(0.01f)] [SerializeField] private float fromBubbleDuration = 0.18f;
    [Tooltip("Tamanio del '!' en el extremo de la transicion, relativo al normal (1.6 = casi el alto de la burbuja).")]
    [SerializeField] private float bubbleScale = 1.6f;
    [Tooltip("Cuanto sube el '!' hacia el centro de la burbuja en el extremo de la transicion (unidades de mundo).")]
    [SerializeField] private float bubbleRise = 0.3f;

    [Header("Latido")]
    [Tooltip("Cuanto sube y baja mientras esta a la vista.")]
    [SerializeField] private float bobAmplitude = 0.035f;
    [Tooltip("Cuanto crece en cada latido (0.08 = 8%).")]
    [SerializeField] private float pulseScale = 0.08f;
    [Tooltip("Latidos por segundo al empezar el aviso.")]
    [SerializeField] private float pulseSpeedMin = 1.6f;
    [Tooltip("Latidos por segundo justo antes de quemarse.")]
    [SerializeField] private float pulseSpeedMax = 4f;

    [Header("Orden de dibujo")]
    [Tooltip("Misma capa que la burbuja de la carne y por debajo de ella (4980+): la burbuja de otra pieza tapa este cartel.")]
    [SerializeField] private string sortingLayerName = "MeatBubbleOverlay";
    [SerializeField] private int sortingOrder = 4950;
    [Tooltip("Orden mientras se desvanece al abrirse la burbuja: por encima del panel y la barra (5000-5010), si no la transicion queda tapada.")]
    [SerializeField] private int toBubbleSortingOrder = 5020;

    private const int StateCount = 6;

    private enum Phase { Hidden, PopIn, FromBubble, Shown, ToBubble }

    private Meat meat;
    private Phase phase = Phase.Hidden;
    private float phaseT;
    private float pulsePhase;
    private bool wasPointerOver;

    void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (spriteRenderer != null)
            spriteRenderer.sortingLayerID = SortingLayer.NameToID(sortingLayerName);

        SetPhase(Phase.Hidden);
    }

    /// <summary>La pieza a vigilar. La llama <see cref="Meat"/> al instanciar el cartel.</summary>
    public void Bind(Meat target)
    {
        meat = target;
        name = "MeatBurnAlert (" + (target != null ? target.name : "-") + ")";
        SetPhase(Phase.Hidden);
    }

    void LateUpdate()
    {
        if (meat == null || spriteRenderer == null)
        {
            if (meat == null && phase != Phase.Hidden) SetPhase(Phase.Hidden);
            return;
        }

        bool pointerOver = meat.IsPointerOver;
        float urgency = 0f;
        bool warn = meat.isActiveAndEnabled && meat.IsVisualShown && IsAboutToBurn(out urgency);

        if (!warn)
        {
            if (phase != Phase.Hidden) SetPhase(Phase.Hidden);
        }
        else if (pointerOver)
        {
            // La burbuja de estado ocupa el lugar: el "!" se desvanece hacia ella.
            if (phase != Phase.Hidden && phase != Phase.ToBubble)
                SetPhase(Phase.ToBubble);
        }
        else if (phase == Phase.Hidden || phase == Phase.ToBubble)
        {
            // Si recien se fue el puntero, la burbuja se acaba de cerrar: el "!" vuelve desde ella.
            SetPhase(wasPointerOver || phase == Phase.ToBubble ? Phase.FromBubble : Phase.PopIn);
        }

        wasPointerOver = pointerOver;

        if (phase == Phase.Hidden) return;

        AdvancePhase();

        if (phase == Phase.Hidden) return;

        // Fase acumulada: el latido se acelera sin saltos a medida que sube la urgencia.
        pulsePhase += Time.deltaTime * Mathf.Lerp(pulseSpeedMin, pulseSpeedMax, urgency) * 2f * Mathf.PI;

        Apply();
    }

    /// <summary>
    /// La pieza se esta cocinando y a la cara apoyada le falta menos de leadSeconds para llegar a la
    /// zona de aviso de Pasado (o ya esta en ella). <paramref name="urgency"/> es 0 hasta entrar en la
    /// zona y sube a 1 justo antes de quemarse.
    /// </summary>
    private bool IsAboutToBurn(out float urgency)
    {
        urgency = 0f;

        if (!meat.IsCooking || meat.IsFlipping || meat.ActiveSideState == MeatStates.Quemado)
            return false;

        // 0..1 dentro del segmento Pasado (el anteultimo), igual que MeatCookHoverBar.
        float intoPasado = meat.ActiveSideProgress01 * StateCount - (StateCount - 2);
        if (intoPasado < warningStart)
        {
            // Aparece leadSeconds antes del umbral, con el calor que recibe ahora.
            float warningHeat = meat.HeatScale * (StateCount - 2 + warningStart) / StateCount;
            if (meat.SecondsToActiveSideHeat(warningHeat) > leadSeconds)
                return false;
        }

        urgency = Mathf.InverseLerp(warningStart, 1f, intoPasado);
        return true;
    }

    // ── Fases ────────────────────────────────────────────────────────────────

    private void SetPhase(Phase next)
    {
        phase = next;
        phaseT = 0f;

        if (spriteRenderer == null) return;

        spriteRenderer.enabled = next != Phase.Hidden;
        spriteRenderer.sortingOrder = next == Phase.ToBubble ? toBubbleSortingOrder : sortingOrder;

        if (next == Phase.PopIn)
            pulsePhase = 0f;

        if (next != Phase.Hidden && meat != null)
            Apply();
    }

    /// <summary>Avanza la transicion en curso (en tiempo sin escalar) y pasa a la fase que sigue al terminar.</summary>
    private void AdvancePhase()
    {
        float duration;
        switch (phase)
        {
            case Phase.PopIn: duration = popDuration; break;
            case Phase.FromBubble: duration = fromBubbleDuration; break;
            case Phase.ToBubble: duration = toBubbleDuration; break;
            default: return;
        }

        phaseT = duration > 0.0001f ? Mathf.MoveTowards(phaseT, 1f, Time.unscaledDeltaTime / duration) : 1f;
        if (phaseT < 1f) return;

        SetPhase(phase == Phase.ToBubble ? Phase.Hidden : Phase.Shown);
    }

    /// <summary>
    /// Apoya la punta de la colita sobre la pieza en reposo (sin el flote del arrastre) y aplica
    /// escala y transparencia de la fase, mas el latido.
    /// </summary>
    private void Apply()
    {
        float phaseScale = 1f;
        float alpha = 1f;
        float rise = 0f;
        float t = phaseT;

        switch (phase)
        {
            case Phase.PopIn:
                phaseScale = PopCurve(t);
                break;

            case Phase.FromBubble:
                // La burbuja se achica hasta ser el "!": arranca grande, transparente y en el
                // centro de la burbuja, y baja a su lugar.
                float settle = EaseOutCubic(t);
                phaseScale = Mathf.Lerp(bubbleScale, 1f, settle);
                rise = bubbleRise * (1f - settle);
                alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 1.6f));
                break;

            case Phase.ToBubble:
                // El "!" se abre en la burbuja: sube hacia su centro, crece y se desvanece por
                // encima de ella mientras la burbuja hace su pop.
                float open = EaseOutCubic(t);
                phaseScale = Mathf.Lerp(1f, bubbleScale, open);
                rise = bubbleRise * open;
                alpha = 1f - Mathf.SmoothStep(0f, 1f, t);
                break;
        }

        // El latido solo sube: la colita no baja nunca hacia la pieza.
        float bob = (0.5f + 0.5f * Mathf.Sin(pulsePhase)) * bobAmplitude;
        transform.position = meat.RestPosition + new Vector3(0f, meat.VisualTopOffset + gapAboveMeat + bob + rise, 0f);
        transform.rotation = Quaternion.identity;

        Sprite sprite = spriteRenderer.sprite;
        float spriteHeight = sprite != null ? sprite.bounds.size.y : 1f;
        float baseScale = spriteHeight > 0.0001f ? height / spriteHeight : 1f;
        float pulse = 1f + pulseScale * (0.5f + 0.5f * Mathf.Sin(pulsePhase));
        float scale = baseScale * phaseScale * pulse;
        transform.localScale = new Vector3(scale, scale, 1f);

        Color c = spriteRenderer.color;
        c.a = alpha;
        spriteRenderer.color = c;
    }

    private static float EaseOutCubic(float t)
    {
        float u = 1f - t;
        return 1f - u * u * u;
    }

    /// <summary>Crece desde la colita con un leve sobrepaso al final.</summary>
    private static float PopCurve(float t)
    {
        if (t >= 1f) return 1f;

        const float overshoot = 1.7f;
        float u = t - 1f;
        float eased = 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
        return Mathf.LerpUnclamped(0.2f, 1f, eased);
    }
}
