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
}
