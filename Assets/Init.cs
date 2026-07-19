using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class Init : MonoBehaviour
{
    private string configFilePath;
    private Dictionary<string, string> configData = new Dictionary<string, string>();

    void Awake()
    {
        configFilePath = Path.Combine(Application.persistentDataPath, "init.cfg");

        LoadConfig();
        ApplySettings();
    }

    private void LoadConfig()
    {
        if (!File.Exists(configFilePath))
        {
            CreateDefaultConfig();
            return;
        }

        string[] lines = File.ReadAllLines(configFilePath);
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

            string[] parts = line.Split('=');
            if (parts.Length == 2)
                configData[parts[0].Trim()] = parts[1].Trim();
        }
    }

    private void CreateDefaultConfig()
    {
        configData["TargetFPS"] = "120";
        configData["ResolutionX"] = "1920";
        configData["ResolutionY"] = "1080";
        configData["Fullscreen"] = "true";

        SaveConfig();
    }

    public void SaveConfig()
    {
        using (StreamWriter writer = new StreamWriter(configFilePath))
        {
            writer.WriteLine("# Game config file");

            foreach (var kvp in configData)
            {
                writer.WriteLine($"{kvp.Key}={kvp.Value}");
            }
        }
    }

    private void ApplySettings()
    {
        if (configData.TryGetValue("TargetFPS", out string fpsString) && int.TryParse(fpsString, out int targetFps))
            Application.targetFrameRate = targetFps;

        else
            Application.targetFrameRate = 120; // Fallback


        int resX = 1920;
        int resY = 1080;
        bool isFullscreen = true;

        if (configData.TryGetValue("ResolutionX", out string xStr) && int.TryParse(xStr, out int parsedX))
            resX = parsedX;

        if (configData.TryGetValue("ResolutionY", out string yStr) && int.TryParse(yStr, out int parsedY))
            resY = parsedY;

        if (configData.TryGetValue("Fullscreen", out string fsStr) && bool.TryParse(fsStr, out bool parsedFs))
            isFullscreen = parsedFs;

        Screen.SetResolution(resX, resY, isFullscreen);
    }

    public string GetValue(string key, string defaultValue = "")
    {
        return configData.TryGetValue(key, out string val) ? val : defaultValue;
    }

    public void SetValue(string key, string value)
    {
        configData[key] = value;
        SaveConfig();
    }
}