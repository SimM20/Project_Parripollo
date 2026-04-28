using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Coal : Item
{
    [Header("Data")]
    [SerializeField] public CoalSO coalData;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Combustion")]
    [SerializeField] private float maxBurnTime = 60f;
    public float currentBurnTime = 0f;

    public CoalStates state = CoalStates.Apagado;

    private readonly List<GridSlot> occupiedSlots = new List<GridSlot>();
    public void SetInitialPosition(Vector3 pos)
    {
        startPosition = pos;
    }

    public override void OnMouseUp()
    {
        isHeldByMouse = false;
        ClearHoverPreview();

        if (TrashZone.TryConsumeAtWorldPoint(transform.position, this))
            return;

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
            //transform.position = CalcCenter(slotsEncontrados);
            Vector3 offset = (coalData != null) ? coalData.visualOffset : Vector3.zero;
            transform.position = CalcCenter(slotsEncontrados) + offset;

            startPosition = transform.position;
            return;
        }

        transform.position = startPosition;
    }

    protected override void OnPickedUp() => UpdateHoverPreview();

    protected override void UpdateHoverPreview()
    {
        Vector2Int requiredSize = GetRequiredGridSize();
        GridSlot[] allSlots = FindObjectsOfType<GridSlot>();

        if (GridSlot.TryFindContiguousPlacement(allSlots, requiredSize, transform.position, itemType, gameObject, out List<GridSlot> slotsEncontrados))
        {
            SetHoverPreview(slotsEncontrados, true);
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
        Vector3 centro = Vector3.zero;
        foreach (var s in slots) centro += s.transform.position;
        return centro / slots.Count;
    }

    public void Burn()
    {
        if (state == CoalStates.Ceniza)
            return;

        if (state == CoalStates.Apagado)
            state = CoalStates.Encendido;

        currentBurnTime += Time.deltaTime;

        if (currentBurnTime >= maxBurnTime)
            state = CoalStates.Ceniza;
    }

    public void SetVisualVisibility(bool isVisible)
    {
        if (spriteRenderer != null)
            spriteRenderer.enabled = isVisible;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.enabled = isVisible;
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
            if (slot != null && slot.currentItem == gameObject)
                slot.ClearSlot();
        }

        occupiedSlots.Clear();
        currentSlot = null;
    }

    private Vector2Int GetRequiredGridSize()
    {
        return Vector2Int.one;
    }

    void OnDestroy() => ReleaseOccupiedSlots();

    void OnValidate()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        itemType = ItemType.Coal;
    }
}

public enum CoalStates
{
    Apagado,
    Encendido,
    Ceniza
}
