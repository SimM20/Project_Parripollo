using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Herramientas de la localización (<see cref="Loc"/>), menú <c>Tools/Localización</c>:
/// validar las tablas contra el código y los assets, recargarlas en caliente y alternar el
/// idioma en Play para probar.
/// </summary>
public static class LocalizationTools
{
    private const string Menu = "Tools/Localización/";
    private const string TablesPath = "Assets/Resources/" + Loc.TablesFolder;

    private static readonly Regex CodeKey = new Regex(@"Loc\.(?:Get|Format|GetOrDefault|TryGet|Has)\(\s*""([^""]+)""");
    private static readonly Regex CodePool = new Regex(@"""(feedback\.phrase\.[a-z_]+)""");
    private static readonly Regex KeyLiteral = new Regex(@"""([a-z_]+(?:\.[a-z0-9_]+)+)""");
    private static readonly Regex AssetKey = new Regex(@"^\s+(?:key|nameKey|descriptionKey|closedLabelKey): (\S+)\s*$", RegexOptions.Multiline);
    private static readonly Regex Placeholder = new Regex(@"\{(\d+)(?:[:,][^}]*)?\}");

    [MenuItem(Menu + "Validar tablas")]
    public static void Validate()
    {
        Loc.Reload();
        var tables = ReadTables(out List<string> languages);
        var report = new StringBuilder();
        int problems = 0;

        // 1. Celdas vacías y placeholders que no coinciden con el idioma fuente.
        foreach (var pair in tables.OrderBy(p => p.Key))
        {
            string source = pair.Value.TryGetValue(Loc.SourceLanguage, out string s) ? s : null;
            var sourceArgs = Placeholders(source);

            foreach (string language in languages)
            {
                pair.Value.TryGetValue(language, out string value);
                if (string.IsNullOrEmpty(value))
                {
                    report.AppendLine($"  [sin traducir] {pair.Key} ({language})");
                    problems++;
                }
                else if (!sourceArgs.SetEquals(Placeholders(value)))
                {
                    report.AppendLine($"  [placeholders] {pair.Key} ({language}): \"{value}\"");
                    problems++;
                }
            }
        }

        // 2. Claves usadas en código y en assets que no están en las tablas.
        // En código: los argumentos de Loc.* y cualquier literal con forma de clave cuyo primer
        // segmento exista en las tablas (así entran las claves elegidas con un ternario).
        var used = new Dictionary<string, string>();
        var namespaces = new HashSet<string>(tables.Keys.Select(k => k.Split('.')[0]));
        foreach (string file in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            if (!text.Contains("Loc.")) continue;
            foreach (Match m in CodeKey.Matches(text)) used[m.Groups[1].Value] = file;
            foreach (Match m in CodePool.Matches(text)) used[m.Groups[1].Value + ".1"] = file;
            foreach (Match m in KeyLiteral.Matches(text))
            {
                string key = m.Groups[1].Value;
                if (namespaces.Contains(key.Split('.')[0]) && !CodePool.IsMatch("\"" + key + "\""))
                    used[key] = file;
            }
        }
        foreach (string file in Directory.GetFiles("Assets", "*.*", SearchOption.AllDirectories)
                     .Where(f => f.EndsWith(".prefab") || f.EndsWith(".unity") || f.EndsWith(".asset")))
        {
            if (file.Replace('\\', '/').StartsWith("Assets/TextMesh Pro")) continue;
            string text = File.ReadAllText(file);
            if (!text.Contains("Key: ") && !text.Contains("  key: ")) continue;
            foreach (Match m in AssetKey.Matches(text))
            {
                string key = m.Groups[1].Value.Trim('\'', '"');
                if (key.Length > 0 && key.Contains(".")) used[key] = file;
            }
        }
        foreach (var pair in used.OrderBy(p => p.Key))
        {
            if (tables.ContainsKey(pair.Key)) continue;
            report.AppendLine($"  [clave inexistente] {pair.Key} (usada en {pair.Value})");
            problems++;
        }

        string header = $"[Loc] Validación: {tables.Count} claves, idiomas {string.Join(", ", languages)}, " +
                        $"{used.Count} claves referenciadas. ";
        if (problems == 0) Debug.Log(header + "Sin problemas.");
        else Debug.LogWarning(header + problems + " problema(s):\n" + report);
    }

