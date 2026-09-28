using System;

/// <summary>
/// Qué control dibuja el ícono de un cartel. Con joystick cada uno muestra su equivalente.
/// ⚠️ Se serializa como int: los valores nuevos van siempre al final.
/// </summary>
public enum HintInput
{
    /// <summary>Sin ícono: cartel informativo.</summary>
    None,
    /// <summary>La tecla o botón de una <see cref="GameAction"/> (Q, Espacio, LB...).</summary>
    Action,
    /// <summary>Click izquierdo · A / Cruz.</summary>
    Click,
    /// <summary>Click derecho · X / Cuadrado.</summary>
    RightClick,
    /// <summary>Mantener el click y arrastrar · mantener A / Cruz.</summary>
    Drag,
    /// <summary>Pasar el mouse por encima · seleccionar con el stick.</summary>
    Hover,
}

/// <summary>El control que pide un cartel. Con <see cref="HintInput.Action"/>, <see cref="action"/> dice cuál.</summary>
[Serializable]
public struct HintPrompt
{
    public HintInput input;
    public GameAction action;

    public HintPrompt(HintInput input, GameAction action = default)
    {
        this.input = input;
        this.action = action;
    }

    public static HintPrompt For(GameAction action) => new HintPrompt(HintInput.Action, action);
}

/// <summary>
/// De qué lado de su objetivo se pone el cartel; la flecha apunta hacia el objetivo.
/// ⚠️ Se serializa como int: los valores nuevos van siempre al final.
/// </summary>
public enum HintPlacement
{
    Right,
    Left,
    Above,
    Below,
}

/// <summary>
/// A qué apunta un cartel. Se resuelve por código en <see cref="TutorialHintContext"/>: las zonas
/// fijas buscan su objeto en la escena y las otras siguen a lo que está pasando (la carne que hay que
/// dar vuelta, el cliente que espera).
/// ⚠️ Se serializa como int: los valores nuevos van siempre al final.
/// </summary>
public enum HintAnchorId
{
    /// <summary>Pestaña del panel de stock (izquierda).</summary>
    StockTab,
    /// <summary>Pestaña del panel de panes, guarniciones y salsas (derecha).</summary>
    ToppingsTab,
    /// <summary>Botón de capa carne / carbón, abajo de la parrilla.</summary>
    LayerButton,
    /// <summary>Centro de la fila de arriba de la parrilla.</summary>
    Grill,
    /// <summary>El plato.</summary>
    Plate,
    /// <summary>La primera carne que hay en la parrilla.</summary>
    MeatOnGrill,
    /// <summary>La carne que pide que la den vuelta (<see cref="HintCondition.MeatNeedsFlip"/>).</summary>
    MeatToFlip,
    /// <summary>Burbuja de pedido del primer cliente que espera y que no está tapado por un panel abierto.</summary>
    WaitingCustomer,
    /// <summary>El primer montón de ceniza de la parrilla.</summary>
    Ash,
    /// <summary>El corte rotable que se está arrastrando (el fantasma del stock o la carne).</summary>
    DraggedPiece,
    /// <summary>El pan del panel derecho que pide el cliente del plato.</summary>
    BreadForOrder,
    /// <summary>El frasco del panel derecho con la primera salsa que le falta al plato.</summary>
    SauceForOrder,
    /// <summary>Burbuja del cliente elegido, si pide un corte sin stock.</summary>
    MissingCutCustomer,
    /// <summary>Burbuja de un cliente que pide un corte sin stock y no es el elegido.</summary>
    CustomerToPick,
    /// <summary>Botón de deshacer del plato.</summary>
    UndoButton,
    /// <summary>La carne que se está por quemar (<see cref="HintCondition.MeatAboutToBurn"/>).</summary>
    MeatAboutToBurn,
    /// <summary>Las X de strikes del HUD.</summary>
    StrikeHud,
    /// <summary>La hora del HUD.</summary>
    ClockHud,
    /// <summary>Tienda: el panel "PARA ARRANCAR MAÑANA".</summary>
    ShopRequirements,
    /// <summary>Tienda: el primer Comprar que se puede apretar, en la parte visible de la grilla.</summary>
    ShopFirstBuyButton,
    /// <summary>Tienda: un Comprar apagado por los mínimos del próximo día (<see cref="HintCondition.ShopPurchaseBlocked"/>).</summary>
    ShopBlockedBuyButton,
    /// <summary>Tienda: la línea de la racha de noches con strikes.</summary>
    ShopStreakLine,
}

