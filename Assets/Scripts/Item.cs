using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Item : MonoBehaviour
{
    [SerializeField] protected ItemType itemType;
    protected Vector3 startPosition;
    protected GridSlot currentSlot;

    protected void OnMouseDown() => startPosition = transform.position;

    protected void OnMouseDrag()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        transform.position = mousePos;
    }

    public virtual void OnMouseUp()
    {
        Collider2D hit = Physics2D.OverlapPoint(transform.position);
        if (hit != null && hit.TryGetComponent<GridSlot>(out GridSlot newSlot))
        {
            if (newSlot.CanPlaceItem(itemType))
            {
                if (currentSlot != null) currentSlot.ClearSlot();

                newSlot.PlaceItem(gameObject);
                currentSlot = newSlot;
                return;
            }
        }
        transform.position = startPosition;
    }
}
