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

    public void Init(CustomerType customerType, Order newOrder, float patienceValue, int assignedSlot)
    {
        type = customerType;
        order = newOrder;

        maxPatience = patienceValue;
        patience = patienceValue;

        slotIndex = assignedSlot;
    }

    public void UpdatePatience(float deltaTime)
    {
        patience -= deltaTime;
    }

    public float Patience01 => maxPatience <= 0.0001f ? 0f : Mathf.Clamp01(patience / maxPatience);
}