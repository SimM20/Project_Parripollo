using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainMenuPanel : MonoBehaviour
{
    public void StartNewGame() => SceneManagementUtils.LoadSceneByName("GameScene");

    public void ExitGame() => Application.Quit();
}
