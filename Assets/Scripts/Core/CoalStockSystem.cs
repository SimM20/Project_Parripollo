using System;
using UnityEngine;

/// <summary>
/// Tracks coal stock (units and bags) for the game session.
/// Coal does NOT gate grill cooking yet — this is a stock-only system with hooks
/// for future active-burning logic. Cooking behavior is unchanged.
/// </summary>
public class CoalStockSystem : MonoBehaviour
{
    [Header("Config")]
    [Tooltip("Number of coal units per bag. Configure in Inspector.")]
    [SerializeField] private int coalUnitsPerBag = 100; // [PLACEHOLDER - configurable]

    [Header("Initial Stock")]
    [SerializeField] private int initialCoalUnits = 0;

    // Runtime stock (persists in memory between days)
    private int coalUnits;

    public event Action OnCoalChanged;

    // ── Properties ──────────────────────────────────────────────────────────

    /// <summary>Current coal units available.</summary>
    public int CoalUnits => coalUnits;

    /// <summary>Units per bag (configurable).</summary>
    public int CoalUnitsPerBag => coalUnitsPerBag;

    /// <summary>Returns true if any coal is in stock.</summary>
    public bool HasCoal => coalUnits > 0;

    /// <summary>
    /// Hook: whether coal is actively burning in the grill.
    /// Always returns true for now — grill cooking is not gated by coal yet.
    /// Future: wire to GrillSystem to check for active burning coal slots.
    /// </summary>
    public bool HasActiveBurningCoal => true; // [HOOK - always true until coal-grill integration]

    // ── Lifecycle ───────────────────────────────────────────────────────────

    void Awake()
    {
        coalUnits = Mathf.Max(0, initialCoalUnits);
    }

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Adds coal by number of bags. Each bag = coalUnitsPerBag units.
    /// </summary>
    public void AddBags(int bags)
    {
        if (bags <= 0) return;
        coalUnits += bags * coalUnitsPerBag;
        OnCoalChanged?.Invoke();
        Debug.Log("[CoalStockSystem] Added " + bags + " bag(s). Total units: " + coalUnits);
    }

    /// <summary>
    /// Tries to consume a number of coal units.
    /// Returns true if successful, false if not enough coal.
    /// </summary>
    public bool TryConsume(int units)
    {
        if (units <= 0) return true;
        if (coalUnits < units) return false;
        coalUnits -= units;
        OnCoalChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Sets coal units directly (used for day persistence / leftover carry-forward).
    /// </summary>
    public void SetCoalUnits(int units)
    {
        coalUnits = Mathf.Max(0, units);
        OnCoalChanged?.Invoke();
    }

    /// <summary>Debug string for logging.</summary>
    public string GetDebugString()
    {
        return "Carbon: " + coalUnits + " unidades (" + (coalUnits / Mathf.Max(1, coalUnitsPerBag)) + " bolsas aprox)";
    }

    [ContextMenu("Debug: Print Coal Stock")]
    public void DebugPrint()
    {
        Debug.Log("[CoalStockSystem] " + GetDebugString());
    }
}
