using UnityEngine;

/// <summary>
/// Arranque de la plataforma en MainMenuScene: aplica la configuración guardada del jugador
/// (resolución, modo de pantalla, FPS, VSync, tipo de control). La lectura y escritura de
/// <c>init.cfg</c> vive en <see cref="GameSettings"/>.
/// </summary>
public class Init : MonoBehaviour
{
    private void Awake()
    {
        GameSettings.ApplyCurrent();
    }
}
