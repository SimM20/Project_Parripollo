using System;
using UnityEngine;

/// <summary>
/// Minimal day cycle manager. Tracks revenue and savings, fires day-end/start events.
/// Persists stock state in runtime memory only (no disk save).
/// No UI is created here — this is the logic layer only.
/// </summary>
public class DayManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CoolerSystem coolerSystem;
    [SerializeField] private CoalStockSystem coalStockSystem;

    [Header("Starting Economy")]
    [Tooltip("Player's starting savings. Configure in Inspector.")]
    [SerializeField] private float startingSavings = 500f; // [PLACEHOLDER]

    // Runtime state
    private float currentSavings;
    private float dayRevenue;
    private int currentDay = 1;

    // Events
    public event Action<int> OnDayStarted;   // int = day number
    public event Action<int, float> OnDayEnded; // int = day, float = revenue

    // ── Properties ──────────────────────────────────────────────────────────

    public float CurrentSavings => currentSavings;
    public float DayRevenue => dayRevenue;
    public float AvailableMoney => currentSavings;
    public int CurrentDay => currentDay;

    // ── Lifecycle ───────────────────────────────────────────────────────────

    void Awake()
    {
        currentSavings = startingSavings;
        dayRevenue = 0f;
    }

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Adds revenue from a delivered order. Called by GameManager on successful serve.
    /// </summary>
    public void AddRevenue(float amount)
    {
        if (amount <= 0f) return;
        dayRevenue += amount;
        currentSavings += amount;
        Debug.Log("[DayManager] Ingreso: $" + amount + " | Total del dia: $" + dayRevenue + " | Ahorros: $" + currentSavings);
    }

    /// <summary>
    /// Deducts money (used by ShopSystem for purchases).
    /// Returns true if the deduction succeeded.
    /// </summary>
    public bool TrySpend(float amount)
    {
        if (amount <= 0f) return true;
        if (currentSavings < amount) return false;
        currentSavings -= amount;
        return true;
    }

    /// <summary>
    /// Ends the current day. Fires OnDayEnded and resets daily revenue.
    /// Stock is NOT reset here — CoolerSystem and CoalStockSystem carry leftover stock forward.
    /// </summary>
    public void EndDay()
    {
        Debug.Log("[DayManager] Fin del dia " + currentDay + ". Recaudacion: $" + dayRevenue);
        OnDayEnded?.Invoke(currentDay, dayRevenue);
        dayRevenue = 0f;
    }

    /// <summary>
    /// Starts a new day. Increments day counter and fires OnDayStarted.
    /// </summary>
    public void StartNewDay()
    {
        currentDay++;
        Debug.Log("[DayManager] Inicio del dia " + currentDay + ". Ahorros: $" + currentSavings);
        OnDayStarted?.Invoke(currentDay);
    }

    [ContextMenu("Debug: Print Day State")]
    public void DebugPrint()
    {
        Debug.Log("[DayManager] Dia: " + currentDay
            + " | Ingresos del dia: $" + dayRevenue
            + " | Ahorros: $" + currentSavings);
    }
}
