using System;
using UnityEngine;

public enum MobAttackChoice
{
    Melee1,
    Melee2,
    Combo
}

/// <summary>
/// Selects one of three Animator triggers and tracks a WHOLE attack sequence.
/// The Animator owns sequence transitions: a Melee_Combo sub-state may play
/// Melee_1 then Melee_2, or the reverse, without a code change.
///
/// Actual attack SEGMENTS are reported by MobAttackSegmentState behaviours.
/// The existing BeginAttack/Enablehitbox/Disablehitbox clip events still own
/// damage data and hitbox timing; this component does NOT apply damage.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterCore))]
[RequireComponent(typeof(CharacterCombat))]
[RequireComponent(typeof(CombatAttackRuntime))]
[RequireComponent(typeof(EnemyCombatReactions))]
public sealed class MobAttackController : MonoBehaviour, ICharacterLifecycleParticipant
{
    [Header("Animator triggers (case-sensitive)")]
    [SerializeField] private string melee1Trigger = "Melee_1";
    [SerializeField] private string melee2Trigger = "Melee_2";
    [SerializeField] private string comboTrigger = "Melee_Combo";

    [Header("CombatAttackSet / BeginAttack IDs (case-sensitive)")]
    [SerializeField] private string melee1AttackId = "Melee_1";
    [SerializeField] private string melee2AttackId = "Melee_2";

    [Header("Animator detection / failsafes")]
    [Tooltip("Set the Tag on EVERY attack state, including both states within Melee_Combo.")]
    [SerializeField] private string attackStateTag = "MobAttack";
    [Min(0.1f)] [SerializeField] private float attackEntryTimeout = 1.0f;
    [Tooltip("Must exceed the full authored combo duration in character time.")]
    [Min(0.2f)] [SerializeField] private float maxAttackDuration = 12f;
    [SerializeField] private bool verboseLogging;

    public bool IsAttacking { get; private set; }
    public MobAttackChoice CurrentChoice { get; private set; }
    public bool IsMelee1SegmentActive => currentSegment == MobAttackSegmentState.Segment.Melee1 && segmentActive;

    public event Action AttackFinished;
    public event Action AttackInterrupted;

