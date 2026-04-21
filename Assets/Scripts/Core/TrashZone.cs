using System.Collections.Generic;
using UnityEngine;

public class TrashZone : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private Collider2D zoneCollider;
    [SerializeField] private bool onlyWhenGrillView = true;
    [SerializeField] private ViewManager viewManager;

    private static readonly List<TrashZone> ActiveZones = new List<TrashZone>();

    void Awake()
    {
        EnsureReferences();
    }

    void OnEnable()
    {
        if (!ActiveZones.Contains(this))
            ActiveZones.Add(this);
    }

    void OnDisable()
    {
        ActiveZones.Remove(this);
    }

    public static bool TryConsumeAtWorldPoint(Vector3 worldPoint, Meat meat)
    {
        if (meat == null)
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

            zone.Consume(meat);
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

    private void Consume(Meat meat)
    {
        if (meat == null)
            return;

        string cutName = meat.cut != null ? meat.cut.cutName : "Sin corte";

        meat.ReleaseOccupiedSlots();
        Destroy(meat.gameObject);

        Debug.Log("Carne tirada a la basura: " + cutName);
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

    void OnValidate()
    {
        EnsureReferences();
    }
}
