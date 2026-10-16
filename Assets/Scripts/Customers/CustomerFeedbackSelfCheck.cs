using UnityEngine;

/// <summary>
/// Script de verificación automatizada del Sistema de Feedback de Entrega y Reacción del Cliente.
/// Valida reglas económicas, categorías visuales, pools de frases argentinas y congelamiento de paciencia.
/// </summary>
public static class CustomerFeedbackSelfCheck
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void RunAutomaticValidation() => Run();

    /// <summary>Corre todas las validaciones. Devuelve la cantidad de fallas.</summary>
    public static int Run()
    {
        int passed = 0;
        int failed = 0;

        void Assert(bool condition, string testName)
        {
            if (condition)
            {
                passed++;
                Debug.Log($"<color=#4ADE80>✔ [PASS]</color> {testName}");
            }
            else
            {
                failed++;
                Debug.LogError($"<color=#EF4444>❌ [FAIL]</color> {testName}");
            }
        }

        Debug.Log("<b>[CustomerFeedbackSelfCheck] Iniciando validación del sistema...</b>");

        // 1. Validar Categorías
        Assert(CustomerFeedbackState.TuristaFeliz.GetCategory() == CustomerFeedbackCategory.Positive, "TuristaFeliz es Positive");
        Assert(CustomerFeedbackState.EntregaExcelente.GetCategory() == CustomerFeedbackCategory.Positive, "EntregaExcelente es Positive");
        Assert(CustomerFeedbackState.EntregaAceptable.GetCategory() == CustomerFeedbackCategory.Intermediate, "EntregaAceptable es Intermediate");
        Assert(CustomerFeedbackState.CambioPorFaltante.GetCategory() == CustomerFeedbackCategory.Intermediate, "CambioPorFaltante es Intermediate");
        Assert(CustomerFeedbackState.SinPropina.GetCategory() == CustomerFeedbackCategory.Negative, "SinPropina es Negative");
        Assert(CustomerFeedbackState.NoPagaSeVa.GetCategory() == CustomerFeedbackCategory.NegativeSevere, "NoPagaSeVa es NegativeSevere");
        Assert(CustomerFeedbackState.EntregaCrudaOQuemada.GetCategory() == CustomerFeedbackCategory.NegativeSevere, "EntregaCrudaOQuemada es NegativeSevere");

        // 2. Validar Configuración y Timings
        var config = CustomerFeedbackConfigSO.Instance;
        Assert(Mathf.Approximately(config.FeedbackDuration, 4.0f), "FeedbackDuration es 4.0s");
        Assert(config.EconomicFeedbackDelay > 0f, "EconomicFeedbackDelay está configurado");
        Assert(config.ExitAnimationDuration > 0f, "ExitAnimationDuration está configurado");

        // 3. Validar Frases no vacías
        Assert(!string.IsNullOrEmpty(config.GetRandomPhrase(CustomerFeedbackState.TuristaFeliz)), "Pool TuristaFeliz tiene frases");
        Assert(!string.IsNullOrEmpty(config.GetRandomPhrase(CustomerFeedbackState.EntregaExcelente)), "Pool EntregaExcelente tiene frases");
        Assert(!string.IsNullOrEmpty(config.GetRandomPhrase(CustomerFeedbackState.EntregaAceptable)), "Pool EntregaAceptable tiene frases");
        Assert(!string.IsNullOrEmpty(config.GetRandomPhrase(CustomerFeedbackState.SinPropina)), "Pool SinPropina tiene frases");
        Assert(!string.IsNullOrEmpty(config.GetRandomPhrase(CustomerFeedbackState.CambioPorFaltante)), "Pool CambioPorFaltante tiene frases");
        Assert(!string.IsNullOrEmpty(config.GetRandomPhrase(CustomerFeedbackState.NoPagaSeVa)), "Pool NoPagaSeVa tiene frases");
        Assert(!string.IsNullOrEmpty(config.GetRandomPhrase(CustomerFeedbackState.EntregaCrudaOQuemada)), "Pool EntregaCrudaOQuemada (crudo) tiene frases");
        Assert(!string.IsNullOrEmpty(config.GetRandomPhrase(CustomerFeedbackState.EntregaCrudaOQuemada, true)), "Pool EntregaCrudaOQuemada (quemado) tiene frases");

        // 4. Propinas (TipRulesSO): calidad, velocidad, tipo de cliente, complejidad y racha
        float basePrice = 1000f;
        TipRulesSO rules = TipRulesSO.Instance;
        float Expected(CustomerType type, float patience01, int offset, int toppings, int streak, float mult = 1f)
        {
            float q = rules.GetQualityFactor(offset);
            if (q <= 0f) return 0f;
            float pct = rules.GetBaseTip(type) * q * rules.GetSpeedFactor(patience01)
                      + (offset == 0 ? rules.GetComplexityBonus(toppings) + rules.GetStreakBonus(streak) : 0f);
            return Mathf.Max(1f, Mathf.Round(basePrice * pct * mult));
        }

        Customer turista = new Customer();
        turista.Init(CustomerType.Turista, new Order(), 30f, 0);
        var evalTurista = CookingDeliveryEvaluator.EvaluateDeliveryFeedback(turista, basePrice, 0);
        Assert(evalTurista.state == CustomerFeedbackState.TuristaFeliz && evalTurista.tipAmount == Expected(CustomerType.Turista, 1f, 0, 0, 0), "Turista perfecto y rápido da TuristaFeliz con la propina más alta");

        Customer normal = new Customer();
        normal.Init(CustomerType.Normal, new Order(), 30f, 0);
        var evalExcelente = CookingDeliveryEvaluator.EvaluateDeliveryFeedback(normal, basePrice, 0);
        Assert(evalExcelente.state == CustomerFeedbackState.EntregaExcelente && evalExcelente.tipAmount == Expected(CustomerType.Normal, 1f, 0, 0, 0), "Normal perfecto y rápido da EntregaExcelente");
        Assert(evalTurista.tipAmount > evalExcelente.tipAmount, "El turista deja más que el cliente normal");

        normal.patience = 5f;
        var evalLento = CookingDeliveryEvaluator.EvaluateDeliveryFeedback(normal, basePrice, 0);
        Assert(evalLento.state == CustomerFeedbackState.EntregaAceptable && evalLento.tipAmount == Expected(CustomerType.Normal, 5f / 30f, 0, 0, 0), "Perfecto pero lento da EntregaAceptable con propina reducida");
        Assert(evalLento.tipAmount < evalExcelente.tipAmount * 0.6f, "Tardar cuesta al menos 40% de la propina");

        normal.patience = 15f;
        var evalDesfase1 = CookingDeliveryEvaluator.EvaluateDeliveryFeedback(normal, basePrice, 1);
        var evalPerfectoMedio = CookingDeliveryEvaluator.EvaluateDeliveryFeedback(normal, basePrice, 0);
        Assert(evalDesfase1.state == CustomerFeedbackState.EntregaAceptable && evalDesfase1.tipAmount == Expected(CustomerType.Normal, 0.5f, 1, 0, 0), "Desfase 1 deja propina parcial");
        Assert(evalDesfase1.tipAmount < evalPerfectoMedio.tipAmount, "Desfase 1 deja menos que el punto exacto");

        var evalDesfase2 = CookingDeliveryEvaluator.EvaluateDeliveryFeedback(normal, basePrice, 2);
        Assert(evalDesfase2.state == CustomerFeedbackState.SinPropina && evalDesfase2.tipAmount == 0f, "Desfase 2 da SinPropina");

        normal.patience = 30f;
        var evalRacha = CookingDeliveryEvaluator.EvaluateDeliveryFeedback(normal, basePrice, 0, 1f, 3);
        Assert(evalRacha.tipAmount == Expected(CustomerType.Normal, 1f, 0, 0, 3) && evalRacha.streakBonusAmount > 0f, "La racha de entregas perfectas suma propina");
        var evalRachaTope = CookingDeliveryEvaluator.EvaluateDeliveryFeedback(normal, basePrice, 0, 1f, 99);
        Assert(evalRachaTope.tipAmount == Expected(CustomerType.Normal, 1f, 0, 0, rules.StreakMaxSteps), "La racha tiene tope");
        var evalRachaImperfecta = CookingDeliveryEvaluator.EvaluateDeliveryFeedback(normal, basePrice, 1, 1f, 3);
        Assert(evalRachaImperfecta.streakBonusAmount == 0f, "Una entrega imperfecta no cobra bonus de racha");

        var orderToppings = new Order();
        orderToppings.toppings.Add(ScriptableObject.CreateInstance<ToppingSO>());
        orderToppings.toppings.Add(ScriptableObject.CreateInstance<ToppingSO>());
        Customer complejo = new Customer();
        complejo.Init(CustomerType.Normal, orderToppings, 30f, 0);
        var evalComplejo = CookingDeliveryEvaluator.EvaluateDeliveryFeedback(complejo, basePrice, 0);
        Assert(evalComplejo.tipAmount == Expected(CustomerType.Normal, 1f, 0, 2, 0) && evalComplejo.tipAmount > evalExcelente.tipAmount, "Un pedido con toppings perfecto deja más propina");

        var evalMejora = CookingDeliveryEvaluator.EvaluateDeliveryFeedback(normal, basePrice, 0, 1.45f);
        Assert(evalMejora.tipAmount == Expected(CustomerType.Normal, 1f, 0, 0, 0, 1.45f), "El multiplicador de mejoras escala la propina");

        normal.IsTipAnulada = true;
        var evalAnulada = CookingDeliveryEvaluator.EvaluateDeliveryFeedback(normal, basePrice, 0, 1f, 3);
        Assert(evalAnulada.state == CustomerFeedbackState.SinPropina && evalAnulada.tipAmount == 0f, "Propina anulada por faltante da SinPropina ($0)");

        // 4a. Niveles de propina para el feedback (popup y sonido)
        Assert(rules.GetTier(0f, basePrice) == TipTier.None, "Sin propina no hay nivel");
        Assert(rules.GetTier(1f, basePrice) == TipTier.Normal, "Propina chica es Normal");
        Assert(evalTurista.tier == TipTier.Great && evalTurista.tier > evalLento.tier, "Turista perfecto y rápido celebra más que una entrega lenta");

        // 4b. Racha y totales de la jornada
        DayStats.ResetDay();
        DayStats.RegisterDelivery(100f, 10f, true);
        DayStats.RegisterDelivery(100f, 10f, true);
        Assert(DayStats.PerfectStreak == 2 && DayStats.BestStreak == 2, "Dos entregas perfectas arman racha 2");
        DayStats.RegisterDelivery(50f, 0f, false);
        Assert(DayStats.PerfectStreak == 0 && DayStats.BestStreak == 2, "Una entrega imperfecta corta la racha y conserva la mejor");
        DayStats.RegisterDelivery(100f, 10f, true);
        DayStats.BreakStreak();
        Assert(DayStats.PerfectStreak == 0, "Un cliente perdido corta la racha");
        Assert(Mathf.Approximately(DayStats.SalesToday, 350f) && Mathf.Approximately(DayStats.TipsToday, 30f), "Ventas y propinas de la jornada se acumulan por separado");
        DayStats.ResetDay();

        // 4c. Margen: ningún corte a la venta se puede vender por menos de lo que cuesta.
        // El catálogo llega por la escena (CustomerSystem): en escenas sin parrilla, como el
        // menú principal, no hay catálogo cargado y el chequeo se omite avisando.
        var customerSystem = Object.FindFirstObjectByType<CustomerSystem>();
        FoodCatalogSO catalog = customerSystem != null ? customerSystem.Catalog : null;
        if (catalog == null)
            Debug.Log("[CustomerFeedbackSelfCheck] Chequeo de márgenes omitido: esta escena no tiene catálogo.");
        else
        {
            foreach (var cut in catalog.GetAllCuts())
            {
                if (cut == null || cut.basePrice <= 0f) continue;
                if (cut.servingMode != ServingMode.SandwichOnly)
                    Assert(cut.sellPricePlate > cut.basePrice, $"{cut.cutName}: precio al plato mayor al costo");
                if (cut.servingMode != ServingMode.PlatedOnly)
                    Assert(cut.sellPriceSandwich > cut.basePrice, $"{cut.cutName}: precio en sándwich mayor al costo");
            }
        }

        // 5. Validar congelamiento de paciencia en feedback
        Customer cFeedback = new Customer();
        cFeedback.Init(CustomerType.Normal, new Order(), 30f, 0);
        cFeedback.StartFeedback();
        cFeedback.UpdatePatience(5f);
        Assert(Mathf.Approximately(cFeedback.patience, 30f), "Paciencia no decae mientras IsInFeedback es true");
        Assert(cFeedback.IsInFeedback, "IsInFeedback permanece true");

        cFeedback.EndFeedback();
        Assert(!cFeedback.IsInFeedback, "EndFeedback restaura IsInFeedback a false");
        cFeedback.UpdatePatience(5f);
        Assert(Mathf.Approximately(cFeedback.patience, 25f), "Paciencia decae normalmente tras EndFeedback");

        Debug.Log($"<b>[CustomerFeedbackSelfCheck] Pruebas finalizadas: {passed} exitosas, {failed} fallidas.</b>");
        return failed;
    }
}
