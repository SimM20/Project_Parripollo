using UnityEngine;

[System.Serializable]
public class NightCutUnlock
{
    [Tooltip("Corte que se desbloquea (tienda y pedidos).")]
    public MeatCutSO cut;

    [Min(1)]
    [Tooltip("Noche a partir de la cual está desbloqueado. La primera noche es la 1.")]
    public int night = 2;
}

[System.Serializable]
public class NightVariantUnlock
{
    [Tooltip("Variante (plato o sándwich) que se desbloquea.")]
    public ProductVariantSO variant;

    [Min(1)]
    [Tooltip("Noche a partir de la cual está desbloqueada. La primera noche es la 1.")]
    public int night = 2;
}
