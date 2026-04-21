using UnityEngine;

public class GridSlot : MonoBehaviour
{
    public ItemType acceptsType;
    public GameObject currentItem;

    void Update()
    {
        if (acceptsType == ItemType.Meat && currentItem != null)
        {
            if (currentItem.TryGetComponent<Meat>(out Meat meat))
            {
                meat.Cook(1f);
            }
        }
    }

    public bool CanPlaceItem(ItemType incomingType) { return currentItem == null && incomingType == acceptsType; }

    public void ClearSlot() { currentItem = null; }

    public void PlaceItem(GameObject item)
    {
        currentItem = item;
        item.transform.position = transform.position;
    }
}