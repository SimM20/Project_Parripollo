using UnityEngine;
using UnityEngine.UI;

public class PauseMenuHandler : MonoBehaviour
{
    [SerializeField] private Slider sFXSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Button endGameButtton;
    [SerializeField] private Button backToMenuButtton;
    [SerializeField] private Button restartButtton;

    private void Awake()
    {
        // Slider bindings
        sFXSlider?.onValueChanged.AddListener(ChangeSFXValue);
        musicSlider?.onValueChanged.AddListener(ChangeMusicValue);

        // Button bindings
        endGameButtton?.onClick.AddListener(EndGame);
        backToMenuButtton?.onClick.AddListener(BackToMenu);
        restartButtton?.onClick.AddListener(RestartDay);
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
        return;
        // Need to implement the end game.
    }

    private void BackToMenu()
    {
        if (!backToMenuButtton) return;
        SceneManagementUtils.LoadSceneByName("MainMenuScene");
    }

    private void RestartDay()
    {
        if (!restartButtton) return;
        UIManager.Instance?.UnPauseGame();
    }
}
