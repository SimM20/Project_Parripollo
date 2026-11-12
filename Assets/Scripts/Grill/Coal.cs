using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Coal : Item, IWorldPointerOverHandler
{
    public static readonly List<Coal> ActiveCoals = new List<Coal>();

    [Header("Data")]
    [SerializeField] public CoalSO coalData;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Combustion")]
    [Tooltip("Fallback: solo se usa si coalData es null. El valor real sale de CoalSO.maxBurnTime.")]
    [SerializeField] private float maxBurnTime = 60f;
    public float currentBurnTime = 0f;

    public float MaxBurnTime => (coalData != null) ? coalData.maxBurnTime : maxBurnTime;

    public CoalStates state = CoalStates.Apagado;

    [Header("Drop Settle")]
    [Tooltip("Segundos que tarda el dibujo en llegar al hueco al soltarlo. El carbon ya quedo ahi " +
             "(slot y collider): solo se lo ve llegar.")]
    [Min(0f)] [SerializeField] private float settleSeconds = 0.1f;
    [Tooltip("Saltito al apoyarse (unidades de mundo). 0 = sin saltito.")]
    [Min(0f)] [SerializeField] private float settleHop = 0.05f;
    [Tooltip("Segundos de la vuelta al origen cuando se suelta donde no entra.")]
    [Min(0f)] [SerializeField] private float rejectReturnSeconds = 0.14f;

    private readonly List<GridSlot> occupiedSlots = new List<GridSlot>();
    private readonly SettleOffset settle = new SettleOffset();
    private Vector3 visualOffset;
    private Collider2D ownCollider;
    private Vector2 baseColliderOffset;
    private bool discarded;

    /// <summary>Renderer del carbón (para los fantasmas de limpieza y del tacho).</summary>
    public SpriteRenderer VisualRenderer => spriteRenderer;

    void Awake()
    {
        ownCollider = GetComponent<Collider2D>();
        if (ownCollider != null)
            baseColliderOffset = ownCollider.offset;
    }

    void OnEnable()
    {
        if (!ActiveCoals.Contains(this))
            ActiveCoals.Add(this);
    }
    public override void OnDisable()
    {
        base.OnDisable();
        StopSettle();
        ActiveCoals.Remove(this);
    }

    protected override void Update()
    {
        base.Update();

        if (settle.Active)
            ApplyVisualOffset(settle.Tick(Time.deltaTime));
        else if (visualOffset != Vector3.zero)
            ApplyVisualOffset(Vector3.zero);
    }

    // ── Asentado al soltar (solo visual) ─────────────────────────────────────

    /// <summary>
    /// Corre el dibujo sin mover el lugar real: el collider se compensa para quedar donde está el
    /// carbón. Supone que nadie movió el transform desde la última llamada (las colocaciones cortan
    /// el asentado antes de mover).
    /// </summary>
    private void ApplyVisualOffset(Vector3 offset)
    {
        Vector3 rest = transform.position - visualOffset;
        visualOffset = offset;
        transform.position = rest + offset;

        if (ownCollider != null)
        {
            Vector2 local = offset == Vector3.zero ? Vector2.zero : (Vector2)transform.InverseTransformVector(offset);
            ownCollider.offset = baseColliderOffset - local;
        }
    }

    private void BeginSettle(Vector3 visualFrom, float seconds, float hop)
    {
        visualFrom.z = transform.position.z;
        settle.Begin(visualFrom - transform.position, seconds, hop);
        ApplyVisualOffset(settle.Evaluate());
    }

    private void StopSettle()
    {
        settle.Stop();
        if (visualOffset != Vector3.zero)
            ApplyVisualOffset(Vector3.zero);
    }

    /// <summary>Asentado de un carbón recién puesto desde el stock: el dibujo llega desde donde se soltó.</summary>
    public void PlayDropSettle(Vector3 visualFrom)
    {
        BeginSettle(visualFrom, settleSeconds, settleHop);
        GamepadHaptics.Play(HapticEvent.CoalOnGrill);
    }

    public float GetCurrentHeatOutput()
    {
        if (state != CoalStates.Encendido) return 0f;

        float lifeFactor = Mathf.Clamp01(1f - (currentBurnTime / MaxBurnTime));
        return 6.5f * lifeFactor;
    }

    public void SetInitialPosition(Vector3 pos) => startPosition = pos;

    public override void OnWorldPointerUp()
    {
        Vector3 releaseVisual = transform.position;
        if (!EndHold()) return;

        if (TrashZone.TryConsumeAtWorldPoint(transform.position, this)) return;

        Vector2Int requiredSize = GetRequiredGridSize();
        GridSlot[] allSlots = FindObjectsByType<GridSlot>(FindObjectsSortMode.None);

        if (GridSlot.TryFindContiguousPlacement(allSlots, requiredSize, transform.position, itemType, gameObject, out List<GridSlot> slotsEncontrados))
        {
            ReleaseOccupiedSlots();
            foreach (var s in slotsEncontrados)
            {
                s.PlaceItem(gameObject);
                RegisterOccupiedSlot(s);
            }

            currentSlot = slotsEncontrados[0];
            Vector3 offset = (coalData != null) ? coalData.visualOffset : Vector3.zero;
            transform.position = CalcCenter(slotsEncontrados) + offset;
            startPosition = transform.position;
            BeginSettle(releaseVisual, settleSeconds, settleHop);
            GamepadHaptics.Play(HapticEvent.CoalOnGrill);
            return;
        }

        // No entra: vuelve a donde estaba, y se lo ve volver.
        transform.position = startPosition;
        BeginSettle(releaseVisual, rejectReturnSeconds, 0f);
        GamepadHaptics.Play(HapticEvent.ActionRefused);
    }

    // Con carbón del stock en la mano, el click sobre un carbón puesto apila otro (StockPlacementMode): no se agarra.
    protected override bool CanBeginHold() => !StockPlacementMode.IsActive;

    protected override void OnPickedUp()
    {
        // Agarrado a mitad de un asentado: el origen es su lugar real, sin el corrimiento visual.
        StopSettle();
        startPosition = transform.position;
        UpdateHoverPreview();
    }

    protected override void UpdateHoverPreview()
    {
        Vector2Int requiredSize = GetRequiredGridSize();
        GridSlot[] allSlots = FindObjectsOfType<GridSlot>();

        if (GridSlot.TryFindPreviewPlacement(allSlots, requiredSize, transform.position, itemType, gameObject, out List<GridSlot> slotsEncontrados, out bool isValid))
        {
            SetHoverPreview(slotsEncontrados, isValid);
            return;
        }

        Collider2D hit = Physics2D.OverlapPoint(transform.position);
        if (hit != null && hit.TryGetComponent<GridSlot>(out GridSlot hoveredSlot))
        {
            SetHoverPreview(hoveredSlot, false);
            return;
        }
        ClearHoverPreview();
    }

    private Vector3 CalcCenter(List<GridSlot> slots)
    {
        Vector3 center = Vector3.zero;
        foreach (var s in slots) center += s.transform.position;
        return center / slots.Count;
    }

    public void Burn()
    {
        if (state == CoalStates.Apagado)
            state = CoalStates.Encendido;

        currentBurnTime += Time.deltaTime;

        if (currentBurnTime >= MaxBurnTime)
            state = CoalStates.Ceniza;

        // Encendido muestra Fase 1 → 2 → 3 según el tiempo quemado (solo visual).
        spriteRenderer.sprite = coalData.GetSpriteForBurn(state, currentBurnTime);
    }

    // ── Limpieza de cenizas ──────────────────────────────────────────────────

    /// <summary>
    /// Click derecho con el puntero encima: limpia todas las cenizas de la pila de este hueco (los
    /// carbones de una pila se superponen, así que el hover cae en cualquiera). Los encendidos quedan.
    /// R (<see cref="GameManager"/>) sigue limpiando la parrilla entera.
    /// </summary>
    public void OnWorldPointerOver()
    {
        if (!InputManager.SecondaryPressed) return;
        // Con stock en la mano el click derecho lo suelta (StockPlacementMode); con una pieza agarrada no se toca la parrilla.
        if (StockPlacementMode.IsActive || InputManager.PrimaryHeld) return;

        int cleaned = currentSlot != null
            ? DiscardAshesIn(currentSlot.stackedCoals)
            : (DiscardAsh() ? 1 : 0);

        NotifyAshesCleaned(cleaned);
    }

    /// <summary>
    /// Si es ceniza: libera el hueco, se vuela en un fantasma y se destruye. False si no es ceniza
    /// o ya se descartó este frame (R y click derecho juntos).
    /// </summary>
    public bool DiscardAsh()
    {
        if (discarded || state != CoalStates.Ceniza) return false;

        discarded = true;
        ReleaseOccupiedSlots();

        // Solo visual: la ceniza se vuela en un fantasma, el carbón ya se fue.
        SpriteGhost.Spawn(spriteRenderer, SpriteGhost.Motion.Vanish(0.22f, 0.32f));
        Destroy(gameObject);
        return true;
    }

    /// <summary>Descarta las cenizas de la lista. Recorre de atrás porque descartar saca el carbón de la pila.</summary>
    public static int DiscardAshesIn(List<Coal> coals)
    {
        int cleaned = 0;

        for (int i = coals.Count - 1; i >= 0; i--)
        {
            if (i >= coals.Count) continue;

            Coal coal = coals[i];
            if (coal != null && coal.DiscardAsh())
                cleaned++;
        }

        return cleaned;
    }

    /// <summary>Vibración y señal del tutorial, si se limpió algo.</summary>
    public static void NotifyAshesCleaned(int cleanedCount)
    {
        if (cleanedCount <= 0) return;

        Debug.Log($"[Grill] Se limpiaron {cleanedCount} montones de ceniza.");
        GamepadHaptics.Play(HapticEvent.AshesCleaned);
        TutorialSignals.Raise(TutorialSignal.AshesCleaned);
    }

    public void SetVisualVisibility(bool isVisible)
    {
        if (spriteRenderer != null) spriteRenderer.enabled = isVisible;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = isVisible;
    }

    public void RegisterOccupiedSlot(GridSlot slot)
    {
        if (slot == null) return;
        if (!occupiedSlots.Contains(slot)) occupiedSlots.Add(slot);
        if (currentSlot == null) currentSlot = slot;
    }

    public void ReleaseOccupiedSlots()
    {
        for (int i = occupiedSlots.Count - 1; i >= 0; i--)
        {
            GridSlot slot = occupiedSlots[i];
            if (slot != null) slot.RemoveCoal(this);
        }
        occupiedSlots.Clear();
        currentSlot = null;
    }

    private Vector2Int GetRequiredGridSize() => Vector2Int.one;

    void OnDestroy() => ReleaseOccupiedSlots();

    void OnValidate()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        itemType = ItemType.Coal;
    }
}

public enum CoalStates
{
    Apagado,
    Encendido,
    Ceniza
}
