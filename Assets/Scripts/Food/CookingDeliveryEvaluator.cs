using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Evaluación económica y de validez por corte según el Sistema de Calor y Cocción.
/// Cada corte se evalúa por separado; la cara con mayor desvío determina el resultado.
/// Crudo o Quemado en cualquier cara no impiden entregar: la entrega se concreta sin cobrar,
/// suma un strike y el cliente se va igual que si no lo hubieran atendido.
/// </summary>
public static class CookingDeliveryEvaluator
{
    // ── Configuración de balance. La propina vive en TipRulesSO. ──
    private const float ReducedPriceMultiplier = 0.5f;

    public struct CutResult
    {
        public int worstOffset;
        public float price;
    }

    public struct DeliveryValidation
    {
        public int rawCount;
        public int burnedCount;
        public List<int> burnedIndices;
        public List<int> rawIndices;

        /// <summary>
        /// Hay al menos una cara Cruda o Quemada: la entrega se acepta pero no paga,
        /// suma un strike y el cliente se retira enojado.
        /// </summary>
        public bool CausesStrike => rawCount > 0 || burnedCount > 0;
    }

    /// <summary>
    /// Valida todas las piezas del armado. Quemado tiene prioridad sobre Crudo:
    /// una pieza con una cara quemada cuenta solo como quemada.
    /// </summary>
    public static DeliveryValidation Validate(IReadOnlyList<BuildStationSystem.CutSideStates> sideStates)
    {
        return Validate(sideStates, null, null);
    }

    /// <summary>
    /// Igual que Validate, pero con una excepción opcional por corte: si isCookingExempt(cut)
    /// es true, esa pieza cruda o quemada se evalúa como cualquier otra y no suma strike
    /// (usado por el tutorial). Sin el predicado, el comportamiento es el normal.
    /// </summary>
    public static DeliveryValidation Validate(
        IReadOnlyList<BuildStationSystem.CutSideStates> sideStates,
        IReadOnlyList<MeatCutSO> cuts,
        System.Func<MeatCutSO, bool> isCookingExempt)
    {
        var result = new DeliveryValidation
        {
            burnedIndices = new List<int>(),
            rawIndices = new List<int>()
        };

        if (sideStates == null)
            return result;

        for (int i = 0; i < sideStates.Count; i++)
        {
            if (!sideStates[i].IsBurned && !sideStates[i].IsRaw)
                continue;

            // Excepción de entrega (tutorial): los cortes eximidos se cobran como cualquier
            // otro y no suman strike, así el guion del tutorial no se rompe.
            if (isCookingExempt != null && cuts != null && i < cuts.Count && isCookingExempt(cuts[i]))
                continue;

            // Quemado tiene prioridad: una pieza con una cara quemada cuenta solo como quemada.
            if (sideStates[i].IsBurned)
            {
                result.burnedCount++;
                result.burnedIndices.Add(i);
            }
            else
            {
                result.rawCount++;
                result.rawIndices.Add(i);
            }
        }

        return result;
    }

    /// <summary>
    /// Evalúa una pieza entregable contra su punto solicitado.
    /// Desfase 0 y 1: 100%. Desfase >=2: 50% con floor. La propina se decide aparte (EvaluateDeliveryFeedback).
    /// </summary>
    public static CutResult EvaluateCut(MeatStates sideA, MeatStates sideB, MeatStates requested, float basePrice)
    {
        int offsetA = Mathf.Abs((int)sideA - (int)requested);
        int offsetB = Mathf.Abs((int)sideB - (int)requested);
        int worst = Mathf.Max(offsetA, offsetB);

        var result = new CutResult { worstOffset = worst };

        result.price = worst < 2 ? basePrice : Mathf.Floor(basePrice * ReducedPriceMultiplier);

        return result;
    }

    /// <summary>Precio con el mismo recorte que un corte con desfase >= 2.</summary>
    public static float ApplyReducedPrice(float price)
    {
        return Mathf.Floor(price * ReducedPriceMultiplier);
    }

    // ── Extras: toppings y pan ──────────────────────────────────────────────

