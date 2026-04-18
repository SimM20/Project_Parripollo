using UnityEngine;

[CreateAssetMenu(fileName = "MeatData", menuName = "Meat/MeatData", order = 1)]
public class MeatTypeSO : ScriptableObject
{
    [SerializeField] private float timeHeatA;
    [SerializeField] private float timeHeatB;
    [SerializeField] private float price;
    [SerializeField] private Sprite meatSpriteA;
    [SerializeField] private Sprite meatSpriteB;
    [SerializeField] private Vector2 grillSpace;

    public float TimeHeatA => timeHeatA;
    public float TimeHeatB => timeHeatB;
    public float Price => price;
    public Sprite MeatSpriteA => meatSpriteA;
    public Sprite MeatSpriteB => meatSpriteB;
    public  Vector2 GrillSpace => grillSpace;
}
