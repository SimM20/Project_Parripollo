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

    [Tooltip("Fracción del maxBurnTime a partir de la cual se muestra el sprite de Prendido Fase 2.")]
    [Range(0f, 1f)] [SerializeField] private float _phase2At = 1f / 3f;

    [Tooltip("Fracción del maxBurnTime a partir de la cual se muestra el sprite de Prendido Fase 3.")]
    [Range(0f, 1f)] [SerializeField] private float _phase3At = 2f / 3f;

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

    /// <summary>Sprite según estado y tiempo quemado: el Encendido pasa por Fase 1 → 2 → 3 antes de la ceniza.</summary>
    public Sprite GetSpriteForBurn(CoalStates state, float burnTime)
    {
        float progress = (_maxBurnTime > 0f) ? Mathf.Clamp01(burnTime / _maxBurnTime) : 1f;
        return coalSprites.GetSpriteForBurn(state, progress, _phase2At, Mathf.Max(_phase2At, _phase3At));
    }
}
