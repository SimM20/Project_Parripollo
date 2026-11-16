// Mismo criterio que Steamworks.NET: fuera de las plataformas de escritorio (o con
// DISABLESTEAMWORKS en los Scripting Define Symbols, para una build sin Steam de una muestra o
// para los profes) el wrapper no compila y este bootstrap queda vacío.
#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>
/// Arranque y apagado de Steam. Se crea solo antes de cargar la primera escena (no hay que
/// ponerlo en ninguna), sobrevive a los cambios de escena y es el único que llama a
/// <c>SteamAPI.Init</c>, <c>RunCallbacks</c> y <c>Shutdown</c>.
///
/// Sin Steam abierto el juego sigue funcionando: en el editor y en las Development Build solo
/// se avisa por consola y los logros quedan en el backend local. En una build de release sin
/// Steam se muestra un mensaje y el juego se cierra (<see cref="SteamConfig.QuitWhenSteamUnavailableInRelease"/>).
///
/// El resto del juego no toca Steamworks directamente: pregunta <see cref="IsInitialized"/>.
/// </summary>
[DisallowMultipleComponent]
public class SteamBootstrap : MonoBehaviour
{
    public static SteamBootstrap Instance { get; private set; }

    /// <summary>Steam respondió y se pueden usar las APIs (logros, stats, overlay).</summary>
    public static bool IsInitialized { get; private set; }

    /// <summary>
    /// Último aviso antes de apagar Steam: el que tenga algo pendiente (stats de los logros) lo
    /// guarda acá. No llamar a Steamworks desde un OnDestroy propio: puede llegar tarde.
    /// </summary>
    public static event Action OnBeforeShutdown;

    /// <summary>
    /// Cómo tiene el juego la cuenta de Steam activa. Solo informativo (logs y soporte): no se
    /// bloquea a nadie por Family Sharing ni por fin de semana gratis, Steam ya lo permite.
    /// </summary>
    public static OwnershipInfo Ownership { get; private set; }

    public struct OwnershipInfo
    {
        public bool subscribed;
        public bool familySharing;
        public bool freeWeekend;
        public override string ToString() =>
            "suscripto=" + subscribed + " | family sharing=" + familySharing + " | fin de semana gratis=" + freeWeekend;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        // El Editor puede entrar a Play sin domain reload: nada de la sesión anterior sirve.
        Instance = null;
        IsInitialized = false;
        OnBeforeShutdown = null;
        Ownership = default;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateOnStartup()
    {
        if (Instance != null) return;

        var go = new GameObject("[SteamBootstrap]");
        DontDestroyOnLoad(go);
        go.AddComponent<SteamBootstrap>(); // Awake corre acá mismo: Steam queda listo antes de la primera escena
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

#if DISABLESTEAMWORKS
        Debug.Log("[Steam] Steamworks deshabilitado en esta plataforma o build (DISABLESTEAMWORKS): el juego corre sin Steam.");
#else
        Initialize();
#endif
    }

#if !DISABLESTEAMWORKS
    // Hay que guardar la referencia: si el GC se lleva el delegado, Steam llama a basura.
    private SteamAPIWarningMessageHook_t warningHook;

