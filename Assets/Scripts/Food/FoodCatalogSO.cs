using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central ScriptableObject catalog for all food items in the game.
/// Owns static/catalog data only. Has no dependency on MonoBehaviours or runtime stock.
/// For combined catalog+stock queries, use FoodAvailabilityService.
/// </summary>
[CreateAssetMenu(fileName = "FoodCatalog", menuName = "Asado/Food Catalog")]
public class FoodCatalogSO : ScriptableObject, IFoodCatalogProvider
{
    [Header("Cuts")]
    [Tooltip("All meat cuts registered in the catalog, regardless of unlock state.")]
    [SerializeField] private List<MeatCutSO> allCuts = new List<MeatCutSO>();

    [Header("Breads")]
    [SerializeField] private List<BreadSO> availableBreads = new List<BreadSO>();

    [Header("Sides / Accompaniments")]
    [SerializeField] private List<SideSO> availableSides = new List<SideSO>();

    [Header("Toppings")]
    [SerializeField] private List<ToppingSO> availableToppings = new List<ToppingSO>();

    [Header("Product Variants")]
    [Tooltip("All sellable product variants. Each variant is a distinct product (e.g. Choripan vs Chorizo al plato).")]
    [SerializeField] private List<ProductVariantSO> allVariants = new List<ProductVariantSO>();

    // ── IFoodCatalogProvider ────────────────────────────────────────────────

    public IReadOnlyList<MeatCutSO> GetUnlockedCuts()
    {
        var result = new List<MeatCutSO>();
        for (int i = 0; i < allCuts.Count; i++)
        {
            MeatCutSO cut = allCuts[i];
            if (cut != null && cut.isUnlocked)
                result.Add(cut);
        }
        return result;
    }

    public IReadOnlyList<ProductVariantSO> GetValidVariants()
    {
        var result = new List<ProductVariantSO>();
        for (int i = 0; i < allVariants.Count; i++)
        {
            ProductVariantSO v = allVariants[i];
            if (v != null && v.isUnlocked && v.cut != null && v.cut.isUnlocked)
                result.Add(v);
        }
        return result;
    }

    public IReadOnlyList<ProductVariantSO> GetVariantsForCut(MeatCutSO cut)
    {
        var result = new List<ProductVariantSO>();
        if (cut == null) return result;
        for (int i = 0; i < allVariants.Count; i++)
        {
            ProductVariantSO v = allVariants[i];
            if (v != null && v.cut == cut)
                result.Add(v);
        }
        return result;
    }

    public IReadOnlyList<ProductVariantSO> GetUnlockedVariantsForCut(MeatCutSO cut)
    {
        var result = new List<ProductVariantSO>();
        if (cut == null) return result;
        for (int i = 0; i < allVariants.Count; i++)
        {
            ProductVariantSO v = allVariants[i];
            if (v != null && v.cut == cut && v.isUnlocked)
                result.Add(v);
        }
        return result;
    }

    public float GetBasePrice(ProductVariantSO variant)
    {
        if (variant == null) return 0f;
        return variant.sellPrice;
    }

    public IReadOnlyList<BreadSO> GetAvailableBreads() => availableBreads.AsReadOnly();

    public IReadOnlyList<SideSO> GetAvailableSides() => availableSides.AsReadOnly();

    public IReadOnlyList<ToppingSO> GetAvailableToppings() => availableToppings.AsReadOnly();

    public IReadOnlyList<MeatCutSO> GetSandwichCapableCuts()
    {
        var result = new List<MeatCutSO>();
        for (int i = 0; i < allCuts.Count; i++)
        {
            MeatCutSO cut = allCuts[i];
            if (cut != null && cut.isUnlocked &&
                (cut.servingMode == ServingMode.SandwichOnly || cut.servingMode == ServingMode.Both))
                result.Add(cut);
        }
        return result;
    }

    // ── Editor helpers ──────────────────────────────────────────────────────

    /// <summary>Returns all registered cuts (editor/debug use).</summary>
    public IReadOnlyList<MeatCutSO> GetAllCuts() => allCuts.AsReadOnly();

    /// <summary>Returns all registered variants (editor/debug use).</summary>
    public IReadOnlyList<ProductVariantSO> GetAllVariants() => allVariants.AsReadOnly();
}
