using System.Collections.Generic;
using UnityEngine;

public class TrashZone : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private Collider2D zoneCollider;
    [SerializeField] private bool onlyWhenGrillView = true;
    [SerializeField] private ViewManager viewManager;

    private static readonly List<TrashZone> ActiveZones = new List<TrashZone>();

    void Awake() => EnsureReferences();

    void OnEnable()
    {
        if (!ActiveZones.Contains(this))
            ActiveZones.Add(this);
    }

    void OnDisable() => ActiveZones.Remove(this);

    public static bool TryConsumeAtWorldPoint(Vector3 worldPoint, Item item)
    {
        if (item == null)
            return false;

        for (int i = 0; i < ActiveZones.Count; i++)
        {
            TrashZone zone = ActiveZones[i];
            if (zone == null)
                continue;

            if (!zone.CanConsumeNow())
                continue;

            if (!zone.ContainsPoint(worldPoint))
                continue;

            zone.Consume(item);
            return true;
        }

        return false;
    }

    private bool CanConsumeNow()
    {
        if (!onlyWhenGrillView)
            return true;

        if (viewManager == null)
            return true;

        return viewManager.CurrentView == ViewType.Grill;
    }

    private bool ContainsPoint(Vector3 worldPoint)
    {
        Collider2D targetCollider = GetZoneCollider();
        if (targetCollider == null)
            return false;

        return targetCollider.OverlapPoint(new Vector2(worldPoint.x, worldPoint.y));
    }

    private void Consume(Item item)
    {
        if (item == null)
            return;

        string itemName = "Item desconocido";

        if (item is Meat meat)
        {
            itemName = meat.cut != null ? meat.cut.cutName : "Carne sin corte";
            meat.ReleaseOccupiedSlots();
        }
        else if (item is Coal coal)
        {
            itemName = "Carbón";
            coal.ReleaseOccupiedSlots();
        }

        Destroy(item.gameObject);

        Debug.Log("Objeto tirado a la basura: " + itemName);
    }

    private Collider2D GetZoneCollider()
    {
        EnsureReferences();
        return zoneCollider;
    }

    private void EnsureReferences()
    {
        if (zoneCollider == null)
            zoneCollider = GetComponent<Collider2D>();

        if (zoneCollider == null)
            zoneCollider = GetComponentInChildren<Collider2D>();

        if (viewManager == null)
            viewManager = FindFirstObjectByType<ViewManager>();
    }

    void OnValidate() => EnsureReferences();
}
