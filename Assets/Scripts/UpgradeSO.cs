using UnityEngine;

/// <summary>Que variable del juego toca la mejora.</summary>
public enum UpgradeEffectType
{
    CoalBurnTime,
    MaxSimultaneousCustomers
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
    }
}
