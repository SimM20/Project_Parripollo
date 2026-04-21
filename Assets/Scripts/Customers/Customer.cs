using UnityEngine;

public class Customer
{
    public Order order;

    public float patience;
    public float maxPatience;

    public bool IsAngry => patience <= 0f;

    public void Init(Order newOrder, float patienceValue)
    {
        order = newOrder;
        maxPatience = patienceValue;
        patience = patienceValue;
    }

    public void UpdatePatience(float deltaTime)
    {
        patience -= deltaTime;
    }
}