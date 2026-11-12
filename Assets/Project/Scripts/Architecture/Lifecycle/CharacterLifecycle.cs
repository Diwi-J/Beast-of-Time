using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Owns ONE preplaced character's death/reset lifecycle, separate from
/// CharacterCore (identity) and Health (health values).
///
/// The existing Phase 02 CharacterCombat still triggers the death animation
/// and disables combat colliders. This component never triggers Killed twice.
///
/// Ordinary mob corpses remain active and visible until a world reset.
/// The scene coordinator decides whether a defeated boss may be reset.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterCore))]
[RequireComponent(typeof(CharacterCombat))]
public sealed class CharacterLifecycle : MonoBehaviour
{
    [Header("Player death input lock")]
    [Tooltip("Stops player movement/combat input while dead. Does not disable Animator. " +
             "A death clip that needs applied ROOT MOTION may require later integration.")]
    [SerializeField] private bool disablePlayerInputWhileDead = true;

    public CharacterCore Core { get; private set; }
    public Vector3 StartingPosition { get; private set; }
    public Quaternion StartingRotation { get; private set; }

    public event Action<CharacterLifecycle> Died;
    public event Action<CharacterLifecycle> ResetCompleted;

    private Health health;
    private Animator animator;
    private CharacterCombat combat;
    private CharacterController characterController;
    private NavMeshAgent agent;
    private PlayerControls playerControls;
    private PlayerCombat playerCombat;

    private bool playerControlsInitiallyEnabled;
    private bool playerCombatInitiallyEnabled;
    private bool agentInitiallyEnabled;
    private bool agentInitiallyStopped;

    private static readonly int SlowDownHash = Animator.StringToHash("SlowDown");

    private void Awake()
    {
        Core = GetComponent<CharacterCore>();
        health = GetComponent<Health>();
        animator = GetComponent<Animator>();
        combat = GetComponent<CharacterCombat>();
        characterController = GetComponent<CharacterController>();
        agent = GetComponent<NavMeshAgent>();
        playerControls = GetComponent<PlayerControls>();
        playerCombat = GetComponent<PlayerCombat>();

        StartingPosition = transform.position;
        StartingRotation = transform.rotation;

        if (playerControls != null)
            playerControlsInitiallyEnabled = playerControls.enabled;
        if (playerCombat != null)
            playerCombatInitiallyEnabled = playerCombat.enabled;

        if (agent != null)
        {
            agentInitiallyEnabled = agent.enabled;
            if (agent.enabled && agent.isOnNavMesh)
                agentInitiallyStopped = agent.isStopped;
        }
    }

    private void OnEnable()
    {
        if (health != null)
            health.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnDeath -= HandleDeath;
    }

    private void HandleDeath()
    {
        // Leave the GameObject and Animator enabled: the corpse must remain.
        // CharacterCombat already handles Killed, combat colliders and Overdrive.
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        if (Core.IsPlayer && disablePlayerInputWhileDead)
        {
            if (playerControls != null) playerControls.enabled = false;
            if (playerCombat != null) playerCombat.enabled = false;
        }

        NotifyParticipantsOfDeath();
        Died?.Invoke(this);
    }

    /// <summary>
    /// Used for all ordinary enemies, and living bosses, on world reset.
    /// Rewinds to the pose captured when the scene instance awoke.
    /// </summary>
    public void RestoreStartingState()
    {
        RestoreAt(StartingPosition, StartingRotation);
    }

    /// <summary>
    /// Used for the PLAYER on respawn: resurrect at the active checkpoint,
    /// or the player's original position if no checkpoint was activated.
    /// </summary>
    public void RespawnAt(Vector3 worldPosition, Quaternion worldRotation)
    {
        RestoreAt(worldPosition, worldRotation);
    }

    /// <summary>
    /// Used for the PLAYER while praying: refill health and clean up combat
    /// without moving the player or rewinding the prayer animation.
    /// </summary>
    public void RestoreAtCurrentPosition()
    {
        if (health == null || health.IsDead)
        {
            Debug.LogWarning("[CharacterLifecycle] Cannot rest with a dead actor.", this);
            return;
        }

        health.ResetHealth();
        if (combat != null) combat.ResetCombatState();
        NotifyParticipantsOfReset();
        ResetCompleted?.Invoke(this);
    }

    private void RestoreAt(Vector3 worldPosition, Quaternion worldRotation)
    {
        // Scene objects should remain active as corpses; this is a safety net
        // for a manually disabled actor, not a spawning mechanism.
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        bool controllerWasEnabled = characterController != null && characterController.enabled;
        if (controllerWasEnabled)
            characterController.enabled = false;

        // Stop old navigation. New AI will supply its own reset logic through
        // ICharacterLifecycleParticipant.
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        // Rebind BEFORE teleport: sampled animation defaults might otherwise
        // move a root-motion character away from the requested spawn point.
        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
            if (Core.IsEnemy && HasAnimatorFloat(animator, SlowDownHash))
                animator.SetFloat(SlowDownHash, 1f);
        }

        transform.SetPositionAndRotation(worldPosition, worldRotation);

        if (agent != null)
        {
            // Restore the original component enable state, not an AI-specific one.
            agent.enabled = agentInitiallyEnabled;
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.Warp(worldPosition);
                agent.ResetPath();
                agent.isStopped = agentInitiallyStopped;
            }
        }

        if (controllerWasEnabled)
            characterController.enabled = true;

        if (health != null) health.ResetHealth();
        if (combat != null) combat.ResetCombatState();

        // Restore player input after Health and combat have been reset.
        if (Core.IsPlayer)
        {
            if (playerControls != null)
            {
                playerControls.enabled = playerControlsInitiallyEnabled;
                // Re-sync the Animator's focus bool with the existing movement
                // controller; don't modify the externally owned camera lock-on.
                playerControls.SetLockOnTarget(playerControls.lockOnTarget);
            }
            if (playerCombat != null)
                playerCombat.enabled = playerCombatInitiallyEnabled;
        }

        NotifyParticipantsOfReset();
        ResetCompleted?.Invoke(this);
    }

    private static bool HasAnimatorFloat(Animator target, int parameterHash)
    {
        foreach (AnimatorControllerParameter parameter in target.parameters)
            if (parameter.nameHash == parameterHash &&
                parameter.type == AnimatorControllerParameterType.Float)
                return true;
        return false;
    }

    private void NotifyParticipantsOfDeath()
    {
        foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
            if (component is ICharacterLifecycleParticipant participant)
                participant.OnCharacterDied();
    }

    private void NotifyParticipantsOfReset()
    {
        foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
            if (component is ICharacterLifecycleParticipant participant)
                participant.OnCharacterReset();
    }
}
