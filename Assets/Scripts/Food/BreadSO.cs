using UnityEngine;

/// <summary>
/// ScriptableObject that represents a bread type used for sandwiches.
/// </summary>
[CreateAssetMenu(fileName = "Bread", menuName = "Asado/Bread")]
public class BreadSO : ScriptableObject
{
    [Header("Display")]
    public string breadName;

    [Header("Visual")]
    public Sprite breadSprite;

    [Header("Economy")]
    [Tooltip("Purchase cost placeholder - configure in Inspector.")]
    public float purchasePrice; // [PLACEHOLDER]
}
