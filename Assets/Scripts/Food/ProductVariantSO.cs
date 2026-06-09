using UnityEngine;

[CreateAssetMenu(fileName = "ProductVariant", menuName = "Asado/Product Variant")]
public class ProductVariantSO : ScriptableObject
{
    [Header("Display")]
    [Tooltip("The exact display name of this product as shown to the player.")]
    public string displayName;

    [Tooltip("Sprite shown on the plate when this variant is assembled.")]
    public Sprite variantSprite;

    [Header("Visual by Cooking State")]
    [Tooltip("Sprites for each cooking state of the variant.")]
    public CookingSprites cookingSprites;

    public Sprite GetSpriteForState(MeatStates state)
    {
        Sprite result = cookingSprites.GetSpriteForState(state);
        if (result == null)
            result = variantSprite;
        return result;
    }

    [Header("Composition")]
    [Tooltip("The meat cut required for this variant.")]
    public MeatCutSO cut;

    [Tooltip("Required bread for sandwich variants. Null for plated variants.")]
    public BreadSO bread;

    [Header("Economy")]
    [Tooltip("Sell price placeholder - configure in Inspector.")]
    public float sellPrice; 

    [Header("Progression")]
    [Tooltip("Whether this variant is currently available to the player.")]
    public bool isUnlocked = true;

    public bool IsSandwich => bread != null;

    public bool IsPlated => bread == null;
}
