using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "MeatCut", menuName = "Asado/Meat Cut")]
public class MeatCutSO : ItemDataSO
{
    public string cutName => itemName;
    public float price => basePrice;

    [Header("Visual")]
    [SerializeField] private Vector3 _visualOffset;
    [FormerlySerializedAs("cutSprite")]
    [SerializeField] public Sprite meatSpriteA;
    [SerializeField] public Sprite meatSpriteB;

    [Header("Cooking Sprites – Side A")]
    [Tooltip("Sprites for each cooking state when showing side A")]
    [SerializeField] public CookingSprites cookingSpritesA;

    [Header("Cooking Sprites – Side B")]
    [Tooltip("Sprites for each cooking state when showing side B")]
    [SerializeField] public CookingSprites cookingSpritesB;

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

    public Sprite GetSpriteForState(MeatStates state, bool sideA)
    {
        CookingSprites sprites = sideA ? cookingSpritesA : cookingSpritesB;
        Sprite result = sprites.GetSpriteForState(state);

        if (result == null)
        {
            CookingSprites fallbackSprites = sideA ? cookingSpritesB : cookingSpritesA;
            result = fallbackSprites.GetSpriteForState(state);
        }

        if (result == null)
            result = GetSpriteForSide(sideA);

        return result;
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

    public Vector3 visualOffset => _visualOffset;

    public Sprite cutSprite => GetDefaultSprite();
    public float cookTimePerSide => GetHeatTimeForSide(true);

    [Header("Serving Rules")]
    [Tooltip("Defines how this cut can be served: plated, sandwich, or both.")]
    [SerializeField] public ServingMode servingMode;

    [Tooltip("Required bread for sandwich variants. Null for plated-only cuts.")]
    [SerializeField] public BreadSO requiredBread;

    [Header("Unlock")]
    [Tooltip("Whether this cut is currently available to the player.")]
    [SerializeField] public bool isUnlocked = true;

    [Header("Sell Prices")]
    [Tooltip("Sell price when served plated. Placeholder - configure in Inspector.")]
    [SerializeField] public float sellPricePlate;

    [Tooltip("Sell price when served as sandwich. 0 if not applicable. Placeholder.")]
    [SerializeField] public float sellPriceSandwich;

    void OnValidate()
    {
        category = ItemType.Meat;

        timeHeatA = Mathf.Max(0f, timeHeatA);
        timeHeatB = Mathf.Max(0f, timeHeatB);

        grillSpace.x = Mathf.Max(1, grillSpace.x);
        grillSpace.y = Mathf.Max(1, grillSpace.y);
    }
}