    /// <summary>
    /// Resultado de comparar lo pedido contra lo armado en toppings y pan.
    /// Cada faltante o sobrante cuenta como un punto de desfase, en la misma escala
    /// que el punto de cocción: 1 tolera (sin propina), >= 2 mitad de precio.
    /// </summary>
    public struct ExtrasResult
    {
        public int offset;
        public List<ToppingSO> missingToppings;
        public List<ToppingSO> extraToppings;
        /// <summary>Pidieron al plato y el armado tiene pan.</summary>
        public bool extraBread;

        public bool HasIssues => offset > 0;
    }

    /// <summary>
    /// Compara toppings pedidos vs armados como conjuntos (verter dos veces la misma salsa
    /// no es error) y detecta pan de más. La falta de pan no entra acá: bloquea antes en
    /// BuildStationSystem.TryBuildSandwich.
    /// </summary>
    public static ExtrasResult EvaluateExtras(
        IReadOnlyList<ToppingSO> requestedToppings,
        IReadOnlyList<ToppingSO> assembledToppings,
        bool breadRequested,
        bool breadAssembled)
    {
        var result = new ExtrasResult
        {
            missingToppings = new List<ToppingSO>(),
            extraToppings = new List<ToppingSO>()
        };

        if (requestedToppings != null)
        {
            for (int i = 0; i < requestedToppings.Count; i++)
            {
                ToppingSO topping = requestedToppings[i];
                if (topping == null || result.missingToppings.Contains(topping)) continue;

                if (!Contains(assembledToppings, topping))
                    result.missingToppings.Add(topping);
            }
        }

        if (assembledToppings != null)
        {
            for (int i = 0; i < assembledToppings.Count; i++)
            {
                ToppingSO topping = assembledToppings[i];
                if (topping == null || result.extraToppings.Contains(topping)) continue;

                if (!Contains(requestedToppings, topping))
                    result.extraToppings.Add(topping);
            }
        }

        result.extraBread = !breadRequested && breadAssembled;

        result.offset = result.missingToppings.Count
                      + result.extraToppings.Count
                      + (result.extraBread ? 1 : 0);

        return result;
    }

