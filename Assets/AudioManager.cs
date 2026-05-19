using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioClip taskCompleted;

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

        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    [ContextMenu("TEST")]
    public void PlayTaskCompleted() => audioSource.PlayOneShot(taskCompleted);
}
