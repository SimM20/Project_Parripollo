using System.Text;
using UnityEngine;

public static class MeatHoverText
{
    public static string ToHoverString(this Meat meat)
    {
        if (meat == null) return string.Empty;

        var sb = new StringBuilder();

        string cutName = meat.cut != null ? meat.cut.cutName : "Carne";
        sb.Append(cutName);

        sb.Append("\nEstado: ");
        sb.Append(GetStateDisplayName(meat.state));

        sb.Append("\nLado: ");
        sb.Append(meat.isSideA ? "A" : "B");

        return sb.ToString();
    }

    private static string GetStateDisplayName(MeatStates state)
    {
        switch (state)
        {
            case MeatStates.Crudo:      return "Crudo";
            case MeatStates.Jugoso:     return "Jugoso";
            case MeatStates.Hecho:      return "Hecho";
            case MeatStates.Muy_Hecho:  return "Muy Hecho";
            case MeatStates.Pasado:     return "Pasado";
            default:                    return state.ToString();
        }
    }
}
