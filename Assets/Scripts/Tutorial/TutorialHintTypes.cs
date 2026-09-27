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
