using System;
using UnityEngine;

/// <summary>
/// Único dueño de <see cref="Time.timeScale"/>, <see cref="Camera.eventMask"/> y
/// <see cref="AudioListener.pause"/>. Hay dos fuentes de pausa independientes (el menú de
/// ESC y el diálogo de oferta del tutorial): el juego queda pausado mientras cualquiera
/// esté activa, y nadie más debe escribir <c>Time.timeScale</c>.
///
/// Con timeScale = 0 se congela todo lo que dependa de Time.deltaTime / Time.time /
/// WaitForSeconds (cocción, carbón, paciencia, spawn, feedback). eventMask = 0 apaga los
/// OnMouseXXX de los colliders del mundo, así que solo responde la UI del canvas de pausa.
/// Los arrastres en curso se cancelan vía <see cref="OnPaused"/>: cada draggable se
/// suscribe al empezar a arrastrar y se desuscribe al soltar o cancelar.
///
/// timeScale y AudioListener.pause persisten entre escenas: <see cref="SceneManagementUtils"/>
/// llama a <see cref="Reset"/> antes de cada carga.
/// </summary>
public static class GamePause
{
    public static bool IsPaused => menuPaused || dialogPaused;
    public static bool IsMenuPaused => menuPaused;
    public static bool IsDialogPaused => dialogPaused;

    /// <summary>Se dispara una sola vez por transición no pausado → pausado.</summary>
    public static event Action OnPaused;

    private static bool menuPaused;
    private static bool dialogPaused;
    private static bool applied;

    private static Camera maskedCamera;
    private static int savedEventMask = -1;

    public static void SetMenuPaused(bool paused)
    {
        menuPaused = paused;
        Apply();
    }

    public static void SetDialogPaused(bool paused)
    {
        dialogPaused = paused;
        Apply();
    }

    /// <summary>Limpia todas las fuentes y restaura tiempo, audio e input del mundo.</summary>
    public static void Reset()
    {
        menuPaused = false;
        dialogPaused = false;
        applied = false;

        Time.timeScale = 1f;
        AudioListener.pause = false;
        RestoreWorldInput();
    }

    private static void Apply()
    {
        bool shouldPause = IsPaused;
        if (shouldPause == applied)
            return;

        applied = shouldPause;

        Time.timeScale = shouldPause ? 0f : 1f;
        AudioListener.pause = shouldPause;

        if (shouldPause)
        {
            BlockWorldInput();
            OnPaused?.Invoke();
        }
        else
        {
            RestoreWorldInput();
        }
    }

    private static void BlockWorldInput()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        maskedCamera = cam;
        savedEventMask = cam.eventMask;
        cam.eventMask = 0;
    }

    private static void RestoreWorldInput()
    {
        if (maskedCamera != null)
            maskedCamera.eventMask = savedEventMask;

        maskedCamera = null;
        savedEventMask = -1;
    }

    // Los estáticos sobreviven a un Play sin domain reload: se limpian al arrancar.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Reset();
        OnPaused = null;
    }
}
