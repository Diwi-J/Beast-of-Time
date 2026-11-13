using System;
using UnityEngine;

/// <summary>
/// Owns the Knight's CURRENT combo sequence and one buffered follow-up.
/// Animation events define the input window and commit frame. Animator
/// Controller transitions remain authored by the developer (no CrossFade).
///
/// IMPORTANT: each of the five attack clips needs string-argument events:
/// OpenComboWindow(AttackId), CommitComboWindow(AttackId),
/// OnAttackClipFinished(AttackId). Existing CharacterCombat.BeginAttack(AttackId)
/// and hitbox events remain in place. CombatAttackRuntime owns damage/hit lists.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerActionCoordinator))]
[RequireComponent(typeof(CharacterCombat))]
[RequireComponent(typeof(CombatAttackRuntime))]
public sealed class PlayerComboController : MonoBehaviour, ICharacterLifecycleParticipant
{
    [SerializeField] private PlayerComboConfig comboConfig;
    [SerializeField] private bool logComboDecisions;

    private Animator animator;
    private PlayerActionCoordinator actions;
    private CharacterCombat combat;
    private CombatAttackRuntime attackRuntime;

    private PlayerComboConfig.Move currentMove;
    private PlayerComboConfig.Move queuedMove;
    private bool windowOpen;
    private bool nextLightUsesSecondOpener;
    private bool warnedMissingConfig;
    private bool sequenceActive;

    /// <summary>Fired when an attack trigger is requested, including combo follow-ups.</summary>
    public event Action<string> AttackRequested;

