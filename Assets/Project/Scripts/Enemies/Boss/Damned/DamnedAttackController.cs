using System;
using UnityEngine;

public enum DamnedAttackKind
{
    Melee1, Melee2, Melee3, Melee4, MeleeCombo,
    Stomp, Running, BloodShout, Choke
}

/// <summary>
/// Starts complete authored Animator attacks/substates; it never plays substate clips individually.
/// For Running, the AI calls TryFinishRunning when in range.
/// The existing CombatCollider/HitDetector/BeginAttack animation events handle damage.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CharacterCombat))]
public sealed class DamnedAttackController : MonoBehaviour, ICharacterLifecycleParticipant
{
    [Header("Animator trigger names (case sensitive)")]
    [SerializeField] private string[] attackTriggers =
    {
        "Melee_1", "Melee_2", "Melee_3", "Melee_4", "Melee_Combo",
        "Stomp", "Running_Atk_start", "Blood_Atk", "Choke"
    };
    [SerializeField] private string runningFinishTrigger = "Running_Atk_Range";
    [Header("Animator detection")]
    [Tooltip("Tag ALL attack clips in the Base layer (also clips inside substates) with this tag.")]
    [SerializeField] private string attackStateTag = "BossAttack";
    [Tooltip("Full Animator state path, used only for forced disengagement/timeouts. Adapt to your graph.")]
    [SerializeField] private string idleStatePath = "Base Layer.Idle";
    [SerializeField] private bool logDebug;

    public bool IsAttacking { get; private set; }
    public DamnedAttackKind CurrentAttack { get; private set; }
    public bool RunningWindup => IsAttacking && CurrentAttack == DamnedAttackKind.Running && !runningFinishRequested;
    public event Action<DamnedAttackKind> AttackCompleted;
    public event Action AttackCancelled;

    private Animator animator;
    private CharacterCombat combat;
    private CharacterTimeModifier timeModifier;
    private Health health;
    private EnemyCombatReactions reactions;
    private DamnedAIConfig config;
    private bool runningFinishRequested;
    private bool enteredTaggedState;
    private int framesOutside;
    private float elapsed;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        combat = GetComponent<CharacterCombat>();
        timeModifier = GetComponent<CharacterTimeModifier>();
        health = GetComponent<Health>();
        reactions = GetComponent<EnemyCombatReactions>();
    }

    public void Configure(DamnedAIConfig settings) => config = settings;

    private void OnEnable()
    {
        if (reactions != null) reactions.ReactionStarted += OnReactionStarted;
    }

    private void OnDisable()
    {
        if (reactions != null) reactions.ReactionStarted -= OnReactionStarted;
        ClearInternalState();
    }

    private void Update()
    {
        if (!IsAttacking) return;
        if (health == null || health.IsDead) { CancelAttack(false); return; }
        if (config == null) return;

        elapsed += timeModifier != null ? timeModifier.GameplayDeltaTime : Time.deltaTime;
        bool tagged = InTaggedAttackState();
        if (tagged)
        {
            enteredTaggedState = true;
            framesOutside = 0;
        }
        else if (enteredTaggedState)
        {
            if (++framesOutside >= 2) { FinishAttack(); return; }
        }
        else if (elapsed >= config.attackEntryTimeout)
        {
            Debug.LogWarning($"[DamnedAttackController] {name}: attack never entered tagged state. Check transitions and tags.", this);
            CancelAttack(true);
            return;
        }

        float limit = CurrentAttack == DamnedAttackKind.Running
            ? config.runningAttackMaxDuration : config.attackMaxDuration;
        if (elapsed > limit)
        {
            Debug.LogWarning($"[DamnedAttackController] {name}: attack timed out; returning to idle.", this);
            CancelAttack(true);
        }
    }

    public bool TryStart(DamnedAttackKind kind)
    {
        if (IsAttacking || animator == null || health == null || health.IsDead ||
            (reactions != null && reactions.IsReacting) || InTaggedAttackState()) return false;
        int index = (int)kind;
        if (attackTriggers == null || index < 0 || index >= attackTriggers.Length ||
            !HasTrigger(attackTriggers[index]))
        {
            Debug.LogWarning($"[DamnedAttackController] Missing trigger for {kind} on {name}.", this);
            return false;
        }
        ResetTriggers();
        CurrentAttack = kind;
        IsAttacking = true;
        runningFinishRequested = false;
        enteredTaggedState = false;
        framesOutside = 0;
        elapsed = 0f;
        combat.SetComboMode(kind == DamnedAttackKind.MeleeCombo);
        animator.SetTrigger(attackTriggers[index]);
        if (logDebug) Debug.Log($"[Damned] Started {kind}", this);
        return true;
    }

    public bool TryFinishRunning()
    {
        if (!RunningWindup || !HasTrigger(runningFinishTrigger)) return false;
        // Only signal the substate transition; the Animator owns the actual finish clip.
        runningFinishRequested = true;
        animator.SetTrigger(runningFinishTrigger);
        return true;
    }

    /// <summary>Optional event on the FINAL clip of an authored sequence.</summary>
    public void OnDamnedAttackFinished()
    {
        if (IsAttacking) FinishAttack();
    }

    public void CancelAttack(bool forceAnimatorIdle)
    {
        if (!IsAttacking) return;
        ClearInternalState();
        if (forceAnimatorIdle && animator != null &&
            !string.IsNullOrEmpty(idleStatePath) && animator.HasState(0, Animator.StringToHash(idleStatePath)))
            animator.CrossFadeInFixedTime(Animator.StringToHash(idleStatePath), 0.15f, 0);
        AttackCancelled?.Invoke();
    }

    private void FinishAttack()
    {
        DamnedAttackKind kind = CurrentAttack;
        ClearInternalState();
        AttackCompleted?.Invoke(kind);
    }

    private void OnReactionStarted(EnemyReactionKind reaction)
    {
        // A poise break interrupts any move; ordinary boss hits are idle-only.
        if (IsAttacking) CancelAttack(false);
    }

    private void ClearInternalState()
    {
        IsAttacking = false;
        runningFinishRequested = false;
        enteredTaggedState = false;
        framesOutside = 0;
        elapsed = 0f;
        if (animator != null) ResetTriggers();
        if (combat != null)
        {
            combat.EndAttack();
            combat.SetComboMode(false);
        }
    }

    private void ResetTriggers()
    {
        if (animator == null) return;
        if (attackTriggers != null)
            foreach (string trigger in attackTriggers)
                if (HasTrigger(trigger)) animator.ResetTrigger(trigger);
        if (HasTrigger(runningFinishTrigger)) animator.ResetTrigger(runningFinishTrigger);
    }

    private bool HasTrigger(string name)
    {
        if (animator == null || string.IsNullOrWhiteSpace(name)) return false;
        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.name == name && p.type == AnimatorControllerParameterType.Trigger) return true;
        return false;
    }

    private bool InTaggedAttackState()
    {
        if (animator == null || animator.layerCount == 0) return false;
        if (animator.GetCurrentAnimatorStateInfo(0).IsTag(attackStateTag)) return true;
        return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag(attackStateTag);
    }

    public void OnCharacterDied() => ClearInternalState();
    public void OnCharacterReset() => ClearInternalState();
}
