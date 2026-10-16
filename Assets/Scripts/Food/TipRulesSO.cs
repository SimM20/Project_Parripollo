using UnityEngine;

/// <summary>Qué tan buena fue la propina, para el feedback visual y sonoro.</summary>
public enum TipTier { None, Normal, Good, Great }

/// <summary>
/// Reglas de propina: único lugar donde viven sus números. La propina premia el dominio del
/// juego, no el azar: sale del punto de cocción, la velocidad, el tipo de cliente, la
/// complejidad del pedido y la racha de entregas perfectas. Se carga desde
/// Resources/TipRules; sin asset usa estos valores por defecto. Balance justificado en
/// Docs/Balance/economy_balance.md.
/// </summary>
[CreateAssetMenu(fileName = "TipRules", menuName = "Parripollo/Tip Rules")]
public class TipRulesSO : ScriptableObject
{
    private static TipRulesSO runtimeDefault;

    public static TipRulesSO Instance
    {
        get
        {
            if (runtimeDefault == null)
            {
                runtimeDefault = Resources.Load<TipRulesSO>("TipRules");
                if (runtimeDefault == null)
                {
                    runtimeDefault = CreateInstance<TipRulesSO>();
                    runtimeDefault.name = "TipRules (Default)";
                }
            }
            return runtimeDefault;
        }
    }

    [Header("Propina base por tipo de cliente (fracción del precio, entrega perfecta)")]
    [SerializeField] private float normalTip = 0.10f;
    [SerializeField] private float camioneroTip = 0.08f;
    [SerializeField] private float apuradoTip = 0.12f;
    [SerializeField] private float turistaTip = 0.18f;

    [Header("Calidad")]
    [Tooltip("Parte de la propina que se conserva con desfase 1 (punto o extras casi bien). Desfase 2+ no deja nada.")]
    [Range(0f, 1f)] [SerializeField] private float offByOneQuality = 0.4f;

    [Header("Velocidad (según la paciencia que le queda al cliente al entregar)")]
    [Tooltip("Multiplicador con la paciencia en 0.")]
    [SerializeField] private float slowestSpeedFactor = 0.35f;
    [Tooltip("Multiplicador con la paciencia llena.")]
    [SerializeField] private float fastestSpeedFactor = 1.2f;
    [Tooltip("Paciencia mínima para que una entrega perfecta cuente como excelente (feedback verde).")]
    [Range(0f, 1f)] [SerializeField] private float excellentPatience = 0.5f;

    [Header("Complejidad")]
    [Tooltip("Fracción del precio que suma cada topping pedido, si el pedido llegó perfecto.")]
    [SerializeField] private float toppingBonus = 0.02f;

    [Header("Racha de entregas perfectas")]
    [Tooltip("Fracción del precio que suma cada entrega perfecta previa de la racha.")]
    [SerializeField] private float streakBonusPerStep = 0.015f;
    [Tooltip("Tope de pasos de racha que suman bonus.")]
    [Min(0)] [SerializeField] private int streakMaxSteps = 4;

    [Header("Niveles de propina (fracción del precio efectivamente cobrada)")]
    [Tooltip("Desde acá la propina se celebra como buena.")]
    [SerializeField] private float goodTipFraction = 0.12f;
    [Tooltip("Desde acá la propina se celebra como excepcional.")]
    [SerializeField] private float greatTipFraction = 0.20f;

    public float ExcellentPatience => excellentPatience;
    public int StreakMaxSteps => streakMaxSteps;

    public float GetBaseTip(CustomerType type)
    {
        switch (type)
        {
            case CustomerType.Camionero: return camioneroTip;
            case CustomerType.Apurado: return apuradoTip;
            case CustomerType.Turista: return turistaTip;
            default: return normalTip;
        }
    }

    /// <summary>1 con desfase 0, <c>offByOneQuality</c> con desfase 1, 0 con desfase 2 o más.</summary>
    public float GetQualityFactor(int worstOffset)
        => worstOffset <= 0 ? 1f : worstOffset == 1 ? offByOneQuality : 0f;

    public float GetSpeedFactor(float patience01)
        => Mathf.Lerp(slowestSpeedFactor, fastestSpeedFactor, Mathf.Clamp01(patience01));

    /// <summary>Bonus de racha: solo cuentan las entregas perfectas previas, hasta el tope.</summary>
    public float GetStreakBonus(int perfectStreak)
        => streakBonusPerStep * Mathf.Clamp(perfectStreak, 0, streakMaxSteps);

    public float GetComplexityBonus(int toppingCount) => toppingBonus * Mathf.Max(0, toppingCount);

    /// <summary>Nivel de la propina según qué parte del precio representa lo que dejó.</summary>
    public TipTier GetTier(float tipAmount, float basePrice)
    {
        if (tipAmount <= 0f) return TipTier.None;
        float fraction = basePrice > 0f ? tipAmount / basePrice : 0f;
        return fraction >= greatTipFraction ? TipTier.Great
             : fraction >= goodTipFraction ? TipTier.Good
             : TipTier.Normal;
    }
}
