using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System.Linq;

public static class OrderText
{
    public static string ToHoverString(this Order order)
    {
        if (order == null) return "Pedido: -";

        var sb = new StringBuilder();

        var cutName = order.PrimaryCut != null ? order.PrimaryCut.cutName : "Sin corte";
        sb.Append(cutName);

        if (order.IsSandwich)
        {
            var bread = order.bread != null ? order.bread.breadName : "Pan";
            sb.Append("\nSándwich (" + bread + ")");
        }
        else
        {
            sb.Append("\nAl plato");
        }

        if (order.sides != null && order.sides.Count > 0)
            sb.Append("\nAcomp: " + string.Join(", ", order.sides.Select(s => s.sideName)));

        if (order.toppings != null && order.toppings.Count > 0)
            sb.Append("\nToppings: " + string.Join(", ", order.toppings.Select(t => t.toppingName)));

        return sb.ToString();
    }
}