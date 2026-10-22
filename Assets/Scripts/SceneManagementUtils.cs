using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneManagementUtils
{
    public static event Action OnSceneLoaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] //This initialize the next function before any scene load!
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnInternalSceneLoaded;
        SceneManager.sceneLoaded += OnInternalSceneLoaded;
    }

    private static void OnInternalSceneLoaded(Scene scene, LoadSceneMode mode) => OnSceneLoaded?.Invoke();

    // Todas las cargas pasan por el fundido de salida de SceneFadeIn: la carga real (y cualquier
    // limpieza que la acompañe) corre recién cuando la pantalla quedó en negro.
    public static void LoadSceneByName(string sceneName)
    {
        if (Application.CanStreamedLevelBeLoaded(sceneName))
            SceneFadeIn.FadeOutThenLoad(() => LoadNow(sceneName));
        else
            Debug.LogError($"scene: '{sceneName}' its not avaiable. Check Build settings.");
    }

    public static void LoadSceneByIndex(int index)
    {
        if (index >= 0 && index < SceneManager.sceneCountInBuildSettings)
            SceneFadeIn.FadeOutThenLoad(() => LoadNow(index));
        else
            Debug.LogError($"scene {index} out of range.");
    }

    public static void ReLoadScene()
    {
        int index = SceneManager.GetActiveScene().buildIndex;
        SceneFadeIn.FadeOutThenLoad(() => LoadNow(index));
    }

    private static void LoadNow(string sceneName)
    {
        GamePause.Reset(); // timeScale / AudioListener.pause persisten entre escenas
        SceneManager.LoadScene(sceneName);
    }

    private static void LoadNow(int index)
    {
        GamePause.Reset();
        SceneManager.LoadScene(index);
    }

    public static string GetCurrentName() => SceneManager.GetActiveScene().name;

    /// <summary>
    /// Reinicia la run entera y arranca de nuevo en el Día 1. Lo usa el botón "Reintentar"
    /// de la pantalla de Derrota Total.
    ///
    /// Hace lo mismo que <see cref="ReturnToMainMenu"/> con los DDOL, y además limpia lo que
    /// ese camino no limpia: la racha de strikes y los ScriptableObject de mejoras.
    /// </summary>
    public static void RestartRun(FoodCatalogSO catalog) =>
        SceneFadeIn.FadeOutThenLoad(() => RestartRunNow(catalog));

    // La limpieza va al final del fundido, junto con la carga: la escena vieja sigue viva mientras
    // se oscurece y no puede quedarse sin sus singletons.
    private static void RestartRunNow(FoodCatalogSO catalog)
    {
        CoolerSystem.PrepareForNewGame();

        // DestroyImmediate y no Destroy: acá se carga GameScene en el mismo frame, y GameScene
        // trae sus propias copias de los cuatro singletons. Con destrucción diferida los
        // viejos siguen vivos durante el Awake de los nuevos, los nuevos se autodestruyen por
        // el guard de singleton, y recién después mueren los viejos: quedan las cuatro
        // Instance en null y la escena arranca rota.
        // ReturnToMainMenu no tiene el problema porque MainMenuScene no trae copias.
        if (PlayerWallet.Instance != null) UnityEngine.Object.DestroyImmediate(PlayerWallet.Instance.gameObject);
        if (CoalConsumptionTracker.Instance != null) UnityEngine.Object.DestroyImmediate(CoalConsumptionTracker.Instance.gameObject);
        if (CoolerSystem.Instance != null) UnityEngine.Object.DestroyImmediate(CoolerSystem.Instance.gameObject);
        if (ToppingStock.Instance != null) UnityEngine.Object.DestroyImmediate(ToppingStock.Instance.gameObject);

        StrikeSystem.ResetStreak();
        DayStats.ResetDay();
        RunStateReset.ResetRunState(catalog);
        RadioStation.Shutdown();

        LoadNow("GameScene");
    }

    public static void ReturnToMainMenu() => SceneFadeIn.FadeOutThenLoad(ReturnToMainMenuNow);

    private static void ReturnToMainMenuNow()
    {
        CoolerSystem.PrepareForNewGame(); // clears backup so next new game starts clean
        if (PlayerWallet.Instance != null) UnityEngine.Object.Destroy(PlayerWallet.Instance.gameObject);
        if (CoalConsumptionTracker.Instance != null) UnityEngine.Object.Destroy(CoalConsumptionTracker.Instance.gameObject);
        if (CoolerSystem.Instance != null) UnityEngine.Object.Destroy(CoolerSystem.Instance.gameObject);
        if (ToppingStock.Instance != null) UnityEngine.Object.Destroy(ToppingStock.Instance.gameObject);

        // La racha de noches es un estatico: sobrevive el cambio de escena igual que los DDOL,
        // asi que hay que limpiarla a mano o la run siguiente arranca con la racha de la anterior
        // (y si venia en el tope, pierde en su primer EndScene).
        StrikeSystem.ResetStreak();
        DayStats.ResetDay();
        RadioStation.Shutdown(); // DDOL: sin esto la canción seguiría (muda) en la run siguiente

        LoadNow("MainMenuScene");
    }
}