    private void Initialize()
    {
        if (!Packsize.Test())
        {
            Debug.LogError("[Steam] Steamworks.NET está compilado para otra plataforma (Packsize). Steam queda apagado.");
            HandleSteamUnavailable("Packsize");
            return;
        }

        if (!DllCheck.Test())
        {
            Debug.LogError("[Steam] La versión de steam_api64.dll no coincide con la de Steamworks.NET. Steam queda apagado.");
            HandleSteamUnavailable("DllCheck");
            return;
        }

#if !UNITY_EDITOR
        // Si el .exe se abrió por fuera de Steam, Steam lo vuelve a lanzar como corresponde
        // (con overlay, logros y su DRM si lo tiene) y esta copia se cierra.
        // Con steam_appid.txt junto al .exe (Development Build) devuelve false: así se prueba.
        if (SteamConfig.IsAppIdConfigured)
        {
            try
            {
                if (SteamAPI.RestartAppIfNecessary(new AppId_t(SteamConfig.AppId)))
                {
                    Debug.Log("[Steam] El juego no se abrió desde Steam: se relanza por Steam y esta copia se cierra.");
                    Application.Quit();
                    return;
                }
            }
            catch (DllNotFoundException e)
            {
                Debug.LogError("[Steam] No se encontró steam_api64.dll junto al juego: " + e.Message);
                HandleSteamUnavailable("steam_api64.dll");
                return;
            }
        }
        else
        {
            Debug.LogError("[Steam] SteamConfig.AppId está en 0: no se puede verificar que el juego se abrió desde Steam.");
        }
#endif

        ESteamAPIInitResult result;
        string error;

        try
        {
            result = SteamAPI.InitEx(out error);
        }
        catch (DllNotFoundException e)
        {
            Debug.LogWarning("[Steam] No se pudo cargar steam_api64.dll: " + e.Message);
            HandleSteamUnavailable("steam_api64.dll");
            return;
        }

        if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
        {
            // Lo normal en el editor sin Steam abierto o sin steam_appid.txt en la raíz.
            Debug.LogWarning("[Steam] No se inicializó (" + result + "): " + error +
                             " — El juego sigue sin Steam (logros solo por consola).");
            HandleSteamUnavailable(error);
            return;
        }

        IsInitialized = true;

        if (Debug.isDebugBuild)
        {
            warningHook = OnSteamWarning;
            SteamClient.SetWarningMessageHook(warningHook);
        }

        CheckOwnership();

        Debug.Log("[Steam] Inicializado. App ID " + SteamUtils.GetAppID() +
                  " | Usuario: " + SteamFriends.GetPersonaName() + " | " + Ownership);
    }

    /// <summary>
    /// Verificación de propiedad. <c>SteamAPI.Init</c> ya falla si la cuenta no tiene licencia,
    /// así que esto es una segunda línea: en release, sin suscripción, el juego se cierra igual
    /// que sin Steam. Family Sharing y fin de semana gratis son licencias válidas.
    /// </summary>
    private void CheckOwnership()
    {
        Ownership = new OwnershipInfo
        {
            subscribed = SteamApps.BIsSubscribed(),
            familySharing = SteamApps.BIsSubscribedFromFamilySharing(),
            freeWeekend = SteamApps.BIsSubscribedFromFreeWeekend(),
        };

        if (!Ownership.subscribed)
        {
            Debug.LogError("[Steam] La cuenta activa no tiene licencia de este App ID.");
            HandleSteamUnavailable("sin licencia");
        }
    }

    private void Update()
    {
        // Sin esto no llegan los callbacks (logro desbloqueado, overlay, etc.).
        if (IsInitialized)
            SteamAPI.RunCallbacks();
    }

    [AOT.MonoPInvokeCallback(typeof(SteamAPIWarningMessageHook_t))] // por si algún día se compila con IL2CPP
    private static void OnSteamWarning(int severity, System.Text.StringBuilder text)
    {
        Debug.LogWarning("[Steam] " + text);
    }
#endif

    /// <summary>
    /// Steam no está disponible. En el editor y en las Development Build se sigue jugando; en
    /// release se avisa y se cierra, que es lo que espera Steam de un juego que exige su cliente.
    /// </summary>
    private static void HandleSteamUnavailable(string reason)
    {
        if (Application.isEditor || Debug.isDebugBuild || !SteamConfig.QuitWhenSteamUnavailableInRelease)
            return;

        ShowFatalMessage(
            "Parrilla 40 necesita Steam abierto y una copia del juego en tu biblioteca.\n" +
            "Parrilla 40 needs Steam running and a copy of the game in your library.\n\n(" + reason + ")");
        Application.Quit();
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    private static void ShowFatalMessage(string message) => MessageBox(IntPtr.Zero, message, "Parrilla 40", 0x10); // MB_ICONERROR
#else
    private static void ShowFatalMessage(string message) => Debug.LogError("[Steam] " + message);
#endif

    private void OnApplicationQuit() => Shutdown();

    private void OnDestroy()
    {
        if (Instance != this) return;

        Shutdown();
        Instance = null;
    }

    private static void Shutdown()
    {
        if (!IsInitialized) return;

        try
        {
            OnBeforeShutdown?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

#if !DISABLESTEAMWORKS
        SteamAPI.Shutdown();
#endif
        IsInitialized = false;
        Debug.Log("[Steam] Apagado.");
    }
}
