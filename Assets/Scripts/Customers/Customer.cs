using UnityEngine;

public class Customer
{
    public CustomerType type;
    public Order order;

    public float patience;
    public float maxPatience;

    // Slot visual en pantalla (0..maxSimultaneous-1)
    public int slotIndex = -1;

    public bool IsAngry => patience <= 0f;

    /// <summary>
    /// Indica si el cliente está mostrando actualmente su reacción/feedback de 4 segundos.
    /// Mientras esté en feedback, su paciencia se congela y no admite nuevas interacciones.
    /// </summary>
    public bool IsInFeedback { get; private set; }

    /// <summary>
    /// Indica si la propina quedó anulada por cambio de pedido tras informar faltante de corte.
    /// </summary>
    public bool IsTipAnulada { get; set; }

    public void StartFeedback()
    {
        IsInFeedback = true;
    }

    public void EndFeedback()
    {
        IsInFeedback = false;
    }

    public void Init(CustomerType customerType, Order newOrder, float patienceValue, int assignedSlot)
    {
        type = customerType;
        order = newOrder;

        maxPatience = patienceValue;
        patience = patienceValue;

        slotIndex = assignedSlot;
        IsInFeedback = false;
        IsTipAnulada = false;
    }

    public void UpdatePatience(float deltaTime)
    {
        if (IsInFeedback) return;
        patience -= deltaTime;
    }

    public float Patience01 => maxPatience <= 0.0001f ? 0f : Mathf.Clamp01(patience / maxPatience);
}