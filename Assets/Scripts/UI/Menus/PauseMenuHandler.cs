using UnityEngine;
using UnityEngine.UI;

public class PauseMenuHandler : MonoBehaviour
{
    [SerializeField] private Slider sFXSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Button endGameButtton;
    [SerializeField] private Button backToMenuButtton;
    [SerializeField] private Button restartButtton;

    [Header("Opciones")]
    [SerializeField] private Button optionsButton;
    [SerializeField] private OptionsMenuPanel optionsPanel;
    [Tooltip("Lo que se oculta mientras el panel de opciones está abierto (sliders y botones).")]
    [SerializeField] private GameObject[] hideWhileOptionsOpen;

    private void Awake()
    {
        // Slider bindings
        sFXSlider?.onValueChanged.AddListener(ChangeSFXValue);
        musicSlider?.onValueChanged.AddListener(ChangeMusicValue);

        // Button bindings
        endGameButtton?.onClick.AddListener(EndGame);
        backToMenuButtton?.onClick.AddListener(BackToMenu);
        restartButtton?.onClick.AddListener(RestartDay);
        optionsButton?.onClick.AddListener(OpenOptions);

        if (optionsPanel != null)
            optionsPanel.OnClosed += HandleOptionsClosed;
    }

    private void OnDestroy()
    {
        if (optionsPanel != null)
            optionsPanel.OnClosed -= HandleOptionsClosed;
    }

    /// <summary>Cierra el panel de opciones si está abierto (UIManager lo llama al despausar).</summary>
    public void CloseOptions()
    {
        if (optionsPanel != null)
            optionsPanel.Close();
    }

    // El menú sigue visible un instante mientras sale (PauseMenuTransition): un Submit de
    // teclado/gamepad en ese lapso no tiene que disparar nada.
    private static bool Closing => !GamePause.IsMenuPaused;

    private void OpenOptions()
    {
        if (optionsPanel == null || Closing) return;

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

    private void ChangeSFXValue(float value)
    {
        if (!sFXSlider) return;
        AudioListener.volume = value;
    }

    private void ChangeMusicValue(float value)
    {
        if (!musicSlider) return;
        return;
        // Need to implement mixer groups! It cant be used yet.
    }

    private void EndGame()
    {
        if (!endGameButtton || Closing) return;

        // Cierre anticipado a pedido del jugador: suma a la racha de noches de la run.
        GameManager.Instance?.EndNightEarly();
    }

    private void BackToMenu()
    {
        if (!backToMenuButtton || Closing) return;
        SceneManagementUtils.ReturnToMainMenu();
    }

    private void RestartDay()
    {
        if (!restartButtton || Closing) return;
        UIManager.Instance?.UnPauseGame();
    }
}
