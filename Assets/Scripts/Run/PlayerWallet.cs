// PlayerWallet.cs
using System;
using UnityEngine;

public class PlayerWallet : MonoBehaviour
{
    [SerializeField] private float startingMoney = 1000f;
    public float Money { get; private set; }
    public event Action<float> OnMoneyChanged;
    
    public static PlayerWallet Instance { get; private set; }

    void Awake()
    {
        Debug.Log("Awake del PlayerWallet");
        
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        Money = startingMoney;
    }

    public bool CanAfford(float amount) => amount <= Money + 0.0001f;

    public bool TrySpend(float amount)
    {
        if (!CanAfford(amount)) return false;
        Money -= amount;
        OnMoneyChanged?.Invoke(Money);
        return true;
    }

    public void Add(float amount)
    {
        Money += amount;
        OnMoneyChanged?.Invoke(Money);
    }
    
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
