using System;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyReactionKind
{
    None,
    IdleHit,
    Parried,
    Stunned
}

/// <summary>
/// Enemy-only presentation/action-interruption bridge for confirmed combat
/// outcomes. No AI or player lock-on code is used here.
///
/// Mobs:      player parry -> Parried, regardless of poise.
/// Mobs:      clean hit while Idle -> directional left/right hit trigger.
/// Bosses:   player parry -> no ordinary parry reaction; poise still accumulates.
/// Bosses:   clean hit while Idle -> one hit trigger.
/// Both:     full poise, then next heavy -> Stunned, even outside Idle.
///
/// Existing enemy Animator transitions must be configured to honour the
/// trigger requests. Future AI should check IsReacting (or subscribe to events)
/// before moving or selecting a new attack.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterCore))]
[RequireComponent(typeof(CharacterCombat))]
[RequireComponent(typeof(CombatResolver))]
[RequireComponent(typeof(EnemyPoise))]
[RequireComponent(typeof(Health))]
public sealed class EnemyCombatReactions : MonoBehaviour, ICharacterLifecycleParticipant
{
    [Header("Animator trigger names (must match this enemy's Animator)")]
    [SerializeField] private string mobParriedTrigger = "Parried";
    [SerializeField] private string mobHitFromLeftTrigger = "HitLeft";
    [SerializeField] private string mobHitFromRightTrigger = "HitRight";
    [SerializeField] private string bossHitTrigger = "Hit";
    [SerializeField] private string stunnedTrigger = "Stunned";

    [Header("Idle-only ordinary hit reaction")]
    [Tooltip("Set the Idle Animator state's Tag to this value. If Idle is a locomotion blend tree, tag the enclosing state and leave Speed near zero at idle.")]
    [SerializeField] private string idleStateTag = "Idle";
    [Tooltip("Float used by the enemy's movement blend tree.")]
    [SerializeField] private string speedParameter = "Speed";
    [Min(0f)] [SerializeField] private float idleSpeedTolerance = 0.05f;

    [Header("Reaction cleanup")]
    [Tooltip("Failsafe in character gameplay seconds if an OnReactionAnimationFinished animation event is missing. Tune above the full reaction/substate duration.")]
    [Min(0.1f)] [SerializeField] private float reactionTimeout = 8f;

    public EnemyReactionKind CurrentReaction { get; private set; } = EnemyReactionKind.None;
    public bool IsReacting => CurrentReaction != EnemyReactionKind.None;
    public event Action<EnemyReactionKind> ReactionStarted;
    public event Action<EnemyReactionKind> ReactionFinished;

    private CharacterCore core;
    private CharacterCombat combat;
    private CombatResolver resolver;
    private EnemyPoise poise;
    private Health health;
    private Animator animator;
    private CharacterTimeModifier timeModifier;
    private int speedHash;
    private bool hasSpeedParameter;
    private float reactionElapsed;
    private string activeTrigger;
    private readonly HashSet<string> warnedTriggers = new HashSet<string>();

    private void Awake()
    {
        core = GetComponent<CharacterCore>();
        combat = GetComponent<CharacterCombat>();
        resolver = GetComponent<CombatResolver>();
        poise = GetComponent<EnemyPoise>();
        health = GetComponent<Health>();
        animator = GetComponent<Animator>();
        timeModifier = GetComponent<CharacterTimeModifier>();

        speedHash = Animator.StringToHash(speedParameter);
        hasSpeedParameter = HasParameter(speedParameter, AnimatorControllerParameterType.Float);

        if (core != null && !core.IsEnemy)
            Debug.LogError("[EnemyCombatReactions] Attach this only to mobs and bosses.", this);
    }

    private void OnEnable()
    {
        if (resolver != null)
        {
            resolver.HitLanded += HandleOwnAttackOutcome;
            resolver.HitReceived += HandleReceivedHit;
        }
        if (poise != null)
        {
            poise.PoiseBroken += HandlePoiseBreak;
            poise.HeavyHitProcessed += HandleHeavyHitProcessed;
        }
    }

    private void OnDisable()
    {
        if (resolver != null)
        {
            resolver.HitLanded -= HandleOwnAttackOutcome;
            resolver.HitReceived -= HandleReceivedHit;
        }
        if (poise != null)
        {
            poise.PoiseBroken -= HandlePoiseBreak;
            poise.HeavyHitProcessed -= HandleHeavyHitProcessed;
        }
        ClearReaction(notify: false);
    }

    private void Update()
    {
        if (!IsReacting || health == null || health.IsDead) return;

        // The same time scale used by the Animator: slowdown lengthens the
        // reaction in real time but not in character-animation time.
        float dt = timeModifier != null ? timeModifier.GameplayDeltaTime : Time.deltaTime;
        reactionElapsed += dt;
        if (reactionElapsed >= reactionTimeout)
        {
            Debug.LogWarning($"[EnemyCombatReactions] {name}: '{CurrentReaction}' timed out. Add an OnReactionAnimationFinished event to the FINAL clip in the reaction sequence, or increase timeout.", this);
            ClearReaction(notify: true);
        }
    }

    private void HandleOwnAttackOutcome(CombatHitResult hit)
    {
        if (!CanReact() || core.Kind != CharacterKind.Mob) return;
        if (hit.Outcome != CombatHitOutcome.Parried || hit.Attacker != combat) return;
        if (!IsPlayer(hit.Defender)) return;

        // Ordinary mob parry reaction is independent of the hidden poise gauge.
        // The player's parry does not automatically stagger bosses.
        EnterReaction(EnemyReactionKind.Parried, mobParriedTrigger, interruptAttack: true);
    }

