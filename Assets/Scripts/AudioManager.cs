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

    private AudioSource audioSource;

    private void Awake()
    {
        if (Instance != null && Instance == this)
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
}
