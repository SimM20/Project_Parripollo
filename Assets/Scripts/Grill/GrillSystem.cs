using System.Collections.Generic;
using UnityEngine;

public class GrillSystem : MonoBehaviour
{
    public List<GridSlot> slots = new List<GridSlot>();

    public GameObject meatPrefab;
    public GameObject coalPrefab;
    [Min(0.05f)] public float slotDropRadius = 0.8f;

    [Header("Coal Stack Counter")]
    [SerializeField] private CoalStackCounterStyle coalStackCounterStyle = new CoalStackCounterStyle();

    private readonly List<CoalStackCounter> coalStackCounters = new List<CoalStackCounter>();

    void Update() => UpdateHeatPropagation();

    private void Awake() => GridSlot.AssignGridCoordinates(slots);

    // Los contadores se crean en Start: TextMeshPro no admite asignar la fuente hasta que su Awake corrio.
    private void Start() => SetupCoalStackCounters();

    private void SetupCoalStackCounters()
    {
        coalStackCounters.Clear();

        foreach (var slot in slots)
        {
            if (slot == null || slot.acceptsType != ItemType.Coal) continue;

            CoalStackCounter counter = CoalStackCounter.Attach(slot, coalStackCounterStyle);
            if (counter != null) coalStackCounters.Add(counter);
        }
    }

