using UnityEngine;

[System.Serializable]
public class WeightedOrderCut
{
    [Tooltip("Corte que puede aparecer en los pedidos.")]
    public MeatCutSO cut;

    [Min(0f)]
    [Tooltip("Peso relativo de aparición. Un peso 0 evita que sea elegido.")]
    public float weight = 1f;
}
