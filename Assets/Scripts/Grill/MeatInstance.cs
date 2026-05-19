using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MeatInstance : Meat
{
    private AudioSource audioSource;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = true;
    }

    public override void Cook(float heatFromSlot)
    {
        base.Cook(heatFromSlot);

        float currentFrameTotalHeat = 0f;


        if (heatFromSlot > 100)
            ChangeClipIfNeeded(hardSound);

        else if (heatFromSlot > 0f)
            ChangeClipIfNeeded(softSound);

        else
        {
            if (audioSource.isPlaying) audioSource.Stop();
        }
    }

    private void ChangeClipIfNeeded(AudioClip targetClip)
    {
        if (targetClip == null) return;

        if (audioSource.clip != targetClip)
        {
            audioSource.clip = targetClip;
            audioSource.Play();
        }

        else if (!audioSource.isPlaying)
            audioSource.Play();
    }

    protected override void OnPickedUp()
    {
        base.OnPickedUp();
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }

}