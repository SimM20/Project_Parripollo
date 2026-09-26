using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Idioma del juego. TODO: todavía no hay localización; el valor solo se guarda.</summary>
public enum GameLanguage
{
    Spanish,
    English,
}

/// <summary>Valores de configuración del jugador. Es un struct: el menú edita una copia y la aplica entera.</summary>
[Serializable]
public struct SettingsData
{
    public int resolutionWidth;
    public int resolutionHeight;
    public FullScreenMode displayMode;
    /// <summary>FPS máximos. &lt;= 0 = sin límite. Se ignora con VSync.</summary>
    public int targetFps;
    public bool vSync;
    public InputMode inputMode;
    public GameLanguage language;

    public static SettingsData Defaults
    {
        get
        {
            Resolution native = Screen.currentResolution;
            return new SettingsData
            {
                resolutionWidth = native.width > 0 ? native.width : 1920,
                resolutionHeight = native.height > 0 ? native.height : 1080,
                displayMode = FullScreenMode.FullScreenWindow,
                targetFps = 120,
                vSync = true,
                inputMode = InputMode.Auto,
                language = GameLanguage.Spanish,
            };
        }
    }

    public bool Equals(SettingsData other)
    {
        return resolutionWidth == other.resolutionWidth && resolutionHeight == other.resolutionHeight
            && displayMode == other.displayMode && targetFps == other.targetFps && vSync == other.vSync
            && inputMode == other.inputMode && language == other.language;
    }
}

/// <summary>
/// Configuración del jugador (pantalla, FPS, tipo de control, idioma). Se persiste en
/// <c>persistentDataPath/init.cfg</c> como líneas <c>Clave=Valor</c>; las claves que no conoce se
/// conservan al reescribir el archivo. Carga perezosa: el primer acceso a <see cref="Current"/> lee el disco.
///
/// <see cref="Init"/> la aplica al arrancar y el menú de opciones (<see cref="OptionsMenuPanel"/>)
/// la cambia con <see cref="ApplyAndSave"/>.
/// </summary>
public static class GameSettings
{
    private const string FileName = "init.cfg";

    private const string KeyTargetFps = "TargetFPS";
    private const string KeyResolutionX = "ResolutionX";
    private const string KeyResolutionY = "ResolutionY";
    private const string KeyLegacyFullscreen = "Fullscreen";
    private const string KeyDisplayMode = "DisplayMode";
    private const string KeyVSync = "VSync";
    private const string KeyInputMode = "InputMode";
    private const string KeyLanguage = "Language";

    /// <summary>Se aplicó una configuración nueva.</summary>
    public static event Action<SettingsData> OnApplied;

    private static SettingsData current;
    private static bool loaded;
    private static readonly Dictionary<string, string> extraKeys = new Dictionary<string, string>();

    private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public static SettingsData Current
    {
        get
        {
            EnsureLoaded();
            return current;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        loaded = false;
        extraKeys.Clear();
        OnApplied = null;
    }

    /// <summary>Aplica la configuración guardada (pantalla, FPS, tipo de control).</summary>
    public static void ApplyCurrent() => Apply(Current);

    /// <summary>Reemplaza la configuración, la aplica y la guarda en disco.</summary>
    public static void ApplyAndSave(SettingsData data)
    {
        EnsureLoaded();
        current = data;
        Apply(current);
        Save();
    }

    private static void Apply(SettingsData data)
    {
        // Sin cambios no se reaplica: SetResolution puede parpadear la ventana (Init corre en cada vuelta al menú).
        bool screenChanged = Screen.width != data.resolutionWidth || Screen.height != data.resolutionHeight
                             || Screen.fullScreenMode != data.displayMode;
        if (screenChanged && data.resolutionWidth > 0 && data.resolutionHeight > 0)
            Screen.SetResolution(data.resolutionWidth, data.resolutionHeight, data.displayMode);

        // Con VSync Unity ignora targetFrameRate: el tope lo pone el monitor.
        QualitySettings.vSyncCount = data.vSync ? 1 : 0;
        Application.targetFrameRate = data.vSync || data.targetFps <= 0 ? -1 : data.targetFps;

        InputManager.SetInputMode(data.inputMode);

        // TODO(idioma): aplicar data.language cuando exista la localización de textos.

        OnApplied?.Invoke(data);
    }

    // ── Archivo ──

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;

        current = SettingsData.Defaults;
        extraKeys.Clear();

        if (!File.Exists(FilePath))
        {
            Save();
            return;
        }

        var values = new Dictionary<string, string>();
        try
        {
            foreach (string line in File.ReadAllLines(FilePath))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                int separator = line.IndexOf('=');
                if (separator <= 0) continue;
                values[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameSettings] No se pudo leer {FilePath}: {e.Message}. Se usan los valores por defecto.");
            return;
        }

        if (TryGetInt(values, KeyResolutionX, out int width) && TryGetInt(values, KeyResolutionY, out int height)
            && width > 0 && height > 0)
        {
            current.resolutionWidth = width;
            current.resolutionHeight = height;
        }

        if (values.TryGetValue(KeyDisplayMode, out string modeText) && Enum.TryParse(modeText, out FullScreenMode mode))
            current.displayMode = mode;
        else if (values.TryGetValue(KeyLegacyFullscreen, out string fsText) && bool.TryParse(fsText, out bool fullscreen))
            current.displayMode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        if (TryGetInt(values, KeyTargetFps, out int fps))
            current.targetFps = fps;
        if (values.TryGetValue(KeyVSync, out string vSyncText) && bool.TryParse(vSyncText, out bool vSync))
            current.vSync = vSync;
        if (values.TryGetValue(KeyInputMode, out string inputText) && Enum.TryParse(inputText, out InputMode inputMode))
            current.inputMode = inputMode;
        if (values.TryGetValue(KeyLanguage, out string languageText) && Enum.TryParse(languageText, out GameLanguage language))
            current.language = language;

        foreach (var pair in values)
        {
            if (!IsKnownKey(pair.Key))
                extraKeys[pair.Key] = pair.Value;
        }
    }

    private static void Save()
    {
        try
        {
            using (var writer = new StreamWriter(FilePath))
            {
                writer.WriteLine("# Game config file");
                writer.WriteLine($"{KeyResolutionX}={current.resolutionWidth}");
                writer.WriteLine($"{KeyResolutionY}={current.resolutionHeight}");
                writer.WriteLine($"{KeyDisplayMode}={current.displayMode}");
                writer.WriteLine($"{KeyTargetFps}={current.targetFps}");
                writer.WriteLine($"{KeyVSync}={current.vSync.ToString().ToLowerInvariant()}");
                writer.WriteLine($"{KeyInputMode}={current.inputMode}");
                writer.WriteLine($"{KeyLanguage}={current.language}");

                foreach (var pair in extraKeys)
                    writer.WriteLine($"{pair.Key}={pair.Value}");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameSettings] No se pudo guardar {FilePath}: {e.Message}");
        }
    }

    private static bool IsKnownKey(string key)
    {
        return key == KeyTargetFps || key == KeyResolutionX || key == KeyResolutionY || key == KeyLegacyFullscreen
            || key == KeyDisplayMode || key == KeyVSync || key == KeyInputMode || key == KeyLanguage;
    }

    private static bool TryGetInt(Dictionary<string, string> values, string key, out int value)
    {
        value = 0;
        return values.TryGetValue(key, out string text) && int.TryParse(text, out value);
    }
}
