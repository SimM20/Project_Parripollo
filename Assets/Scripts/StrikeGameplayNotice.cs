using System.Collections;
using UnityEngine;
using TMPro;

public class StrikeGameplayNotice : MonoBehaviour
{
    [SerializeField] private StrikeSystem strikeSystem;
    [SerializeField] private GameObject noticeRoot;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;

    private Coroutine hideRoutine;

    void OnEnable()
    {
        if (strikeSystem != null)
            strikeSystem.OnMaxReached += Show;
        if (noticeRoot != null) noticeRoot.SetActive(false);
    }

    void OnDisable()
    {
        if (strikeSystem != null)
            strikeSystem.OnMaxReached -= Show;
    }

    private void Show()
    {
        if (noticeRoot == null || strikeSystem == null) return;

        if (titleText != null) titleText.text = strikeSystem.NoticeTitle;
        if (bodyText != null) bodyText.text = strikeSystem.NoticeBody;

        noticeRoot.SetActive(true);

        if (hideRoutine != null) StopCoroutine(hideRoutine);
        hideRoutine = StartCoroutine(HideAfterDelay(strikeSystem.NoticeDuration));
    }

    private IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (noticeRoot != null) noticeRoot.SetActive(false);
        hideRoutine = null;
    }
}