using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Evaluación económica y de validez por corte según el Sistema de Calor y Cocción.
/// Cada corte se evalúa por separado; la cara con mayor desvío determina el resultado.
/// Crudo o Quemado en cualquier cara bloquean la entrega completa.
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

        public bool IsBlocked => rawCount > 0 || burnedCount > 0;
    }

    /// <summary>
    /// Valida todas las piezas del armado. Quemado tiene prioridad sobre Crudo:
    /// una pieza con una cara quemada cuenta solo como quemada.
    /// </summary>
    public static DeliveryValidation Validate(IReadOnlyList<BuildStationSystem.CutSideStates> sideStates)
    {
        var result = new DeliveryValidation { burnedIndices = new List<int>() };

        if (sideStates == null)
            return result;

        for (int i = 0; i < sideStates.Count; i++)
        {
            if (sideStates[i].IsBurned)
            {
                result.burnedCount++;
                result.burnedIndices.Add(i);
            }
            else if (sideStates[i].IsRaw)
            {
                result.rawCount++;
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
    /// Mensaje de bloqueo con contadores. Adapta singular/plural y omite contadores en cero.
    /// Incluye la instrucción de descarte solo si hay quemados.
    /// </summary>
    public static string BuildBlockedMessage(int rawCount, int burnedCount)
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

        if (burnedCount > 0)
            sb.Append("\nApretá X para desechar " + (burnedCount == 1 ? "el corte quemado." : "los cortes quemados."));

        return sb.ToString();
    }
}
