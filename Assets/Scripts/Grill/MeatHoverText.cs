using System.Text;
using UnityEngine;

public static class MeatHoverText
{
    public static string ToHoverString(this Meat meat)
    {
        if (meat == null) return string.Empty;

        var sb = new StringBuilder();

        string cutName = meat.cut != null ? meat.cut.cutName : Loc.Get("meat.generic");
        sb.Append(cutName);

        // Solo la cara activa; sin etiquetas A/B (regla de la barra de hover).
        sb.Append('\n');
        sb.Append(Loc.Format("meat.state", GetStateDisplayName(meat.ActiveSideState)));

        return sb.ToString();
    }

    /// <summary>Nombre del punto de cocción para la UI, en el idioma activo.</summary>
    public static string GetStateDisplayName(MeatStates state)
    {
        switch (state)
        {
            case MeatStates.Crudo:      return Loc.Get("cooking.raw");
            case MeatStates.Jugoso:     return Loc.Get("cooking.rare");
            case MeatStates.Hecho:      return Loc.Get("cooking.medium");
            case MeatStates.Muy_Hecho:  return Loc.Get("cooking.well_done");
            case MeatStates.Pasado:     return Loc.Get("cooking.overcooked");
            case MeatStates.Quemado:    return Loc.Get("cooking.burnt");
            default:                    return state.ToString();
        }
    }
}
