using UnityEngine;

[CreateAssetMenu(fileName = "CoalData", menuName = "Asado/Coal Type")]
public class CoalSO : ItemDataSO
{
    [SerializeField] private GameObject _coalPrefab;

    [Header("Visual Carbón")]
    [SerializeField] private Sprite _coalSprite;
    [SerializeField] private Vector3 _visualOffset;

    [Header("Combustión")]
    [SerializeField] private float _maxBurnTime = 60f;

    [Tooltip("Poder calórico que emite hacia la carne.")]
    [SerializeField] private float _heatPower = 1.5f;

    public GameObject coalPrefab => _coalPrefab;
    public Sprite coalSprite => _coalSprite;
    public Vector3 visualOffset => _visualOffset;
    public float maxBurnTime => _maxBurnTime;
    public float heatPower => _heatPower;

    void OnValidate() => category = ItemType.Coal;
}
