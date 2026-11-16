/// <summary>
/// Datos fijos de la integración con Steam. El App ID vive acá y en ningún otro lado: el
/// bootstrap lo usa para <c>RestartAppIfNecessary</c> y el editor lo copia a <c>steam_appid.txt</c>
/// (raíz del proyecto, solo para desarrollo). Ver STEAMWORKS.md en la raíz del repo.
/// </summary>
public static class SteamConfig
{
    /// <summary>
    /// App ID de Parrilla 40 en Steamworks (el número de la URL de la página de la tienda).
    /// 0 = todavía sin cargar: el juego corre igual, pero no reinicia por Steam y una build
    /// de release falla a propósito (<c>SteamBuildGuard</c>) hasta que se complete.
    /// </summary>
    public const uint AppId = 5636520;

    /// <summary>
    /// Build de release sin Steam (no abierto, sin licencia o sin DLL): mensaje y se cierra.
    /// En el editor y en las Development Build nunca se cierra: el juego sigue sin logros.
    /// </summary>
    public static readonly bool QuitWhenSteamUnavailableInRelease = true;

    public static bool IsAppIdConfigured => AppId != 0;
}
