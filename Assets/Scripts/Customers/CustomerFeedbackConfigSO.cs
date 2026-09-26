using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configuración del Sistema de Feedback de Entrega y Reacción del Cliente.
/// Contiene duraciones, colores y sprites por estado. Las frases están en las tablas de <see cref="Loc"/>.
/// </summary>
[CreateAssetMenu(fileName = "CustomerFeedbackConfig", menuName = "Parripollo/Customer Feedback Config")]
public class CustomerFeedbackConfigSO : ScriptableObject
{
    private static CustomerFeedbackConfigSO runtimeDefault;

    public static CustomerFeedbackConfigSO Instance
    {
        get
        {
            if (runtimeDefault == null)
            {
                var loaded = Resources.Load<CustomerFeedbackConfigSO>("CustomerFeedbackConfig");
                if (loaded != null)
                {
                    runtimeDefault = loaded;
                }
                else
                {
                    runtimeDefault = CreateInstance<CustomerFeedbackConfigSO>();
                    runtimeDefault.name = "CustomerFeedbackConfig (Default)";
                }
            }
            return runtimeDefault;
        }
    }

    [Header("Timings (Spec Doc)")]
    [Tooltip("Duración total del feedback en segundos antes de retirar al cliente (4s según spec).")]
    [SerializeField] private float feedbackDuration = 4.0f;

    [Tooltip("Separación temporal en segundos entre la aparición de la frase y el desglose económico.")]
    [SerializeField] private float economicFeedbackDelay = 0.35f;

    [Tooltip("Duración de la animación de salida / desvanecimiento al completarse los 4s.")]
    [SerializeField] private float exitAnimationDuration = 0.25f;

    [Header("Color Coding (Spec Doc)")]
    [Tooltip("Categoría positiva: Verde (Turista feliz, Entrega excelente).")]
    [SerializeField] private Color positiveColor = new Color(0.18f, 0.80f, 0.44f, 1f);

    [Tooltip("Categoría intermedia: Amarillo (Entrega aceptable, Cambio por faltante).")]
    [SerializeField] private Color intermediateColor = new Color(0.96f, 0.76f, 0.12f, 1f);

    [Tooltip("Categoría negativa: Rojo (Sin propina).")]
    [SerializeField] private Color negativeColor = new Color(0.92f, 0.26f, 0.26f, 1f);

    [Tooltip("Categoría negativa severa: Rojo fuerte con máxima intensidad visual (No paga / se va).")]
    [SerializeField] private Color negativeSevereColor = new Color(0.72f, 0.08f, 0.08f, 1f);

    [Header("Reaction Sprites (Arte TBD - Opcionales, con fallback)")]
    [SerializeField] private Sprite turistaFelizSprite;
    [SerializeField] private Sprite entregaExcelenteSprite;
    [SerializeField] private Sprite entregaAceptableSprite;
    [SerializeField] private Sprite sinPropinaSprite;
    [SerializeField] private Sprite cambioPorFaltanteSprite;
    [SerializeField] private Sprite noPagaSeVaSprite;
    [SerializeField] private Sprite entregaCrudaOQuemadaSprite;

    // Las frases viven en la tabla de localización (Resources/Localization/Customers.csv) como
    // pools numerados: feedback.phrase.<estado>.1, .2, ... Cada idioma puede tener su propia cantidad.

    public float FeedbackDuration => feedbackDuration;
    public float EconomicFeedbackDelay => economicFeedbackDelay;
    public float ExitAnimationDuration => exitAnimationDuration;

    public Color GetColor(CustomerFeedbackCategory category)
    {
        switch (category)
        {
            case CustomerFeedbackCategory.Positive:
                return positiveColor;
            case CustomerFeedbackCategory.Intermediate:
                return intermediateColor;
            case CustomerFeedbackCategory.Negative:
                return negativeColor;
            case CustomerFeedbackCategory.NegativeSevere:
                return negativeSevereColor;
            default:
                return intermediateColor;
        }
    }

    public Sprite GetSprite(CustomerFeedbackState state)
    {
        switch (state)
        {
            case CustomerFeedbackState.TuristaFeliz: return turistaFelizSprite;
            case CustomerFeedbackState.EntregaExcelente: return entregaExcelenteSprite;
            case CustomerFeedbackState.EntregaAceptable: return entregaAceptableSprite;
            case CustomerFeedbackState.SinPropina: return sinPropinaSprite;
            case CustomerFeedbackState.CambioPorFaltante: return cambioPorFaltanteSprite;
            case CustomerFeedbackState.NoPagaSeVa: return noPagaSeVaSprite;
            case CustomerFeedbackState.EntregaCrudaOQuemada: return entregaCrudaOQuemadaSprite;
            default: return null;
        }
    }

    public string GetFallbackEmoji(CustomerFeedbackState state)
    {
        switch (state)
        {
            case CustomerFeedbackState.TuristaFeliz: return "🤩";
            case CustomerFeedbackState.EntregaExcelente: return "😄";
            case CustomerFeedbackState.EntregaAceptable: return "🙂";
            case CustomerFeedbackState.SinPropina: return "😐";
            case CustomerFeedbackState.CambioPorFaltante: return "😕";
            case CustomerFeedbackState.NoPagaSeVa: return "😡";
            case CustomerFeedbackState.EntregaCrudaOQuemada: return "🤢";
            default: return "🙂";
        }
    }

    /// <summary>
    /// Frase de reacción del estado. <paramref name="burnedVariant"/> solo lo mira
    /// <see cref="CustomerFeedbackState.EntregaCrudaOQuemada"/>, que tiene un pool por motivo:
    /// quejarse de carne cruda no es lo mismo que quejarse de un carbón.
    /// </summary>
    public string GetRandomPhrase(CustomerFeedbackState state, bool burnedVariant = false)
    {
        string pool = GetPhrasePoolKey(state, burnedVariant);
        if (pool == null) return string.Empty;

        List<string> phrases = Loc.GetPool(pool);
        return phrases.Count > 0 ? phrases[Random.Range(0, phrases.Count)] : string.Empty;
    }

    /// <summary>Prefijo del pool de frases del estado en las tablas de <see cref="Loc"/>.</summary>
    public static string GetPhrasePoolKey(CustomerFeedbackState state, bool burnedVariant = false)
    {
        switch (state)
        {
            case CustomerFeedbackState.TuristaFeliz: return "feedback.phrase.tourist_happy";
            case CustomerFeedbackState.EntregaExcelente: return "feedback.phrase.excellent";
            case CustomerFeedbackState.EntregaAceptable: return "feedback.phrase.acceptable";
            case CustomerFeedbackState.SinPropina: return "feedback.phrase.no_tip";
            case CustomerFeedbackState.CambioPorFaltante: return "feedback.phrase.missing_cut";
            case CustomerFeedbackState.NoPagaSeVa: return "feedback.phrase.leaves_angry";
            case CustomerFeedbackState.EntregaCrudaOQuemada:
                return burnedVariant ? "feedback.phrase.burnt" : "feedback.phrase.raw";
            default: return null;
        }
    }
}
