public enum ViewType
{
    Grill,
    /// <summary>
    /// DEPRECADO: reemplazado por el StockPanel dentro de la Vista Parrilla.
    /// No se navega mas hacia esta vista. El miembro se conserva porque los assets
    /// de tutorial serializan ViewType como int y quitarlo correria Build y Shop.
    /// </summary>
    Cooler,
    Build,
    Shop
}
