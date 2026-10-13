using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Balance del sistema de reputación: experiencia por nivel, cuánto suma o resta cada evento,
/// qué tanto mejora la jornada cada nivel y los locales (escenarios) que destraban el crecimiento.
///
/// Vive en <c>Resources/ReputationConfig.asset</c>: lo carga <see cref="ReputationSystem"/>, que es
/// estático y tiene que andar en cualquier escena (la partida y la tienda) sin setup.
/// </summary>
[CreateAssetMenu(fileName = "ReputationConfig", menuName = "Parripollo/Reputation Config")]
public class ReputationConfigSO : ScriptableObject
{
    public const string ResourcePath = "ReputationConfig";

    [System.Serializable]
    public class Venue
    {
        [Tooltip("Nombre interno, para el inspector y el log. Por ahora no se muestra en el juego.")]
        public string id = "Local";

        [Tooltip("Nivel de reputación desde el que se puede comprar. El primer local (con el que se arranca) lo ignora.")]
        [Min(0)] public int requiredLevel;

        [Tooltip("Precio del local. El primero (con el que se arranca) lo ignora.")]
        [Min(0)] public int price;
    }

    [Header("Niveles")]
    [Tooltip("Experiencia que pide cada nivel, contada desde el nivel anterior: el elemento 0 es lo que " +
             "hay que juntar para pasar del nivel 0 al 1. La cantidad de elementos es el nivel máximo.")]
    [SerializeField] private int[] xpPerLevel = { 100, 250, 400, 500, 750, 900, 1000, 1200, 1350, 1500 };

    [Header("Entregas")]
    [Tooltip("Corte entregado en el punto exacto que pidió el cliente.")]
    [SerializeField] private int perfectDeliveryXp = 15;

    [Tooltip("Extra si además el cliente se fue contento: entrega excelente (paciencia >= 50%) o turista feliz.")]
    [SerializeField] private int happyCustomerBonusXp = 5;

    [Tooltip("Corte a un punto de distancia del pedido (se cobra entero, sin propina).")]
    [SerializeField] private int acceptableDeliveryXp = 5;

    [Tooltip("Se RESTA si el corte llegó a dos puntos o más del pedido (se cobra la mitad).")]
    [Min(0)] [SerializeField] private int poorDeliveryPenalty = 5;

    [Tooltip("Se RESTA por cada topping que faltó o sobró, y por el pan de más.")]
    [Min(0)] [SerializeField] private int extrasMistakePenalty = 5;

    [Header("Clientes enojados")]
    [Tooltip("Se RESTA si se entrega un corte crudo o quemado: el cliente se va sin pagar.")]
    [Min(0)] [SerializeField] private int badCookingPenalty = 20;

    [Tooltip("Se RESTA si un cliente se va porque se le acabó la paciencia.")]
    [Min(0)] [SerializeField] private int angryLeavePenalty = 15;

    [Header("Terminar la jornada antes de tiempo")]
    [Tooltip("Se RESTA al terminar la jornada a mano desde la pausa, aunque el local ya haya cerrado.")]
    [Min(0)] [SerializeField] private int earlyEndPenaltyMin = 20;

    [Tooltip("Se RESTA si se la termina apenas abre el local. En el medio se reparte según la hora: " +
             "cuanto más temprano se corta la jornada, más se pierde.")]
    [Min(0)] [SerializeField] private int earlyEndPenaltyMax = 80;

    [Header("Efecto en la jornada")]
    [Tooltip("Clientes esperados extra por cada nivel de reputación (0.05 = +5% por nivel). Cuenta también " +
             "lo avanzado dentro del nivel, y se resuelve al abrir el local: lo que pasa hoy mueve la curva de mañana.")]
    [Min(0f)] [SerializeField] private float customersBonusPerLevel = 0.05f;

    [Tooltip("Plata extra por corte entregado por cada nivel de reputación (0.04 = +4% por nivel). " +
             "Se aplica al precio del corte, no a la propina.")]
    [Min(0f)] [SerializeField] private float moneyBonusPerLevel = 0.04f;

    [Header("Locales")]
    [Tooltip("Locales en orden. El primero es con el que se arranca. Cada uno de los siguientes se puede " +
             "comprar desde su nivel de reputación, y hasta comprarlo la reputación no pasa de ese nivel. " +
             "Por ahora comprar un local solo destraba la reputación: no cambia nada visual.")]
    [SerializeField] private List<Venue> venues = new List<Venue>
    {
        new Venue { id = "Parrilla 40", requiredLevel = 0, price = 0 },
        new Venue { id = "Segundo local", requiredLevel = 4, price = 3000 },
        new Venue { id = "Tercer local", requiredLevel = 8, price = 4000 }
    };

    // ── Niveles ─────────────────────────────────────────────────────────

    /// <summary>Nivel más alto posible. Se arranca en 0.</summary>
    public int MaxLevel => xpPerLevel != null && xpPerLevel.Length > 0 ? xpPerLevel.Length : 1;

