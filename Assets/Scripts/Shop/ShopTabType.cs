/// <summary>
/// Secciones de la tienda. ⚠️ Los botones de pestaña la serializan como int: los valores nuevos
/// van siempre al final. El orden en pantalla lo da <see cref="ShopTabs"/>, no el del enum.
/// </summary>
public enum ShopTabType
{
    Coal,
    Meat,
    Upgrades,
    Toppings,
    /// <summary>La ruta de paradas: la progresión de la run (ver <see cref="LocationProgression"/>).</summary>
    Route
}

/// <summary>Orden de las pestañas en pantalla y en el botón "Siguiente". Toppings es siempre la última.</summary>
public static class ShopTabs
{
    private static readonly ShopTabType[] Sequence =
    {
        ShopTabType.Coal, ShopTabType.Meat, ShopTabType.Upgrades, ShopTabType.Route, ShopTabType.Toppings
    };

    /// <summary>Posición de la pestaña de izquierda a derecha.</summary>
    public static int OrderOf(ShopTabType tab) => System.Array.IndexOf(Sequence, tab);

    /// <summary>
    /// La pestaña que sigue (después de la última vuelve a la primera). Con
    /// <paramref name="includeRoute"/> en false saltea la de la ruta.
    /// </summary>
    public static ShopTabType Next(ShopTabType tab, bool includeRoute)
    {
        int index = OrderOf(tab);
        for (int step = 1; step <= Sequence.Length; step++)
        {
            ShopTabType candidate = Sequence[(index + step) % Sequence.Length];
            if (includeRoute || candidate != ShopTabType.Route)
                return candidate;
        }
        return tab;
    }
}
