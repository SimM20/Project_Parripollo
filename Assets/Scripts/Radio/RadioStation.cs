using UnityEngine;

/// <summary>
/// La emisora: la música de la radio, que suena siempre, como una radio de verdad.
///
/// Arranca la primera vez que aparece una <see cref="Radio"/> (la mejora ya está comprada) y desde ahí no
/// se corta: las canciones siguen aunque la radio esté apagada, en la tienda o entre noches. Prender o apagar
/// solo sube o baja el volumen, así que al prenderla la canción sigue por donde iba y no desde el principio.
/// Al terminar una canción pasa a otra al azar, nunca la misma dos veces seguidas (con una sola, se repite).
///
/// Es DDOL y se crea sola: <c>GameScene</c> se recarga cada noche y una radio de escena empezaría de cero.
/// Suena solo mientras hay una <see cref="Radio"/> en escena y está prendida. En el menú de pausa se pausa con el
/// resto del audio (<see cref="GamePause"/> → <c>AudioListener.pause</c>) y al salir sigue donde estaba.
/// <see cref="SceneManagementUtils"/> la corta del todo al reiniciar la run o volver al menú.
/// </summary>
[DisallowMultipleComponent]
public sealed class RadioStation : MonoBehaviour
{
    /// <summary>Hasta qué parte de la primera canción puede arrancar: que no empiece casi terminando.</summary>
    private const float MaxStartFraction = 0.6f;

    /// <summary>Segundos que se espera a que una canción empiece a sonar (carga en segundo plano) antes de saltearla.</summary>
    private const float StartTimeout = 10f;

    /// <summary>Prendida o apagada. Sobrevive entre noches; arranca prendida.</summary>
    public static bool IsOn { get; private set; } = true;

    private static RadioStation instance;

    // Serializado para que los clips sigan referenciados al descargarse la escena que los trajo.
    [SerializeField] private AudioClip[] playlist = new AudioClip[0];

    private AudioSource source;
    private Radio listener;
    private int current = -1;
    private float volumeOn = 0.5f;
    private float fadeDuration = 0.4f;

    // Recién después de verla sonar, que no suene quiere decir que terminó: con Load In Background
    // el clip tarda unos frames en cargar y mientras tanto isPlaying da false.
    private bool started;
    private float startDeadline;
    private float nextRetry;
    private float pendingSeek = -1f;

    /// <summary>
    /// Una radio entró a la escena: le pasa las canciones y arranca la emisora si todavía no estaba.
    /// Si ya estaba, la canción sigue: solo se actualiza la lista para las próximas.
    /// </summary>
    public static void Tune(Radio radio, AudioClip[] songs, float volume, float fade)
    {
        if (radio == null) return;

        RadioStation station = GetOrCreate();
        station.listener = radio;
        station.playlist = songs ?? new AudioClip[0];
        station.volumeOn = Mathf.Clamp01(volume);
        station.fadeDuration = Mathf.Max(0f, fade);

        // Si la lista cambió, el índice tiene que seguir apuntando a lo que suena (para no repetirlo después).
        station.current = station.source.clip != null ? System.Array.IndexOf(station.playlist, station.source.clip) : -1;

        if (station.source.clip == null)
            station.PlayNext(true);
    }

    /// <summary>La radio salió de la escena: la emisora sigue, pero no se escucha.</summary>
    public static void Leave(Radio radio)
    {
        if (instance != null && instance.listener == radio)
            instance.listener = null;
    }

    public static void SetOn(bool on) => IsOn = on;

    public static void Toggle() => IsOn = !IsOn;

    /// <summary>Corta la emisora del todo (run nueva). La próxima radio arranca otra vez al azar.</summary>
    public static void Shutdown()
    {
        IsOn = true;
        if (instance != null)
            Destroy(instance.gameObject);
        instance = null;
    }

    private static RadioStation GetOrCreate()
    {
        if (instance != null) return instance;

        var go = new GameObject("[RadioStation]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<RadioStation>();
        return instance;
    }

    private void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.ignoreListenerPause = false; // el menú de pausa la pausa con el resto del audio
    }

    private void Update()
    {
        float target = listener != null && IsOn ? volumeOn : 0f;
        float step = fadeDuration > 0f ? volumeOn / fadeDuration * Time.unscaledDeltaTime : 1f;
        source.volume = Mathf.MoveTowards(source.volume, target, step);

        if (source.clip == null)
            return;

        // En pausa (menú del juego o pausa del Editor) el audio está frenado: que no suene no es que terminó.
        // El plazo para arrancar tampoco corre mientras tanto.
        if (IsAudioPaused())
        {
            startDeadline += Time.unscaledDeltaTime;
            return;
        }

        if (source.isPlaying)
        {
            if (!started)
            {
                started = true;
                // El salto al medio de la canción va cuando ya cargó: antes no tiene efecto.
                if (pendingSeek >= 0f)
                    source.time = pendingSeek;
                pendingSeek = -1f;
            }
            return;
        }

        // Ya cargó y nunca arrancó: el Play se perdió (por ejemplo, una pausa mientras cargaba). Se reintenta.
        if (!started && source.clip.loadState == AudioDataLoadState.Loaded && Time.unscaledTime <= startDeadline)
        {
            // Espaciado: un Play recién pedido puede tardar un instante en dar isPlaying.
            if (Time.unscaledTime >= nextRetry)
            {
                nextRetry = Time.unscaledTime + 0.5f;
                source.Play();
            }
            return;
        }

        // No suena: terminó, o no pudo arrancar (clip roto) y se pasa a otra.
        if (started || Time.unscaledTime > startDeadline)
            PlayNext(false);
    }

    private static bool IsAudioPaused()
    {
#if UNITY_EDITOR
        if (UnityEditor.EditorApplication.isPaused)
            return true;
#endif
        return AudioListener.pause;
    }

    /// <summary>
    /// Pasa a otra canción al azar. La primera de la emisora arranca en un punto al azar: al comprar la
    /// radio ya estaba sonando algo.
    /// </summary>
    private void PlayNext(bool first)
    {
        int next = PickSong();
        if (next < 0)
        {
            current = -1;
            source.clip = null;
            return;
        }

        current = next;
        started = false;
        startDeadline = Time.unscaledTime + StartTimeout;
        nextRetry = Time.unscaledTime + 0.5f;
        pendingSeek = first ? Random.Range(0f, playlist[next].length * MaxStartFraction) : -1f;

        source.clip = playlist[next];
        source.Play();
    }

    /// <summary>Una canción al azar de la lista, salteando los huecos y la que acaba de sonar. -1 si no hay ninguna.</summary>
    private int PickSong()
    {
        int valid = 0;
        for (int i = 0; i < playlist.Length; i++)
            if (playlist[i] != null) valid++;

        if (valid == 0) return -1;

        // Con una sola canción no hay otra para elegir: se repite.
        bool skipCurrent = valid > 1 && current >= 0 && current < playlist.Length && playlist[current] != null;
        int pick = Random.Range(0, skipCurrent ? valid - 1 : valid);

        for (int i = 0; i < playlist.Length; i++)
        {
            if (playlist[i] == null || (skipCurrent && i == current)) continue;
            if (pick-- == 0) return i;
        }

        return -1;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // Los estáticos sobreviven a un Play sin domain reload: se limpian al arrancar.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsOn = true;
        instance = null;
    }
}
