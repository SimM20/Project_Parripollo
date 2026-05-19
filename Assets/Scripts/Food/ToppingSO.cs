using UnityEngine;

[CreateAssetMenu(fileName = "Topping", menuName = "Asado/Topping")]
public class ToppingSO : ScriptableObject
{
    [Header("Display")]
    public string toppingName;

    [Header("Visual")]
    public Sprite toppingSprite;

    [Header("Sauce Capacity")]
    [Tooltip("Total amount of sauce this container holds (arbitrary units).")]
    public float maxSauceAmount = 100f;

    [Tooltip("Amount of sauce consumed per second while pouring.")]
    public float sauceDrainRate = 10f;

    [Header("Economy")]
    [Tooltip("Purchase cost placeholder - configure in Inspector.")]
    public float purchasePrice;
}