    [MenuItem(Menu + "Recargar tablas")]
    public static void Reload()
    {
        Loc.Reload();
        Debug.Log($"[Loc] Tablas recargadas. Idioma activo: {Loc.Current}.");
    }

    [MenuItem(Menu + "Siguiente idioma (Play)")]
    public static void NextLanguage()
    {
        var languages = Loc.Languages.ToList();
        int index = (languages.IndexOf(Loc.Current) + 1) % languages.Count;
        Loc.SetLanguage(languages[index]);
        Debug.Log($"[Loc] Idioma activo: {Loc.Current} (solo esta sesión: no se guarda en init.cfg).");
    }

    [MenuItem(Menu + "Siguiente idioma (Play)", true)]
    private static bool NextLanguageValidate() => Application.isPlaying;

    [MenuItem(Menu + "Abrir carpeta de tablas")]
    public static void RevealTables() => EditorUtility.RevealInFinder(TablesPath);

    /// <summary>Lee todas las tablas como clave → (idioma → texto crudo).</summary>
    public static Dictionary<string, Dictionary<string, string>> ReadTables(out List<string> languages)
    {
        var tables = new Dictionary<string, Dictionary<string, string>>();
        languages = new List<string>();

        foreach (string file in Directory.GetFiles(TablesPath, "*.csv"))
        {
            string text = File.ReadAllText(file, Encoding.UTF8).TrimStart('﻿');
            char separator = text.Split('\n')[0].Contains('\t') ? '\t'
                           : text.Split('\n')[0].Contains(';') && !text.Split('\n')[0].Contains(',') ? ';' : ',';
            var rows = Loc.ParseCsv(text, separator);
            if (rows.Count == 0) continue;

            var header = rows[0];
            for (int c = 1; c < header.Count; c++)
            {
                string code = header[c].Trim().ToLowerInvariant();
                if (code.Length > 0 && !languages.Contains(code)) languages.Add(code);
            }

            for (int r = 1; r < rows.Count; r++)
            {
                string key = rows[r].Count > 0 ? rows[r][0].Trim() : "";
                if (key.Length == 0 || key.StartsWith("#")) continue;

                var cells = new Dictionary<string, string>();
                for (int c = 1; c < rows[r].Count && c < header.Count; c++)
                    cells[header[c].Trim().ToLowerInvariant()] = rows[r][c];
                tables[key] = cells;
            }
        }
        return tables;
    }

    private static HashSet<string> Placeholders(string text)
    {
        var set = new HashSet<string>();
        if (string.IsNullOrEmpty(text)) return set;
        foreach (Match m in Placeholder.Matches(text)) set.Add(m.Groups[1].Value);
        return set;
    }
}

/// <summary>Inspector de <see cref="LocalizedText"/>: muestra el texto de la clave en cada idioma.</summary>
[CustomEditor(typeof(LocalizedText))]
[CanEditMultipleObjects]
public class LocalizedTextEditor : Editor
{
    private static Dictionary<string, Dictionary<string, string>> cache;
    private static List<string> cachedLanguages;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SerializedProperty keyProperty = serializedObject.FindProperty("key");
        EditorGUILayout.PropertyField(keyProperty);
        serializedObject.ApplyModifiedProperties();

        if (keyProperty.hasMultipleDifferentValues) return;

        if (cache == null || GUILayout.Button("Releer tablas", EditorStyles.miniButton))
            cache = LocalizationTools.ReadTables(out cachedLanguages);

        string key = keyProperty.stringValue;
        if (string.IsNullOrEmpty(key)) return;

        if (!cache.TryGetValue(key, out var cells))
        {
            EditorGUILayout.HelpBox($"La clave '{key}' no está en Resources/{Loc.TablesFolder}.", MessageType.Error);
            return;
        }

        foreach (string language in cachedLanguages)
        {
            cells.TryGetValue(language, out string value);
            EditorGUILayout.LabelField(language, string.IsNullOrEmpty(value) ? "(sin traducir)" : value.Replace("\\n", "\n"),
                                       EditorStyles.wordWrappedLabel);
        }
    }
}