    private static bool Contains(IReadOnlyList<ToppingSO> list, ToppingSO topping)
    {
        if (list == null) return false;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == topping)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Resumen corto de los extras mal entregados, por ejemplo
    /// "Falta Chimichurri · Sobra Salsa criolla · Sobra el pan". Null si no hay errores.
    /// </summary>
    public static string BuildExtrasMessage(ExtrasResult extras)
    {
        if (!extras.HasIssues)
            return null;

        var parts = new List<string>();

        if (extras.missingToppings != null)
        {
            for (int i = 0; i < extras.missingToppings.Count; i++)
                parts.Add(Loc.Format("delivery.extras.missing", extras.missingToppings[i].toppingName));
        }

        if (extras.extraToppings != null)
        {
            for (int i = 0; i < extras.extraToppings.Count; i++)
                parts.Add(Loc.Format("delivery.extras.extra", extras.extraToppings[i].toppingName));
        }

        if (extras.extraBread)
            parts.Add(Loc.Get("delivery.extras.extra_bread"));

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// Estructura con el desglose económico y el estado de feedback correspondiente a la entrega.
    /// </summary>
    public struct DeliveryFeedbackEvaluation
    {
        public CustomerFeedbackState state;
        public float tipAmount;
        /// <summary>Fracción del precio que terminó siendo propina, antes de mejoras.</summary>
        public float tipPercent;
        /// <summary>Parte de <c>tipAmount</c> que aportó la racha de entregas perfectas.</summary>
        public float streakBonusAmount;
        /// <summary>Multiplicador de velocidad aplicado (1 = a tiempo, &lt;1 = tardó).</summary>
        public float speedFactor;
        /// <summary>Nivel de la propina para el feedback (None si no dejó nada).</summary>
        public TipTier tier;
    }

    /// <summary>
    /// Evalúa la satisfacción del cliente y la propina con <see cref="TipRulesSO"/>:
    /// propina = precio × (base del tipo de cliente × calidad × velocidad + complejidad + racha) × mejoras.
    /// Calidad: desfase 0 entero, desfase 1 parcial, desfase 2+ o propina anulada = $0.
    /// Complejidad y racha solo suman con entrega perfecta. `tipMultiplier` es el bonus de las
    /// mejoras de tienda (`UpgradeEffectType.TipPercent`), `1` = sin mejoras.
    /// `perfectStreak` = entregas perfectas seguidas ANTES de esta (ver <see cref="DayStats"/>).
    /// </summary>
    public static DeliveryFeedbackEvaluation EvaluateDeliveryFeedback(
        Customer customer, float basePrice, int worstOffset, float tipMultiplier = 1f, int perfectStreak = 0)
    {
        var result = new DeliveryFeedbackEvaluation { state = CustomerFeedbackState.SinPropina };
        TipRulesSO rules = TipRulesSO.Instance;

        if (customer == null || customer.IsTipAnulada)
            return result;

        float quality = rules.GetQualityFactor(worstOffset);
        if (quality <= 0f)
            return result;

        bool perfect = worstOffset <= 0;
        int toppings = customer.order != null && customer.order.toppings != null ? customer.order.toppings.Count : 0;

        result.speedFactor = rules.GetSpeedFactor(customer.Patience01);
        float streakPercent = perfect ? rules.GetStreakBonus(perfectStreak) : 0f;
        result.tipPercent = rules.GetBaseTip(customer.type) * quality * result.speedFactor
                          + (perfect ? rules.GetComplexityBonus(toppings) : 0f)
                          + streakPercent;

        float multiplier = Mathf.Max(0f, tipMultiplier);
        result.tipAmount = Mathf.Max(1f, Mathf.Round(basePrice * result.tipPercent * multiplier));
        result.streakBonusAmount = Mathf.Min(result.tipAmount, Mathf.Round(basePrice * streakPercent * multiplier));
        result.tier = rules.GetTier(result.tipAmount, basePrice);

        if (perfect && customer.Patience01 >= rules.ExcellentPatience)
            result.state = customer.type == CustomerType.Turista
                ? CustomerFeedbackState.TuristaFeliz
                : CustomerFeedbackState.EntregaExcelente;
        else
            result.state = CustomerFeedbackState.EntregaAceptable;

        return result;
    }

    /// <summary>
    /// Explicación corta de por qué la propina salió así, para el texto de feedback de entrega.
    /// Null cuando no hay nada que explicar (propina completa sin racha).
    /// </summary>
    public static string BuildTipMessage(DeliveryFeedbackEvaluation eval, int worstOffset, bool tipVoided)
    {
        if (tipVoided)
            return Loc.Get("delivery.tip.voided");
        if (eval.tipAmount <= 0f)
            return worstOffset >= 2 ? Loc.Get("delivery.tip.none") : null;

        var parts = new List<string>();
        if (worstOffset == 1)
            parts.Add(Loc.Get("delivery.tip.almost"));
        if (eval.speedFactor < 0.8f)
            parts.Add(Loc.Get("delivery.tip.slow"));
        if (eval.streakBonusAmount > 0f)
            parts.Add(Loc.Format("delivery.tip.streak", "$" + (int)eval.streakBonusAmount));

        return parts.Count > 0 ? string.Join(" · ", parts) : null;
    }

    /// <summary>
    /// Mensaje del strike por cocción, con contadores. Adapta singular/plural y omite
    /// contadores en cero. La aclaración de que el cliente se fue la agrega GameManager.
    /// </summary>
    public static string BuildBadCookingMessage(int rawCount, int burnedCount)
    {
        var sb = new System.Text.StringBuilder("El pedido tiene ");

        if (rawCount > 0 && burnedCount > 0)
        {
            sb.Append(rawCount == 1 ? "1 corte con una cara cruda" : rawCount + " cortes con una cara cruda");
            sb.Append(" y ");
            sb.Append(burnedCount == 1 ? "1 corte quemado" : burnedCount + " cortes quemados");
            sb.Append(".");
        }
        else if (rawCount > 0)
        {
            sb.Append(rawCount == 1 ? "1 corte con una cara cruda." : rawCount + " cortes con una cara cruda.");
        }
        else
        {
            sb.Append(burnedCount == 1 ? "1 corte quemado." : burnedCount + " cortes quemados.");
        }

        return sb.ToString();
    }
}
