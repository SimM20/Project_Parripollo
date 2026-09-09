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

    [Header("Coal Sprites")]
    [SerializeField] public CoalSprites coalSprites;

    public GameObject coalPrefab => _coalPrefab;
    public Sprite coalSprite => _coalSprite;
    public Vector3 visualOffset => _visualOffset;
    public float maxBurnTime => _maxBurnTime;
    public float heatPower => _heatPower;
    
    [Tooltip("Cuántas unidades aporta una bolsa")]
    public int unitsPerBag = 1;

    /// <summary>Usado por UpgradeSO para aplicar mejoras de combustion.</summary>
    public void SetMaxBurnTime(float value) => _maxBurnTime = Mathf.Max(0f, value);

    void OnValidate() => category = ItemType.Coal;

    public Sprite GetSpriteForState(CoalStates state) { return coalSprites.GetSpriteForState(state); }
}
