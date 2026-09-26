using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System.Linq;

public static class OrderText
{
    public static string ToHoverString(this Order order)
    {
        if (order == null) return Loc.Get("order.none");

        var sb = new StringBuilder();

        var cutName = order.PrimaryCut != null ? order.PrimaryCut.cutName : Loc.Get("order.no_cut");
        sb.Append(cutName);

        if (order.PrimaryCut != null)
            sb.Append('\n').Append(Loc.Format("order.doneness", MeatHoverText.GetStateDisplayName(order.GetRequestedState(0))));

        if (order.IsSandwich)
        {
            var bread = order.bread != null ? order.bread.DisplayName : Loc.Get("order.bread");
            sb.Append('\n').Append(Loc.Format("order.sandwich", bread));
        }
        else
        {
            sb.Append('\n').Append(Loc.Get("order.plated"));
        }

        if (order.sides != null && order.sides.Count > 0)
            sb.Append('\n').Append(Loc.Format("order.sides", string.Join(", ", order.sides.Select(s => s.DisplayName))));

        if (order.toppings != null && order.toppings.Count > 0)
            sb.Append('\n').Append(Loc.Format("order.toppings", string.Join(", ", order.toppings.Select(t => t.toppingName))));

        return sb.ToString();
    }
}