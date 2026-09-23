using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PlayerSwordSound : MonoBehaviour
{
    // Sword being pulled out of its sheath.
    [SerializeField] private AudioClip drawClip;

    // Sword cutting through the air. 
    [SerializeField] private AudioClip sliceClip;

    // Sword hitting another sword, or a successful parry.
    [SerializeField] private AudioClip clashClip;

    // Sword cutting through enemy. 
    [SerializeField] private AudioClip hitClip;
    [SerializeField] private AudioClip chargeClip;

    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        // Stops the AudioSource from playing a clip on its own when the scene starts.
        audioSource.playOnAwake = false;
    }

    // Call this when the player pulls out their sword.
    public void PlayDraw()
    {
        PlayClip(drawClip, 1f);
    }
    public void PlayCharge()
    {
        PlayClip(chargeClip, 1f);
    }

    // Call this at the moment the sword swings.
    public void PlaySlice()
    {
        
        PlayClip(sliceClip, 1f);
    }

    // Call this when swords collide, for example next to gauge.OnParrySuccess().
    public void PlayClash()
    {
        PlayClip(clashClip, 1f);
    }
    // Call this at the moment enemy hit.
    public void PlayHit()
    {

        PlayClip(hitClip, 1f);
    }

    // Shared helper so all three sounds are played the same way.
    private void PlayClip(AudioClip clip, float pitch)
    {
        if (clip == null)
        {
            return;
        }

        // The pitch is set every time, so one sound never inherits another's random pitch.
        audioSource.pitch = pitch;

        // PlayOneShot lets sounds overlap, so fast attacks don't cut each other off.
        audioSource.PlayOneShot(clip, volume);
    }
}