    public bool IsSequenceActive => sequenceActive;
    public string CurrentAttackId => sequenceActive && currentMove != null ? currentMove.AttackId : string.Empty;
    public string QueuedAttackId => queuedMove != null ? queuedMove.AttackId : string.Empty;
    public bool IsInputWindowOpen => sequenceActive && windowOpen;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        actions = GetComponent<PlayerActionCoordinator>();
        combat = GetComponent<CharacterCombat>();
        attackRuntime = GetComponent<CombatAttackRuntime>();
    }

    private void OnEnable()
    {
        if (actions != null)
            actions.AttackSequenceExited += OnAttackSequenceExited;
    }

    private void OnDisable()
    {
        if (actions != null)
            actions.AttackSequenceExited -= OnAttackSequenceExited;
        ClearSequence();
    }

    /// <summary>
    /// Called by PlayerCombat on light/heavy input. Starts an opener from
    /// neutral or buffers the FIRST valid follow-up during the clip window.
    /// </summary>
    public bool TryRequestAttack(PlayerComboInput input)
    {
        if (comboConfig == null)
        {
            if (!warnedMissingConfig)
            {
                Debug.LogWarning($"[PlayerComboController] Assign a PlayerComboConfig on '{name}'.", this);
                warnedMissingConfig = true;
            }
            return false;
        }

        if (!sequenceActive)
            return TryStartOpener(input);

        if (!windowOpen || queuedMove != null || currentMove == null) return false;
        if (!IsAttackAction(actions.CurrentAction)) return false;

        string nextId = currentMove.FollowUpId(input);
        if (string.IsNullOrEmpty(nextId) || string.Equals(nextId, currentMove.AttackId, StringComparison.Ordinal))
            return false; // The immediate next clip must have a distinct ID for event safety.
        if (!comboConfig.TryGetMove(nextId, out PlayerComboConfig.Move next) ||
            next.Input != input || !HasTrigger(next.AnimatorTrigger))
        {
            Debug.LogWarning($"[PlayerComboController] Invalid {input} follow-up '{nextId}' after '{currentMove.AttackId}'. Check config and Animator triggers.", this);
            return false;
        }

        queuedMove = next; // First valid input wins; no repeated queueing.
        if (logComboDecisions)
            Debug.Log($"[Combo] Buffered {next.AttackId} after {currentMove.AttackId}", this);
        return true;
    }

    private bool TryStartOpener(PlayerComboInput input)
    {
        if (!comboConfig.TryGetOpener(input, nextLightUsesSecondOpener, out PlayerComboConfig.Move opener) ||
            !HasTrigger(opener.AnimatorTrigger))
        {
            Debug.LogWarning($"[PlayerComboController] Missing/misconfigured {input} opener or Animator trigger.", this);
            return false;
        }

        bool allowed = input == PlayerComboInput.Light
            ? actions.TryBeginLightAttack()
            : actions.TryBeginHeavyAttack();
        if (!allowed) return false;

        ClearSequence();
        sequenceActive = true;
        currentMove = opener;
        combat.SetComboMode(false); // First attack is standalone: NO combo bonus.

        if (input == PlayerComboInput.Light && comboConfig.HasSecondLightOpener)
            nextLightUsesSecondOpener = !nextLightUsesSecondOpener;

        RequestAnimatorAttack(opener);
        return true;
    }

    /// <summary>Animation event with THIS clip's Attack ID, at buffer start.</summary>
    public void OpenComboWindow(string attackId)
    {
        if (!IsCurrentClip(attackId)) return;
        windowOpen = true;
        queuedMove = null;
        if (logComboDecisions) Debug.Log($"[Combo] Open {attackId}", this);
    }

    /// <summary>
    /// Animation event with THIS clip's Attack ID, at the point where the
    /// Animator is allowed to transition into a queued move. The trigger is
    /// issued here; the authored Animator transition chooses duration/offset.
    /// </summary>
    public void CommitComboWindow(string attackId)
    {
        if (!IsCurrentClip(attackId) || !windowOpen) return;
        windowOpen = false;
        PlayerComboConfig.Move next = queuedMove;
        queuedMove = null;
        if (next == null) return;

        PlayerActionKind nextKind = next.Input == PlayerComboInput.Heavy
            ? PlayerActionKind.HeavyAttack : PlayerActionKind.LightAttack;
        if (!actions.TryContinueAttack(nextKind))
        {
            ClearSequence();
            return;
        }

        // Only after the old hit window has closed: a chained move receives
        // its own target list and combo multiplier when its BeginAttack event runs.
        combat.EndAttack();
        combat.SetComboMode(true);
        currentMove = next;
        RequestAnimatorAttack(next);
        if (logComboDecisions) Debug.Log($"[Combo] Committed {next.AttackId}", this);
    }

    /// <summary>
    /// Animation event near the end of THIS clip (string argument must match
    /// CharacterCombat.BeginAttack ID). Events from an outgoing blended clip
    /// are ignored if a follow-up has already been committed.
    /// </summary>
    public void OnAttackClipFinished(string attackId)
    {
        if (!IsCurrentClip(attackId)) return;
        ClearSequence();
        actions.CompleteAttackSequence();
    }

    private bool IsCurrentClip(string id)
    {
        return sequenceActive && currentMove != null &&
            string.Equals(currentMove.AttackId, id, StringComparison.Ordinal) &&
            IsAttackAction(actions.CurrentAction);
    }

    private void RequestAnimatorAttack(PlayerComboConfig.Move move)
    {
        animator.SetTrigger(move.AnimatorTrigger);
        AttackRequested?.Invoke(move.AttackId);
    }

    private bool HasTrigger(string trigger)
    {
        if (string.IsNullOrEmpty(trigger)) return false;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == trigger && parameter.type == AnimatorControllerParameterType.Trigger)
                return true;
        }
        return false;
    }

    private static bool IsAttackAction(PlayerActionKind kind)
    {
        return kind == PlayerActionKind.LightAttack || kind == PlayerActionKind.HeavyAttack;
    }

    private void OnAttackSequenceExited() => ClearSequence();

    private void ClearSequence()
    {
        sequenceActive = false;
        windowOpen = false;
        queuedMove = null;
        currentMove = null;
        if (combat != null)
        {
            combat.EndAttack();
            combat.SetComboMode(false);
        }
        // Phase 07 only knew the first 3 attack trigger names. Make sure
        // a queued finisher trigger can never survive hit/death/reset.
        if (comboConfig != null && animator != null && comboConfig.Moves != null)
        {
            foreach (PlayerComboConfig.Move move in comboConfig.Moves)
            {
                if (move != null && HasTrigger(move.AnimatorTrigger))
                    animator.ResetTrigger(move.AnimatorTrigger);
            }
        }
    }

    public void OnCharacterDied() => ClearSequence();

    public void OnCharacterReset()
    {
        ClearSequence();
        nextLightUsesSecondOpener = false;
        warnedMissingConfig = false;
    }
}
