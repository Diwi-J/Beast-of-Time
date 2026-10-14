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
    [SerializeField] private AudioClip runningClip;
    [SerializeField] private AudioClip monsterClip;
    [SerializeField] private AudioClip monsterStompClip;

    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    private AudioSource audioSource;
    private AudioSource runSource; // dedicated source so the running sound can be stopped
    private AudioSource stompSource; // dedicated looping source so the stomp can be stopped

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        // Stops the AudioSource from playing a clip on its own when the scene starts.
        audioSource.playOnAwake = false;
        // Make a second AudioSource just for the looping running sound.
        runSource = gameObject.AddComponent<AudioSource>();
        runSource.playOnAwake = false;
        runSource.loop = true;
        runSource.clip = runningClip;
        runSource.volume = volume;

        stompSource = gameObject.AddComponent<AudioSource>();
        stompSource.playOnAwake = false;
        stompSource.loop = true;
        stompSource.clip = monsterStompClip;
        stompSource.volume = volume;
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

    public void PlayRun()
    {
        // Only start it if it isn't already playing, so it doesn't restart every frame.
        if (runningClip == null || runSource.isPlaying) return;
        runSource.Play();
    }

    public void StopRun()
    {
        if (runSource.isPlaying) runSource.Stop();
    }
    public void PlayStomp()
    {
        if (monsterStompClip == null)
        {
            Debug.LogWarning("[Stomp] monsterStompClip is NOT assigned");
            return;
        }
        if (stompSource.isPlaying) return;
        Debug.Log("[Stomp] playing");
        stompSource.Play();
    }

    public void StopStomp()
    {
        if (stompSource.isPlaying) stompSource.Stop();
    }

    public void PlayRoar()
    {

        PlayClip(monsterClip, 1f);
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