    private Animator animator;
    private CharacterCombat combat;
    private CharacterTimeModifier timeModifier;
    private Health health;
    private EnemyCombatReactions reactions;
    private bool enteredAttackState;
    private bool segmentActive;
    private MobAttackSegmentState.Segment currentSegment;
    private float elapsed;
    private int framesOutsideAttack;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        combat = GetComponent<CharacterCombat>();
        health = GetComponent<Health>();
        timeModifier = GetComponent<CharacterTimeModifier>();
        reactions = GetComponent<EnemyCombatReactions>();
    }

    private void OnEnable()
    {
        if (reactions != null)
            reactions.ReactionStarted += OnReactionStarted;
    }

    private void OnDisable()
    {
        if (reactions != null)
            reactions.ReactionStarted -= OnReactionStarted;
        CancelWithoutNotification();
    }

    private void Update()
    {
        if (!IsAttacking) return;
        if (health == null || health.IsDead)
        {
            CancelAttack();
            return;
        }

        float dt = timeModifier != null ? timeModifier.GameplayDeltaTime : Time.deltaTime;
        elapsed += dt;

        bool animatorInAttack = AnimatorInAttackState();
        if (animatorInAttack)
        {
            enteredAttackState = true;
            framesOutsideAttack = 0;
        }
        else if (!enteredAttackState)
        {
            if (elapsed >= attackEntryTimeout)
            {
                Debug.LogWarning($"[MobAttackController] {name}: attack trigger never entered a '{attackStateTag}' state. Check Animator transitions/tags.", this);
                CancelAttack();
            }
        }
        else
        {
            // Require consecutive non-attack frames to avoid treating a brief
            // sub-state transition as the end of the whole authored combo.
            framesOutsideAttack++;
            if (framesOutsideAttack >= 2)
            {
                CompleteAttack();
                return;
            }
        }

        if (IsAttacking && elapsed >= maxAttackDuration)
        {
            Debug.LogWarning($"[MobAttackController] {name}: attack exceeded {maxAttackDuration}s of character time. Check state tags and transitions.", this);
            CancelAttack();
        }
    }

    public bool TryStartAttack(MobAttackChoice choice)
    {
        if (IsAttacking || health == null || health.IsDead ||
            reactions == null || reactions.IsReacting || animator == null ||
            AnimatorInAttackState())
            return false;

        string trigger = choice == MobAttackChoice.Melee1 ? melee1Trigger :
                         choice == MobAttackChoice.Melee2 ? melee2Trigger : comboTrigger;

        if (!HasTrigger(trigger))
        {
            Debug.LogWarning($"[MobAttackController] {name}: missing Animator Trigger '{trigger}'.", this);
            return false;
        }

        ClearAttackTriggers();
        CurrentChoice = choice;
        IsAttacking = true;
        enteredAttackState = false;
        segmentActive = false;
        elapsed = 0f;
        framesOutsideAttack = 0;

        // A single shared clip may play standalone or as part of a combo.
        // This flag must be set BEFORE its BeginAttack animation event.
        combat.SetComboMode(choice == MobAttackChoice.Combo);
        animator.SetTrigger(trigger);

        if (verboseLogging)
            Debug.Log($"[MobAttackController] {name}: requested {choice}.", this);
        return true;
    }

    /// <summary>Called by MobAttackSegmentState, including nested combo states.</summary>
    public void EnterSegment(MobAttackSegmentState.Segment segment)
    {
        if (!IsAttacking) return;
        currentSegment = segment;
        segmentActive = true;
        enteredAttackState = true;
        if (verboseLogging)
            Debug.Log($"[MobAttackController] {name}: entering {segment} (sequence={CurrentChoice}).", this);
    }

    public void ExitSegment(MobAttackSegmentState.Segment segment)
    {
        // During crossfades, the NEW segment's OnStateEnter can occur before
        // the OLD segment's OnStateExit. Never clear a newer segment.
        if (segmentActive && currentSegment == segment)
            segmentActive = false;
    }

    /// <summary>
    /// A parry only INTERRUPTS an actual Melee_1 strike. Prefer the attack ID
    /// captured by CombatResolver; use the state relay for legacy clips that
    /// have not yet received BeginAttack animation events.
    /// Melee_2 still gets parried (zero damage, gauge), just no reaction.
    /// </summary>
    public bool ShouldInterruptFromParry(CombatHitResult hit)
    {
        if (!IsAttacking) return false;
        if (!string.IsNullOrEmpty(hit.AttackId))
            return string.Equals(hit.AttackId, melee1AttackId, StringComparison.Ordinal);
        return IsMelee1SegmentActive;
    }

    /// <summary>Called by reactions on parry/stun; also valid for leash/reset.</summary>
    public void CancelAttack()
    {
        if (!IsAttacking) return;
        CancelWithoutNotification();
        AttackInterrupted?.Invoke();
    }

    private void CompleteAttack()
    {
        if (!IsAttacking) return;
        IsAttacking = false;
        enteredAttackState = false;
        segmentActive = false;
        elapsed = 0f;
        framesOutsideAttack = 0;
        combat.SetComboMode(false);
        // In the intended Animator graph, all weapon hitbox windows are
        // already closed before the sequence returns to locomotion.
        combat.EndAttack();
        AttackFinished?.Invoke();
    }

    private void CancelWithoutNotification()
    {
        IsAttacking = false;
        enteredAttackState = false;
        segmentActive = false;
        elapsed = 0f;
        framesOutsideAttack = 0;
        if (animator != null) ClearAttackTriggers();
        if (combat != null)
        {
            combat.EndAttack();
            combat.SetComboMode(false);
        }
    }

    private void OnReactionStarted(EnemyReactionKind kind)
    {
        if (IsAttacking)
            CancelAttack();
    }

    private bool AnimatorInAttackState()
    {
        if (animator == null || animator.layerCount == 0 || string.IsNullOrEmpty(attackStateTag))
            return false;
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (current.IsTag(attackStateTag)) return true;
        if (animator.IsInTransition(0))
            return animator.GetNextAnimatorStateInfo(0).IsTag(attackStateTag);
        return false;
    }

    private bool HasTrigger(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        int hash = Animator.StringToHash(name);
        foreach (AnimatorControllerParameter param in animator.parameters)
            if (param.nameHash == hash && param.type == AnimatorControllerParameterType.Trigger)
                return true;
        return false;
    }

    private void ClearAttackTriggers()
    {
        ResetIfConfigured(melee1Trigger);
        ResetIfConfigured(melee2Trigger);
        ResetIfConfigured(comboTrigger);
    }

    private void ResetIfConfigured(string trigger)
    {
        if (HasTrigger(trigger)) animator.ResetTrigger(trigger);
    }

    public void OnCharacterDied() => CancelWithoutNotification();
    public void OnCharacterReset() => CancelWithoutNotification();
}
