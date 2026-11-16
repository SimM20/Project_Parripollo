/// <summary>
/// Dónde terminan los logros y stats. <see cref="AchievementService"/> decide QUÉ se desbloquea;
/// el backend solo sabe CÓMO guardarlo (Steam, o la consola cuando Steam no está).
/// </summary>
public interface IAchievementBackend
{
    /// <summary>Nombre corto para los logs.</summary>
    string Name { get; }

    bool IsUnlocked(string achievementId);

    /// <summary>Desbloquea el logro. Idempotente: desbloquear dos veces no hace nada.</summary>
    void Unlock(string achievementId);

    int GetStat(string statId);

    void SetStat(string statId, int value);

    /// <summary>Barra de progreso en el overlay ("50/100"). Opcional: puede no hacer nada.</summary>
    void IndicateProgress(string achievementId, int current, int max);

    /// <summary>Manda lo pendiente. Steam limita la frecuencia: no llamarlo por frame.</summary>
    void Store();

    /// <summary>Solo QA: borra stats y logros de la cuenta activa.</summary>
    void ResetAll();
}
