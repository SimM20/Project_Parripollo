using UnityEngine;

[CreateAssetMenu(fileName = "CoalData", menuName = "Asado/Coal Type")]
public class CoalSO : ItemDataSO
{
    public GameObject coalPrefab;

    [Header("Visual Carbón")]
    public Sprite coalSprite;

    [Header("Combustión")]
    [Tooltip("Tiempo en segundos que tarda en volverse ceniza.")]
    public float maxBurnTime = 60f;

    [Tooltip("Poder calórico que emite hacia la carne.")]
    public float heatPower = 1.5f;

    void OnValidate() => category = ItemType.Coal;
}
