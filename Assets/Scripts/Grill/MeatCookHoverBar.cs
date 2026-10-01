using UnityEngine;

/// <summary>
/// Barra de cocción contextual por hover. Singleton de escena (mismo patrón que MeatHoverBubble).
/// Muestra los seis estados (Crudo..Quemado) y una aguja con el progreso flotante de la cara activa;
/// lo que la aguja todavía no alcanzó queda tapado por un velo oscuro. Solo aparece durante hover
/// sobre una pieza en la parrilla. No identifica caras ni muestra el punto solicitado por los clientes.
///
/// Setup: prefab Prefabs/UI/MeatCookHoverBar. Hijos directos de la raíz: la barra ("Barra Coccion v4",
/// marco + seis segmentos pintados) y la aguja ("Indicador de Progreso"). El velo se crea en runtime.
/// Las medidas de los segmentos están en píxeles del sprite de la barra, contados desde arriba a la
/// izquierda como en Aseprite; el tamaño en pantalla se ajusta con la escala de la raíz.
/// </summary>
public class MeatCookHoverBar : MonoBehaviour
{
    private const int StateCount = 6;

    public static MeatCookHoverBar Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Transform indicator;
    [SerializeField] private SpriteRenderer barBackground;

    [Header("Layout")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.6f, 0f);

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

    private Meat target;
    private SpriteRenderer pendingShade;

    void Awake()
    {
        Instance = this;
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

        transform.position = target.transform.position + worldOffset;
        UpdateBar();
    }

    public void Show(Meat meat)
    {
        if (meat == null)
            return;

        target = meat;
        gameObject.SetActive(true);
        transform.position = meat.transform.position + worldOffset;
        UpdateBar();
    }

    public void Hide()
    {
        target = null;
        gameObject.SetActive(false);
    }

    /// <summary>Oculta solo si la barra está mostrando esta pieza.</summary>
    public void HideIfTarget(Meat meat)
    {
        if (target == meat)
            Hide();
    }

    private void UpdateBar()
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

        UpdatePendingShade(needleX);
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

    /// <summary>Estira el velo desde la aguja hasta el final del último segmento.</summary>
    private void UpdatePendingShade(float needleX)
    {
        if (pendingShade == null)
            return;

        float trackEnd = firstSegmentX + StateCount * segmentWidth + (StateCount - 1) * dividerWidth;
        Vector3 topLeft = ArtPixelToParentLocal(needleX, segmentTopY);
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
