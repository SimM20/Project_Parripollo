using UnityEngine;

/// <summary>
/// ScriptableObject that represents a side dish / accompaniment (guarnicion).
/// </summary>
[CreateAssetMenu(fileName = "Side", menuName = "Asado/Side")]
public class SideSO : ScriptableObject
{
    [Header("Display")]
    public string sideName;

    [Tooltip("Clave de Loc para el nombre. Vacía = sideName tal cual.")]
    public string nameKey;

    /// <summary>Nombre para la UI, en el idioma activo.</summary>
    public string DisplayName => Loc.GetOrDefault(nameKey, sideName);

    [Header("Visual")]
    public Sprite sideSprite;

    [Header("Economy")]
    [Tooltip("Purchase cost placeholder - configure in Inspector.")]
    public float purchasePrice; // [PLACEHOLDER]
}
