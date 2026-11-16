using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Cuida <c>steam_appid.txt</c> y el App ID en las builds de Windows (ver STEAMWORKS.md):
///
/// - Editor: deja <c>steam_appid.txt</c> en la raíz del proyecto con <see cref="SteamConfig.AppId"/>.
///   Sin ese archivo Steam no sabe qué juego es el editor. Está en .gitignore: se genera solo.
/// - Development Build: copia <c>steam_appid.txt</c> junto al .exe, para abrirlo sin pasar por Steam.
/// - Release: lo borra si quedó en la carpeta. Con ese archivo el juego se abre por fuera de
///   Steam y <c>RestartAppIfNecessary</c> no hace nada. Además, sin App ID la build no se hace.
///
/// Con DISABLESTEAMWORKS en los Scripting Define Symbols (build sin Steam) no se exige nada.
/// </summary>
public class SteamBuildGuard : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    private const string AppIdFileName = "steam_appid.txt";

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (!IsSteamTarget(report.summary.platform) || IsSteamDisabled())
            return;

        bool development = (report.summary.options & BuildOptions.Development) != 0;

        if (!development && !SteamConfig.IsAppIdConfigured)
        {
            throw new BuildFailedException(
                "[Steam] SteamConfig.AppId está en 0. Cargá el App ID de Parrilla 40 en " +
                "Assets/Scripts/Platform/Steam/SteamConfig.cs antes de hacer una build de release, " +
                "o tildá Development Build, o agregá DISABLESTEAMWORKS para una build sin Steam.");
        }
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (!IsSteamTarget(report.summary.platform))
            return;

        string buildDir = Path.GetDirectoryName(report.summary.outputPath);
        if (string.IsNullOrEmpty(buildDir)) return;

        string target = Path.Combine(buildDir, AppIdFileName);
        bool development = (report.summary.options & BuildOptions.Development) != 0;

        if (development && !IsSteamDisabled())
        {
            string source = Path.Combine(ProjectRoot, AppIdFileName);
            if (File.Exists(source))
            {
                File.Copy(source, target, true);
                Debug.Log("[Steam] Development Build: se copió " + AppIdFileName + " junto al .exe para probar sin abrir desde Steam.");
            }
            return;
        }

        if (File.Exists(target))
        {
            File.Delete(target);
            Debug.Log("[Steam] Build de release: se borró " + AppIdFileName + " de " + buildDir + ". No se sube a Steam.");
        }
    }

    private static bool IsSteamTarget(BuildTarget target) =>
        target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneWindows ||
        target == BuildTarget.StandaloneLinux64 || target == BuildTarget.StandaloneOSX;

    private static bool IsSteamDisabled()
    {
        string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);
        return defines != null && defines.Contains("DISABLESTEAMWORKS");
    }

    private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

    // ── steam_appid.txt del editor ──────────────────────────────────────

    [InitializeOnLoadMethod]
    private static void SyncAppIdFileOnLoad()
    {
        // delayCall: Steamworks.NET también escribe el archivo al abrir (con 480, el juego de
        // ejemplo de Valve) si no existe. Este va después y lo deja con el nuestro.
        EditorApplication.delayCall += SyncAppIdFile;
    }

    private static void SyncAppIdFile()
    {
        if (!SteamConfig.IsAppIdConfigured)
            return;

        string path = Path.Combine(ProjectRoot, AppIdFileName);
        string expected = SteamConfig.AppId.ToString();

        if (File.Exists(path) && File.ReadAllText(path).Trim() == expected)
            return;

        File.WriteAllText(path, expected);
        Debug.Log("[Steam] " + AppIdFileName + " actualizado con el App ID " + expected + ".");
    }

    // ── QA ──────────────────────────────────────────────────────────────

    [MenuItem("Parripollo/Steam/Resetear logros y stats (cuenta activa)")]
    private static void ResetAchievements()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Steam", "Entrá a Play con Steam abierto para resetear los logros de tu cuenta.", "OK");
            return;
        }

        if (EditorUtility.DisplayDialog("Steam",
                "Se borran TODOS los logros y stats de Parrilla 40 de la cuenta de Steam activa (backend: " +
                AchievementService.Backend.Name + "). ¿Seguir?", "Resetear", "Cancelar"))
        {
            AchievementService.ResetAllForQA();
        }
    }

    [MenuItem("Parripollo/Steam/Estado de Steam")]
    private static void LogSteamStatus()
    {
        Debug.Log("[Steam] App ID en SteamConfig: " + (SteamConfig.IsAppIdConfigured ? SteamConfig.AppId.ToString() : "SIN CARGAR (0)") +
                  " | Inicializado: " + SteamBootstrap.IsInitialized +
                  " | Backend de logros: " + AchievementService.Backend.Name +
                  " | Propiedad: " + SteamBootstrap.Ownership);
    }
}
