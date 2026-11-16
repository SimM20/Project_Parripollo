using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Build de Windows SIN Steam, para pasarle a alguien que no tiene el juego en Steam (profes,
/// muestras, jurados). Compila con <c>DISABLESTEAMWORKS</c> solo para esta build: no toca
/// Player Settings, así que no hay nada que acordarse de sacar antes de la build de Steam.
///
/// En esa build el juego no pide Steam, no se cierra y los logros quedan en el backend local
/// (solo consola). Es una build de release: sin marca de Development Build.
/// </summary>
public static class NoSteamBuild
{
    private const string LastFolderKey = "Parripollo.NoSteamBuild.LastFolder";

    [MenuItem("Parripollo/Steam/NoSteamBuild")]
    private static void Build()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            EditorUtility.DisplayDialog("Build sin Steam", "No hay escenas activas en Build Settings.", "OK");
            return;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string defaultFolder = EditorPrefs.GetString(LastFolderKey, Path.Combine(projectRoot, "Builds", "SinSteam"));

        string folder = EditorUtility.SaveFolderPanel("Carpeta de la build sin Steam", defaultFolder, "");
        if (string.IsNullOrEmpty(folder))
            return; // cancelado

        EditorPrefs.SetString(LastFolderKey, folder);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(folder, PlayerSettings.productName + ".exe"),
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None,
            // Solo para esta build: Steamworks.NET y SteamBootstrap compilan vacíos.
            extraScriptingDefines = new[] { "DISABLESTEAMWORKS" },
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result != BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog("Build sin Steam",
                "La build falló (" + summary.result + "). Mirá la consola para el detalle.", "OK");
            return;
        }

        // Sin Steam no hace falta (ni sirve) el appid; SteamBuildGuard ya lo borra en release, esto es por las dudas.
        string appIdFile = Path.Combine(folder, "steam_appid.txt");
        if (File.Exists(appIdFile)) File.Delete(appIdFile);

        Debug.Log("[Steam] Build SIN Steam lista en " + folder + " (" + (summary.totalSize / (1024 * 1024)) + " MB). " +
                  "No la subas a Steam: no verifica licencia ni desbloquea logros.");
        EditorUtility.RevealInFinder(summary.outputPath);
    }
}
