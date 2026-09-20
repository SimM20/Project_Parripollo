using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configuración de la progresión por días: qué cortes se desbloquean cada día y cuánto
/// dura la ventana de entrada de clientes.
///
/// La progresión depende <b>exclusivamente del número de día</b>. No interviene el dinero
/// ganado, los clientes atendidos, las propinas ni la calidad de los pedidos: superar un día
/// alcanza para acceder al contenido del siguiente.
///
/// Todo es configurable desde el inspector para poder hacer playtesting sin tocar código.
/// </summary>
[CreateAssetMenu(fileName = "ProgressionConfig", menuName = "Asado/Progression Config")]
public class ProgressionConfigSO : ScriptableObject
{
    [System.Serializable]
    public class DayCutUnlock
    {
        [Min(1)]
        [Tooltip("Día a partir del cual estos cortes pueden aparecer en los pedidos.")]
        public int day = 1;

        [Tooltip("Cortes que se suman ese día. Sus variantes de servicio se desbloquean junto con el corte.")]
        public List<MeatCutSO> cuts = new List<MeatCutSO>();
    }

    [Header("Calendario de cortes")]
    [Tooltip("Un corte que no figure en ninguna entrada se considera desbloqueado desde el día 1.")]
    [SerializeField] private List<DayCutUnlock> cutUnlocks = new List<DayCutUnlock>();

    [Header("Ventana de entrada de clientes")]
    [Tooltip("Duración de la ventana del día 1, en segundos. Por defecto 180 = 3:00.")]
    [Min(1f)]
    [SerializeField] private float firstDaySeconds = 180f;

    [Tooltip("Aumento excepcional del día 1 al día 2, en segundos. Por defecto +40 → 3:40.")]
    [Min(0f)]
    [SerializeField] private float secondDayIncrementSeconds = 40f;

    [Tooltip("Aumento habitual de cada día a partir del día 3, en segundos. Por defecto +20.")]
    [Min(0f)]
    [SerializeField] private float standardIncrementSeconds = 20f;

    [Tooltip("Día de compensación que NO aumenta respecto del anterior. Por defecto el 8 (queda en 5:20). " +
             "En 0 no se saltea ningún día.")]
    [Min(0)]
    [SerializeField] private int dayWithoutIncrement = 8;

    [Tooltip("Tope absoluto de la ventana, en segundos. Por defecto 420 = 7:00.")]
    [Min(1f)]
    [SerializeField] private float maxWindowSeconds = 420f;

    public float FirstDaySeconds => firstDaySeconds;
    public float MaxWindowSeconds => maxWindowSeconds;

    // ── Ventana de entrada ──────────────────────────────────────────────────

    /// <summary>
    /// Segundos que dura la ventana de entrada de clientes del día indicado.
    /// Día 1 = <see cref="firstDaySeconds"/>; el día 2 suma el incremento excepcional;
    /// de ahí en más suma el incremento estándar, salteando
    /// <see cref="dayWithoutIncrement"/>, siempre con tope en <see cref="maxWindowSeconds"/>.
    /// </summary>
    public float GetWindowSeconds(int day)
    {
        float seconds = firstDaySeconds;

        for (int d = 2; d <= Mathf.Max(1, day); d++)
        {
            if (d == dayWithoutIncrement)
                continue;

            seconds += d == 2 ? secondDayIncrementSeconds : standardIncrementSeconds;
        }

        return Mathf.Min(seconds, maxWindowSeconds);
    }

    // ── Calendario de cortes ────────────────────────────────────────────────

    /// <summary>Día en que se desbloquea el corte. Si no está en el calendario, día 1.</summary>
    public int GetUnlockDay(MeatCutSO cut)
    {
        if (cut == null)
            return 1;

        for (int i = 0; i < cutUnlocks.Count; i++)
        {
            DayCutUnlock entry = cutUnlocks[i];
            if (entry == null || entry.cuts == null)
                continue;

            if (entry.cuts.Contains(cut))
                return Mathf.Max(1, entry.day);
        }

        return 1;
    }

    /// <summary>Cortes que se suman exactamente ese día (para avisos de desbloqueo).</summary>
    public List<MeatCutSO> GetCutsUnlockedOn(int day)
    {
        var result = new List<MeatCutSO>();

        for (int i = 0; i < cutUnlocks.Count; i++)
        {
            DayCutUnlock entry = cutUnlocks[i];
            if (entry == null || entry.cuts == null || Mathf.Max(1, entry.day) != day)
                continue;

            for (int c = 0; c < entry.cuts.Count; c++)
                if (entry.cuts[c] != null && !result.Contains(entry.cuts[c]))
                    result.Add(entry.cuts[c]);
        }

        return result;
    }

    /// <summary>
    /// Escribe <c>isUnlocked</c> en todos los cortes y variantes del catálogo según el día.
    /// Escribe siempre el valor completo (true y false), así el estado guardado en los assets
    /// no puede quedar desfasado: el día manda.
    /// Al desbloquear un corte se desbloquean <b>todas</b> sus variantes de servicio.
    /// </summary>
    public void ApplyUnlocks(int day, FoodCatalogSO catalog)
    {
        if (catalog == null)
        {
            Debug.LogWarning(
                "[Progresion] No hay catálogo: no se pueden aplicar los desbloqueos del día " + day + "."
            );
            return;
        }

        var cuts = catalog.GetAllCuts();
        var unlockedNames = new List<string>();

        for (int i = 0; i < cuts.Count; i++)
        {
            MeatCutSO cut = cuts[i];
            if (cut == null)
                continue;

            cut.isUnlocked = GetUnlockDay(cut) <= day;

            if (cut.isUnlocked)
                unlockedNames.Add(cut.cutName);
        }

        var variants = catalog.GetAllVariants();

        for (int i = 0; i < variants.Count; i++)
        {
            ProductVariantSO variant = variants[i];
            if (variant == null)
                continue;

            // Sin corte asociado no hay progresión que aplicarle: queda disponible.
            variant.isUnlocked = variant.cut == null || variant.cut.isUnlocked;
        }

        Debug.Log(
            "[Progresion] Día " + day + " | Cortes desbloqueados (" + unlockedNames.Count + "): " +
            string.Join(", ", unlockedNames)
        );
    }

    private void OnValidate()
    {
        maxWindowSeconds = Mathf.Max(firstDaySeconds, maxWindowSeconds);
    }
}
