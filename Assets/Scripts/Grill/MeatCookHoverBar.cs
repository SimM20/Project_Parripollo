using UnityEngine;

/// <summary>
/// Barra de cocción contextual por hover. Singleton de escena (mismo patrón que MeatHoverBubble).
/// Muestra los seis estados (Crudo..Quemado) y un indicador rojo continuo con el progreso
/// flotante de la cara activa. Solo aparece durante hover sobre una pieza en la parrilla.
/// No identifica caras ni muestra el punto solicitado por los clientes.
///
/// Setup de escena: GameObject con este componente, un SpriteRenderer de fondo con los seis
/// segmentos y un Transform hijo como indicador rojo. Tamaño/arte: TBD del doc.
/// </summary>
public class MeatCookHoverBar : MonoBehaviour
{
    public static MeatCookHoverBar Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Transform indicator;
    [SerializeField] private SpriteRenderer barBackground;

    [Tooltip("Rellenos de los seis segmentos (Crudo..Quemado), en orden. Crecen de izquierda a derecha según el progreso.")]
    [SerializeField] private SpriteRenderer[] segmentFills = new SpriteRenderer[6];

    [Header("Layout")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.6f, 0f);
    [Tooltip("Ancho total de la barra en unidades locales. El indicador recorre de -ancho/2 a +ancho/2.")]
    [SerializeField] private float barWidth = 1.5f;
    [Tooltip("Margen interno de cada segmento (mismo valor usado al construir los slots de fondo).")]
    [SerializeField] private float segmentInset = 0.015f;

    private Meat target;

    void Awake()
    {
        Instance = this;
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
        UpdateIndicator();
        UpdateSegmentFills();
    }

    public void Show(Meat meat)
    {
        if (meat == null)
            return;

        target = meat;
        gameObject.SetActive(true);
        transform.position = meat.transform.position + worldOffset;
        UpdateIndicator();
        UpdateSegmentFills();
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

    private void UpdateIndicator()
    {
        if (indicator == null || target == null)
            return;

        // Progreso 0..1 sobre la escala completa S; se detiene en el inicio de Quemado (5/6).
        float progress = target.ActiveSideProgress01;

        Vector3 local = indicator.localPosition;
        local.x = (progress - 0.5f) * barWidth;
        indicator.localPosition = local;
    }

    /// <summary>
    /// Rellena cada segmento de izquierda a derecha según el progreso de la cara activa.
    /// El segmento Quemado solo se llena cuando la cara entró en Quemado (la acumulación se detiene ahí).
    /// </summary>
    private void UpdateSegmentFills()
    {
        if (segmentFills == null || target == null)
            return;

        float progress = target.ActiveSideProgress01;
        bool burned = target.ActiveSideState == MeatStates.Quemado;
        float segWidth = barWidth / 6f;
        float fillWidth = segWidth - segmentInset;

        for (int i = 0; i < segmentFills.Length && i < 6; i++)
        {
            SpriteRenderer fill = segmentFills[i];
            if (fill == null || fill.sprite == null)
                continue;

            // Progreso 0..1 dentro de la banda i. Quemado (i=5) es binario.
            float amount = i == 5
                ? (burned ? 1f : 0f)
                : Mathf.Clamp01(progress * 6f - i);

            float spriteWidth = fill.sprite.bounds.size.x;
            if (spriteWidth <= 0f)
                continue;

            float segLeft = -barWidth * 0.5f + segWidth * i + segmentInset * 0.5f;

            Vector3 scale = fill.transform.localScale;
            scale.x = (fillWidth * amount) / spriteWidth;
            fill.transform.localScale = scale;

            // Anclar el borde izquierdo del sprite en segLeft sin asumir pivot centrado:
            // leftEdge = pos.x + bounds.min.x * scale.x
            Vector3 pos = fill.transform.localPosition;
            pos.x = segLeft - fill.sprite.bounds.min.x * scale.x;
            fill.transform.localPosition = pos;
        }
    }
}
