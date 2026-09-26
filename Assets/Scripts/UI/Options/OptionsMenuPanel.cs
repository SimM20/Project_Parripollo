using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menú de opciones: resolución, modo de pantalla, VSync, tope de FPS, tipo de control e idioma.
/// Los cambios quedan pendientes hasta "APLICAR" (que los aplica y guarda en init.cfg vía
/// <see cref="GameSettings"/>); "VOLVER", Back (Esc · B / ○) o Pause (Start / Options) cierra y
/// descarta lo no aplicado.
/// </summary>
public class OptionsMenuPanel : MonoBehaviour
{
    [Header("Filas")]
    [SerializeField] private OptionSelectorUI resolutionRow;
    [SerializeField] private OptionSelectorUI displayModeRow;
    [SerializeField] private OptionSelectorUI vSyncRow;
    [SerializeField] private OptionSelectorUI fpsRow;
    [SerializeField] private OptionSelectorUI inputModeRow;
    [SerializeField] private OptionSelectorUI languageRow;

    [Header("Botones")]
    [SerializeField] private Button applyButton;
    [SerializeField] private Button backButton;

    /// <summary>Se cerró el panel (con o sin aplicar).</summary>
    public event Action OnClosed;

    public bool IsOpen => gameObject.activeSelf;

    /// <summary>
    /// Hay un menú de opciones abierto, o se cerró este mismo frame. En teclado Back y Pause
    /// comparten Esc: GameManager lo consulta para que Esc cierre las opciones sin despausar,
    /// sin importar qué Update corra primero.
    /// </summary>
    public static bool AnyOpen => openPanels > 0 || lastClosedFrame == Time.frameCount;

    private static int openPanels;
    private static int lastClosedFrame = -1;

    private static readonly int[] DefaultFpsOptions = { 30, 60, 120, 144, 240, 0 };

    private static readonly FullScreenMode[] DisplayModes =
    {
#if UNITY_STANDALONE_WIN
        FullScreenMode.ExclusiveFullScreen,
#endif
        FullScreenMode.FullScreenWindow,
        FullScreenMode.Windowed,
    };

    private static readonly InputMode[] InputModes = { InputMode.Auto, InputMode.KeyboardMouse, InputMode.Gamepad };
    private static readonly GameLanguage[] Languages = { GameLanguage.Spanish, GameLanguage.English };

    private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    private readonly List<int> fpsOptions = new List<int>();
    private SettingsData saved;
    private SettingsData pending;

    private void Awake()
    {
        if (resolutionRow != null) resolutionRow.OnValueChanged += i => { pending.resolutionWidth = resolutions[i].x; pending.resolutionHeight = resolutions[i].y; RefreshState(); };
        if (displayModeRow != null) displayModeRow.OnValueChanged += i => { pending.displayMode = DisplayModes[i]; RefreshState(); };
        if (vSyncRow != null) vSyncRow.OnValueChanged += i => { pending.vSync = i == 0; RefreshState(); };
        if (fpsRow != null) fpsRow.OnValueChanged += i => { pending.targetFps = fpsOptions[i]; RefreshState(); };
        if (inputModeRow != null) inputModeRow.OnValueChanged += i => { pending.inputMode = InputModes[i]; RefreshState(); };
        if (languageRow != null) languageRow.OnValueChanged += i => { pending.language = Languages[i]; RefreshState(); };

        if (applyButton != null) applyButton.onClick.AddListener(Apply);
        if (backButton != null) backButton.onClick.AddListener(Close);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        openPanels = 0;
        lastClosedFrame = -1;
    }

    private void OnEnable() => openPanels++;

    private void OnDisable()
    {
        openPanels = Mathf.Max(0, openPanels - 1);
        lastClosedFrame = Time.frameCount;
    }

