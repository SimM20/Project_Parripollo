using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dibujos de teclas, botones y gestos del mouse para los carteles del tutorial.
///
/// Se busca por el control físico del binding real (<see cref="InputPrompts.GetControl"/>), igual
/// que los textos: cambiar una tecla en GameControls.inputactions cambia el ícono solo. Un control
/// sin dibujo propio se muestra como una tecla (o botón de joystick) en blanco con su nombre encima
/// (<see cref="InputPrompts.GetLabel(string, InputScheme, GamepadFamily)"/>), así que cualquier
/// binding o joystick anda sin arte nuevo.
/// </summary>
[CreateAssetMenu(fileName = "InputGlyphs", menuName = "Tutorial/Input Glyphs")]
public class InputGlyphSetSO : ScriptableObject
{
    public enum Device
    {
        Keyboard,
        Mouse,
        Xbox,
        PlayStation,
    }

    [Serializable]
    public struct Entry
    {
        public Device device;
        [Tooltip("Control del binding, como en Input.csv: q, space, leftShoulder, buttonSouth. " +
                 "Mouse: leftButton, rightButton, drag, hover. Joystick, para pasar por encima: leftStick.")]
        public string control;
        public Sprite sprite;
    }

    [Header("Base para los controles sin dibujo propio")]
    [Tooltip("Tecla en blanco: el nombre de la tecla se escribe encima. Se usa Sliced.")]
    [SerializeField] private Sprite blankKey;
    [Tooltip("Botón de joystick en blanco: el nombre del botón se escribe encima. Se usa Sliced.")]
    [SerializeField] private Sprite blankPadButton;

    [Header("Dibujos propios")]
    [SerializeField] private List<Entry> entries = new List<Entry>();

    /// <summary>Ícono para el control que se está usando ahora.</summary>
    public HintGlyph Resolve(HintPrompt prompt) =>
        Resolve(prompt, InputManager.ActiveScheme, InputManager.ActiveGamepadFamily);

    public HintGlyph Resolve(HintPrompt prompt, InputScheme scheme, GamepadFamily family)
    {
        bool gamepad = scheme == InputScheme.Gamepad;
        Device pad = family == GamepadFamily.PlayStation ? Device.PlayStation : Device.Xbox;

        switch (prompt.input)
        {
            case HintInput.Action:
                return FromBinding(prompt.action.ToString(), scheme, family, gamepad ? pad : Device.Keyboard);

            // Los botones del mouse no están en el mapa Gameplay: con mouse se dibuja el gesto.
            case HintInput.Click:
                return gamepad
                    ? FromBinding("PointerPrimary", scheme, family, pad)
                    : FromMouse("leftButton", null, "PointerPrimary", family);

            case HintInput.Drag:
                return gamepad
                    ? FromBinding("PointerPrimary", scheme, family, pad)
                    : FromMouse("drag", "leftButton", "PointerPrimary", family);

            case HintInput.RightClick:
                return gamepad
                    ? FromBinding("PointerSecondary", scheme, family, pad)
                    : FromMouse("rightButton", null, "PointerSecondary", family);

            // Pasar por encima no tiene tecla: sin dibujo, el cartel queda solo con el texto.
            // Con joystick es seleccionar con el stick.
            case HintInput.Hover:
                return new HintGlyph(Find(gamepad ? pad : Device.Mouse, gamepad ? "leftStick" : "hover"), null, null);

            default:
                return default;
        }
    }

    private HintGlyph FromBinding(string actionName, InputScheme scheme, GamepadFamily family, Device device)
    {
        string control = InputPrompts.GetControl(actionName, scheme);
        Sprite sprite = control != null ? Find(device, control) : null;
        Sprite blank = device == Device.Keyboard ? blankKey : blankPadButton;

        return new HintGlyph(sprite, blank, InputPrompts.GetLabel(actionName, scheme, family));
    }

    private HintGlyph FromMouse(string control, string fallbackControl, string actionName, GamepadFamily family)
    {
        Sprite sprite = Find(Device.Mouse, control);
        if (sprite == null && fallbackControl != null)
            sprite = Find(Device.Mouse, fallbackControl);

        return new HintGlyph(sprite, blankKey, InputPrompts.GetLabel(actionName, InputScheme.KeyboardMouse, family));
    }

    private Sprite Find(Device device, string control)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (entry.device == device && entry.sprite != null
                && string.Equals(entry.control, control, StringComparison.OrdinalIgnoreCase))
                return entry.sprite;
        }
        return null;
    }
}

/// <summary>
/// Cómo dibujar el ícono de un cartel: un dibujo entero (<see cref="Sprite"/>) o, si no hay, una
/// tecla en blanco (<see cref="Blank"/>) con el nombre del control (<see cref="Label"/>) encima.
/// </summary>
public readonly struct HintGlyph
{
    public readonly Sprite Sprite;
    public readonly Sprite Blank;
    public readonly string Label;

    public HintGlyph(Sprite sprite, Sprite blank, string label)
    {
        Sprite = sprite;
        Blank = blank;
        Label = label;
    }

    public bool IsEmpty => Sprite == null && string.IsNullOrEmpty(Label);
}