    private void UpdateHeatPropagation()
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Count; i++)
        {
            GridSlot slot = slots[i];
            if (slot != null)
                slot.ResetReceivedHeat();
        }

        for (int i = 0; i < slots.Count; i++)
        {
            GridSlot source = slots[i];
            if (source == null || source.internalHeat <= 0) continue;

            for (int j = 0; j < slots.Count; j++)
            {
                GridSlot target = slots[j];
                if (target == null || source == target) continue;

                int diffX = Mathf.Abs(source.gridX - target.gridX);
                int diffY = Mathf.Abs(source.gridY - target.gridY);

                if (diffX <= 1 && diffY <= 1)
                {
                    float factor = (diffX == 0 || diffY == 0) ? 0.35f : 0.20f;
                    target.AddExternalHeat(source.internalHeat * factor);
                }
                else if (diffX <= 1 && source.gridY > target.gridY && diffY <= 3)
                {
                    float verticalFactor = 0.4f / diffY;
                    target.AddExternalHeat(source.internalHeat * verticalFactor);
                }
            }
        }
    }

    public bool SpawnMeat(MeatCutSO cut, bool rotateFootprint = false)
    {
        Vector3 spawnPoint = GetDefaultSpawnPoint();
        return TrySpawnMeatAtPoint(cut, spawnPoint, rotateFootprint);
    }

    public bool TrySpawnMeatAtPoint(MeatCutSO cut, Vector3 worldPoint, bool rotateFootprint = false)
    {
        Meat unused;
        return TrySpawnMeatAtPoint(cut, worldPoint, out unused, rotateFootprint);
    }

    public bool TrySpawnCoalAtPoint(CoalSO coalData, Vector3 worldPoint, out Coal spawnedCoal)
    {
        spawnedCoal = null;

        GameObject prefabToUse = (coalData.coalPrefab != null) ? coalData.coalPrefab : coalPrefab;

        if (coalData == null || prefabToUse == null)
        {
            Debug.LogWarning("Faltan datos de carb�n o prefab para spawnear.");
            return false;
        }

        GameObject obj = Instantiate(prefabToUse);
        Coal coal = obj.GetComponent<Coal>();

        if (coal == null) { Destroy(obj); return false; }

        coal.coalData = coalData;

        Vector2Int requiredSize = Vector2Int.one;

        if (!GridSlot.TryFindContiguousPlacement(slots, requiredSize, worldPoint, ItemType.Coal, coal.gameObject, out List<GridSlot> placementSlots))
        {
            Debug.Log("No hay espacio para el carb�n en esta posici�n de la grilla.");
            Destroy(obj);
            return false;
        }

        foreach (var slot in placementSlots)
        {
            slot.PlaceItem(coal.gameObject);
            coal.RegisterOccupiedSlot(slot);
        }

        Vector3 offset = (coalData != null) ? coalData.visualOffset : Vector3.zero;
        coal.transform.position = GetCenter(placementSlots) + offset;

        spawnedCoal = coal;
        CoalConsumptionTracker.Instance?.ReportConsumption(1);
        return true;
    }

    public bool TrySpawnMeatAtPoint(MeatCutSO cut, Vector3 worldPoint, out Meat spawnedMeat, bool rotateFootprint = false)
    {
        spawnedMeat = null;

        if (cut == null || meatPrefab == null)
        {
            Debug.LogWarning("Falta cut o meatPrefab");
            return false;
        }

        GameObject obj = Instantiate(meatPrefab);
        Meat meat = obj.GetComponent<Meat>();
        if (meat == null)
        {
            Destroy(obj);
            return false;
        }

        meat.SetCut(cut);
        meat.SetGridRotation(rotateFootprint);

        Vector2Int requiredSize = cut.GrillSpace;
        if (rotateFootprint)
            requiredSize = new Vector2Int(requiredSize.y, requiredSize.x);

        if (!GridSlot.TryFindContiguousPlacement(slots, requiredSize, worldPoint, ItemType.Meat, meat.gameObject, out List<GridSlot> placementSlots))
        {
            Destroy(obj);
            return false;
        }

        for (int i = 0; i < placementSlots.Count; i++)
            placementSlots[i].PlaceMeat(meat);

        Vector3 rotatedOffset = meat.transform.rotation * cut.visualOffset;
        meat.transform.position = GetCenter(placementSlots) + rotatedOffset;

        spawnedMeat = meat;
        return true;
    }

    public Meat GetCookedMeat(MeatCutSO cut)
    {
        if (cut == null) return null;

        foreach (var slot in slots)
        {
            if (slot.IsOccupied)
            {
                var meat = slot.currentMeat;
                if (meat == null) continue;

                if (meat.cut == cut && (meat.state == MeatStates.Hecho || meat.state == MeatStates.Muy_Hecho))
                {
                    meat.ReleaseOccupiedSlots();
                    return meat;
                }
            }
        }
        return null;
    }

    public void SetMeatVisualsVisible(bool isVisible)
    {
        for (int i = 0; i < coalStackCounters.Count; i++)
        {
            if (coalStackCounters[i] != null) coalStackCounters[i].SetViewVisible(isVisible);
        }

        for (int i = 0; i < slots.Count; i++)
        {
            GridSlot slot = slots[i];
            if (slot == null) continue;

            if (slot.currentItem != null)
            {
                if (slot.currentItem.TryGetComponent<Item>(out Item genericItem))
                    SetItemVisible(genericItem, isVisible);
            }

            if (slot.stackedCoals != null)
            {
                foreach (var coal in slot.stackedCoals)
                {
                    if (coal != null)
                        SetItemVisible(coal, isVisible);
                }
            }
        }
    }

    private static void SetItemVisible(Item item, bool isVisible)
    {
        if (item == null) return;

        SpriteRenderer[] renderers = item.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = isVisible;

        Collider2D[] colliders = item.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = isVisible;
    }

    public void RemoveMeat(Meat meat)
    {
        if (meat == null) return;
        meat.ReleaseOccupiedSlots();
        Destroy(meat.gameObject);
    }

    private Vector3 GetDefaultSpawnPoint()
    {
        if (slots.Count > 0 && slots[0] != null)
            return slots[0].transform.position;

        return transform.position;
    }

    private static Vector3 GetCenter(List<GridSlot> placementSlots)
    {
        if (placementSlots == null || placementSlots.Count == 0) return Vector3.zero;

        Vector3 sum = Vector3.zero;
        for (int i = 0; i < placementSlots.Count; i++)
            sum += placementSlots[i].transform.position;

        return sum / placementSlots.Count;
    }
    
    /// <summary>Devuelve true si hay al menos un Coal en estado Encendido.</summary>
    public bool HasActiveCoal()
    {
        for (int i = 0; i < Coal.ActiveCoals.Count; i++)
        {
            var coal = Coal.ActiveCoals[i];
            if (coal != null && coal.state == CoalStates.Encendido)
                return true;
        }
        return false;
    }
    /// <summary>Devuelve true si hay al menos un Meat en estado entregable.</summary>
    public bool HasDeliverableMeat()
    {
        if (slots == null) return false;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null || slot.currentMeat == null) continue;

            var state = slot.currentMeat.state;
            if (state == MeatStates.Jugoso ||
                state == MeatStates.Hecho ||
                state == MeatStates.Muy_Hecho)
                return true;
        }
        return false;
    }

    /// <summary>Devuelve true si hay al menos un Meat crudo o cocinándose (no quemado).</summary>
    public bool HasRawOrCookingMeat()
    {
        if (slots == null) return false;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null || slot.currentMeat == null) continue;

            var state = slot.currentMeat.state;
            if (state == MeatStates.Crudo)
                return true;
        }
        return false;
    }
}