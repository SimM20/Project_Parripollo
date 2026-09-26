using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Menú principal: botones (Jugar / Opciones / Salir), fade-in al cargar y número de versión.
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

    [Header("Opciones")]
    [SerializeField] private OptionsMenuPanel optionsPanel;
    [Tooltip("Lo que se oculta mientras el panel de opciones está abierto (botones, subtítulo...).")]
    [SerializeField] private GameObject[] hideWhileOptionsOpen;

    private void Start()
    {
        if (versionText != null)
            versionText.text = "v" + Application.version;

        if (optionsPanel != null)
        {
            optionsPanel.OnClosed += HandleOptionsClosed;
            optionsPanel.gameObject.SetActive(false);
        }

        if (fadeGroup != null && fadeInSeconds > 0f)
            StartCoroutine(FadeIn());
    }

    private void OnDestroy()
    {
        if (optionsPanel != null)
            optionsPanel.OnClosed -= HandleOptionsClosed;
    }

    public void StartNewGame() => SceneManagementUtils.LoadSceneByName("GameScene");

    public void OpenOptions()
    {
        if (optionsPanel == null) return;

        SetMenuVisible(false);
        optionsPanel.Open();
    }

    private void HandleOptionsClosed() => SetMenuVisible(true);

    private void SetMenuVisible(bool visible)
    {
        if (hideWhileOptionsOpen == null) return;
        foreach (GameObject go in hideWhileOptionsOpen)
            if (go != null) go.SetActive(visible);
    }

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
