// CoalStock.cs
using System;
using UnityEngine;

public class CoalStock : MonoBehaviour
{
    [SerializeField] private int startingUnits = 0;
    public int Units { get; private set; }
    public event Action<int> OnChanged;

    void Awake() { Units = startingUnits; }

    public void Add(int amount)
    {
        if (amount <= 0) return;
        Units += amount;
        OnChanged?.Invoke(Units);
    }

    public bool TryConsume(int amount)
    {
        if (amount <= 0 || Units < amount) return false;
        Units -= amount;
        OnChanged?.Invoke(Units);
        return true;
    }
}