/// <summary>
/// Acciones de juego que no son el puntero. Cada una es una acción del mapa "Gameplay" de
/// <c>Assets/Input/GameControls.inputactions</c> con el mismo nombre: los bindings de teclado
/// y gamepad se editan ahí, no en código.
/// </summary>
public enum GameAction
{
    Pause,
    ToggleStockPanel,
    ToggleToppingsPanel,
    ToggleGrillLayer,
    /// <summary>Rota el footprint de la pieza que se está arrastrando.</summary>
    Rotate,
    CleanAshes,
    ClearPlate,
    MissingCut,
    /// <summary>Volver / cerrar en menús (Esc · B / ○).</summary>
    Back,
}

/// <summary>
/// Tipo de control elegido en Opciones. Auto cambia solo entre mouse y gamepad; los otros dos
/// apagan los dispositivos del otro esquema (ver <see cref="InputManager.SetInputMode"/>).
/// </summary>
public enum InputMode
{
    Auto,
    KeyboardMouse,
    /// <summary>Solo gamepad. Sin ningún gamepad conectado el mouse sigue andando, para no dejar el juego sin control.</summary>
    Gamepad,
}

/// <summary>Con qué está jugando la persona ahora: define de dónde sale el puntero.</summary>
public enum InputScheme
{
    KeyboardMouse,
    /// <summary>El puntero es un cursor virtual que mueve el stick.</summary>
    Gamepad,
}

/// <summary>Familia del último gamepad usado. Sirve para mostrar los íconos correctos (A/B vs ✕/○).</summary>
public enum GamepadFamily
{
    None,
    Xbox,
    PlayStation,
    Generic,
}