    /// <summary>Experiencia para pasar de <paramref name="level"/> − 1 a <paramref name="level"/>.</summary>
    public int GetXpForLevel(int level)
    {
        if (level < 1 || level > MaxLevel) return 0;
        if (xpPerLevel == null || xpPerLevel.Length == 0) return 100;
        return Mathf.Max(1, xpPerLevel[level - 1]);
    }

    /// <summary>Experiencia total juntada al llegar justo a <paramref name="level"/> (0 en el nivel 0).</summary>
    public int GetTotalXpForLevel(int level)
    {
        level = Mathf.Clamp(level, 0, MaxLevel);

        int total = 0;
        for (int i = 1; i <= level; i++)
            total += GetXpForLevel(i);
        return total;
    }

    /// <summary>Nivel que corresponde a una experiencia total.</summary>
    public int GetLevelForXp(int totalXp)
    {
        int level = 0;
        int needed = 0;

        while (level < MaxLevel)
        {
            needed += GetXpForLevel(level + 1);
            if (totalXp < needed) break;
            level++;
        }

        return level;
    }

    // ── Experiencia por evento ──────────────────────────────────────────

    /// <summary>
    /// Experiencia de una entrega cobrada. Manda el punto de cocción (exacto, a uno, a dos o más) y cada
    /// topping o pan mal puesto descuenta; un cliente contento suma un extra. Puede dar negativo: una
    /// entrega mala también baja la reputación.
    /// </summary>
    public int GetDeliveryXp(int cookingOffset, int extrasMistakes, CustomerFeedbackState state)
    {
        int xp;

        if (cookingOffset <= 0) xp = perfectDeliveryXp;
        else if (cookingOffset == 1) xp = acceptableDeliveryXp;
        else xp = -poorDeliveryPenalty;

        if (state == CustomerFeedbackState.EntregaExcelente || state == CustomerFeedbackState.TuristaFeliz)
            xp += happyCustomerBonusXp;

        xp -= extrasMistakePenalty * Mathf.Max(0, extrasMistakes);
        return xp;
    }

    /// <summary>Experiencia (negativa) de un cliente que se fue enojado, según por qué se fue.</summary>
    public int GetAngryLeaveXp(CustomerFeedbackState state)
    {
        return state == CustomerFeedbackState.EntregaCrudaOQuemada ? -badCookingPenalty : -angryLeavePenalty;
    }

    /// <summary>
    /// Experiencia (negativa) por terminar la jornada a mano. <paramref name="remainingDay01"/> es lo que
    /// faltaba de jornada: 1 = recién abría, 0 = ya había cerrado.
    /// </summary>
    public int GetEarlyEndXp(float remainingDay01)
    {
        float penalty = Mathf.Lerp(earlyEndPenaltyMin, Mathf.Max(earlyEndPenaltyMin, earlyEndPenaltyMax), Mathf.Clamp01(remainingDay01));
        return -Mathf.RoundToInt(penalty);
    }

    // ── Efectos ─────────────────────────────────────────────────────────

    /// <summary>Multiplicador de los clientes esperados en la jornada. <paramref name="reputation"/> = nivel + avance.</summary>
    public float GetCustomersMultiplier(float reputation) => 1f + customersBonusPerLevel * Mathf.Max(0f, reputation);

    /// <summary>Multiplicador del precio de cada corte entregado. <paramref name="reputation"/> = nivel + avance.</summary>
    public float GetMoneyMultiplier(float reputation) => 1f + moneyBonusPerLevel * Mathf.Max(0f, reputation);

    // ── Locales ─────────────────────────────────────────────────────────

    /// <summary>Cantidad de locales, incluido el inicial. Nunca menos de 1.</summary>
    public int VenueCount => venues != null && venues.Count > 0 ? venues.Count : 1;

    /// <summary>Local en la posición <paramref name="index"/> (0 = el inicial). Null si no existe.</summary>
    public Venue GetVenue(int index)
    {
        if (venues == null || index < 0 || index >= venues.Count) return null;
        return venues[index];
    }

    // ── Carga ───────────────────────────────────────────────────────────

    /// <summary>El asset de Resources o, si falta, uno con los valores por defecto (avisa una vez).</summary>
    public static ReputationConfigSO LoadOrDefault()
    {
        ReputationConfigSO loaded = Resources.Load<ReputationConfigSO>(ResourcePath);
        if (loaded != null) return loaded;

        Debug.LogWarning(
            "[Reputation] No se encontró Resources/" + ResourcePath + ".asset: se usan los valores por defecto."
        );

        ReputationConfigSO fallback = CreateInstance<ReputationConfigSO>();
        fallback.name = "ReputationConfig (Default)";
        return fallback;
    }

    void OnValidate()
    {
        if (xpPerLevel != null)
        {
            for (int i = 0; i < xpPerLevel.Length; i++)
                xpPerLevel[i] = Mathf.Max(1, xpPerLevel[i]);
        }

        earlyEndPenaltyMax = Mathf.Max(earlyEndPenaltyMin, earlyEndPenaltyMax);
    }
}
