using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "MeatCut", menuName = "Asado/Meat Cut")]
public class MeatCutSO : ItemDataSO
{
    public string cutName => itemName;
    public float price => basePrice;

    [Header("Visual")]
    [SerializeField] private Vector3 _visualOffset;
    [FormerlySerializedAs("cutSprite")]
    [SerializeField] public Sprite meatSpriteA;
    [SerializeField] public Sprite meatSpriteB;

    [Header("Cooking Sprites – Side A")]
    [Tooltip("Sprites for each cooking state when showing side A")]
    [SerializeField] public CookingSprites cookingSpritesA;

    [Header("Cooking Sprites – Side B")]
    [Tooltip("Sprites for each cooking state when showing side B")]
    [SerializeField] public CookingSprites cookingSpritesB;

    [Header("Cooking")]
    [FormerlySerializedAs("cookTimePerSide")]
    [SerializeField] public float timeHeatA;
    [SerializeField] public float timeHeatB;

    [Tooltip("Escala total de calor por cara (S). Se divide en 6 bandas iguales: " +
             "Crudo, Jugoso, Hecho, Bien Hecho, Pasado, Quemado. " +
             "Si es 0, se deriva de timeHeat (S = 3 * timeHeat) para que Hecho arranque en el target anterior.")]
    [SerializeField] public float heatScale;

    [Header("Espaciado")]
    [SerializeField] private Vector2Int grillSpace;

    public Vector2Int GrillSpace => grillSpace;

    public Sprite GetDefaultSprite()
    {
        if (meatSpriteA != null)
            return meatSpriteA;

        return meatSpriteB;
    }

    public Sprite GetSpriteForSide(bool sideA)
    {
        Sprite preferred = sideA ? meatSpriteA : meatSpriteB;
        if (preferred != null)
            return preferred;

        return sideA ? meatSpriteB : meatSpriteA;
    }

    public Sprite GetSpriteForState(MeatStates state, bool sideA)
    {
        CookingSprites sprites = sideA ? cookingSpritesA : cookingSpritesB;
        Sprite result = sprites.GetSpriteForState(state);

        if (result == null)
        {
            CookingSprites fallbackSprites = sideA ? cookingSpritesB : cookingSpritesA;
            result = fallbackSprites.GetSpriteForState(state);
        }

        if (result == null)
            result = GetSpriteForSide(sideA);

        return result;
    }

    public float GetHeatTimeForSide(bool sideA)
    {
        float preferred = sideA ? timeHeatA : timeHeatB;
        if (preferred > 0f)
            return preferred;

        float fallback = sideA ? timeHeatB : timeHeatA;
        if (fallback > 0f)
            return fallback;

        return 1f;
    }

    public Vector3 visualOffset => _visualOffset;

    public Sprite cutSprite => GetDefaultSprite();
    public float cookTimePerSide => GetHeatTimeForSide(true);

    // ── Escala de cocción por caras (6 bandas iguales) ──────────────────────

    /// <summary>Escala total S por cara. Misma para ambas caras.</summary>
    public float GetHeatScale()
    {
        if (heatScale > 0f)
            return heatScale;

        return GetHeatTimeForSide(true) * 3f;
    }

    /// <summary>Ancho de cada banda de estado: S / 6.</summary>
    public float GetStateBandHeat() => GetHeatScale() / 6f;

    /// <summary>Umbral de entrada a Quemado: (5 * S) / 6. Máximo efectivo de acumulación.</summary>
    public float GetBurnThreshold() => (5f * GetHeatScale()) / 6f;

    /// <summary>
    /// Convierte calor acumulado a estado. Intervalos cerrados por izquierda, abiertos por derecha.
    /// Alcanzar exactamente un límite entra al estado siguiente; alcanzar burnThreshold entra a Quemado.
    /// </summary>
    public MeatStates GetStateForHeat(float accumulatedHeat)
    {
        float band = GetStateBandHeat();
        if (band <= 0f)
            return MeatStates.Crudo;

        float clamped = Mathf.Clamp(accumulatedHeat, 0f, GetBurnThreshold());
        int stateIndex = Mathf.Min(Mathf.FloorToInt(clamped / band), 5);

        // Corrección de precisión: si el valor sin clamp alcanzó el umbral de Quemado, es Quemado.
        if (accumulatedHeat >= GetBurnThreshold())
            stateIndex = 5;

        return (MeatStates)stateIndex;
    }

    [Header("Serving Rules")]
    [Tooltip("Defines how this cut can be served: plated, sandwich, or both.")]
    [SerializeField] public ServingMode servingMode;

    [Tooltip("Required bread for sandwich variants. Null for plated-only cuts.")]
    [SerializeField] public BreadSO requiredBread;

    [Header("Unlock")]
    [Tooltip("Whether this cut is currently available to the player.")]
    [SerializeField] public bool isUnlocked = true;

    [Header("Sell Prices")]
    [Tooltip("Sell price when served plated. Placeholder - configure in Inspector.")]
    [SerializeField] public float sellPricePlate;

    [Tooltip("Sell price when served as sandwich. 0 if not applicable. Placeholder.")]
    [SerializeField] public float sellPriceSandwich;

    void OnValidate()
    {
        category = ItemType.Meat;

        timeHeatA = Mathf.Max(0f, timeHeatA);
        timeHeatB = Mathf.Max(0f, timeHeatB);
        heatScale = Mathf.Max(0f, heatScale);

        grillSpace.x = Mathf.Max(1, grillSpace.x);
        grillSpace.y = Mathf.Max(1, grillSpace.y);
    }
}