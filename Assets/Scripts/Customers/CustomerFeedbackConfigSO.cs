using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configuración del Sistema de Feedback de Entrega y Reacción del Cliente.
/// Contiene duraciones, colores, sprites por estado y pools de frases argentinas.
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

    [Header("Phrase Pools (Spec Doc)")]
    [SerializeField]
    private List<string> turistaFelizPhrases = new List<string>
    {
        "¡Una maravilla!",
        "¡Esto está de lujo!",
        "¡Qué joyita, maestro!",
        "¡Me alegraste el viaje!",
        "¡Espectacular, che!"
    };

    [SerializeField]
    private List<string> entregaExcelentePhrases = new List<string>
    {
        "¡Así da gusto!",
        "¡De primera!",
        "¡Una joya!",
        "¡Terrible laburo!",
        "¡Me atendiste de diez!"
    };

    [SerializeField]
    private List<string> entregaAceptablePhrases = new List<string>
    {
        "Bien ahí.",
        "Zafa bastante.",
        "Ta’ bien.",
        "Cumple.",
        "Safó lindo."
    };

    [SerializeField]
    private List<string> sinPropinaPhrases = new List<string>
    {
        "Mmm… hasta ahí.",
        "No me convenció.",
        "Flojito.",
        "Estuvo medio pelo.",
        "No era lo que esperaba."
    };

    [SerializeField]
    private List<string> cambioPorFaltantePhrases = new List<string>
    {
        "Bueno… mandame eso nomás.",
        "Y bueno, traeme otro.",
        "No era lo que quería, pero va.",
        "Dale, lo cambio.",
        "Bueno, zafamos con eso."
    };

    [SerializeField]
    private List<string> noPagaSeVaPhrases = new List<string>
    {
        "Nah, dejá.",
        "Así no te pago.",
        "Me voy re caliente.",
        "Cualquiera esto.",
        "Un desastre, che."
    };

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
            default: return "🙂";
        }
    }

    public string GetRandomPhrase(CustomerFeedbackState state)
    {
        List<string> pool = null;

        switch (state)
        {
            case CustomerFeedbackState.TuristaFeliz:
                pool = turistaFelizPhrases;
                break;
            case CustomerFeedbackState.EntregaExcelente:
                pool = entregaExcelentePhrases;
                break;
            case CustomerFeedbackState.EntregaAceptable:
                pool = entregaAceptablePhrases;
                break;
            case CustomerFeedbackState.SinPropina:
                pool = sinPropinaPhrases;
                break;
            case CustomerFeedbackState.CambioPorFaltante:
                pool = cambioPorFaltantePhrases;
                break;
            case CustomerFeedbackState.NoPagaSeVa:
                pool = noPagaSeVaPhrases;
                break;
        }

        if (pool != null && pool.Count > 0)
        {
            int index = Random.Range(0, pool.Count);
            return pool[index];
        }

        return string.Empty;
    }
}
