using UnityEngine;

[System.Serializable]
public struct CoalSprites
{
    [Tooltip("Sprite when the coal is off")]
    public Sprite coalOff;

    [Tooltip("Sprite when the coal is off")]
    public Sprite coalOnn;

    [Tooltip("Sprite when the coal is off")]
    public Sprite coalAshes;

    public Sprite GetSpriteForState(CoalStates state)
    {
        switch (state)
        {
            case CoalStates.Apagado: return coalOff;
            case CoalStates.Encendido: return coalOnn;
            case CoalStates.Ceniza: return coalAshes;
            default: return coalOff;
        }
    }
}