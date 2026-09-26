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

    private void OpenOptions()
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
        if (!endGameButtton) return;

        // Cierre anticipado a pedido del jugador: suma a la racha de noches de la run.
        GameManager.Instance?.EndNightEarly();
    }

    private void BackToMenu()
    {
        if (!backToMenuButtton) return;
        SceneManagementUtils.ReturnToMainMenu();
    }

    private void RestartDay()
    {
        if (!restartButtton) return;
        UIManager.Instance?.UnPauseGame();
    }
}
