using System.Text;
using UnityEngine;

/// <summary>
/// Run del evento: a nombre de quién está y cuándo se cierra para guardar su puntaje.
///
/// El puntaje es <see cref="PlayerWallet.TotalEarned"/>: todo lo cobrado, sin restar compras
/// ni mejoras. La run se abre con <see cref="Begin"/> (al confirmar el nombre) y se cierra
/// con <see cref="Finish"/>, que es idempotente: la llaman la derrota, la vuelta al menú,
/// el arranque de la run siguiente y el cierre del juego, y solo la primera guarda.
/// </summary>
public static class EventRun
{
    public const int MaxNameLength = 20;

    /// <summary>
    /// Días por run: al completar el último la run termina sola (ver
    /// <see cref="RunDefeatReason.EventDaysLimit"/>). Así gana el que mejor juega y no el que
    /// más horas aguanta. Se puede perder antes, igual que siempre.
    /// </summary>
    public const int MaxDays = 3;

    /// <summary>Dueño de la run en curso. Null = no hay run del evento abierta.</summary>
    public static string PlayerName { get; private set; }

    /// <summary>Último nombre confirmado: precarga el cartel del nombre al reintentar.</summary>
    public static string LastPlayerName { get; private set; }

    public static bool IsActive => !string.IsNullOrEmpty(PlayerName);

    /// <summary>
    /// Abre una run nueva. Si quedó una abierta la cierra antes, todavía con la billetera
    /// vieja: el reinicio la destruye recién al final del fundido.
    /// </summary>
    public static void Begin(string playerName)
    {
        Finish();

        PlayerName = SanitizeName(playerName);
        LastPlayerName = PlayerName;

        Debug.Log("[EventRun] Run a nombre de '" + PlayerName + "'.");
    }

    /// <summary>
    /// Cierra la run y guarda su puntaje en el leaderboard. Sin run abierta no hace nada.
    /// </summary>
    /// <param name="upload">False al cerrar el juego: no hay tiempo de subir, queda en cola.</param>
    public static void Finish(bool upload = true)
    {
        if (!IsActive) return;

        float totalEarned = PlayerWallet.Instance != null ? PlayerWallet.Instance.TotalEarned : 0f;
        string playerName = PlayerName;
        PlayerName = null;

        EventLeaderboard.Record(playerName, totalEarned, upload);
    }

    /// <summary>Sin espacios de más ni caracteres de control, y como mucho <see cref="MaxNameLength"/>.</summary>
    public static string SanitizeName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        var sb = new StringBuilder(raw.Length);
        bool pendingSpace = false;

        foreach (char c in raw)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = sb.Length > 0;
                continue;
            }
            if (char.IsControl(c)) continue;

            if (pendingSpace) sb.Append(' ');
            pendingSpace = false;
            sb.Append(c);
        }

        string clean = sb.ToString();
        return clean.Length > MaxNameLength ? clean.Substring(0, MaxNameLength).TrimEnd() : clean;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        PlayerName = null;
        LastPlayerName = null;

        Application.quitting -= HandleQuitting;
        Application.quitting += HandleQuitting;
    }

    // Cerrar el juego a mitad de run también cuenta: se guarda local y se sube al próximo arranque.
    private static void HandleQuitting() => Finish(upload: false);
}
