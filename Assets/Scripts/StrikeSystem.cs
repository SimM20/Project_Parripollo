using System;
using UnityEngine;

public class StrikeSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CustomerSystem customerSystem;
    [SerializeField] private StrikeConfigSO config;

    [Header("Events")]
    public Action<int, int> OnStrikeAdded;   // (current, max)
    public Action OnMaxReached;

    public int CurrentStrikes { get; private set; } = 0;
    public int MaxStrikes => config != null ? config.maxStrikes : 3;
    public float NoticeDuration => config != null ? config.gameplayNoticeDuration : 5f;
    public string NoticeTitle => config != null ? config.noticeTitle : "";
    public string NoticeBody => config != null ? config.noticeBody : "";

    private bool maxReached = false;

    void Start()
    {
        if (customerSystem == null)
        {
            Debug.LogError("[StrikeSystem] Falta CustomerSystem asignado.");
            return;
        }

        if (config == null)
        {
            Debug.LogError("[StrikeSystem] Falta StrikeConfigSO asignado.");
            return;
        }

        StrikeSessionFlag.Instance?.Reset();
        CurrentStrikes = 0;
        maxReached = false;

        customerSystem.OnCustomerLostByPatience += HandleCustomerLostByPatience;

        UIManager.Instance?.SetStrikes(CurrentStrikes, MaxStrikes);
    }

    void OnDestroy()
    {
        if (customerSystem != null)
            customerSystem.OnCustomerLostByPatience -= HandleCustomerLostByPatience;
    }

    private void HandleCustomerLostByPatience(Customer customer)
    {
        if (maxReached) return;

        CurrentStrikes = Mathf.Min(CurrentStrikes + 1, MaxStrikes);
        OnStrikeAdded?.Invoke(CurrentStrikes, MaxStrikes);

        UIManager.Instance?.SetStrikes(CurrentStrikes, MaxStrikes);
        UIManager.Instance?.TriggerStrikeShake();

        AudioManager.Instance?.PlayStrikeSound();

        if (CurrentStrikes >= MaxStrikes)
            HandleMaxReached();
    }

    private void HandleMaxReached()
    {
        maxReached = true;
        customerSystem.BlockNewSpawns();
        StrikeSessionFlag.Instance?.MarkClosedByStrikes();
        OnMaxReached?.Invoke();
    }
}