using UnityEngine;

/// <summary>
/// ScriptableObject that represents a side dish / accompaniment (guarnicion).
/// </summary>
[CreateAssetMenu(fileName = "Side", menuName = "Asado/Side")]
public class SideSO : ScriptableObject
{
    [Header("Display")]
    public string sideName;

    [Header("Visual")]
    public Sprite sideSprite;

    [Header("Economy")]
    [Tooltip("Purchase cost placeholder - configure in Inspector.")]
    public float purchasePrice; // [PLACEHOLDER]
}
