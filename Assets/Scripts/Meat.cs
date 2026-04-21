using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Meat : Item
{
    [SerializeField] public MeatTypeSO meatData;
    
    private bool isCookingSideA = false;
    private float cookedPercent = 0f;
    private float actualTimeA = 0f;
    private float actualTimeB = 0f;
    private MeatStates actualState = MeatStates.Crudo;
    private ItemType type = ItemType.Meat;

    public override void OnMouseUp()
    {
        Bounds meatBounds = GetComponent<Collider2D>().bounds;
        Collider2D[] hits = Physics2D.OverlapBoxAll(meatBounds.center, meatBounds.size, 0);

        List<GridSlot> slotsEncontrados = new List<GridSlot>();

        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<GridSlot>(out GridSlot slot))
            {
                if (slot.CanPlaceItem(itemType))
                {
                    slotsEncontrados.Add(slot);
                }
            }
        }

        int celdasNecesarias = meatData.GrillSpace.x * meatData.GrillSpace.y;

        if (slotsEncontrados.Count >= celdasNecesarias)
        {
            foreach (var s in slotsEncontrados)
            {
                s.PlaceItem(gameObject);
            }

            transform.position = CalcCenter(slotsEncontrados);
            return;
        }

        transform.position = startPosition;
    }

    private Vector3 CalcCenter(List<GridSlot> slots)
    {
        Vector3 centro = Vector3.zero;
        foreach (var s in slots) centro += s.transform.position;
        return centro / slots.Count;
    }

    public void Cook(float heat)
    {
        if (isCookingSideA)
        {
            actualTimeA += Time.deltaTime;
        }
        else
        {
            actualTimeB += Time.deltaTime;
        }
        CheckMeatState();
    }

    public void FlipSide()
    {
        isCookingSideA = !isCookingSideA;
    }

    private void CheckMeatState()
    {
        float totalTimeNeeded = meatData.TimeHeatA + meatData.TimeHeatB;
        float totalTimeElapsed = actualTimeA + actualTimeB;
        float currentPercent = (totalTimeElapsed / totalTimeNeeded) * 100f;

        if (currentPercent < 20) actualState = MeatStates.Crudo;
        else if (currentPercent < 45) actualState = MeatStates.Jugoso;
        else if (currentPercent < 75) actualState = MeatStates.Hecho;
        else if (currentPercent < 100) actualState = MeatStates.Muy_Hecho;
        else actualState = MeatStates.Pasado;
    }

    public float GetCookedPercent()
    {
        return cookedPercent;
    }
}
