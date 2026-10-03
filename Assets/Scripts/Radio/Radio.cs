using UnityEngine;

/// <summary>
/// La radio del mostrador (mejora <c>RadioUpgrade</c>, la activa <see cref="UpgradeUnlockActivator"/>).
/// Click / A-✕ la prende o la apaga. La música la lleva <see cref="RadioStation"/>, que sigue sonando
/// aunque esté apagada: al prenderla, la canción sigue por donde iba.
///
/// Las canciones se cargan en <see cref="songs"/>: pueden ser cualquier cantidad, incluso ninguna
/// (la radio se prende y se apaga igual, sin sonido). Prendida, salta al ritmo como en el demake.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Radio : MonoBehaviour
{
    [Header("Canciones")]
    [Tooltip("Se eligen al azar, sin repetir la misma dos veces seguidas. Huecos vacíos se saltean. " +
             "Para temas largos conviene Load Type = Streaming en el import.")]
    [SerializeField] private AudioClip[] songs = new AudioClip[0];
    [Tooltip("Volumen con la radio prendida: va de fondo, debajo de los efectos.")]
    [SerializeField, Range(0f, 1f)] private float volume = 0.5f;
    [Tooltip("Segundos que tarda en subir o bajar el volumen al prender o apagar.")]
    [SerializeField, Min(0f)] private float fadeDuration = 0.4f;

    [Header("Visual")]
    [Tooltip("Sprite de la radio. Conviene que esté en un hijo: es lo que salta, y así el collider queda quieto.")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("Opcional: sprite con la radio prendida. Vacío = no cambia.")]
    [SerializeField] private Sprite onSprite;
    [Tooltip("Opcional: sprite con la radio apagada. Vacío = no cambia.")]
    [SerializeField] private Sprite offSprite;
    [Tooltip("Cuánto salta mientras suena (unidades locales del padre del sprite).")]
    [SerializeField, Min(0f)] private float bobHeight = 0.04f;
    [Tooltip("Saltos por segundo mientras suena.")]
    [SerializeField, Min(0f)] private float bobsPerSecond = 2f;

    private Transform visual;
    private Vector3 basePosition;

    public bool IsOn => RadioStation.IsOn;

    void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        visual = spriteRenderer != null ? spriteRenderer.transform : transform;
        basePosition = visual.localPosition;
    }

    void OnEnable()
    {
        RadioStation.Tune(this, songs, volume, fadeDuration);
        RefreshVisual();
    }

    void OnDisable()
    {
        RadioStation.Leave(this);
        visual.localPosition = basePosition;
    }

    void OnWorldPointerDown()
    {
        Toggle();
    }

    public void Toggle()
    {
        RadioStation.Toggle();
        RefreshVisual();
    }

    void Update()
    {
        // Salto seco de dos posiciones, al estilo del demake. Time.time: en la pausa se frena con la música.
        bool up = IsOn && bobHeight > 0f && Mathf.Repeat(Time.time * bobsPerSecond, 1f) >= 0.5f;
        visual.localPosition = basePosition + (up ? Vector3.up * bobHeight : Vector3.zero);
    }

    private void RefreshVisual()
    {
        if (spriteRenderer == null) return;

        Sprite sprite = IsOn ? onSprite : offSprite;
        if (sprite != null)
            spriteRenderer.sprite = sprite;
    }
}
