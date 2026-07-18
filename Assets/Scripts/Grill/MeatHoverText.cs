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

        // Solo la cara activa; sin etiquetas A/B (regla de la barra de hover).
        sb.Append("\nEstado: ");
        sb.Append(GetStateDisplayName(meat.ActiveSideState));

        return sb.ToString();
    }

    public static string GetStateDisplayName(MeatStates state)
    {
        switch (state)
        {
            case MeatStates.Crudo:      return "Crudo";
            case MeatStates.Jugoso:     return "Jugoso";
            case MeatStates.Hecho:      return "Hecho";
            case MeatStates.Muy_Hecho:  return "Bien Hecho";
            case MeatStates.Pasado:     return "Pasado";
            case MeatStates.Quemado:    return "Quemado";
            default:                    return state.ToString();
        }
    }
}
