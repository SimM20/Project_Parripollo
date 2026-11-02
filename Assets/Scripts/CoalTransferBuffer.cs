using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Preview de slots de la parrilla mientras se pone carbón desde el stock (StockPlacementMode).
/// </summary>
public class CoalTransferBuffer : MonoBehaviour
{
    [Header("Systems")]
    [SerializeField] private GrillSystem grillSystem;

    private readonly List<GridSlot> coalHolderHoverSlots = new List<GridSlot>();

    public void UpdateCoalHolderHover(CoalSO coal, Vector3 worldPoint)
    {
        if (coal == null || grillSystem == null)
        {
            ClearCoalHolderHover();
            return;
        }

        Vector2Int requiredSize = Vector2Int.one;

        if (GridSlot.TryFindPreviewPlacement(grillSystem.slots, requiredSize, worldPoint, ItemType.Coal, null, out List<GridSlot> placementSlots, out bool isValid))
            SetCoalHolderHover(placementSlots, isValid);

        else
        {
            GridSlot hoveredSlot = GetSlotAtWorldPoint(worldPoint);
            if (hoveredSlot != null)
                SetCoalHolderHover(new List<GridSlot> { hoveredSlot }, false);

            else
                ClearCoalHolderHover();
        }
    }

    public void ClearCoalHolderHover()
    {
        foreach (var slot in coalHolderHoverSlots)
        {
            if (slot != null) slot.ClearHoverPreview();
        }
        coalHolderHoverSlots.Clear();
    }

    private void SetCoalHolderHover(List<GridSlot> slots, bool isValid)
    {
        ClearCoalHolderHover();
        foreach (var slot in slots)
        {
            if (slot != null)
            {
                slot.SetHoverPreview(true, isValid);
                coalHolderHoverSlots.Add(slot);
            }
        }
    }

    private GridSlot GetSlotAtWorldPoint(Vector3 worldPoint)
    {
        if (grillSystem == null) return null;
        foreach (var slot in grillSystem.slots)
        {
            Collider2D col = slot.GetComponent<Collider2D>();
            if (col != null && col.bounds.Contains(new Vector3(worldPoint.x, worldPoint.y, col.bounds.center.z)))
                return slot;
        }
        return null;
    }
}
