using UnityEngine;
using UnityEngine.Serialization;

public abstract class ItemDataSO : ScriptableObject
{
    [FormerlySerializedAs("cutName")]
    public string itemName;

    [Tooltip("Clave de Loc para el nombre que ve el jugador. Vacía = se muestra itemName tal cual " +
             "(los cortes de carne no se traducen).")]
    public string nameKey;

    [FormerlySerializedAs("price")]
    public float basePrice;

    public ItemType category;

    /// <summary>Nombre para la UI, en el idioma activo. No usar itemName para mostrar.</summary>
    public string DisplayName => Loc.GetOrDefault(nameKey, itemName);
}