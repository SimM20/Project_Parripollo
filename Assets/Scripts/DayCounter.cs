using UnityEngine;

public class DayCounter : MonoBehaviour
{
    public static DayCounter Instance { get; private set; }

    public int CurrentDay { get; private set; } = 1;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void AdvanceDay() => CurrentDay++;

    public void ResetRun() => CurrentDay = 1;
}