using System.Collections.Generic;
using UnityEngine;

public class GrillSystem : MonoBehaviour
{
    public List<GridSlot> slots = new List<GridSlot>();

    public GameObject meatPrefab;
    [Min(0.05f)] public float slotDropRadius = 0.8f;

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

    public bool TrySpawnMeatAtPoint(MeatCutSO cut, Vector3 worldPoint, out Meat spawnedMeat, bool rotateFootprint = false)
    {
        spawnedMeat = null;

        if (cut == null)
        {
            Debug.LogWarning("Intentaste spawnear una carne sin corte asignado");
            return false;
        }

        if (meatPrefab == null)
        {
            Debug.LogWarning("No hay meatPrefab asignado en GrillSystem");
            return false;
        }

        GameObject obj = Instantiate(meatPrefab);
        Meat meat = obj.GetComponent<Meat>();
        if (meat == null)
        {
            Debug.LogWarning("El prefab de carne no tiene componente Meat");
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
            Debug.Log("No hay espacio en la parrilla para el tamano requerido");
            Destroy(obj);
            return false;
        }

        for (int i = 0; i < placementSlots.Count; i++)
            placementSlots[i].PlaceMeat(meat);

        meat.transform.position = GetCenter(placementSlots);
        spawnedMeat = meat;
        return true;
    }

    public Meat GetCookedMeat(MeatCutSO cut)
    {
        if (cut == null)
            return null;

        foreach (var slot in slots)
        {
            if (slot.IsOccupied)
            {
                var meat = slot.currentMeat;
                if (meat == null)
                    continue;

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
        for (int i = 0; i < slots.Count; i++)
        {
            GridSlot slot = slots[i];
            if (slot == null || !slot.IsOccupied)
                continue;

            if (slot.currentItem.TryGetComponent<Item>(out Item genericItem))
                SetItemVisible(genericItem, isVisible);
        }
    }

    private static void SetItemVisible(Item item, bool isVisible)
    {
        if (item == null)
            return;

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
        for (int i = 0; i < slots.Count; i++)
        {
            GridSlot slot = slots[i];
            if (slot != null)
                return slot.transform.position;
        }

        return transform.position;
    }

    private static Vector3 GetCenter(List<GridSlot> placementSlots)
    {
        if (placementSlots == null || placementSlots.Count == 0)
            return Vector3.zero;

        Vector3 sum = Vector3.zero;
        for (int i = 0; i < placementSlots.Count; i++)
            sum += placementSlots[i].transform.position;

        return sum / placementSlots.Count;
    }
}