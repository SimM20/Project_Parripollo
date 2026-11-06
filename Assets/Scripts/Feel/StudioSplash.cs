using UnityEngine;

/// <summary>
/// Pantalla del logo del estudio al abrir el juego. Reproduce una secuencia de sprites sobre un
/// <see cref="SpriteRenderer"/> y pasa al menú principal; cualquier entrada la corta. Vive en
/// StudioSplashScene, la primera escena del build, así que no vuelve a salir al regresar al menú.
/// Corre en tiempo sin escalar.
/// </summary>
public sealed class StudioSplash : MonoBehaviour
{
    [SerializeField] private SpriteRenderer target;
    [Tooltip("Un sprite por cuadro. Los cuadros quietos repiten la misma referencia.")]
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float framesPerSecond = 25f;
    [Tooltip("Espera antes del primer cuadro, para que no arranque tapado por el fundido de entrada.")]
    [SerializeField] private float startDelay = 0.3f;
    [Tooltip("Tiempo que queda quieto el último cuadro antes de salir.")]
    [SerializeField] private float holdSeconds = 0.6f;
    [SerializeField] private string nextScene = "MainMenuScene";

    private float elapsed;
    private bool leaving;

    private void Awake()
    {
        target.sprite = null;
    }

    private void Update()
    {
        if (leaving) return;

        if (MenuSequence.SkipPressed())
        {
            Leave();
            return;
        }

        elapsed += Mathf.Min(Time.unscaledDeltaTime, MenuSequence.MaxFrameStep);
        float time = elapsed - startDelay;
        if (time < 0f) return;

        int frame = Mathf.FloorToInt(time * framesPerSecond);
        target.sprite = frames[Mathf.Min(frame, frames.Length - 1)];

        if (time >= frames.Length / framesPerSecond + holdSeconds)
            Leave();
    }

    private void Leave()
    {
        leaving = true;
        SceneManagementUtils.LoadSceneByName(nextScene);
    }
}
