using System.Collections.Generic;

/// <summary>
/// Runtime representation of a sandwich assembled at the build station.
/// </summary>
public class Sandwich
{
    /// <summary>The cooked cut inside the sandwich.</summary>
    public MeatCutSO cut;

    /// <summary>The bread used for this sandwich.</summary>
    public BreadSO bread;

    /// <summary>Toppings added to the sandwich.</summary>
    public List<ToppingSO> toppings = new List<ToppingSO>();

    /// <summary>Returns true if the sandwich has both a cut and a bread.</summary>
    public bool IsComplete => cut != null && bread != null;

    public Sandwich() { }

    public Sandwich(MeatCutSO sandwichCut, BreadSO sandwichBread)
    {
        cut = sandwichCut;
        bread = sandwichBread;
    }
}
