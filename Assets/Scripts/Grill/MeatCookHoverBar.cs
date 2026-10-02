using UnityEngine;

/// <summary>
/// Barra de cocción contextual por hover. Es parte de la burbuja de la carne (<see cref="MeatHoverBubble"/>):
/// vive como hija suya, la burbuja la prende solo con la pieza en la parrilla y la acomoda al pie del
/// panel con <see cref="SetLayoutPosition"/>.
/// Muestra los seis estados (Crudo..Quemado) y una aguja con el progreso flotante de la cara activa;
/// lo que la aguja todavía no alcanzó queda tapado por un velo oscuro. Cuando la cara está por
/// quemarse, la barra tiembla y titila en rojo (misma curva que la barra de paciencia de los clientes).
/// Ya quemada deja de temblar: el velo tapa todos los segmentos y la barra late en rojo, más lento.
/// Solo aparece durante hover sobre una pieza en la parrilla. No identifica caras ni muestra el punto
/// solicitado por los clientes.
///
/// Setup: prefab Prefabs/UI/MeatCookHoverBar, instanciado como hijo de MeatHoverBubble. Hijos directos
/// de la raíz: la barra ("Barra Coccion v4", marco + seis segmentos pintados) y la aguja ("Indicador de Progreso"). El velo se crea en runtime.
/// Las medidas de los segmentos están en píxeles del sprite de la barra, contados desde arriba a la
/// izquierda como en Aseprite; el tamaño en pantalla se ajusta con la escala de la raíz.
/// </summary>
public class MeatCookHoverBar : MonoBehaviour
{
    private const int StateCount = 6;

    [Header("References")]
    [SerializeField] private Transform indicator;
    [SerializeField] private SpriteRenderer barBackground;

    [Header("Segmentos del sprite (píxeles, desde arriba a la izquierda)")]
    [Tooltip("X del primer píxel del segmento Crudo (ancho del marco izquierdo).")]
    [SerializeField] private float firstSegmentX = 7f;
    [Tooltip("Ancho de cada segmento de color.")]
    [SerializeField] private float segmentWidth = 40f;
    [Tooltip("Ancho del separador entre segmentos. El cambio de estado cae en su centro.")]
    [SerializeField] private float dividerWidth = 5f;
    [Tooltip("Y del primer píxel de color de los segmentos.")]
    [SerializeField] private float segmentTopY = 14f;
    [Tooltip("Alto del color de los segmentos.")]
    [SerializeField] private float segmentHeight = 26f;

    [Header("Pendiente")]
    [Tooltip("Velo sobre la parte de la barra que la aguja todavía no alcanzó.")]
    [SerializeField] private Color pendingShadeColor = new Color(0f, 0f, 0f, 0.55f);
    [Tooltip("Entre la barra y la aguja.")]
    [SerializeField] private int pendingShadeSortingOrder = 5005;

    [Header("Aviso de quemado")]
    [Tooltip("Parte del segmento Pasado desde la que la barra tiembla y titila (0 = inicio de Pasado, 1 = al quemarse).")]
    [Range(0f, 1f)]
    [SerializeField] private float burnWarningStart = 0.5f;
    [Tooltip("Amplitud del temblor en unidades del mundo. 0.06 se ve igual que el temblor de la barra de paciencia.")]
    [SerializeField] private float burnShakeAmplitude = 0.06f;
    [SerializeField] private float burnShakeFrequency = 22f;
    [Tooltip("Tinte de la barra en el pico del titileo.")]
    [SerializeField] private Color burnBlinkColor = new Color(1f, 0.25f, 0.25f, 1f);
    [Tooltip("Velocidad del latido rojo una vez quemada (rad/s). El aviso va de 6 a 16.")]
    [SerializeField] private float burnedPulseSpeed = 3f;

    private Meat target;
    private SpriteRenderer pendingShade;
    private Color barBaseColor = Color.white;
    private Vector3 layoutPosition;

    void Awake()
    {
        if (barBackground != null)
            barBaseColor = barBackground.color;
        BuildPendingShade();
        gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (target == null || !target.IsOnGrill)
        {
            Hide();
            return;
        }

        Refresh();
    }

    /// <summary>
    /// Rectángulo de la barra en el espacio del padre (la burbuja), relativo a la posición de la raíz.
    /// Sale del sprite y no de los bounds del renderer porque tiene que valer con la barra apagada.
    /// </summary>
    public Bounds GetLocalBounds()
    {
        if (barBackground == null || barBackground.sprite == null)
            return new Bounds(Vector3.zero, Vector3.zero);

        Bounds b = barBackground.sprite.bounds;
        Transform bar = barBackground.transform;
        Vector3 center = Vector3.Scale(transform.localScale, bar.localPosition + Vector3.Scale(bar.localScale, b.center));
        Vector3 size = Vector3.Scale(transform.localScale, Vector3.Scale(bar.localScale, b.size));
        return new Bounds(center, size);
    }

    /// <summary>Posición de reposo en el espacio del padre. El temblor de quemado se suma encima.</summary>
    public void SetLayoutPosition(Vector3 localPosition)
    {
        layoutPosition = localPosition;
        transform.localPosition = localPosition;
    }

    public void Show(Meat meat)
    {
        if (meat == null)
            return;

        target = meat;
        gameObject.SetActive(true);
        Refresh();
    }

    public void Hide()
    {
        target = null;
        gameObject.SetActive(false);
    }

    private void Refresh()
    {
        bool burned = target.ActiveSideState == MeatStates.Quemado;
        float urgency = burned ? 0f : GetBurnUrgency();
        transform.localPosition = layoutPosition + GetShakeOffset(urgency);
        UpdateBar(burned);
        UpdateBurnBlink(urgency, burned);
    }

