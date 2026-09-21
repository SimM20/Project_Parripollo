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

    public static void LoadSceneByName(string sceneName)
    {
        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            GamePause.Reset(); // timeScale / AudioListener.pause persisten entre escenas
            SceneManager.LoadScene(sceneName);
        }
        else
            Debug.LogError($"scene: '{sceneName}' its not avaiable. Check Build settings.");
    }

    public static void LoadSceneByIndex(int index)
    {
        if (index >= 0 && index < SceneManager.sceneCountInBuildSettings)
        {
            GamePause.Reset();
            SceneManager.LoadScene(index);
        }
        else
            Debug.LogError($"scene {index} out of range.");
    }

    public static void ReLoadScene()
    {
        GamePause.Reset();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public static string GetCurrentName() => SceneManager.GetActiveScene().name;

    /// <summary>
    /// Reinicia la run entera y arranca de nuevo en el Día 1. Lo usa el botón "Reintentar"
    /// de la pantalla de Derrota Total.
    ///
    /// Hace lo mismo que <see cref="ReturnToMainMenu"/> con los DDOL, y además limpia lo que
    /// ese camino no limpia: la racha de strikes y los ScriptableObject de mejoras.
    /// </summary>
    public static void RestartRun(FoodCatalogSO catalog)
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

        LoadSceneByName("GameScene");
    }

    public static void ReturnToMainMenu()
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

        LoadSceneByName("MainMenuScene");
    }
}