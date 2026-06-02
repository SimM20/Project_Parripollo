using UnityEngine;

[System.Serializable]
public struct CookingSprites
{
    [Tooltip("Sprite when the meat is raw (Crudo)")]
    public Sprite crudo;

    [Tooltip("Sprite when the meat is juicy/rare (Jugoso)")]
    public Sprite jugoso;

    [Tooltip("Sprite when the meat is done (Hecho)")]
    public Sprite hecho;

    [Tooltip("Sprite when the meat is well done (Muy Hecho)")]
    public Sprite muyHecho;

    [Tooltip("Sprite when the meat is overcooked/burnt (Pasado)")]
    public Sprite pasado;

    public Sprite GetSpriteForState(MeatStates state)
    {
        switch (state)
        {
            case MeatStates.Crudo:     return crudo;
            case MeatStates.Jugoso:    return jugoso;
            case MeatStates.Hecho:     return hecho;
            case MeatStates.Muy_Hecho: return muyHecho;
            case MeatStates.Pasado:    return pasado;
            default:                   return crudo;
        }
    }
}
