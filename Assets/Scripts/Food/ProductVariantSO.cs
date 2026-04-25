using UnityEngine;

/// <summary>
/// Represents a distinct sellable product variant.
/// A variant is the combination of a cooked cut with an optional bread.
/// Examples: "Chorizo al plato", "Choripan", "Hamburguesa de paty", "Sandwich de vacio".
/// </summary>
[CreateAssetMenu(fileName = "ProductVariant", menuName = "Asado/Product Variant")]
public class ProductVariantSO : ScriptableObject
{
    [Header("Display")]
    [Tooltip("The exact display name of this product as shown to the player.")]
    public string displayName;

    [Header("Composition")]
    [Tooltip("The meat cut required for this variant.")]
    public MeatCutSO cut;

    [Tooltip("Required bread for sandwich variants. Null for plated variants.")]
    public BreadSO bread;

    [Header("Economy")]
    [Tooltip("Sell price placeholder - configure in Inspector.")]
    public float sellPrice; // [PLACEHOLDER]

    [Header("Progression")]
    [Tooltip("Whether this variant is currently available to the player.")]
    public bool isUnlocked = true;

    /// <summary>
    /// Returns true if this is a sandwich variant (requires bread).
    /// </summary>
    public bool IsSandwich => bread != null;

    /// <summary>
    /// Returns true if this is a plated variant (no bread).
    /// </summary>
    public bool IsPlated => bread == null;
}
