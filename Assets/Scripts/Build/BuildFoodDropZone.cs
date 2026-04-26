using System.Collections.Generic;
using UnityEngine;

public class BuildFoodDropZone : MonoBehaviour
{
    [SerializeField] private BuildStationSystem buildStationSystem;
    [SerializeField] private Collider2D zoneCollider;

    private static readonly List<BuildFoodDropZone> ActiveZones = new List<BuildFoodDropZone>();

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

    public static bool TryAcceptAt(Vector3 worldPoint, BuildDraggableFoodItem item)
    {
        for (int i = 0; i < ActiveZones.Count; i++)
        {
            BuildFoodDropZone zone = ActiveZones[i];
            if (zone == null || zone.zoneCollider == null || zone.buildStationSystem == null)
                continue;

            if (!zone.zoneCollider.OverlapPoint(new Vector2(worldPoint.x, worldPoint.y)))
                continue;

            if (!item.HasExactlyOneData())
            {
                Debug.LogWarning("[BuildFoodDropZone] Item " + item.gameObject.name +
                    " does not have exactly one SO assigned.");
                return false;
            }

            if (item.breadData != null)
            {
                zone.buildStationSystem.SetBread(item.breadData);
                Debug.Log("[Build] Pan arrastrado: " + item.breadData.breadName);
            }
            else if (item.sideData != null)
            {
                zone.buildStationSystem.AddSide(item.sideData);
                Debug.Log("[Build] Acompañamiento arrastrado: " + item.sideData.sideName);
            }
            else if (item.toppingData != null)
            {
                zone.buildStationSystem.AddTopping(item.toppingData);
                Debug.Log("[Build] Topping arrastrado: " + item.toppingData.toppingName);
            }

            return true;
        }

        return false;
    }

    private void EnsureReferences()
    {
        if (zoneCollider == null)
            zoneCollider = GetComponent<Collider2D>();

        if (buildStationSystem == null)
            buildStationSystem = FindFirstObjectByType<BuildStationSystem>();
    }

    void OnValidate()
    {
        EnsureReferences();
    }
}
