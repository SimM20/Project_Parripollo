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
            SceneManager.LoadScene(sceneName);
        else
            Debug.LogError($"scene: '{sceneName}' its not avaiable. Check Build settings.");
    }

    public static void LoadSceneByIndex(int index)
    {
        if (index >= 0 && index < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(index);
        else
            Debug.LogError($"scene {index} out of range.");
    }

    public static void ReLoadScene() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

    public static string GetCurrentName() => SceneManager.GetActiveScene().name;
}
