using UnityEngine;

/// <summary>
/// Script de verificación automatizada del Sistema de Feedback de Entrega y Reacción del Cliente.
/// Valida reglas económicas, categorías visuales, pools de frases argentinas y congelamiento de paciencia.
/// </summary>
public static class CustomerFeedbackSelfCheck
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void RunAutomaticValidation()
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

        // 4. Validar Evaluación Económica (Spec Doc)
        float basePrice = 1000f;

        // Turista + punto exacto -> TuristaFeliz (20%)
        Customer turista = new Customer();
        turista.Init(CustomerType.Turista, new Order(), 30f, 0);
        var evalTurista = CookingDeliveryEvaluator.EvaluateDeliveryFeedback(turista, basePrice, 0);
        Assert(evalTurista.state == CustomerFeedbackState.TuristaFeliz && evalTurista.tipAmount == 200f, "Turista punto exacto da TuristaFeliz con 20% propina");

        // Normal + punto exacto + paciencia alta -> EntregaExcelente (10%)
        Customer normal = new Customer();
        normal.Init(CustomerType.Normal, new Order(), 30f, 0);
        var evalExcelente = CookingDeliveryEvaluator.EvaluateDeliveryFeedback(normal, basePrice, 0);
        Assert(evalExcelente.state == CustomerFeedbackState.EntregaExcelente && evalExcelente.tipAmount == 100f, "Cliente normal con alta paciencia da EntregaExcelente con 10% propina");

        // Normal + punto exacto + paciencia baja -> EntregaAceptable (5%)
        normal.patience = 5f; // < 50%
        var evalAceptable = CookingDeliveryEvaluator.EvaluateDeliveryFeedback(normal, basePrice, 0);
        Assert(evalAceptable.state == CustomerFeedbackState.EntregaAceptable && evalAceptable.tipAmount == 50f, "Cliente normal con baja paciencia da EntregaAceptable con 5% propina");

        // Normal + desfase 1 + paciencia media -> EntregaAceptable (5%)
        normal.patience = 15f; // >= 30%
        var evalDesfase1 = CookingDeliveryEvaluator.EvaluateDeliveryFeedback(normal, basePrice, 1);
        Assert(evalDesfase1.state == CustomerFeedbackState.EntregaAceptable && evalDesfase1.tipAmount == 50f, "Desfase 1 con paciencia media da EntregaAceptable");

        // Desfase 2 -> SinPropina ($0)
        var evalDesfase2 = CookingDeliveryEvaluator.EvaluateDeliveryFeedback(normal, basePrice, 2);
        Assert(evalDesfase2.state == CustomerFeedbackState.SinPropina && evalDesfase2.tipAmount == 0f, "Desfase 2 da SinPropina");

        // Propina anulada (por faltante) -> SinPropina ($0) incluso con punto exacto
        normal.IsTipAnulada = true;
        var evalAnulada = CookingDeliveryEvaluator.EvaluateDeliveryFeedback(normal, basePrice, 0);
        Assert(evalAnulada.state == CustomerFeedbackState.SinPropina && evalAnulada.tipAmount == 0f, "Propina anulada por faltante da SinPropina ($0)");

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
    }
}
