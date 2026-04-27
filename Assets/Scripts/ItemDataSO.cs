using UnityEngine;
using UnityEngine.Serialization;

public abstract class ItemDataSO : ScriptableObject
{
    [FormerlySerializedAs("cutName")]
    public string itemName;

    [FormerlySerializedAs("price")]
    public float basePrice;

    public ItemType category;
}