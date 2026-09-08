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
    
    [Header("Strikes")]
    [SerializeField] private AudioClip strikeClip;

    [Header("Delivery Feedback Audio (Spec Doc)")]
    [SerializeField] private AudioClip positiveFeedback;
    [SerializeField] private AudioClip intermediateFeedback;
    [SerializeField] private AudioClip negativeFeedback;

    private AudioSource audioSource;

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

    public void PlayStrikeSound() => audioSource?.PlayOneShot(strikeClip);

    public void PlayPositiveFeedback()
    {
        AudioClip clip = positiveFeedback != null ? positiveFeedback : taskCompleted;
        if (clip != null) audioSource?.PlayOneShot(clip);
    }

    public void PlayIntermediateFeedback()
    {
        AudioClip clip = intermediateFeedback != null ? intermediateFeedback : taskCompleted;
        if (clip != null) audioSource?.PlayOneShot(clip);
    }

    public void PlayNegativeFeedback()
    {
        if (negativeFeedback != null)
            audioSource?.PlayOneShot(negativeFeedback);
    }
}