/// <summary>
/// Algo del estado de la partida que un cartel necesita para mostrarse (o para darse por aprendido).
/// Se evalúan en <see cref="TutorialHintContext"/>.
/// ⚠️ Se serializa como int: los valores nuevos van siempre al final.
/// </summary>
public enum HintCondition
{
    Always,
    StockPanelOpen,
    StockPanelClosed,
    ToppingsPanelClosed,
    MeatLayerActive,
    CoalLayerActive,
    MeatOnGrill,
    CoalOnGrill,
    /// <summary>Ningún carbón encendido o apagado en la parrilla (la ceniza no cuenta).</summary>
    NoCoalOnGrill,
    /// <summary>Hay un cliente esperando: no se fue, no lo atendieron, no está reaccionando.</summary>
    CustomerWaiting,
    /// <summary>Una carne de la parrilla ya se hizo de un lado y el otro sigue crudo.</summary>
    MeatNeedsFlip,
    /// <summary>Una carne de la parrilla está a un punto o menos de lo que pide un cliente que espera, sin caras crudas ni quemadas.</summary>
    MeatReadyForOrder,
    PlateHasMeat,
    PlateEmpty,
    ToppingsPanelOpen,
    /// <summary>Hay ceniza en la parrilla.</summary>
    AshOnGrill,
    /// <summary>Se está arrastrando un corte que se puede rotar (su footprint no es cuadrado).</summary>
    DraggingRotatablePiece,
    /// <summary>El cliente que espera el corte del plato lo pidió en sándwich y el plato no tiene pan.</summary>
    PlateNeedsBread,
    /// <summary>El cliente que espera el corte del plato pidió una salsa que el plato no tiene.</summary>
    PlateNeedsTopping,
    /// <summary><see cref="PlateNeedsBread"/> o <see cref="PlateNeedsTopping"/>.</summary>
    PlateNeedsExtras,
    /// <summary>Un cliente que espera pidió el corte del plato, y el plato tiene el pan y las salsas que pidió.</summary>
    PlateReadyForCustomer,
    /// <summary>El cliente elegido (al que va la M) pide un corte sin stock, ni en la parrilla ni en el plato.</summary>
    SelectedWantsMissingCut,
    /// <summary>Otro cliente, no el elegido, pide un corte sin stock (y el elegido no).</summary>
    OtherWantsMissingCut,
    /// <summary>
    /// El cliente rechazó el plato y sigue igual: con carne que no espera nadie, o que no se arregla
    /// agregando pan o salsa. Se olvida al vaciarse el plato.
    /// </summary>
    PlateRejected,
    /// <summary>Una carne de la parrilla tiene la cara de abajo en Pasado: lo próximo es quemarse.</summary>
    MeatAboutToBurn,
    /// <summary>Hay al menos un strike y todavía no se llegó al límite.</summary>
    StrikeWarning,
    /// <summary>El local cerró a la hora (no por strikes) y quedan clientes esperando.</summary>
    ClosedWithCustomers,
    /// <summary>La tienda está a la vista y se puede usar: sin el popup de cierre por strikes encima.</summary>
    ShopReady,
    /// <summary>Hay un Comprar habilitado a la vista.</summary>
    ShopCanBuy,
    /// <summary>Hay un Comprar apagado porque la compra dejaría los mínimos del próximo día fuera de alcance.</summary>
    ShopPurchaseBlocked,
    /// <summary>La racha de noches con strikes pasó de 0 (la línea está a la vista).</summary>
    StrikeStreakActive,
}
