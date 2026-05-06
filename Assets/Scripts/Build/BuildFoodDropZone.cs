using System.Collections.Generic;
using UnityEngine;

public class BuildFoodDropZone : MonoBehaviour
{
    [SerializeField] private BuildStationSystem buildStationSystem;
    [SerializeField] private MeatTransferBuffer meatTransferBuffer;
    [SerializeField] private Collider2D zoneCollider;

    /// <summary>Exposes the station so external scripts (e.g. ToppingDraggable) can register items.</summary>
    public BuildStationSystem BuildStation => buildStationSystem;

    [Header("Plate Visuals")]
    [SerializeField] private float plateVisualWorldSpacing = 1.2f;
    [SerializeField] private Vector3 plateVisualDirection = Vector3.right;
    [SerializeField] private int plateVisualSortingOrder = 500;
    [SerializeField] private Vector3 plateVisualScale = new Vector3(0.12f, 0.12f, 0.12f);

    private readonly List<GameObject> plateSideTopVisuals = new List<GameObject>();
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

    void OnDestroy()
    {
        ActiveZones.Remove(this);
    }

    public static bool TryAcceptAt(Vector3 worldPoint, BuildDraggableFoodItem item)
    {
        for (int i = 0; i < ActiveZones.Count; i++)
        {
            BuildFoodDropZone zone = ActiveZones[i];
            if (zone == null || !zone.isActiveAndEnabled || zone.zoneCollider == null || zone.buildStationSystem == null)
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
                ProductVariantSO variant = zone.buildStationSystem.TryResolveVariant();
                if (variant != null && variant.variantSprite != null)
                    zone.meatTransferBuffer?.UpdatePlateMeatSprite(variant.variantSprite);
                else
                    Debug.LogWarning("[Build] Sin sprite de variante para: " + item.breadData.breadName);
                Debug.Log("[Build] Pan arrastrado: " + item.breadData.breadName);
            }
            else if (item.sideData != null)
            {
                zone.buildStationSystem.AddSide(item.sideData);
                zone.SpawnPlateVisual(item.GetComponent<SpriteRenderer>()?.sprite);
                Debug.Log("[Build] Acompañamiento arrastrado: " + item.sideData.sideName);
            }
            else if (item.toppingData != null)
            {
                zone.buildStationSystem.AddTopping(item.toppingData);
                zone.SpawnPlateVisual(item.GetComponent<SpriteRenderer>()?.sprite);
                Debug.Log("[Build] Topping arrastrado: " + item.toppingData.toppingName);
            }

            return true;
        }

        return false;
    }

    public static bool TryAcceptMeatAt(Vector3 worldPoint, MeatCutSO cut)
    {
        if (cut == null)
            return false;

        for (int i = 0; i < ActiveZones.Count; i++)
        {
            BuildFoodDropZone zone = ActiveZones[i];
            if (zone == null || !zone.isActiveAndEnabled || zone.zoneCollider == null || zone.buildStationSystem == null)
                continue;

            if (!zone.zoneCollider.OverlapPoint(new Vector2(worldPoint.x, worldPoint.y)))
                continue;

            zone.buildStationSystem.AddCut(cut);
            Debug.Log("[Build] Carne arrastrada: " + cut.cutName);
            return true;
        }

        return false;
    }

    public static void ClearActivePlateVisuals()
    {
        for (int i = 0; i < ActiveZones.Count; i++)
        {
            if (ActiveZones[i] != null)
                ActiveZones[i].ClearPlateItemVisuals();
        }
    }

    public static void SetActivePlateVisualsVisible(bool visible)
    {
        for (int i = 0; i < ActiveZones.Count; i++)
        {
            if (ActiveZones[i] == null) continue;
            List<GameObject> visuals = ActiveZones[i].plateSideTopVisuals;
            for (int j = 0; j < visuals.Count; j++)
            {
                if (visuals[j] != null)
                    visuals[j].SetActive(visible);
            }
        }
    }

    public void SpawnPlateVisual(Sprite sprite)
    {
        if (sprite == null)
            return;

        Vector3 dir = plateVisualDirection.sqrMagnitude > 0f
            ? plateVisualDirection.normalized
            : Vector3.right;

        Vector3 spawnPos = transform.position + dir * plateVisualWorldSpacing * plateSideTopVisuals.Count;
        spawnPos.z = transform.position.z;

        GameObject go = new GameObject("PlateVisual_" + plateSideTopVisuals.Count);
        go.transform.position = spawnPos;
        go.transform.localScale = plateVisualScale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = plateVisualSortingOrder;

        plateSideTopVisuals.Add(go);
    }

    private void ClearPlateItemVisuals()
    {
        for (int i = 0; i < plateSideTopVisuals.Count; i++)
        {
            if (plateSideTopVisuals[i] != null)
                Destroy(plateSideTopVisuals[i]);
        }

        plateSideTopVisuals.Clear();
    }

    private void EnsureReferences()
    {
        if (zoneCollider == null)
            zoneCollider = GetComponent<Collider2D>();

        if (buildStationSystem == null)
            buildStationSystem = FindFirstObjectByType<BuildStationSystem>();

        if (meatTransferBuffer == null)
            meatTransferBuffer = FindFirstObjectByType<MeatTransferBuffer>();
    }

    void OnValidate()
    {
        EnsureReferences();
    }
}
