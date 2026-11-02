using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cuánta salsa le queda a cada frasco. Es DDOL: lo que no se usó en el día pasa al siguiente.
/// La tienda rellena el frasco hasta el tope (<see cref="Refill"/>) y el frasco de la parrilla
/// (<see cref="ToppingDraggable"/>) lo va gastando. Un topping sin registrar está lleno: es el
/// frasco con el que se arranca la run.
/// </summary>
public class ToppingStock : MonoBehaviour
{
    public static ToppingStock Instance { get; private set; }

    public event Action OnStockChanged;

    private readonly Dictionary<ToppingSO, float> sauceByTopping = new Dictionary<ToppingSO, float>();

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

    public float GetSauceAmount(ToppingSO topping)
    {
        if (topping == null) return 0f;
        return sauceByTopping.TryGetValue(topping, out float amount) ? amount : topping.maxSauceAmount;
    }

    public float GetFillRatio(ToppingSO topping)
    {
        if (topping == null || topping.maxSauceAmount <= 0f) return 0f;
        return Mathf.Clamp01(GetSauceAmount(topping) / topping.maxSauceAmount);
    }

    public bool IsFull(ToppingSO topping)
        => topping != null && GetSauceAmount(topping) >= topping.maxSauceAmount;

    public void SetSauceAmount(ToppingSO topping, float amount)
    {
        if (topping == null) return;

        amount = Mathf.Clamp(amount, 0f, topping.maxSauceAmount);
        if (Mathf.Approximately(GetSauceAmount(topping), amount)) return;

        sauceByTopping[topping] = amount;
        OnStockChanged?.Invoke();
    }

    public void Refill(ToppingSO topping)
    {
        if (topping == null) return;
        SetSauceAmount(topping, topping.maxSauceAmount);
    }
}