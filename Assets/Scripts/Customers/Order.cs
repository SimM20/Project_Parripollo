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

    /// <summary>
    /// Punto de cocción solicitado por corte, alineado con 'cuts'.
    /// Solo Jugoso..Pasado (nunca Crudo ni Quemado).
    /// </summary>
    public List<MeatStates> requestedStates = new List<MeatStates>();

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
        SetSingleCut(cut, MeatStates.Hecho);
    }

    /// <summary>Sets a single cut with its requested cooking point.</summary>
    public void SetSingleCut(MeatCutSO cut, MeatStates requestedState)
    {
        meat = cut;
        cuts.Clear();
        requestedStates.Clear();
        if (cut != null)
        {
            cuts.Add(cut);
            requestedStates.Add(requestedState);
        }
    }

    /// <summary>Returns the primary (first) cut, or null.</summary>
    public MeatCutSO PrimaryCut => cuts != null && cuts.Count > 0 ? cuts[0] : meat;

    /// <summary>Punto solicitado para el corte en 'index'. Fallback: Hecho.</summary>
    public MeatStates GetRequestedState(int index)
    {
        if (requestedStates != null && index >= 0 && index < requestedStates.Count)
            return requestedStates[index];

        return MeatStates.Hecho;
    }
}
