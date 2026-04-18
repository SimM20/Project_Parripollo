using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Meat : MonoBehaviour
{
    [SerializeField] private MeatTypeSO meatData;
    
    private bool isCookingSideA = false;
    private float cookedPercent = 0f;
    private float actualTimeA = 0f;
    private float actualTimeB = 0f;
    private float percentSideA => Mathf.Clamp01(actualTimeA / meatData.TimeHeatA);
    private float percentSideB => Mathf.Clamp01(actualTimeB / meatData.TimeHeatB);
    private float percentGeneral => (percentSideA + percentSideB) / (meatData.TimeHeatA + meatData.TimeHeatB);
    private MeatStates actualState = MeatStates.Crudo;
    private ItemType type = ItemType.Meat;

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
        switch (percentGeneral)
        {
            case 0:
                actualState = MeatStates.Crudo;
                break;
            case 20:
                actualState = MeatStates.Jugoso;
                break;
            case 45:
                actualState = MeatStates.Hecho;
                break;
            case 75:
                actualState = MeatStates.Muy_Hecho;
                break;
            case 105:
                actualState = MeatStates.Pasado;
                break;
        }
    }

    public float GetCookedPercent()
    {
        return cookedPercent;
    }
}
