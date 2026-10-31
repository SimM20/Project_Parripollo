using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Menú principal: botones (Jugar / Opciones / Salir) y número de versión.
/// La entrada del menú al cargar la escena la hace <see cref="MainMenuIntro"/>.
/// </summary>
public class MainMenuPanel : MonoBehaviour
{
    [Header("Versión")]
    [Tooltip("Muestra Application.version (Player Settings → Version). Vacío = no se toca.")]
    [SerializeField] private TMP_Text versionText;

    [Header("Opciones")]
    [SerializeField] private OptionsMenuPanel optionsPanel;
    [Tooltip("Lo que se oculta mientras el panel de opciones está abierto (botones, subtítulo...).")]
    [SerializeField] private GameObject[] hideWhileOptionsOpen;

    private GameObject focusBeforeOptions;

    private void Start()
    {
        if (versionText != null)
            versionText.text = "v" + Application.version;

        if (optionsPanel != null)
        {
            optionsPanel.OnClosed += HandleOptionsClosed;
            optionsPanel.gameObject.SetActive(false);
        }
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

        focusBeforeOptions = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        SetMenuVisible(false);
        optionsPanel.Open();
    }

    private void HandleOptionsClosed()
    {
        SetMenuVisible(true);

        // El cursor vuelve al botón desde el que se entró (Opciones), no al primero del menú.
        if (focusBeforeOptions != null && focusBeforeOptions.activeInHierarchy && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(focusBeforeOptions);
    }

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
}
