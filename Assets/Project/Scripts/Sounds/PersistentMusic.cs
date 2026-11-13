using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PersistentMusic : MonoBehaviour
{
    private static PersistentMusic instance;

    [SerializeField] private AudioClip clip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.6f;

    private AudioSource source;

    private void Awake()
    {
        // If one already exists (e.g. you returned to the main menu), destroy this duplicate
        // so the original keeps playing without restarting.
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        source = GetComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.volume = volume;
        source.playOnAwake = false;
        source.Play();
    }

    // Call this when the actual game starts so the menu music stops.
    public static void Stop()
    {
        if (instance == null) return;
        instance.source.Stop();
        Destroy(instance.gameObject);
        instance = null;
    }
}