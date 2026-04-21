using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "MeatCut", menuName = "Asado/Meat Cut")]
public class MeatCutSO : ScriptableObject
{
    [Header("Nombre")]
    public string cutName;
    [SerializeField] public float price;


    [Header("Visual")]
    [FormerlySerializedAs("cutSprite")]
    [SerializeField] public Sprite meatSpriteA;
    [SerializeField] public Sprite meatSpriteB;

    [Header("Cooking")]
    [FormerlySerializedAs("cookTimePerSide")]
    [SerializeField] public float timeHeatA;
    [SerializeField] public float timeHeatB;

    [Header("Espaciado")]
    [SerializeField] private Vector2Int grillSpace;

    public Vector2Int GrillSpace => grillSpace;

    public Sprite GetDefaultSprite()
    {
        if (meatSpriteA != null)
            return meatSpriteA;

        return meatSpriteB;
    }

    public Sprite GetSpriteForSide(bool sideA)
    {
        Sprite preferred = sideA ? meatSpriteA : meatSpriteB;
        if (preferred != null)
            return preferred;

        return sideA ? meatSpriteB : meatSpriteA;
    }

    public float GetHeatTimeForSide(bool sideA)
    {
        float preferred = sideA ? timeHeatA : timeHeatB;
        if (preferred > 0f)
            return preferred;

        float fallback = sideA ? timeHeatB : timeHeatA;
        if (fallback > 0f)
            return fallback;

        return 1f;
    }

    public Sprite cutSprite => GetDefaultSprite();
    public float cookTimePerSide => GetHeatTimeForSide(true);

    void OnValidate()
    {
        timeHeatA = Mathf.Max(0f, timeHeatA);
        timeHeatB = Mathf.Max(0f, timeHeatB);

        grillSpace.x = Mathf.Max(1, grillSpace.x);
        grillSpace.y = Mathf.Max(1, grillSpace.y);
    }

}