using UnityEngine;

/// <summary>
/// ScriptableObject that represents a topping (condiment) added to a dish.
/// </summary>
[CreateAssetMenu(fileName = "Topping", menuName = "Asado/Topping")]
public class ToppingSO : ScriptableObject
{
    [Header("Display")]
    public string toppingName;

    [Header("Visual")]
    public Sprite toppingSprite;

    [Header("Economy")]
    [Tooltip("Purchase cost placeholder - configure in Inspector.")]
    public float purchasePrice; // [PLACEHOLDER]
}
