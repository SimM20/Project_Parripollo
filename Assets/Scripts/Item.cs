using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Item : MonoBehaviour
{
    [SerializeField] protected ItemType itemType;
    protected Vector3 startPosition;
    protected GridSlot currentSlot;
    protected bool isHeldByMouse;
    private readonly List<GridSlot> hoveredSlots = new List<GridSlot>();

    protected void OnMouseDown()
    {
        startPosition = transform.position;
        isHeldByMouse = true;
        OnPickedUp();
        UpdateHoverPreview();
    }

    protected void OnMouseDrag()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 mousePos = cam.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        transform.position = mousePos;
        UpdateHoverPreview();
    }

    protected virtual void Update()
    {
        if (!isHeldByMouse)
            return;

        HandleHeldInput();
        UpdateHoverPreview();
    }

    public virtual void OnMouseUp()
    {
        isHeldByMouse = false;
        ClearHoverPreview();

        Collider2D hit = Physics2D.OverlapPoint(transform.position);
        if (hit != null && hit.TryGetComponent<GridSlot>(out GridSlot newSlot))
        {
            if (newSlot.CanPlaceItem(itemType, gameObject))
            {
                if (currentSlot != null && currentSlot != newSlot && currentSlot.currentItem == gameObject)
                    currentSlot.ClearSlot();

                newSlot.PlaceItem(gameObject);
                currentSlot = newSlot;
                return;
            }
        }
        transform.position = startPosition;
    }

    protected virtual void HandleHeldInput()
    {
    }

    protected virtual void OnPickedUp()
    {
    }

    protected virtual void UpdateHoverPreview()
    {
        Collider2D hit = Physics2D.OverlapPoint(transform.position);
        if (hit != null && hit.TryGetComponent<GridSlot>(out GridSlot slot))
        {
            bool canPlace = slot.CanPlaceItem(itemType, gameObject);
            SetHoverPreview(slot, canPlace);
            return;
        }

        ClearHoverPreview();
    }

    protected void SetHoverPreview(GridSlot slot, bool isValid)
    {
        if (slot == null)
        {
            ClearHoverPreview();
            return;
        }

        List<GridSlot> singleSlot = new List<GridSlot>(1) { slot };
        SetHoverPreview(singleSlot, isValid);
    }

    protected void SetHoverPreview(List<GridSlot> slots, bool isValid)
    {
        if (slots == null || slots.Count == 0)
        {
            ClearHoverPreview();
            return;
        }

        if (hoveredSlots.Count == slots.Count)
        {
            bool sameSlots = true;
            for (int i = 0; i < hoveredSlots.Count; i++)
            {
                if (hoveredSlots[i] != slots[i])
                {
                    sameSlots = false;
                    break;
                }
            }

            if (sameSlots)
            {
                for (int i = 0; i < hoveredSlots.Count; i++)
                {
                    if (hoveredSlots[i] != null)
                        hoveredSlots[i].SetHoverPreview(true, isValid);
                }
                return;
            }
        }

        ClearHoverPreview();

        for (int i = 0; i < slots.Count; i++)
        {
            GridSlot slot = slots[i];
            if (slot == null)
                continue;

            slot.SetHoverPreview(true, isValid);
            hoveredSlots.Add(slot);
        }
    }

    protected void ClearHoverPreview()
    {
        for (int i = 0; i < hoveredSlots.Count; i++)
        {
            GridSlot slot = hoveredSlots[i];
            if (slot != null)
                slot.ClearHoverPreview();
        }

        hoveredSlots.Clear();
    }

    protected virtual void OnDisable()
    {
        isHeldByMouse = false;
        ClearHoverPreview();
    }
}
