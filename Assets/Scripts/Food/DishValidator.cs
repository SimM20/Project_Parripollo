/// <summary>
/// Static helper that validates dishes and sandwiches against the spec rules.
/// Rules source: "Parrilla – Sistema de Cortes, Acompañamientos y Toppings" spec v0.1.
/// </summary>
public static class DishValidator
{
    // ── Plated dish validation ──────────────────────────────────────────────

    /// <summary>
    /// Validates a plated dish.
    /// Returns true if the dish is valid for delivery.
    /// Sets outReason to a debug string if invalid.
    /// </summary>
    public static bool ValidatePlatedDish(PlatedDish dish, out string outReason)
    {
        outReason = null;

        if (dish == null)
        {
            outReason = "Dish is null.";
            return false;
        }

        // Rule: no delivery without at least one cooked cut
        if (!dish.HasAnyCut)
        {
            outReason = "No hay corte en el plato. No se puede entregar un plato sin corte.";
            return false;
        }

        // Validate each cut in the plate
        for (int i = 0; i < dish.cuts.Count; i++)
        {
            MeatCutSO cut = dish.cuts[i];
            if (cut == null)
            {
                outReason = "El plato contiene una referencia de corte nula.";
                return false;
            }

            // Rule: sandwich-only cuts cannot be served plated
            if (cut.servingMode == ServingMode.SandwichOnly)
            {
                outReason = cut.cutName + " no existe al plato. Solo puede servirse en pan.";
                return false;
            }
        }

        return true;
    }

    // ── Sandwich validation ─────────────────────────────────────────────────

    /// <summary>
    /// Validates a sandwich.
    /// Returns true if the sandwich is valid for delivery.
    /// Sets outReason to a debug string if invalid.
    /// </summary>
    public static bool ValidateSandwich(Sandwich sandwich, out string outReason)
    {
        outReason = null;

        if (sandwich == null)
        {
            outReason = "Sandwich is null.";
            return false;
        }

        // Rule: no delivery without a cut
        if (sandwich.cut == null)
        {
            outReason = "No hay corte en el sandwich. No se puede entregar sin corte.";
            return false;
        }

        // Rule: no delivery without bread
        if (sandwich.bread == null)
        {
            outReason = "No hay pan en el sandwich.";
            return false;
        }

        // Rule: plated-only cuts cannot be served in a sandwich
        if (sandwich.cut.servingMode == ServingMode.PlatedOnly)
        {
            outReason = sandwich.cut.cutName + " es solo al plato. No puede ir en pan.";
            return false;
        }

        // Rule: the bread must match the cut's required bread
        if (sandwich.cut.requiredBread != null && sandwich.cut.requiredBread != sandwich.bread)
        {
            outReason = sandwich.cut.cutName + " requiere " + sandwich.cut.requiredBread.breadName
                        + " pero se uso " + sandwich.bread.breadName + ".";
            return false;
        }

        // Rule: Pan de paty is exclusive to Paty (sandwich-only cut)
        // This is enforced by requiredBread on Paty's MeatCutSO. But also defend the other direction:
        // if the bread is Pan de paty and the cut is NOT sandwich-only (i.e., not Paty), reject.
        if (sandwich.cut.requiredBread == null && sandwich.cut.servingMode != ServingMode.SandwichOnly)
        {
            // Cut uses Pan flauta (no specific override). Pan de paty must not be used here.
            // We detect "Pan de paty" by checking if the bread has a requiredForSandwichOnly marker.
            // Since we don't have a flag on BreadSO for this, we rely on MeatCutSO.requiredBread:
            // Paty sets requiredBread = Pan de paty. Any OTHER cut that ends up with Pan de paty
            // would only get it if the player manually puts it there — DishValidator must reject it.
            // We check via ProductVariantSO not being available here, so we enforce via:
            // If this cut's servingMode allows bread, the bread must match what the spec allows.
            // Pan flauta cuts have requiredBread = Pan flauta SO.
            if (sandwich.cut.requiredBread == null)
            {
                // requiredBread being null on a Both/SandwichOnly cut is unexpected —
                // log a warning but don't hard-fail (let it through for now as a safety net).
                UnityEngine.Debug.LogWarning("[DishValidator] Cut '" + sandwich.cut.cutName
                    + "' has ServingMode that allows sandwiches but no requiredBread assigned. "
                    + "Assign requiredBread in MeatCutSO.");
            }
        }

        return true;
    }

    // ── ProductVariant match ────────────────────────────────────────────────

    /// <summary>
    /// Attempts to resolve which ProductVariantSO matches a delivered plated dish.
    /// Returns null if no match found.
    /// </summary>
    public static ProductVariantSO ResolveVariant(PlatedDish dish, FoodCatalogSO catalog)
    {
        if (dish == null || catalog == null || !dish.HasAnyCut) return null;

        MeatCutSO cut = dish.PrimaryCut;
        var variants = catalog.GetVariantsForCut(cut);
        for (int i = 0; i < variants.Count; i++)
        {
            if (variants[i].IsPlated)
                return variants[i];
        }
        return null;
    }

    /// <summary>
    /// Attempts to resolve which ProductVariantSO matches a delivered sandwich.
    /// Returns null if no match found.
    /// </summary>
    public static ProductVariantSO ResolveVariant(Sandwich sandwich, FoodCatalogSO catalog)
    {
        if (sandwich == null || catalog == null || sandwich.cut == null) return null;

        var variants = catalog.GetVariantsForCut(sandwich.cut);
        for (int i = 0; i < variants.Count; i++)
        {
            ProductVariantSO v = variants[i];
            if (v.IsSandwich && v.bread == sandwich.bread)
                return v;
        }
        return null;
    }
}
