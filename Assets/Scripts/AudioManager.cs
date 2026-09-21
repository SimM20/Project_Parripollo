using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioClip taskCompleted;
    [SerializeField] private AudioClip newClientBell;
    [SerializeField] private AudioClip toppingShake;

    [Header("Sliding Panels")]
    [Tooltip("Suena una vez cada vez que se despliega un panel lateral (stock o items de armado).")]
    [SerializeField] private AudioClip tableSlide;

    [Header("Delivery Feedback Audio (Spec Doc)")]
    [Tooltip("Se elige uno al azar en cada entrega. Vacio = cae a taskCompleted.")]
    [SerializeField] private AudioClip[] positiveFeedbackClips;
    [Tooltip("Se elige uno al azar en cada entrega. Vacio = cae a taskCompleted.")]
    [SerializeField] private AudioClip[] intermediateFeedbackClips;
    [Tooltip("Se elige uno al azar en cada entrega. Vacio = no suena nada.")]
    [SerializeField] private AudioClip[] negativeFeedbackClips;

    private AudioSource audioSource;

    // Ultimo indice reproducido de cada grupo, para no repetir el mismo clip dos veces seguidas.
    private int lastPositiveIndex = -1;
    private int lastIntermediateIndex = -1;
    private int lastNegativeIndex = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();

        if (!audioSource) return;
        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    public void PlayTaskCompleted() => audioSource?.PlayOneShot(taskCompleted);

    public void PlayNewClientBell() => audioSource?.PlayOneShot(newClientBell);

    public void PlayOnUseTopping() => audioSource?.PlayOneShot(toppingShake);

    public void PlayTableSlide()
    {
        if (tableSlide != null)
            audioSource?.PlayOneShot(tableSlide);
    }

    public void PlayPositiveFeedback()
    {
        AudioClip clip = PickRandomClip(positiveFeedbackClips, ref lastPositiveIndex) ?? taskCompleted;
        if (clip != null) audioSource?.PlayOneShot(clip);
    }

    public void PlayIntermediateFeedback()
    {
        AudioClip clip = PickRandomClip(intermediateFeedbackClips, ref lastIntermediateIndex) ?? taskCompleted;
        if (clip != null) audioSource?.PlayOneShot(clip);
    }

    public void PlayNegativeFeedback()
    {
        AudioClip clip = PickRandomClip(negativeFeedbackClips, ref lastNegativeIndex);
        if (clip != null) audioSource?.PlayOneShot(clip);
    }

    /// <summary>
    /// Devuelve un clip al azar del array, evitando repetir el ultimo elegido cuando hay
    /// mas de una opcion. Ignora los slots vacios del array. Null si no hay nada valido.
    /// </summary>
    private static AudioClip PickRandomClip(AudioClip[] clips, ref int lastIndex)
    {
        if (clips == null || clips.Length == 0)
            return null;

        int validCount = 0;
        for (int i = 0; i < clips.Length; i++)
            if (clips[i] != null) validCount++;

        if (validCount == 0)
            return null;

        // Con un solo clip valido no hay nada que variar: se devuelve directo.
        if (validCount == 1)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null)
                {
                    lastIndex = i;
                    return clips[i];
                }
            }
        }

        int index;
        do
        {
            index = Random.Range(0, clips.Length);
        }
        while (clips[index] == null || index == lastIndex);

        lastIndex = index;
        return clips[index];
    }
}
