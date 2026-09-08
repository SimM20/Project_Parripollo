/// <summary>
/// Estados de satisfacción y feedback definidos en el spec doc
/// (Parrilla40 – Sistema Feedback de Entrega y Reacción del Cliente).
/// </summary>
public enum CustomerFeedbackState
{
    /// <summary>Estado 1: Turista feliz (20% propina, verde, 🤩 / 💸)</summary>
    TuristaFeliz = 1,

    /// <summary>Estado 2: Entrega excelente (10% propina, verde, 😄 / 👍)</summary>
    EntregaExcelente = 2,

    /// <summary>Estado 3: Entrega aceptable (3% a 7% propina, amarillo, 🙂)</summary>
    EntregaAceptable = 3,

    /// <summary>Estado 4: Sin propina ($0 propina, lectura visual negativa, rojo, 😐)</summary>
    SinPropina = 4,

    /// <summary>Estado 5: Cambio por faltante (pedido pendiente, propina anulada, amarillo, 😕 / 🤷)</summary>
    CambioPorFaltante = 5,

    /// <summary>Estado 6: No paga / se va (abandono por paciencia 0, rojo fuerte intenso, 😡 / 💢)</summary>
    NoPagaSeVa = 6
}

/// <summary>
/// Categoría visual para código de colores (Verde, Amarillo, Rojo, Rojo Fuerte).
/// </summary>
public enum CustomerFeedbackCategory
{
    Positive,         // Verde (Turista feliz, Entrega excelente)
    Intermediate,     // Amarillo (Entrega aceptable, Cambio por faltante)
    Negative,         // Rojo (Sin propina)
    NegativeSevere    // Rojo fuerte de máxima intensidad (No paga / se va)
}

public static class CustomerFeedbackExtensions
{
    public static CustomerFeedbackCategory GetCategory(this CustomerFeedbackState state)
    {
        switch (state)
        {
            case CustomerFeedbackState.TuristaFeliz:
            case CustomerFeedbackState.EntregaExcelente:
                return CustomerFeedbackCategory.Positive;

            case CustomerFeedbackState.EntregaAceptable:
            case CustomerFeedbackState.CambioPorFaltante:
                return CustomerFeedbackCategory.Intermediate;

            case CustomerFeedbackState.SinPropina:
                return CustomerFeedbackCategory.Negative;

            case CustomerFeedbackState.NoPagaSeVa:
                return CustomerFeedbackCategory.NegativeSevere;

            default:
                return CustomerFeedbackCategory.Intermediate;
        }
    }
}
