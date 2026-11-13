using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public enum RespawnPresentationMode
{
    WaitForUIButton,
    Automatic
}

/// <summary>
/// Player-only bridge between the existing death animation, the externally
/// owned UI, and WorldResetCoordinator. Does not edit or build any UI.
/// Attach to the SAME root GameObject as the player's Animator so an
/// animation event can call OnDeathAnimationFinished().
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Health))]
public sealed class PlayerRespawnFlow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WorldResetCoordinator worldResetCoordinator;

    [Header("Death screen / respawn behaviour")]
    [SerializeField] private RespawnPresentationMode mode = RespawnPresentationMode.WaitForUIButton;
    [Min(0f)]
    [Tooltip("Used if you have NOT yet added OnDeathAnimationFinished as an animation event.")]
    [SerializeField] private float deathAnimationFallbackSeconds = 3.5f;
    [Min(0f)]
    [Tooltip("Automatic mode: time the death screen remains visible before respawning.")]
    [SerializeField] private float automaticRespawnDelay = 2f;

    [Header("Optional UI integration - wire in Inspector")]
    [SerializeField] private UnityEvent onDeathScreenReady;
    [SerializeField] private UnityEvent onRespawned;

    public bool IsRespawnReady { get; private set; }

    private Health health;
    private bool awaitingDeathPresentation;
    private Coroutine fallbackRoutine;
    private Coroutine autoRoutine;

    private void Awake()
    {
        health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        health.OnDeath += OnPlayerDied;
    }

    private void OnDisable()
    {
        health.OnDeath -= OnPlayerDied;
        StopPendingCoroutines();
    }

    private void OnPlayerDied()
    {
        if (awaitingDeathPresentation) return;
        awaitingDeathPresentation = true;
        IsRespawnReady = false;
        StopPendingCoroutines();
        fallbackRoutine = StartCoroutine(DeathAnimationFallback());
    }

    private IEnumerator DeathAnimationFallback()
    {
        yield return new WaitForSecondsRealtime(deathAnimationFallbackSeconds);
        fallbackRoutine = null;
        MarkDeathPresentationReady();
    }

    /// <summary>
    /// Preferred: add this Animation Event near the end of the PLAYER death clip.
    /// Fallback delay is used when no event exists.
    /// </summary>
    public void OnDeathAnimationFinished()
    {
        MarkDeathPresentationReady();
    }

    private void MarkDeathPresentationReady()
    {
        if (!awaitingDeathPresentation || IsRespawnReady) return;

        if (fallbackRoutine != null)
        {
            StopCoroutine(fallbackRoutine);
            fallbackRoutine = null;
        }

        IsRespawnReady = true;
        onDeathScreenReady?.Invoke();

        if (mode == RespawnPresentationMode.Automatic)
            autoRoutine = StartCoroutine(AutoRespawnAfterDelay());
    }

    private IEnumerator AutoRespawnAfterDelay()
    {
        yield return new WaitForSecondsRealtime(automaticRespawnDelay);
        autoRoutine = null;
        RespawnNow();
    }

    /// <summary>
    /// Public UI Button / UnityEvent handler. The UI developer can call this
    /// after revealing the death screen; no dependency on UI implementation.
    /// </summary>
    public void RespawnNow()
    {
        if (!IsRespawnReady || worldResetCoordinator == null)
        {
            Debug.LogWarning("[PlayerRespawnFlow] Respawn not ready or coordinator missing.", this);
            return;
        }

        if (!worldResetCoordinator.RespawnPlayer())
        {
            Debug.LogWarning("[PlayerRespawnFlow] Coordinator declined respawn.", this);
            return;
        }

        StopPendingCoroutines();
        awaitingDeathPresentation = false;
        IsRespawnReady = false;
        onRespawned?.Invoke();
    }

    [ContextMenu("DEBUG / Respawn Now")]
    private void DebugRespawn()
    {
        if (Application.isPlaying) RespawnNow();
        else Debug.LogWarning("Enter Play mode to test respawning.", this);
    }

    private void StopPendingCoroutines()
    {
        if (fallbackRoutine != null)
        {
            StopCoroutine(fallbackRoutine);
            fallbackRoutine = null;
        }
        if (autoRoutine != null)
        {
            StopCoroutine(autoRoutine);
            autoRoutine = null;
        }
    }
}