    private void HandleReceivedHit(CombatHitResult hit)
    {
        if (!CanReact() || hit.Defender != combat ||
            hit.Outcome != CombatHitOutcome.Damaged || hit.DamageApplied <= 0f ||
            !IsPlayer(hit.Attacker))
            return;

        // Heavy hits are handled by EnemyPoise.HeavyHitProcessed AFTER poise
        // evaluation, so a poise break cannot also request an idle hit reaction
        // depending on C# event subscription order.
        if (hit.IsHeavyAttack) return;
        TryPlayIdleHit(hit);
    }

    private void HandleHeavyHitProcessed(CombatHitResult hit, bool brokePoise)
    {
        if (!brokePoise) TryPlayIdleHit(hit);
    }

    private void HandlePoiseBreak(CombatHitResult hit)
    {
        if (!CanReact()) return;
        // A true poise stagger overrides ordinary attacks and Idle-only hit
        // reactions. It remains a gameplay decision even for a boss.
        EnterReaction(EnemyReactionKind.Stunned, stunnedTrigger, interruptAttack: true);
    }

    private void TryPlayIdleHit(CombatHitResult hit)
    {
        if (!CanReact() || IsReacting || !IsIdleForHitReaction()) return;

        if (core.Kind == CharacterKind.Boss)
        {
            EnterReaction(EnemyReactionKind.IdleHit, bossHitTrigger, interruptAttack: false);
            return;
        }

        // Use attacker's position in defender-local space to select the side
        // the incoming attack came FROM. This is not per-hurtbox damage.
        Vector3 toAttacker = hit.Attacker.transform.position - transform.position;
        toAttacker.y = 0f;
        bool fromRight = Vector3.Dot(transform.right, toAttacker) >= 0f;
        EnterReaction(EnemyReactionKind.IdleHit,
            fromRight ? mobHitFromRightTrigger : mobHitFromLeftTrigger,
            interruptAttack: false);
    }

    private bool IsIdleForHitReaction()
    {
        if (animator == null || animator.layerCount == 0 ||
            animator.IsInTransition(0) || string.IsNullOrEmpty(idleStateTag))
            return false;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        if (!info.IsTag(idleStateTag)) return false;
        if (hasSpeedParameter && Mathf.Abs(animator.GetFloat(speedHash)) > idleSpeedTolerance)
            return false;
        return true;
    }

    private bool EnterReaction(EnemyReactionKind kind, string trigger, bool interruptAttack)
    {
        if (!CanReact() || !HasParameter(trigger, AnimatorControllerParameterType.Trigger))
        {
            WarnMissingTrigger(trigger);
            return false;
        }

        // A stagger is allowed to replace a current hit/parried reaction.
        // Ordinary parries/hits should not replace a poise stagger.
        if (IsReacting && kind != EnemyReactionKind.Stunned) return false;

        if (IsReacting) ClearReaction(notify: true);
        if (interruptAttack)
        {
            combat.EndAttack();
            combat.SetComboMode(false);
        }

        activeTrigger = trigger;
        CurrentReaction = kind;
        reactionElapsed = 0f;
        animator.SetTrigger(trigger);
        ReactionStarted?.Invoke(kind);
        return true;
    }

    /// <summary>
    /// Preferred Animation Event on the LAST clip of each reaction sequence,
    /// including the final stunned animation in a boss stun substate.
    /// </summary>
    public void OnReactionAnimationFinished()
    {
        if (IsReacting) ClearReaction(notify: true);
    }

    private void ClearReaction(bool notify)
    {
        if (!IsReacting) return;
        EnemyReactionKind finished = CurrentReaction;
        CurrentReaction = EnemyReactionKind.None;
        reactionElapsed = 0f;

        if (animator != null && !string.IsNullOrEmpty(activeTrigger))
            animator.ResetTrigger(activeTrigger);
        activeTrigger = null;

        if (notify) ReactionFinished?.Invoke(finished);
    }

    private bool CanReact()
    {
        return core != null && core.IsEnemy && health != null && !health.IsDead &&
               animator != null && combat != null;
    }

    private bool HasParameter(string parameterName, AnimatorControllerParameterType type)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName)) return false;
        int hash = Animator.StringToHash(parameterName);
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.nameHash == hash && parameter.type == type)
                return true;
        return false;
    }

    private void WarnMissingTrigger(string trigger)
    {
        string key = string.IsNullOrEmpty(trigger) ? "<empty>" : trigger;
        if (warnedTriggers.Add(key))
            Debug.LogWarning($"[EnemyCombatReactions] {name}: Animator trigger '{key}' is not configured. Set it in the component Inspector and Animator. Poise logic continues, but this visual reaction will not play.", this);
    }

    private static bool IsPlayer(CharacterCombat target)
    {
        if (target == null) return false;
        CharacterCore targetCore = target.GetComponent<CharacterCore>();
        return targetCore != null && targetCore.IsPlayer;
    }

    public void OnCharacterDied() => ClearReaction(notify: false);
    public void OnCharacterReset() => ClearReaction(notify: false);

    [ContextMenu("DEBUG / Request Parried Reaction (Play Mode)")]
    private void DebugParried()
    {
        if (Application.isPlaying && core != null && core.Kind == CharacterKind.Mob)
            EnterReaction(EnemyReactionKind.Parried, mobParriedTrigger, interruptAttack: true);
    }

    [ContextMenu("DEBUG / Request Stunned Reaction (Play Mode)")]
    private void DebugStunned()
    {
        if (Application.isPlaying)
            EnterReaction(EnemyReactionKind.Stunned, stunnedTrigger, interruptAttack: true);
    }
}
