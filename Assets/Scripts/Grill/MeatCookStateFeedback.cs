using System.Collections;
using UnityEngine;

/// <summary>
/// Feedback de cambio de punto mientras el corte se cocina en la parrilla. El cambio de
/// sprite solo no alcanza para notarlo con varios cortes a la vez, asi que se refuerza con
/// cosas que le pasan a la carne de verdad en una parrilla:
///
///  - Contraccion: un squash corto, como el rebote de aterrizaje del flip. Tapa el salto del sprite.
///  - Brillo de brasa: una copia del sprite tenida de ambar que se enciende y se apaga.
///  - Bocanada: el FlipPuff del corte con mas particulas. Vapor claro en los primeros puntos,
///    humo mas oscuro y cargado a medida que se acerca al quemado.
///  - Etiqueta: el nombre del punto nuevo sube y se desvanece (CookStateLabel). Pasado en
///    naranja, Quemado en rojo. No sale si la burbuja de hover ya muestra este corte.
///  - Sonido opcional (stateChangeSound). Vacio = mudo, igual que el flipSound.
///
/// Escucha Meat.OnCookStateAdvanced: solo dispara cocinando, nunca al dar vuelta el corte ni
/// al restaurar tiempos desde el plato o la bandeja. Todo usa Time.deltaTime: GamePause lo congela.
/// </summary>
[RequireComponent(typeof(Meat))]
public class MeatCookStateFeedback : MonoBehaviour
{
    [Header("Contraccion")]
    [SerializeField] private float pulseDuration = 0.22f;
    [Tooltip("Squash del pulso: se ensancha en X y se aplasta en Y este porcentaje.")]
    [Range(0f, 0.3f)] [SerializeField] private float pulseStrength = 0.07f;
    [Tooltip("Multiplicador del pulso en el ultimo tramo (Pasado/Quemado).")]
    [SerializeField] private float pulseStrengthNearBurn = 1.5f;

    [Header("Brillo de brasa")]
    [SerializeField] private bool glowEnabled = true;
    [SerializeField] private Color glowColor = new Color(1f, 0.7f, 0.35f, 1f);
    [Range(0f, 1f)] [SerializeField] private float glowAlpha = 0.45f;
    [SerializeField] private float glowAttack = 0.05f;
    [SerializeField] private float glowRelease = 0.4f;
    [Tooltip("Orden de dibujo relativo al sprite de la carne. Debajo del humo (+4) y del puff (+5).")]
    [SerializeField] private int glowSortingOrderOffset = 1;

    [Header("Bocanada")]
    [SerializeField] private bool puffEnabled = true;
    [Tooltip("Color en los primeros puntos: vapor de los jugos.")]
    [SerializeField] private Color steamTint = new Color(0.93f, 0.91f, 0.87f, 1f);
    [Tooltip("Color al llegar al quemado: humo.")]
    [SerializeField] private Color smokeTint = new Color(0.42f, 0.39f, 0.37f, 1f);
    [Tooltip("Intensidad (cantidad y tamano) en Jugoso y en Quemado; interpola en el medio.")]
    [SerializeField] private float puffIntensityFirst = 1.8f;
    [SerializeField] private float puffIntensityBurned = 3.2f;
    [SerializeField] private float puffContactOffsetY = 0.04f;

    [Header("Etiqueta del punto")]
    [SerializeField] private bool labelEnabled = true;
    [SerializeField] private CookStateLabelStyle labelStyle = new CookStateLabelStyle();

    [Header("Sonido")]
    [Tooltip("One-shot al cambiar de punto. Vacio = mudo. No usar los loops de coccion.")]
    [SerializeField] private AudioClip stateChangeSound;
    [Range(0f, 1f)] [SerializeField] private float soundVolume = 0.6f;

    private Meat meat;
    private FlipPuff puff;
    private AudioSource audioSource;

    private SpriteRenderer glowRenderer;
    private Coroutine pulseRoutine;
    private Coroutine glowRoutine;
    private CookStateLabel activeLabel;

    void Awake()
    {
        meat = GetComponent<Meat>();
        puff = GetComponentInChildren<FlipPuff>(true);
        audioSource = GetComponent<AudioSource>();
    }

    void OnEnable()
    {
        if (meat != null)
            meat.OnCookStateAdvanced += HandleStateAdvanced;
    }

    void OnDisable()
    {
        if (meat != null)
            meat.OnCookStateAdvanced -= HandleStateAdvanced;

        StopPulse(restoreScale: false);
        StopGlow();
    }

    void OnDestroy()
    {
        // La carne se destruye al pasar al plato: la etiqueta no queda flotando sola.
        if (activeLabel != null)
            Destroy(activeLabel.gameObject);
    }

    private void HandleStateAdvanced(MeatStates previous, MeatStates current)
    {
        // 0 en Jugoso → 1 en Quemado. Crudo nunca es destino de un avance.
        float t = Mathf.InverseLerp((int)MeatStates.Jugoso, (int)MeatStates.Quemado, (int)current);

        // Durante el flip la escala y el sprite los maneja Meat.FlipRoutine: solo bocanada, etiqueta y sonido.
        if (!meat.IsFlipping)
        {
            StartPulse(t);
            if (glowEnabled) StartGlow();
        }

        if (puffEnabled) EmitPuff(t);
        if (labelEnabled) SpawnLabel(current);
        PlaySound();
    }

    // ---------- Contraccion ----------

