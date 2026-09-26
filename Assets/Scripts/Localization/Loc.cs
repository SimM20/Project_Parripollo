using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Localización de textos. Lee todas las tablas CSV de <c>Resources/Localization/</c> (una fila por
/// clave, una columna por idioma: <c>key,es,en,...</c>) y devuelve el texto de la clave en el idioma
/// activo. Sumar un idioma = sumar una columna con su código (ISO 639-1) en cada tabla: no hay que
/// tocar código.
///
/// Reglas de las tablas:
/// <list type="bullet">
/// <item>La primera columna es la clave; las demás, un idioma cada una (el encabezado es su código).</item>
/// <item>El separador se detecta del encabezado (<c>,</c> <c>;</c> o tab): Excel en español guarda con <c>;</c>.</item>
/// <item>Celdas entre comillas (con <c>""</c> para una comilla). <c>\n</c> dentro de una celda es un salto de línea.</item>
/// <item>Filas vacías o que empiezan con <c>#</c> se ignoran.</item>
/// <item>Celda vacía = sin traducir: se usa el idioma fuente (<see cref="SourceLanguage"/>) y, si tampoco está, la clave.</item>
/// </list>
///
/// Carga perezosa: el primer acceso lee las tablas y toma el idioma de <see cref="GameSettings"/>.
/// Los textos que quedan en pantalla escuchan <see cref="OnLanguageChanged"/> (ver <see cref="LocalizedText"/>).
/// </summary>
public static class Loc
{
    /// <summary>Idioma en el que se escribe el juego: fallback de cualquier celda vacía.</summary>
    public const string SourceLanguage = "es";

    /// <summary>Carpeta dentro de Resources con las tablas (.csv, cualquier cantidad).</summary>
    public const string TablesFolder = "Localization";

    /// <summary>Clave cuyo valor es el nombre del idioma en su propio idioma ("Español", "English").</summary>
    public const string LanguageNameKey = "language.name";

    /// <summary>Cambió el idioma activo. Los textos visibles tienen que volver a pedir su clave.</summary>
    public static event Action OnLanguageChanged;

    private static readonly List<string> languages = new List<string>();
    private static readonly Dictionary<string, string[]> table = new Dictionary<string, string[]>();
    private static readonly HashSet<string> warnedKeys = new HashSet<string>();
    private static bool loaded;
    private static string current = SourceLanguage;
    private static int currentIndex;
    private static int sourceIndex;

    /// <summary>Código del idioma activo ("es", "en").</summary>
    public static string Current
    {
        get
        {
            EnsureLoaded();
            return current;
        }
    }

    /// <summary>Idiomas disponibles, en el orden de las columnas de las tablas.</summary>
    public static IReadOnlyList<string> Languages
    {
        get
        {
            EnsureLoaded();
            return languages;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        loaded = false;
        OnLanguageChanged = null;
        warnedKeys.Clear();
    }

    // ── Consulta ──

    /// <summary>Texto de <paramref name="key"/> en el idioma activo (o en el fuente, o la clave si no existe).</summary>
    public static string Get(string key)
    {
        if (TryGet(key, out string value)) return value;

        if (!string.IsNullOrEmpty(key) && warnedKeys.Add(key))
            Debug.LogWarning($"[Loc] Falta la clave '{key}' en las tablas de Resources/{TablesFolder}.");
        return key;
    }

    /// <summary><see cref="Get"/> + <c>string.Format</c>.</summary>
    public static string Format(string key, params object[] args)
    {
        string pattern = Get(key);
        try
        {
            return string.Format(pattern, args);
        }
        catch (FormatException)
        {
            Debug.LogWarning($"[Loc] Formato inválido en '{key}' ({current}): \"{pattern}\".");
            return pattern;
        }
    }

    /// <summary>Busca la clave sin avisar si falta. Celda vacía cae al idioma fuente.</summary>
    public static bool TryGet(string key, out string value)
    {
        EnsureLoaded();
        value = null;
        if (string.IsNullOrEmpty(key) || !table.TryGetValue(key, out string[] row)) return false;

        value = Cell(row, currentIndex);
        if (string.IsNullOrEmpty(value)) value = Cell(row, sourceIndex);
        return value != null;
    }

    /// <summary>Texto de la clave, o <paramref name="fallback"/> si la clave está vacía o no existe.</summary>
    public static string GetOrDefault(string key, string fallback)
    {
        return TryGet(key, out string value) ? value : fallback;
    }

    public static bool Has(string key)
    {
        EnsureLoaded();
        return !string.IsNullOrEmpty(key) && table.ContainsKey(key);
    }

    /// <summary>
    /// Pool de textos numerados: <c>prefix.1</c>, <c>prefix.2</c>, ... hasta el primer número que
    /// falte. Sirve para listas de largo variable (frases de los clientes).
    /// </summary>
    public static List<string> GetPool(string prefix)
    {
        var pool = new List<string>();
        for (int i = 1; TryGet($"{prefix}.{i}", out string value); i++)
        {
            if (!string.IsNullOrEmpty(value)) pool.Add(value);
        }
        return pool;
    }

    /// <summary>Nombre del idioma en su propio idioma ("Español", "English"), para el menú de opciones.</summary>
    public static string GetLanguageName(string code)
    {
        EnsureLoaded();
        int index = languages.IndexOf(code);
        if (index >= 0 && table.TryGetValue(LanguageNameKey, out string[] row))
        {
            string name = Cell(row, index);
            if (!string.IsNullOrEmpty(name)) return name;
        }
        return code.ToUpperInvariant();
    }

    // ── Idioma ──

    /// <summary>Cambia el idioma activo y avisa a los textos. Lo llama <see cref="GameSettings"/> al aplicar.</summary>
    public static void SetLanguage(string code)
    {
        EnsureLoaded();
        string resolved = Resolve(code);
        if (resolved == current) return;

        current = resolved;
        currentIndex = languages.IndexOf(current);
        OnLanguageChanged?.Invoke();
    }

    /// <summary>
    /// Código válido para <paramref name="code"/>: acepta códigos ("en") y los nombres del enum viejo
    /// de init.cfg ("English"). Un idioma que no tiene columna cae al fuente.
    /// </summary>
    public static string Resolve(string code)
    {
        EnsureLoaded();
        string normalized = Normalize(code);
        return normalized != null && languages.Contains(normalized) ? normalized : SourceLanguage;
    }

    /// <summary>Idioma inicial: el del sistema operativo si hay tabla para él; si no, inglés (o el fuente).</summary>
    public static string DetectSystemLanguage()
    {
        EnsureLoaded();
        string system = FromSystemLanguage(Application.systemLanguage);
        if (system != null && languages.Contains(system)) return system;
        return languages.Contains("en") ? "en" : SourceLanguage;
    }

    private static string Normalize(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        code = code.Trim();

        // init.cfg anterior a la localización guardaba el nombre del enum GameLanguage.
        if (Enum.TryParse(code, true, out SystemLanguage legacy) && code.Length > 3)
            return FromSystemLanguage(legacy);

        return code.ToLowerInvariant();
    }

    private static string FromSystemLanguage(SystemLanguage language)
    {
        switch (language)
        {
            case SystemLanguage.Spanish: return "es";
            case SystemLanguage.English: return "en";
            case SystemLanguage.Portuguese: return "pt";
            case SystemLanguage.French: return "fr";
            case SystemLanguage.German: return "de";
            case SystemLanguage.Italian: return "it";
            case SystemLanguage.Russian: return "ru";
            case SystemLanguage.Polish: return "pl";
            case SystemLanguage.Turkish: return "tr";
            case SystemLanguage.Japanese: return "ja";
            case SystemLanguage.Korean: return "ko";
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified: return "zh";
            default: return null;
        }
    }

    // ── Carga ──

    /// <summary>Vuelve a leer las tablas (herramientas de editor, recarga en caliente).</summary>
    public static void Reload()
    {
        loaded = false;
        warnedKeys.Clear();
        EnsureLoaded();
        OnLanguageChanged?.Invoke();
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;

        LoadTables();

        // Las tablas van antes que GameSettings: sus defaults llaman a DetectSystemLanguage.
        string wanted = Application.isPlaying ? GameSettings.Current.language : SourceLanguage;
        current = Resolve(wanted);
        currentIndex = languages.IndexOf(current);
    }

    private static void LoadTables()
    {
        languages.Clear();
        table.Clear();
        languages.Add(SourceLanguage);

        TextAsset[] assets = Resources.LoadAll<TextAsset>(TablesFolder);
        Array.Sort(assets, (a, b) => string.CompareOrdinal(a.name, b.name));
        foreach (TextAsset asset in assets)
            LoadTable(asset);

        sourceIndex = languages.IndexOf(SourceLanguage);
    }

    private static void LoadTable(TextAsset asset)
    {
        string text = asset.text;
        if (string.IsNullOrEmpty(text)) return;
        if (text[0] == '﻿') text = text.Substring(1);

        char separator = DetectSeparator(text);
        List<List<string>> rows = ParseCsv(text, separator);
        if (rows.Count == 0) return;

        // Encabezado: key, es, en, ... → índice global de cada columna.
        List<string> header = rows[0];
        var columnToLanguage = new int[header.Count];
        for (int c = 1; c < header.Count; c++)
        {
            string code = header[c].Trim().ToLowerInvariant();
            if (code.Length == 0) { columnToLanguage[c] = -1; continue; }
            if (!languages.Contains(code)) languages.Add(code);
            columnToLanguage[c] = languages.IndexOf(code);
        }

        for (int r = 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            string key = row.Count > 0 ? row[0].Trim() : null;
            if (string.IsNullOrEmpty(key) || key.StartsWith("#")) continue;

            if (table.ContainsKey(key))
                Debug.LogWarning($"[Loc] Clave repetida '{key}' en {asset.name}.csv: gana la última.");

            var values = new string[Math.Max(languages.Count, 8)];
            for (int c = 1; c < row.Count && c < columnToLanguage.Length; c++)
            {
                int language = columnToLanguage[c];
                if (language < 0) continue;
                if (language >= values.Length) Array.Resize(ref values, language + 1);
                values[language] = Unescape(row[c]);
            }
            table[key] = values;
        }
    }

    private static string Cell(string[] row, int index)
    {
        return index >= 0 && index < row.Length ? row[index] : null;
    }

    private static string Unescape(string value)
    {
        return value.IndexOf('\\') < 0 ? value : value.Replace("\\n", "\n").Replace("\\t", "\t");
    }

    private static char DetectSeparator(string text)
    {
        int end = text.IndexOf('\n');
        string firstLine = end < 0 ? text : text.Substring(0, end);
        if (firstLine.IndexOf('\t') >= 0) return '\t';
        if (firstLine.IndexOf(';') >= 0 && firstLine.IndexOf(',') < 0) return ';';
        return ',';
    }

    /// <summary>CSV RFC 4180: comillas, comillas dobles escapadas y saltos de línea dentro de comillas.</summary>
    public static List<List<string>> ParseCsv(string text, char separator)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else inQuotes = false;
                }
                else cell.Append(c);
                continue;
            }

            if (c == '"') inQuotes = true;
            else if (c == separator) { row.Add(cell.ToString()); cell.Clear(); }
            else if (c == '\r') { }
            else if (c == '\n')
            {
                row.Add(cell.ToString());
                cell.Clear();
                rows.Add(row);
                row = new List<string>();
            }
            else cell.Append(c);
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }
        return rows;
    }
}
