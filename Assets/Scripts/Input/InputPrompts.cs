using UnityEngine;

/// <summary>
/// Nombre de la tecla o botón de una acción para mostrar en los textos, según el control en uso:
/// teclado y mouse, gamepad de Xbox (o genérico) o de PlayStation. Resuelve los tokens
/// <c>[[Accion]]</c> de las tablas de <see cref="Loc"/> (p. ej. "Abrí el stock con [[ToggleStockPanel]]"
/// → "Q" / "LB" / "L1") y avisa a los textos visibles cuando cambia el control.
///
/// Los nombres salen de los bindings reales de <c>GameControls.inputactions</c>
/// (<see cref="InputManager.GetBindingPath"/>): cambiar una tecla ahí cambia el texto solo. La tabla
/// solo traduce el nombre de cada control físico: <c>input.key.&lt;control&gt;</c> (teclas con nombre,
/// como Espacio), <c>input.xbox.&lt;control&gt;</c> y <c>input.ps.&lt;control&gt;</c> (botones del
/// gamepad por familia) e <c>input.mouse.*</c>. Una tecla sin fila se muestra en mayúsculas (Q, R).
///
/// Tokens válidos: los nombres de <see cref="GameAction"/> más <c>PointerPrimary</c> (click / A) y
/// <c>PointerSecondary</c> (click derecho / X).
/// </summary>
public static class InputPrompts
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        Loc.TokenResolver = Resolve;
        // Los dos eventos se limpian en SubsystemRegistration (InputManager.ResetStatics): esto corre después.
        InputManager.OnSchemeChanged += _ => Loc.RefreshTexts();
        InputManager.OnGamepadFamilyChanged += _ => Loc.RefreshTexts();
    }

    /// <summary>Texto de la tecla o botón de <paramref name="actionName"/> en el control activo, en negrita.</summary>
    public static string Resolve(string actionName)
    {
        string label = GetLabel(actionName);
        return label == null ? null : "<b>" + label + "</b>";
    }

    /// <summary>Igual que <see cref="Resolve"/> pero sin formato. Null si la acción no existe.</summary>
    public static string GetLabel(string actionName)
    {
        bool gamepad = InputManager.ActiveScheme == InputScheme.Gamepad;

        // El mouse no está en el mapa Gameplay (sus botones se leen directo del dispositivo).
        if (!gamepad && actionName == "PointerPrimary") return Loc.Get("input.mouse.left");
        if (!gamepad && actionName == "PointerSecondary") return Loc.Get("input.mouse.right");

        string path = InputManager.GetBindingPath(actionName, gamepad ? InputScheme.Gamepad : InputScheme.KeyboardMouse);
        if (string.IsNullOrEmpty(path)) return null;

        string control = path.Substring(path.LastIndexOf('/') + 1);

        if (!gamepad)
            return Loc.GetOrDefault("input.key." + control, control.ToUpperInvariant());

        string family = InputManager.ActiveGamepadFamily == GamepadFamily.PlayStation ? "ps" : "xbox";
        return Loc.GetOrDefault($"input.{family}.{control}", control);
    }
}
