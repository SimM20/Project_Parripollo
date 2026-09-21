using UnityEngine;

/// <summary>
/// Configuración de la Derrota Total de la Run (spec "Sistema de Derrota y Finalización
/// Anticipada del Día" v0.1, puntos 4 a 7).
///
/// Dos reglas distintas viven acá porque las dos definen cuándo se termina la run:
/// los mínimos de recursos para arrancar el día siguiente, y el tope de noches
/// seguidas que pueden cerrar por el límite de strikes.
///
/// Todos los valores son de balance y están sujetos a playtesting.
/// </summary>
[CreateAssetMenu(fileName = "RunDefeatConfig", menuName = "Asado/Run Defeat Config")]
public class RunDefeatConfigSO : ScriptableObject
{
    [Header("Mínimos para arrancar el día (spec punto 5)")]
    [Tooltip("Cortes totales necesarios para comenzar el Día 1.")]
    [Min(0)] public int baseMeatRequirement = 5;

    [Tooltip("Cortes que se suman al mínimo por cada día que pasa.")]
    [Min(0)] public int meatRequirementPerDay = 2;

    [Tooltip("Unidades de carbón necesarias para comenzar el Día 1.")]
    [Min(0)] public int baseCoalRequirement = 10;

    [Tooltip("Unidades de carbón que se suman al mínimo por cada día que pasa.")]
    [Min(0)] public int coalRequirementPerDay = 1;

    [Header("Derrota por noches seguidas con el cartel clavado")]
    [Tooltip("Si está apagado, la racha se sigue contando y mostrando pero nunca provoca derrota.")]
    public bool strikeStreakDefeatEnabled = true;

    [Tooltip("Noches SEGUIDAS que pueden terminar por el límite de strikes antes de perder la run.")]
    [Min(1)] public int maxConsecutiveStrikeNights = 3;

    public int MaxConsecutiveStrikeNights => Mathf.Max(1, maxConsecutiveStrikeNights);

    /// <summary>
    /// Cortes totales necesarios para comenzar el día indicado. El mínimo se compone con
    /// cualquier combinación de cortes: no hay mínimo individual por tipo.
    /// </summary>
    public int GetMeatRequirement(int day)
        => baseMeatRequirement + meatRequirementPerDay * (Mathf.Max(1, day) - 1);

    /// <summary>Unidades de carbón necesarias para comenzar el día indicado. Día 1 = valor base.</summary>
    public int GetCoalRequirement(int day)
        => baseCoalRequirement + coalRequirementPerDay * (Mathf.Max(1, day) - 1);
}
