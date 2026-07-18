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

    [Tooltip("Sprite when the meat is overcooked (Pasado). Still deliverable.")]
    public Sprite pasado;

    [Tooltip("Sprite when the meat is burnt (Quemado). Not deliverable.")]
    public Sprite quemado;

    public Sprite GetSpriteForState(MeatStates state)
    {
        switch (state)
        {
            case MeatStates.Crudo:     return crudo;
            case MeatStates.Jugoso:    return jugoso;
            case MeatStates.Hecho:     return hecho;
            case MeatStates.Muy_Hecho: return muyHecho;
            case MeatStates.Pasado:    return pasado;
            case MeatStates.Quemado:   return quemado != null ? quemado : pasado;
            default:                   return crudo;
        }
    }
}