    /// <summary>
    /// 0 fuera de la zona de aviso; dentro sube de 0.35 a 1 a medida que la cara se acerca a Quemado
    /// (misma curva que PatienceBar). No contempla la cara ya quemada: de eso se ocupa Refresh.
    /// </summary>
    private float GetBurnUrgency()
    {
        // 0..1 dentro del segmento Pasado (el anteúltimo).
        float intoPasado = target.ActiveSideProgress01 * StateCount - (StateCount - 2);
        if (intoPasado < burnWarningStart)
            return 0f;

        return Mathf.Lerp(0.35f, 1f, Mathf.InverseLerp(burnWarningStart, 1f, intoPasado));
    }

    /// <summary>Temblor de toda la barra (aguja y velo incluidos) dentro de la burbuja; la carne no se mueve.</summary>
    private Vector3 GetShakeOffset(float urgency)
    {
        if (urgency <= 0f)
            return Vector3.zero;

        float t = Time.time * burnShakeFrequency;
        float amp = burnShakeAmplitude * (0.5f + urgency);
        return new Vector3(Mathf.Sin(t) * amp, Mathf.Cos(t * 1.7f) * amp * 0.5f, 0f);
    }

    /// <summary>
    /// Titileo en rojo: el latido se acelera a medida que sube la urgencia. Ya quemada, late lento
    /// (burnedPulseSpeed) y sin fin.
    /// </summary>
    private void UpdateBurnBlink(float urgency, bool burned)
    {
        if (barBackground == null)
            return;

        if (!burned && urgency <= 0f)
        {
            barBackground.color = barBaseColor;
            return;
        }

        float beatSpeed = burned ? burnedPulseSpeed : Mathf.Lerp(6f, 16f, urgency);
        float beat = 0.5f + 0.5f * Mathf.Sin(Time.time * beatSpeed);
        barBackground.color = Color.Lerp(barBaseColor, burnBlinkColor, beat);
    }

    private void UpdateBar(bool burned)
    {
        if (target == null || barBackground == null || barBackground.sprite == null)
            return;

        float needleX = GetIndicatorPixelX();

        if (indicator != null)
        {
            Vector3 local = indicator.localPosition;
            local.x = ArtPixelToParentLocal(needleX, 0f).x;
            indicator.localPosition = local;
        }

        // Quemada: el velo vuelve a tapar todos los segmentos. La aguja queda encima, en el negro.
        UpdatePendingShade(burned ? firstSegmentX : needleX);
    }

    /// <summary>
    /// X de la aguja en píxeles del sprite. Cada estado ocupa su segmento y el cambio de estado cae
    /// en el centro del separador. La cocción se frena al entrar en Quemado (progreso 5/6): en ese
    /// estado la aguja salta al centro del segmento negro.
    /// </summary>
    private float GetIndicatorPixelX()
    {
        float pitch = segmentWidth + dividerWidth;

        if (target.ActiveSideState == MeatStates.Quemado)
            return firstSegmentX + (StateCount - 1) * pitch + segmentWidth * 0.5f;

        float bands = Mathf.Clamp(target.ActiveSideProgress01 * StateCount, 0f, StateCount - 1);
        int band = Mathf.Min(Mathf.FloorToInt(bands), StateCount - 2);

        float start = band == 0 ? firstSegmentX : firstSegmentX + band * pitch - dividerWidth * 0.5f;
        float end = firstSegmentX + (band + 1) * pitch - dividerWidth * 0.5f;
        return Mathf.Lerp(start, end, bands - band);
    }

    /// <summary>Estira el velo desde fromX (píxeles del sprite) hasta el final del último segmento.</summary>
    private void UpdatePendingShade(float fromX)
    {
        if (pendingShade == null)
            return;

        float trackEnd = firstSegmentX + StateCount * segmentWidth + (StateCount - 1) * dividerWidth;
        Vector3 topLeft = ArtPixelToParentLocal(fromX, segmentTopY);
        Vector3 bottomRight = ArtPixelToParentLocal(trackEnd, segmentTopY + segmentHeight);

        // Sprite de 1x1 unidad con pivot centrado: posición = centro, escala = tamaño.
        Transform shade = pendingShade.transform;
        shade.localPosition = new Vector3(
            (topLeft.x + bottomRight.x) * 0.5f,
            (topLeft.y + bottomRight.y) * 0.5f,
            shade.localPosition.z);
        shade.localScale = new Vector3(
            Mathf.Max(0f, bottomRight.x - topLeft.x),
            Mathf.Abs(topLeft.y - bottomRight.y),
            1f);
    }

    /// <summary>
    /// Píxel del sprite de la barra (x desde la izquierda, y desde arriba) → espacio local de la raíz.
    /// Supone que la barra, la aguja y el velo son hijos directos de la raíz.
    /// </summary>
    private Vector3 ArtPixelToParentLocal(float x, float yFromTop)
    {
        Sprite sprite = barBackground.sprite;
        float ppu = sprite.pixelsPerUnit;
        Vector3 barLocal = new Vector3(
            (x - sprite.pivot.x) / ppu,
            (sprite.rect.height - yFromTop - sprite.pivot.y) / ppu,
            0f);

        Transform bar = barBackground.transform;
        return bar.localPosition + Vector3.Scale(bar.localScale, barLocal);
    }

    private void BuildPendingShade()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();

        var go = new GameObject("PendingShade");
        go.transform.SetParent(transform, false);
        pendingShade = go.AddComponent<SpriteRenderer>();
        pendingShade.sprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        pendingShade.color = pendingShadeColor;
        if (barBackground != null)
            pendingShade.sortingLayerID = barBackground.sortingLayerID;
        pendingShade.sortingOrder = pendingShadeSortingOrder;
    }
}
