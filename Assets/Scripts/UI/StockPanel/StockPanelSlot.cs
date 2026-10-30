using TMPro;
using UnityEngine;

/// <summary>
/// Celda del panel de stock: un ícono + un contador numérico por variedad.
/// No se arrastra: es un interruptor del <see cref="StockPlacementMode"/> (click = su ítem en la
/// mano, otro click = soltarlo) y late mientras está seleccionada. El carbón queda en la mano y
/// cada click en la parrilla pone uno; de la carne va una sola unidad por selección.
/// </summary>
public class StockPanelSlot : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private TextMeshPro countLabel;
    [Tooltip("Lado mayor del icono en unidades locales de la celda. Los sprites de cortes tienen tamanos distintos y sin esto desbordan el slot.")]
    [SerializeField] [Min(0.01f)] private float iconLocalSize = 0.62f;

    [Header("Colors")]
    [SerializeField] private Color availableColor = Color.white;
    [SerializeField] private Color emptyColor = new Color(0.45f, 0.45f, 0.45f, 1f);
    [Tooltip("Color del contador cuando quedan pocas unidades (y al menos una).")]
    [SerializeField] private Color lowCountColor = new Color(1f, 0.74f, 0.32f, 1f);
    [Tooltip("Con esta cantidad o menos el contador toma lowCountColor. 0 = nunca.")]
    [Min(0)] [SerializeField] private int lowCountThreshold = 3;

    [Header("Feedback")]
    [Tooltip("Escala del icono con el puntero encima. Solo el icono: la celda y su collider no cambian.")]
    [SerializeField] private float iconHoverScale = 1.12f;
    [SerializeField] [Min(0.01f)] private float iconScaleSmoothTime = 0.06f;
    [Tooltip("Golpe del contador cuando cambia la cantidad (0.25 = 25 %).")]
    [SerializeField] private float countPunch = 0.25f;
    [Tooltip("Latido del icono mientras su ítem está en la mano (0.06 = 6 %), sobre iconHoverScale.")]
    [SerializeField] private float iconArmedPulse = 0.06f;
    [SerializeField] [Min(0f)] private float iconArmedPulseSpeed = 7f;

    /// <summary>Ítem actualmente bindeado en esta celda.</summary>
    public ItemDataSO Item => item;

    /// <summary>True si la celda puede seleccionarse (hay stock y está habilitada).</summary>
    public bool IsInteractable => isInteractable;

    /// <summary>Icono de la celda (destino de la vuelta de la pieza al soltarla).</summary>
    public SpriteRenderer IconRenderer => iconRenderer;

    private bool IsArmedHere => owner != null && owner.Placement != null && owner.Placement.SourceCell == this;

    private ItemDataSO item;
    private StockPanelController owner;
    private bool isInteractable;

    // Presentacion: el icono es un hijo sin collider, se puede escalar sin tocar el area de la celda.
    private Vector3 iconBaseScale = Vector3.one;
    private float iconScale = 1f;
    private float iconScaleVelocity;
    private bool pointerOver;
    private ItemDataSO lastBoundItem;
    private int lastBoundCount = -1;

    void Awake()
    {
        if (iconRenderer == null)
            iconRenderer = GetComponent<SpriteRenderer>();

        if (iconRenderer != null)
            iconBaseScale = iconRenderer.transform.localScale;

        EnsureCollider2D();
    }

    void Update() => UpdateIconFeedback();

    void OnDisable()
    {
        pointerOver = false;
        ResetIconScale();
    }

    // ── Feedback de la celda (solo visual) ──────────────────────────────────

    void OnWorldPointerEnter() => pointerOver = true;

    void OnWorldPointerExit() => pointerOver = false;

    /// <summary>
    /// El icono crece con el puntero encima (si hay algo para tomar) y vuelve al salir; con su
    /// ítem en la mano late. Tiempo sin escalar: es respuesta al puntero. El collider es de la
    /// celda y no se toca.
    /// </summary>
    private void UpdateIconFeedback()
    {
        if (iconRenderer == null) return;

        float target = IsArmedHere
            ? iconHoverScale + Mathf.Sin(Time.unscaledTime * iconArmedPulseSpeed) * iconArmedPulse
            : pointerOver && isInteractable ? iconHoverScale : 1f;
        if (Mathf.Abs(iconScale - target) < 0.0005f && Mathf.Abs(iconScaleVelocity) < 0.0005f)
            return;

        iconScale = Mathf.SmoothDamp(iconScale, target, ref iconScaleVelocity, iconScaleSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        iconRenderer.transform.localScale = iconBaseScale * iconScale;
    }

    private void ResetIconScale()
    {
        iconScale = 1f;
        iconScaleVelocity = 0f;
        if (iconRenderer != null)
            iconRenderer.transform.localScale = iconBaseScale;
    }

    // ── Binding ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Configura la celda con un ítem, su cantidad y el controlador dueño.
    /// El carbón en 0 queda grisado y no seleccionable; el resto queda habilitado.
    /// </summary>
    public void Bind(ItemDataSO boundItem, int count, StockPanelController boundOwner)
    {
        item = boundItem;
        owner = boundOwner;
        isInteractable = boundItem != null && count > 0;

        if (iconRenderer != null)
        {
            iconRenderer.sprite = ResolveIcon(boundItem);
            iconRenderer.color = isInteractable ? availableColor : emptyColor;
            FitIconToSlot();
        }

        if (countLabel != null)
        {
            countLabel.text = count.ToString();
            bool low = isInteractable && lowCountThreshold > 0 && count <= lowCountThreshold;
            countLabel.color = !isInteractable ? emptyColor : low ? lowCountColor : availableColor;

            // Golpe del contador solo si cambió la cantidad de la misma variedad (no al reordenar
            // las celdas del pool). Es un texto sin collider.
            if (boundItem != null && boundItem == lastBoundItem && lastBoundCount >= 0 && count != lastBoundCount)
                ScalePunch.Punch(countLabel.transform, countPunch, 0.2f, true);
        }

        lastBoundItem = boundItem;
        lastBoundCount = count;
    }

    /// <summary>
    /// Escala el ícono para que su lado mayor mida <see cref="iconLocalSize"/>, de modo que
    /// cortes con sprites de distinto tamaño se vean uniformes dentro de la celda.
    /// </summary>
    private void FitIconToSlot()
    {
        if (iconRenderer == null || iconRenderer.sprite == null)
            return;

        Vector2 spriteSize = iconRenderer.sprite.bounds.size;
        float largestSide = Mathf.Max(spriteSize.x, spriteSize.y);
        if (largestSide <= 0.0001f)
            return;

        float factor = iconLocalSize / largestSide;
        iconBaseScale = new Vector3(factor, factor, 1f);
        iconRenderer.transform.localScale = iconBaseScale * iconScale;
    }

    /// <summary>Coloca el ícono y el contador por encima del fondo del panel.</summary>
    public void SetSortingOrder(int order)
    {
        if (iconRenderer != null)
            iconRenderer.sortingOrder = order;

        if (countLabel != null)
            countLabel.sortingOrder = order + 1;
    }

    // ── Selección ───────────────────────────────────────────────────────────

    /// <summary>
    /// Con su ítem en la mano, este click lo suelta; si no, lo pone en la mano (carbón o una
    /// unidad de carne), y lo que hubiera de otra celda vuelve a la suya.
    /// </summary>
    void OnWorldPointerDown()
    {
        StockPlacementMode placement = owner != null ? owner.Placement : null;
        if (placement == null)
            return;

        if (IsArmedHere)
        {
            placement.Disarm(true);
            return;
        }

        if (!isInteractable || item == null)
            return;

        if (!owner.CanBeginDrag)
            return;

        if (CoolerSystem.Instance == null || CoolerSystem.Instance.GetCount(item) <= 0)
            return;

        if (!TutorialManager.CheckStockDragAllowed(item))
        {
            Debug.Log($"[StockPanelSlot] Selección de {item.itemName} bloqueada por el tutorial.");
            return;
        }

        placement.Arm(item, this);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static Sprite ResolveIcon(ItemDataSO source)
    {
        if (source is MeatCutSO cut)
            return cut.GetDefaultSprite();

        if (source is CoalSO coal)
            return coal.coalSprite;

        return null;
    }

    private void EnsureCollider2D()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
            return;

        box = gameObject.AddComponent<BoxCollider2D>();
        box.size = Vector2.one;
    }
}
