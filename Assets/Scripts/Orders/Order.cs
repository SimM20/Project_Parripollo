using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents a customer's order.
/// Extended to support bread, sides, and toppings alongside the primary cut.
/// The 'meat' field is kept for backward compatibility with existing CustomerSystem.
/// </summary>
[System.Serializable]
public class Order
{
    // ── Legacy field (kept for backward compat with CustomerSystem/GameManager) ──
    /// <summary>Primary cut. Mirrors cuts[0] when set via SetSingleCut.</summary>
    public MeatCutSO meat;

    // ── Extended fields ──────────────────────────────────────────────────────
    /// <summary>All cuts requested in this order (multi-cut future support).</summary>
    public List<MeatCutSO> cuts = new List<MeatCutSO>();

    /// <summary>Requested bread (null = plated order).</summary>
    public BreadSO bread;

    /// <summary>Requested sides/accompaniments.</summary>
    public List<SideSO> sides = new List<SideSO>();

    /// <summary>Requested toppings.</summary>
    public List<ToppingSO> toppings = new List<ToppingSO>();

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Returns true if this order expects a sandwich (has bread).</summary>
    public bool IsSandwich => bread != null;

    /// <summary>Sets a single cut and keeps legacy 'meat' field in sync.</summary>
    public void SetSingleCut(MeatCutSO cut)
    {
        meat = cut;
        cuts.Clear();
        if (cut != null)
            cuts.Add(cut);
    }

    /// <summary>Returns the primary (first) cut, or null.</summary>
    public MeatCutSO PrimaryCut => cuts != null && cuts.Count > 0 ? cuts[0] : meat;
}
