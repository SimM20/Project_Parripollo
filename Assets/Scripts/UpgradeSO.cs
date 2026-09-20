using UnityEngine;

/// <summary>Que variable del juego toca la mejora.</summary>
public enum UpgradeEffectType
{
    CoalBurnTime,
    MaxSimultaneousCustomers,
    TipPercent,
    CustomerPatience
}

[CreateAssetMenu(fileName = "Upgrade", menuName = "Asado/Upgrade")]
public class UpgradeSO : ItemDataSO
{
    [Header("Display")]
    public Sprite icon;
    [TextArea(2, 4)] public string description;

    [Header("Estado")]
    [Tooltip("Si está bloqueada, aparece oscurecida")]
    public bool isUnlocked = true;

    [Tooltip("Niveles comprables. 1 = mejora de una sola vez.")]
    [Min(1)] public int maxLevel = 1;

    [Tooltip("Niveles ya comprados. Muta en runtime y persiste entre sesiones de Editor.")]
    [Min(0)] public int currentLevel = 0;

    [Header("Efecto")]
    [Tooltip("Que variable modifica la mejora.")]
    public UpgradeEffectType effectType = UpgradeEffectType.CoalBurnTime;

    [Header("Efecto: duracion del carbon")]
    [Tooltip("Carbon afectado. Si es null, la mejora no aplica ningun efecto.")]
    public CoalSO targetCoal;

    [Tooltip("Nuevo maxBurnTime del carbon al comprar la mejora.")]
    public float upgradedMaxBurnTime = 0f;

    [Header("Efecto: clientes simultaneos")]
    [Tooltip("Clientes simultaneos que suma cada nivel comprado.")]
    [Min(1)] public int customersPerLevel = 1;

    [Header("Efecto: propina")]
    [Tooltip("Fraccion extra de propina que suma cada nivel comprado. 0.5 = +50% por nivel (aditivo).")]
    [Min(0f)] public float tipBonusPerLevel = 0.5f;

    [Header("Efecto: paciencia")]
    [Tooltip("Fraccion extra de paciencia que suma cada nivel comprado. 0.15 = +15% por nivel (aditivo).")]
    [Min(0f)] public float patienceBonusPerLevel = 0.15f;

    /// <summary>Niveles comprados, saneado contra maxLevel.</summary>
    public int CurrentLevel => Mathf.Clamp(currentLevel, 0, MaxLevel);

    /// <summary>maxLevel saneado: nunca menor a 1.</summary>
    public int MaxLevel => Mathf.Max(1, maxLevel);

    /// <summary>True cuando ya no quedan niveles por comprar.</summary>
    public bool IsMaxed => CurrentLevel >= MaxLevel;

    /// <summary>Clientes simultaneos extra que aporta la mejora en su nivel actual.</summary>
    public int MaxSimultaneousCustomersBonus =>
        effectType == UpgradeEffectType.MaxSimultaneousCustomers
            ? CurrentLevel * Mathf.Max(1, customersPerLevel)
            : 0;

    /// <summary>
    /// Fraccion extra de propina que aporta la mejora en su nivel actual (aditiva).
    /// Nivel 2 con `tipBonusPerLevel = 0.5` devuelve `1.0` (= +100%).
    /// </summary>
    public float TipMultiplierBonus =>
        effectType == UpgradeEffectType.TipPercent
            ? CurrentLevel * Mathf.Max(0f, tipBonusPerLevel)
            : 0f;

    /// <summary>
    /// Fraccion extra de paciencia que aporta la mejora en su nivel actual (aditiva).
    /// Nivel 1 con `patienceBonusPerLevel = 0.15` devuelve `0.15` (= +15%).
    /// </summary>
    public float PatienceMultiplierBonus =>
        effectType == UpgradeEffectType.CustomerPatience
            ? CurrentLevel * Mathf.Max(0f, patienceBonusPerLevel)
            : 0f;

    /// <summary>Sube un nivel y aplica el efecto. Devuelve false si ya estaba al maximo.</summary>
    public bool Purchase()
    {
        if (IsMaxed) return false;

        currentLevel = CurrentLevel + 1;
        ApplyEffect();
        return true;
    }

    /// <summary>
    /// Aplica el efecto. Idempotente: fija un valor absoluto, no acumula.
    /// `MaxSimultaneousCustomers` no muta nada aca: el bonus vive en `currentLevel` y lo lee
    /// `CustomerSystem` al arrancar la noche (la tienda es post-noche, no hay clientes vivos).
    /// `TipPercent` tampoco muta nada: lo lee `GameManager` en cada entrega via
    /// `FoodCatalogSO.GetTipMultiplier()`.
    /// `CustomerPatience` tampoco muta nada: lo lee `CustomerSystem` al arrancar la noche via
    /// `FoodCatalogSO.GetPatienceMultiplier()`.
    /// </summary>
    public void ApplyEffect()
    {
        if (effectType != UpgradeEffectType.CoalBurnTime) return;
        if (targetCoal == null || upgradedMaxBurnTime <= 0f) return;

        targetCoal.SetMaxBurnTime(upgradedMaxBurnTime);
    }

    void OnValidate()
    {
        maxLevel = Mathf.Max(1, maxLevel);
        currentLevel = Mathf.Clamp(currentLevel, 0, maxLevel);
        tipBonusPerLevel = Mathf.Max(0f, tipBonusPerLevel);
        patienceBonusPerLevel = Mathf.Max(0f, patienceBonusPerLevel);
    }
}
