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

    [Header("Menu / UI / Game State")]
    [SerializeField] private AudioClip menuMusicClip;
    [SerializeField] private AudioClip buttonClickClip;
    [SerializeField] private AudioClip victoryClip;
    [SerializeField] private AudioClip gameOverClip;

    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.6f;

    private AudioSource audioSource;
    private AudioSource runSource;   // dedicated source so the running sound can be stopped
    private AudioSource stompSource; // dedicated looping source so the stomp can be stopped
    private AudioSource musicSource; // dedicated source for menu music

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

        // Looping source for the menu music.
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.clip = menuMusicClip;
        musicSource.volume = musicVolume;
    }

    // ---------- Sword sounds ----------

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

    // ---------- Menu / UI / Game state ----------

    // Call when the main menu opens.
    public void PlayMenuMusic()
    {
        if (menuMusicClip == null)
        {
            Debug.LogWarning("[Music] menuMusicClip is NOT assigned");
            return;
        }
        if (musicSource.isPlaying) return;
        musicSource.Play();
    }

    // Call when the game starts or the menu closes.
    public void StopMenuMusic()
    {
        if (musicSource.isPlaying) musicSource.Stop();
    }

    // Hook this to the OnClick event of every UI button.
    public void PlayButtonClick()
    {
        PlayClip(buttonClickClip, 1f);
    }

    // Call when the player wins.
    public void PlayVictory()
    {
        StopAllLoops();
        PlayClip(victoryClip, 1f);
    }

    // Call when the player dies / loses.
    public void PlayGameOver()
    {
        StopAllLoops();
        PlayClip(gameOverClip, 1f);
    }

    // Silences everything that loops, so the victory/game over sound is heard clearly.
    private void StopAllLoops()
    {
        StopRun();
        StopStomp();
        StopMenuMusic();
    }

    // Shared helper so all one-shot sounds are played the same way.
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