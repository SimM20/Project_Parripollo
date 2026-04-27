using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime MonoBehaviour that combines FoodCatalogSO (static data) with
/// CoolerSystem (live stock) to answer availability queries.
/// FoodCatalogSO itself has no dependency on runtime stock — this service bridges the two.
/// </summary>
public class FoodAvailabilityService : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FoodCatalogSO catalog;
    [SerializeField] private CoolerSystem coolerSystem;

    public FoodCatalogSO Catalog => catalog;

    // ── Stock queries ───────────────────────────────────────────────────────

    /// <summary>Returns the current stock count for a given cut.</summary>
    public int GetCutStock(MeatCutSO cut)
    {
        if (coolerSystem == null || cut == null) return 0;
        return coolerSystem.GetCount(cut);
    }

    /// <summary>Returns true if the given cut has at least 1 unit in stock.</summary>
    public bool IsInStock(MeatCutSO cut)
    {
        return GetCutStock(cut) > 0;
    }

    // ── Combined catalog + stock queries ────────────────────────────────────

    /// <summary>
    /// Returns all unlocked cuts that currently have stock available.
    /// Useful for order generation to know what can realistically be ordered.
    /// </summary>
    public IReadOnlyList<MeatCutSO> GetAvailableCuts()
    {
        if (catalog == null) return new List<MeatCutSO>();

        var unlocked = catalog.GetUnlockedCuts();
        var result = new List<MeatCutSO>(unlocked.Count);
        for (int i = 0; i < unlocked.Count; i++)
        {
            if (IsInStock(unlocked[i]))
                result.Add(unlocked[i]);
        }
        return result;
    }

    /// <summary>
    /// Returns all product variants whose cut is unlocked AND in stock.
    /// </summary>
    public IReadOnlyList<ProductVariantSO> GetAvailableVariants()
    {
        if (catalog == null) return new List<ProductVariantSO>();

        var valid = catalog.GetValidVariants();
        var result = new List<ProductVariantSO>(valid.Count);
        for (int i = 0; i < valid.Count; i++)
        {
            if (IsInStock(valid[i].cut))
                result.Add(valid[i]);
        }
        return result;
    }

    // ── Missing cut hook ────────────────────────────────────────────────────

    /// <summary>
    /// Informs the system that a requested cut is not available.
    /// Delegates to CoolerSystem.InformMissingCut for future customer behavior.
    /// </summary>
    public void InformMissingCut(MeatCutSO cut)
    {
        if (coolerSystem == null || cut == null) return;
        coolerSystem.InformMissingItem(cut);
    }

    // ── Debug ───────────────────────────────────────────────────────────────

    [ContextMenu("Debug: Print Availability")]
    public void DebugPrintAvailability()
    {
        if (catalog == null)
        {
            Debug.Log("[FoodAvailabilityService] No catalog assigned.");
            return;
        }

        var cuts = catalog.GetUnlockedCuts();
        var sb = new System.Text.StringBuilder("[FoodAvailabilityService] Stock:\n");
        for (int i = 0; i < cuts.Count; i++)
        {
            int stock = GetCutStock(cuts[i]);
            sb.AppendLine("  " + cuts[i].cutName + ": " + stock);
        }
        Debug.Log(sb.ToString());
    }
}