    private void Update()
    {
        // Pause también cierra: en teclado Esc ya es Back, pero con gamepad el botón de pausa
        // (Start / Options) es otro, y si no cerrara acá quedaría sin efecto (GameManager lo ignora
        // mientras AnyOpen).
        if (InputManager.WasPressed(GameAction.Back) || InputManager.WasPressed(GameAction.Pause))
            Close();
    }

    public void Open()
    {
        gameObject.SetActive(true);

        saved = GameSettings.Current;
        pending = saved;
        Populate();
        RefreshState();
    }

    public void Close()
    {
        if (!IsOpen) return;

        gameObject.SetActive(false);
        OnClosed?.Invoke();
    }

    public void Apply()
    {
        GameSettings.ApplyAndSave(pending);
        saved = pending;
        RefreshState();
    }

    private void Populate()
    {
        // Resolución: las del monitor sin repetir por frecuencia, de menor a mayor. La guardada
        // se agrega si el monitor no la lista (p. ej. init.cfg editado a mano).
        resolutions.Clear();
        foreach (Resolution r in Screen.resolutions)
        {
            var size = new Vector2Int(r.width, r.height);
            if (!resolutions.Contains(size)) resolutions.Add(size);
        }
        var current = new Vector2Int(pending.resolutionWidth, pending.resolutionHeight);
        if (!resolutions.Contains(current)) resolutions.Add(current);
        resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

        var resolutionLabels = new List<string>(resolutions.Count);
        foreach (Vector2Int size in resolutions)
            resolutionLabels.Add($"{size.x} x {size.y}");
        resolutionRow?.SetOptions(resolutionLabels, resolutions.IndexOf(current));

        var displayLabels = new List<string>(DisplayModes.Length);
        foreach (FullScreenMode mode in DisplayModes)
            displayLabels.Add(DisplayModeLabel(mode));
        displayModeRow?.SetOptions(displayLabels, Mathf.Max(0, Array.IndexOf(DisplayModes, pending.displayMode)));

        vSyncRow?.SetOptions(new[] { "SÍ", "NO" }, pending.vSync ? 0 : 1);

        fpsOptions.Clear();
        fpsOptions.AddRange(DefaultFpsOptions);
        int fps = pending.targetFps <= 0 ? 0 : pending.targetFps;
        if (!fpsOptions.Contains(fps)) fpsOptions.Insert(fpsOptions.Count - 1, fps);
        var fpsLabels = new List<string>(fpsOptions.Count);
        foreach (int value in fpsOptions)
            fpsLabels.Add(value <= 0 ? "SIN LÍMITE" : value.ToString());
        fpsRow?.SetOptions(fpsLabels, fpsOptions.IndexOf(fps));

        inputModeRow?.SetOptions(new[] { "AUTOMÁTICO", "TECLADO Y MOUSE", "JOYSTICK" },
                                 Mathf.Max(0, Array.IndexOf(InputModes, pending.inputMode)));

        languageRow?.SetOptions(new[] { "ESPAÑOL", "ENGLISH" }, Mathf.Max(0, Array.IndexOf(Languages, pending.language)));
        // TODO(idioma): habilitar la fila cuando exista la localización de textos.
        languageRow?.SetInteractable(false);
        languageRow?.SetNote("PRÓXIMAMENTE");
    }

    private void RefreshState()
    {
        // Con VSync el tope de FPS no hace nada (lo pone el monitor): la fila se apaga.
        if (fpsRow != null)
        {
            fpsRow.SetInteractable(!pending.vSync);
            fpsRow.SetDisplayOverride(pending.vSync ? "VSYNC" : null);
        }

        if (applyButton != null)
            applyButton.interactable = !pending.Equals(saved);
    }

    private static string DisplayModeLabel(FullScreenMode mode)
    {
        switch (mode)
        {
            case FullScreenMode.ExclusiveFullScreen: return "COMPLETA";
            case FullScreenMode.FullScreenWindow: return "SIN BORDES";
            case FullScreenMode.Windowed: return "VENTANA";
            default: return mode.ToString().ToUpperInvariant();
        }
    }
}
