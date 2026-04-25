using System.Collections.Generic;

/// <summary>
/// Interface that exposes catalog-level queries for the food system.
/// This interface only covers static/catalog data (cuts, breads, sides, toppings,
/// product variants, unlock state, prices). It does NOT include runtime stock queries.
/// For stock queries, use CoolerSystem or FoodAvailabilityService.
/// </summary>
public interface IFoodCatalogProvider
{
    /// <summary>Returns all currently unlocked cuts.</summary>
    IReadOnlyList<MeatCutSO> GetUnlockedCuts();

    /// <summary>Returns all product variants that are currently unlocked.</summary>
    IReadOnlyList<ProductVariantSO> GetValidVariants();

    /// <summary>Returns all product variants for a specific cut (unlocked or not).</summary>
    IReadOnlyList<ProductVariantSO> GetVariantsForCut(MeatCutSO cut);

    /// <summary>Returns all unlocked product variants for a specific cut.</summary>
    IReadOnlyList<ProductVariantSO> GetUnlockedVariantsForCut(MeatCutSO cut);

    /// <summary>Returns the sell price for a given product variant.</summary>
    float GetBasePrice(ProductVariantSO variant);

    /// <summary>Returns all available breads in the catalog.</summary>
    IReadOnlyList<BreadSO> GetAvailableBreads();

    /// <summary>Returns all available sides in the catalog.</summary>
    IReadOnlyList<SideSO> GetAvailableSides();

    /// <summary>Returns all available toppings in the catalog.</summary>
    IReadOnlyList<ToppingSO> GetAvailableToppings();

    /// <summary>Returns all cuts that can be served as a sandwich (serving mode allows bread).</summary>
    IReadOnlyList<MeatCutSO> GetSandwichCapableCuts();
}
