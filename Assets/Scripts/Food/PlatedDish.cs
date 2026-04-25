using System.Collections.Generic;

/// <summary>
/// Runtime representation of a plated dish assembled at the build station.
/// Supports a list of cuts to allow multi-cut orders in the future.
/// For now, single-cut validation uses the first cut in the list.
/// </summary>
public class PlatedDish
{
    /// <summary>
    /// One or more cooked cuts on the plate.
    /// Multi-cut support is structurally available but currently validated per single cut.
    /// </summary>
    public List<MeatCutSO> cuts = new List<MeatCutSO>();

    /// <summary>Side dishes added to the plate.</summary>
    public List<SideSO> sides = new List<SideSO>();

    /// <summary>Toppings added to the plate.</summary>
    public List<ToppingSO> toppings = new List<ToppingSO>();

    /// <summary>Convenience: the primary cut (first in list). Null if no cuts.</summary>
    public MeatCutSO PrimaryCut => cuts != null && cuts.Count > 0 ? cuts[0] : null;

    /// <summary>Returns true if the plate has at least one cut.</summary>
    public bool HasAnyCut => cuts != null && cuts.Count > 0;

    public PlatedDish() { }

    public PlatedDish(MeatCutSO singleCut)
    {
        if (singleCut != null)
            cuts.Add(singleCut);
    }
}
