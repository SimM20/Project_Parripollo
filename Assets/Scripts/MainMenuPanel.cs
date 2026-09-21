using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Menú principal: botones (Jugar / Salir), fade-in al cargar y número de versión.
/// El fade usa tiempo unscaled: la escena arranca siempre con timeScale = 1
/// (SceneManagementUtils resetea GamePause), pero no depende de eso.
/// </summary>
public class MainMenuPanel : MonoBehaviour
{
    [Header("Intro")]
    [Tooltip("CanvasGroup del menú completo. Vacío = sin fade.")]
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] private float fadeInSeconds = 0.6f;

    [Header("Versión")]
    [Tooltip("Muestra Application.version (Player Settings → Version). Vacío = no se toca.")]
    [SerializeField] private TMP_Text versionText;

    private void Start()
    {
        if (versionText != null)
            versionText.text = "v" + Application.version;

        if (fadeGroup != null && fadeInSeconds > 0f)
            StartCoroutine(FadeIn());
    }

    public void StartNewGame() => SceneManagementUtils.LoadSceneByName("GameScene");

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator FadeIn()
    {
        fadeGroup.alpha = 0f;
        fadeGroup.interactable = false;

        float t = 0f;
        while (t < fadeInSeconds)
        {
            t += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Clamp01(t / fadeInSeconds);
            yield return null;
        }

        fadeGroup.alpha = 1f;
        fadeGroup.interactable = true;
    }
}