    private void StartPulse(float t)
    {
        StopPulse(restoreScale: true);
        float strength = pulseStrength * Mathf.Lerp(1f, pulseStrengthNearBurn, t);
        pulseRoutine = StartCoroutine(PulseRoutine(strength));
    }

    private IEnumerator PulseRoutine(float strength)
    {
        Vector3 baseScale = meat.BaseLocalScale;
        float elapsed = 0f;

        while (elapsed < pulseDuration)
        {
            // Si arranca un flip o la levantan, el pulso cede: el flip maneja su propia escala.
            if (meat.IsFlipping) { pulseRoutine = null; yield break; }
            if (!meat.IsOnGrill) break;

            elapsed += Time.deltaTime;
            float k = Mathf.Sin(Mathf.Clamp01(elapsed / pulseDuration) * Mathf.PI) * strength;
            transform.localScale = new Vector3(baseScale.x * (1f + k), baseScale.y * (1f - k), baseScale.z);
            yield return null;
        }

        transform.localScale = baseScale;
        pulseRoutine = null;
    }

    private void StopPulse(bool restoreScale)
    {
        if (pulseRoutine == null) return;

        StopCoroutine(pulseRoutine);
        pulseRoutine = null;

        if (restoreScale && meat != null && !meat.IsFlipping)
            transform.localScale = meat.BaseLocalScale;
    }

    // ---------- Brillo de brasa ----------

    private void StartGlow()
    {
        if (!EnsureGlowRenderer()) return;

        StopGlow();
        glowRoutine = StartCoroutine(GlowRoutine());
    }

    private IEnumerator GlowRoutine()
    {
        SpriteRenderer source = meat.MainSpriteRenderer;
        glowRenderer.enabled = true;

        float total = glowAttack + glowRelease;
        float elapsed = 0f;

        while (elapsed < total)
        {
            if (meat.IsFlipping || source == null) break;

            elapsed += Time.deltaTime;

            // Sigue al sprite vivo (flipX, cambio de sprite) y a su orden de dibujo.
            glowRenderer.sprite = source.sprite;
            glowRenderer.flipX = source.flipX;
            glowRenderer.sortingLayerID = source.sortingLayerID;
            glowRenderer.sortingOrder = source.sortingOrder + glowSortingOrderOffset;

            float a = elapsed < glowAttack
                ? Mathf.Clamp01(elapsed / Mathf.Max(0.001f, glowAttack))
                : 1f - Mathf.Clamp01((elapsed - glowAttack) / Mathf.Max(0.001f, glowRelease));

            Color c = glowColor;
            c.a = glowColor.a * glowAlpha * a * (source.color.a);
            glowRenderer.color = c;
            yield return null;
        }

        glowRenderer.enabled = false;
        glowRoutine = null;
    }

    private void StopGlow()
    {
        if (glowRoutine != null)
        {
            StopCoroutine(glowRoutine);
            glowRoutine = null;
        }

        if (glowRenderer != null)
            glowRenderer.enabled = false;
    }

    /// <summary>
    /// Copia del sprite como hijo en (0,0,0) local: misma z que la carne (camara en perspectiva,
    /// nota 22) y hereda rotacion y escala, asi acompana el squash y la rotacion con R.
    /// </summary>
    private bool EnsureGlowRenderer()
    {
        if (glowRenderer != null) return true;

        SpriteRenderer source = meat.MainSpriteRenderer;
        if (source == null) return false;

        var go = new GameObject("CookStateGlow");
        go.transform.SetParent(transform, false);

        glowRenderer = go.AddComponent<SpriteRenderer>();
        glowRenderer.sharedMaterial = source.sharedMaterial;
        glowRenderer.enabled = false;
        return true;
    }

    // ---------- Bocanada ----------

    private void EmitPuff(float t)
    {
        if (puff == null) return;

        Vector3 origin = transform.position;
        SpriteRenderer source = meat.MainSpriteRenderer;
        if (source != null)
        {
            Bounds b = source.bounds;
            origin = new Vector3(b.center.x, b.min.y + puffContactOffsetY, transform.position.z);
        }

        puff.Play(origin, Mathf.Lerp(puffIntensityFirst, puffIntensityBurned, t), Color.Lerp(steamTint, smokeTint, t * t));
    }

    // ---------- Etiqueta ----------

    private void SpawnLabel(MeatStates state)
    {
        // Con el mouse encima la burbuja de hover ya dice el punto en vivo: no se duplica.
        if (MeatHoverBubble.Instance != null && MeatHoverBubble.Instance.IsShowing(meat))
            return;

        if (activeLabel != null)
            Destroy(activeLabel.gameObject);

        Vector3 anchor = transform.position;
        SpriteRenderer source = meat.MainSpriteRenderer;
        if (source != null)
        {
            Bounds b = source.bounds;
            anchor = new Vector3(b.center.x, b.max.y, transform.position.z);
        }

        activeLabel = CookStateLabel.Spawn(anchor, state, labelStyle);
    }

    // ---------- Sonido ----------

    /// <summary>
    /// One-shot sobre el AudioSource del prefab, el mismo que usa el flip: MeatInstance lo deja
    /// libre para eso (el chisporroteo va en SizzleAudio).
    /// </summary>
    private void PlaySound()
    {
        if (stateChangeSound == null) return;

        if (audioSource != null)
            audioSource.PlayOneShot(stateChangeSound, soundVolume);
        else
            AudioSource.PlayClipAtPoint(stateChangeSound, transform.position, soundVolume);
    }
}
