using System;
using System.Collections.Generic;
using UnityEngine;

public enum WorldResetReason
{
    PrayerRest,
    PlayerRespawn,
    NewSession
}

/// <summary>
/// Scene-level authority for active checkpoint, preplaced-actor world resets,
/// and boss-defeated flags that last ONLY for this gameplay session.
/// Uses SceneActorRegistry as the sole actor list; no spawning or AI here.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldResetCoordinator : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private SceneActorRegistry actorRegistry;

    public PrayerCheckpoint ActiveCheckpoint { get; private set; }
    public IReadOnlyCollection<CharacterCore> DefeatedBosses => defeatedBosses;

    public event Action<PrayerCheckpoint> CheckpointActivated;
    public event Action<WorldResetReason> WorldResetCompleted;

    private readonly HashSet<CharacterCore> defeatedBosses = new HashSet<CharacterCore>();
    private readonly Dictionary<Health, Action> bossDeathSubscriptions = new Dictionary<Health, Action>();
    private bool isResetting;

    private void Awake()
    {
        if (actorRegistry == null)
            actorRegistry = GetComponent<SceneActorRegistry>();
        if (actorRegistry == null)
            Debug.LogError("[WorldResetCoordinator] Assign SceneActorRegistry.", this);
    }

    private void OnEnable()
    {
        SubscribeToBossDeaths();
    }

    private void Start()
    {
        // Defensive fallback if the coordinator's OnEnable happened before
        // another system initialized. Preplaced actors are Inspector assigned.
        if (bossDeathSubscriptions.Count == 0)
            SubscribeToBossDeaths();
    }

    private void OnDisable()
    {
        foreach (KeyValuePair<Health, Action> entry in bossDeathSubscriptions)
            if (entry.Key != null)
                entry.Key.OnDeath -= entry.Value;
        bossDeathSubscriptions.Clear();
    }

    private void SubscribeToBossDeaths()
    {
        if (actorRegistry == null) return;

        foreach (CharacterCore actor in actorRegistry.Enemies)
        {
            if (actor == null || !actor.IsBoss) continue;
            Health health = actor.GetComponent<Health>();
            if (health == null || bossDeathSubscriptions.ContainsKey(health)) continue;

            CharacterCore capturedBoss = actor;
            Action handler = () => { defeatedBosses.Add(capturedBoss); };
            health.OnDeath += handler;
            bossDeathSubscriptions.Add(health, handler);

            if (health.IsDead)
                defeatedBosses.Add(actor);
        }
    }

    /// <summary>
    /// Called by PrayerCheckpoint.Pray() when an external interaction system
    /// has accepted the player's prayer interaction. Does not teleport player.
    /// </summary>
    public bool RestAt(PrayerCheckpoint checkpoint)
    {
        if (checkpoint == null || actorRegistry == null || isResetting)
            return false;

        CharacterLifecycle player = GetPlayerLifecycle();
        if (player == null || player.GetComponent<Health>().IsDead)
            return false;

        ActiveCheckpoint = checkpoint;
        CheckpointActivated?.Invoke(checkpoint);

        isResetting = true;
        try
        {
            // End Overdrive and heal before restoring the enemy time modifiers.
            player.RestoreAtCurrentPosition();
            ResetAllEnemies();
        }
        finally
        {
            isResetting = false;
        }

        WorldResetCompleted?.Invoke(WorldResetReason.PrayerRest);
        return true;
    }

    /// <summary>
    /// Called ONLY after player death presentation permits respawning.
    /// If no statue has been activated, use the initial player spawn.
    /// </summary>
    public bool RespawnPlayer()
    {
        if (actorRegistry == null || isResetting)
            return false;

        CharacterLifecycle player = GetPlayerLifecycle();
        if (player == null || !player.GetComponent<Health>().IsDead)
            return false;

        Vector3 position = ActiveCheckpoint != null
            ? ActiveCheckpoint.RespawnPosition
            : player.StartingPosition;
        Quaternion rotation = ActiveCheckpoint != null
            ? ActiveCheckpoint.RespawnRotation
            : player.StartingRotation;

        isResetting = true;
        try
        {
            player.RespawnAt(position, rotation);
            ResetAllEnemies();
        }
        finally
        {
            isResetting = false;
        }

        WorldResetCompleted?.Invoke(WorldResetReason.PlayerRespawn);
        return true;
    }

    /// <summary>
    /// Optional explicit fresh-session entry point if the gameplay scene is
    /// reused instead of reloaded. Loading a fresh scene also clears state.
    /// </summary>
    public void BeginNewSession()
    {
        if (actorRegistry == null || isResetting)
            return;

        isResetting = true;
        try
        {
            ActiveCheckpoint = null;
            defeatedBosses.Clear();
            CharacterLifecycle player = GetPlayerLifecycle();
            if (player != null)
                player.RestoreStartingState();
            ResetAllEnemies(ignoreDefeatedBosses: true);
        }
        finally
        {
            isResetting = false;
        }

        WorldResetCompleted?.Invoke(WorldResetReason.NewSession);
    }

    private void ResetAllEnemies(bool ignoreDefeatedBosses = false)
    {
        foreach (CharacterCore enemy in actorRegistry.Enemies)
        {
            if (enemy == null) continue;

            // A boss killed earlier in the session is NOT revived by rest/death.
            // Also record dead bosses defensively if a listener was disabled.
            Health enemyHealth = enemy.GetComponent<Health>();
            if (!ignoreDefeatedBosses && enemy.IsBoss &&
                (defeatedBosses.Contains(enemy) ||
                 (enemyHealth != null && enemyHealth.IsDead)))
            {
                defeatedBosses.Add(enemy);
                continue;
            }

            CharacterLifecycle lifecycle = enemy.GetComponent<CharacterLifecycle>();
            if (lifecycle == null)
            {
                Debug.LogError($"[WorldResetCoordinator] Add CharacterLifecycle to {enemy.name}.", enemy);
                continue;
            }

            lifecycle.RestoreStartingState();
        }
    }

    private CharacterLifecycle GetPlayerLifecycle()
    {
        if (actorRegistry == null || actorRegistry.Player == null)
        {
            Debug.LogError("[WorldResetCoordinator] Registry player is missing.", this);
            return null;
        }

        CharacterLifecycle lifecycle = actorRegistry.Player.GetComponent<CharacterLifecycle>();
        if (lifecycle == null)
            Debug.LogError("[WorldResetCoordinator] Add CharacterLifecycle to the player.", actorRegistry.Player);
        return lifecycle;
    }
}
