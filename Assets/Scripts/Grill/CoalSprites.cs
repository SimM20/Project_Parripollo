using UnityEngine;

[System.Serializable]
public struct CoalSprites
{
    [Tooltip("Sprite when the coal is off")]
    public Sprite coalOff;

    [Tooltip("Sprite when the coal is lit (phase 1, freshly lit)")]
    public Sprite coalOnn;

    [Tooltip("Sprite when the coal is lit (phase 2, burning down)")]
    public Sprite coalOnPhase2;

    [Tooltip("Sprite when the coal is lit (phase 3, almost spent)")]
    public Sprite coalOnPhase3;

    [Tooltip("Sprite when the coal is ashes")]
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

    /// <summary>
    /// Igual que GetSpriteForState, pero mientras está Encendido elige la fase según cuánto se quemó
    /// (burnProgress 0..1). Las fases son solo visuales: el estado sigue siendo Encendido.
    /// </summary>
    public Sprite GetSpriteForBurn(CoalStates state, float burnProgress, float phase2At, float phase3At)
    {
        if (state != CoalStates.Encendido) return GetSpriteForState(state);

        if (burnProgress >= phase3At && coalOnPhase3 != null) return coalOnPhase3;
        if (burnProgress >= phase2At && coalOnPhase2 != null) return coalOnPhase2;
        return coalOnn;
    }
}
