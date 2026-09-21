using UnityEngine;

/// <summary>
/// Lo implementa todo lo que cambia con la hora del día (tinte, sprite, posición del sol...).
/// <see cref="DayCycleBackground"/> se lo pasa a todos sus hijos cada vez que la hora cambia.
/// </summary>
public interface IDayCycleVisual
{
    /// <param name="hour">Hora en horas decimales, 0–24 (6.5 = 06:30).</param>
    void ApplyHour(float hour);
}

/// <summary>
/// Fondo por capas que acompaña la jornada: amanece a la apertura (06:30) y se hace de noche
/// hacia el cierre (21:00). Es solo visual: lee la hora de <see cref="DayClock"/> y nunca la escribe.
///
/// Cada capa hija (<see cref="DayCycleLayer"/>, <see cref="DayCycleArc"/>) recibe la hora por
/// <see cref="IDayCycleVisual.ApplyHour"/> y resuelve su propio aspecto; este componente solo
/// decide QUÉ hora mostrar:
/// - con DayClock: su <see cref="DayClock.CurrentHour"/>. Como el reloj corre con Time.deltaTime,
///   la pausa congela también el cielo. Después del cierre el reloj queda clavado en 21:00, así que
///   mientras se atiende a los últimos clientes afuera ya es de noche.
/// - sin DayClock (el tutorial): <see cref="fallbackHour"/>, fija.
///
/// Si la hora salta de golpe (el cierre anticipado por strikes lleva el reloj directo a las 21:00),
/// el cielo no corta: recorre la diferencia a <see cref="catchUpHoursPerSecond"/>, como un time-lapse
/// de un par de segundos. A la velocidad normal del reloj (14,5 h en 300 s, ≈0,05 h/s) ese tope
/// nunca se alcanza.
///
/// No toca nada en modo edición: la escena se ve con los colores guardados en cada SpriteRenderer
/// (el look de mediodía). Para revisar otras horas, en Play activar <see cref="overrideHour"/> y
/// mover el slider.
/// </summary>
[DisallowMultipleComponent]
public class DayCycleBackground : MonoBehaviour
{
    [Header("Hora")]
    [Tooltip("Hora que se muestra si la escena no tiene DayClock (el tutorial). 12 = mediodía.")]
    [Range(0f, 24f)]
    [SerializeField] private float fallbackHour = 12f;

    [Tooltip("Tope de avance del cielo, en horas de juego por segundo real. Solo se nota cuando la hora " +
             "salta (cierre anticipado): en vez de cortar a la noche, la recorre en un par de segundos. " +
             "0 = sin suavizado.")]
    [Min(0f)]
    [SerializeField] private float catchUpHoursPerSecond = 3f;

    [Header("Debug (solo en Play)")]
    [Tooltip("Ignora el reloj y muestra la hora del slider. Para revisar el cielo sin esperar la jornada.")]
    [SerializeField] private bool overrideHour;

    [Range(0f, 24f)]
    [SerializeField] private float overrideHourValue = 6.5f;

    /// <summary>Hora que muestra el fondo ahora. Durante un salto va por detrás del reloj.</summary>
    public float VisualHour { get; private set; }

    private IDayCycleVisual[] visuals;
    private float lastAppliedHour = float.NaN;

    private void Awake()
    {
        visuals = GetComponentsInChildren<IDayCycleVisual>(true);
    }

    private void Start()
    {
        // La primera hora va sin suavizado: la escena arranca con el cielo que corresponde.
        VisualHour = ReadTargetHour();
        ApplyAll();
    }

    private void Update()
    {
        float target = ReadTargetHour();

        VisualHour = overrideHour || catchUpHoursPerSecond <= 0f
            ? target
            : Mathf.MoveTowards(VisualHour, target, catchUpHoursPerSecond * Time.deltaTime);

        // En pausa o ya cerrado la hora no se mueve: no hay nada que reaplicar.
        if (VisualHour != lastAppliedHour)
            ApplyAll();
    }

    private float ReadTargetHour()
    {
        if (overrideHour)
            return overrideHourValue;

        DayClock clock = DayClock.Instance;
        return clock != null ? clock.CurrentHour : fallbackHour;
    }

    private void ApplyAll()
    {
        lastAppliedHour = VisualHour;

        for (int i = 0; i < visuals.Length; i++)
            visuals[i].ApplyHour(VisualHour);
    }
}
