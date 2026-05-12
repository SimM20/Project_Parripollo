// ShopCart.cs
using System.Collections.Generic;

public class ShopCart
{
    public int coalBags;
    public readonly Dictionary<MeatCutSO, int> cuts = new();

    public void Clear() { coalBags = 0; cuts.Clear(); }

    public void SetCut(MeatCutSO cut, int qty)
    {
        if (cut == null) return;
        if (qty <= 0) cuts.Remove(cut);
        else cuts[cut] = qty;
    }

    public int GetCut(MeatCutSO cut) =>
        cut != null && cuts.TryGetValue(cut, out var q) ? q : 0;
}