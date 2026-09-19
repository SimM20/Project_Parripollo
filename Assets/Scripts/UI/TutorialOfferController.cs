using UnityEngine;

public class TutorialOfferController : MonoBehaviour
{
    [SerializeField] private GameObject offerPanel;

    private static bool hasAskedThisSession = false; // Only ask once per session

    void Start()
    {
        // Don't ask if we already asked during this play session
        // or if we are not in the GameScene.
        if (hasAskedThisSession || SceneManagementUtils.GetCurrentName() != "GameScene")
        {
            if (offerPanel != null) offerPanel.SetActive(false);
            return;
        }

        if (offerPanel != null)
        {
            offerPanel.SetActive(true);
            GamePause.SetDialogPaused(true);
        }
    }

    public void OnSelectYes()
    {
        hasAskedThisSession = true;
        GamePause.SetDialogPaused(false);
        SceneManagementUtils.LoadSceneByName("TutorialScene");
    }

    public void OnSelectNo()
    {
        hasAskedThisSession = true;
        GamePause.SetDialogPaused(false);
        if (offerPanel != null)
        {
            offerPanel.SetActive(false);
        }
    }
}
