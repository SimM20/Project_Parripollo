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
    // ── Configuración de balance (TBD doc: multiplicadores fijos, propina pendiente) ──
    private const float ReducedPriceMultiplier = 0.5f;
    private const float TipPercentOfPrice = 0.2f;
    private const float MinimumPerfectTip = 1f;

    public struct CutResult
    {
        public int worstOffset;
        public float price;
        public bool tipEligible;
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
    /// Desfase 0: 100% + propina. Desfase 1: 100% sin propina. Desfase >=2: 50% con floor, sin propina.
    /// </summary>
    public static CutResult EvaluateCut(MeatStates sideA, MeatStates sideB, MeatStates requested, float basePrice)
    {
        int offsetA = Mathf.Abs((int)sideA - (int)requested);
        int offsetB = Mathf.Abs((int)sideB - (int)requested);
        int worst = Mathf.Max(offsetA, offsetB);

        var result = new CutResult { worstOffset = worst };

        if (worst == 0)
        {
            result.price = basePrice;
            result.tipEligible = true;
        }
        else if (worst == 1)
        {
            result.price = basePrice;
            result.tipEligible = false;
        }
        else
        {
            result.price = Mathf.Floor(basePrice * ReducedPriceMultiplier);
            result.tipEligible = false;
        }

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
                parts.Add("Falta " + extras.missingToppings[i].toppingName);
        }

        if (extras.extraToppings != null)
        {
            for (int i = 0; i < extras.extraToppings.Count; i++)
                parts.Add("Sobra " + extras.extraToppings[i].toppingName);
        }

        if (extras.extraBread)
            parts.Add("Sobra el pan");

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// Propina de una pieza perfecta. Siempre mayor que cero; escala con la paciencia restante.
    /// Fórmula TBD de balance.
    /// </summary>
    public static float CalculateTip(float basePrice, float patience01)
    {
        float tip = Mathf.Floor(basePrice * TipPercentOfPrice * Mathf.Clamp01(patience01));
        return Mathf.Max(MinimumPerfectTip, tip);
    }

    /// <summary>
    /// Estructura con el desglose económico y el estado de feedback correspondiente a la entrega.
    /// </summary>
    public struct DeliveryFeedbackEvaluation
    {
        public CustomerFeedbackState state;
        public float tipAmount;
    }

    /// <summary>
    /// Evalúa la satisfacción del cliente y la propina según las reglas del documento:
    /// - Turista feliz: corte en punto exacto + cliente Turista -> 20% propina.
    /// - Entrega excelente: corte en punto exacto + paciencia alta (>= 50%) -> 10% propina.
    /// - Entrega aceptable: corte con desfase 1 o paciencia media/baja -> 3% a 7% (5% promedio).
    /// - Sin propina: desfase >= 2, o propina anulada por faltante, o propina evaluada en 0 -> $0.
    /// `tipMultiplier` es el bonus aditivo de las mejoras de tienda (`UpgradeEffectType.TipPercent`);
    /// `1` = sin mejoras. Se aplica antes del redondeo y del piso de $1.
    /// </summary>
    public static DeliveryFeedbackEvaluation EvaluateDeliveryFeedback(Customer customer, float basePrice, int worstOffset, float tipMultiplier = 1f)
    {
        var result = new DeliveryFeedbackEvaluation();

        tipMultiplier = Mathf.Max(0f, tipMultiplier);

        if (customer == null || customer.IsTipAnulada)
        {
            result.state = CustomerFeedbackState.SinPropina;
            result.tipAmount = 0f;
            return result;
        }

        if (worstOffset == 0)
        {
            if (customer.type == CustomerType.Turista)
            {
                result.state = CustomerFeedbackState.TuristaFeliz;
                result.tipAmount = Mathf.Max(1f, Mathf.Round(basePrice * 0.20f * tipMultiplier));
            }
            else if (customer.Patience01 >= 0.5f)
            {
                result.state = CustomerFeedbackState.EntregaExcelente;
                result.tipAmount = Mathf.Max(1f, Mathf.Round(basePrice * 0.10f * tipMultiplier));
            }
            else
            {
                result.state = CustomerFeedbackState.EntregaAceptable;
                result.tipAmount = Mathf.Max(1f, Mathf.Round(basePrice * 0.05f * tipMultiplier));
            }
        }
        else if (worstOffset == 1)
        {
            if (customer.Patience01 >= 0.3f)
            {
                result.state = CustomerFeedbackState.EntregaAceptable;
                result.tipAmount = Mathf.Max(1f, Mathf.Round(basePrice * 0.05f * tipMultiplier));
            }
            else
            {
                result.state = CustomerFeedbackState.SinPropina;
                result.tipAmount = 0f;
            }
        }
        else
        {
            result.state = CustomerFeedbackState.SinPropina;
            result.tipAmount = 0f;
        }

        return result;
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
