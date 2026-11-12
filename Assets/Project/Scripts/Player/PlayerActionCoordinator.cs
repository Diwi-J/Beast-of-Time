using UnityEngine;

public enum PlayerActionKind
{
    Neutral,
    LightAttack,
    HeavyAttack,
    Blocking,
    Dodge,
    HitReaction,
    Dead
}

/// <summary>
/// Authoritative player ACTION gates and interruption rules. The Animator still
/// owns actual clip transitions; this component requests them and tracks the
/// intent while those transitions play.
///
/// PlayerCombat translates input into requests. CombatResolver reports clean
/// hits. CharacterCombat alone owns collider availability, including i-frames.
/// Existing PlayerCameraFocus is not accessed or changed.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(CharacterCombat))]
[RequireComponent(typeof(CombatResolver))]
public sealed class PlayerActionCoordinator : MonoBehaviour, ICharacterLifecycleParticipant
{
    [Header("Dodge Animator requests")]
    [Tooltip("Trigger for the existing single forward-roll clip in Freelook.")]
    [SerializeField] private string freelookRollTrigger = "Roll";
    [Tooltip("Trigger for the Focus four-way dodge blend tree/state.")]
    [SerializeField] private string focusDodgeTrigger = "Dodge";
    [Tooltip("NEW float for a latched dodge direction (not continuously updated MoveX).")]
    [SerializeField] private string dodgeXParameter = "DodgeX";
    [SerializeField] private string dodgeZParameter = "DodgeZ";

    [Header("Single player hit reaction")]
    [Tooltip("Must match the player's general hit-reaction Animator trigger.")]
    [SerializeField] private string hitTrigger = "Hit";

    [Header("Animator state tags (on base layer)")]
    [SerializeField] private string attackTag = "Attack";
    [SerializeField] private string blockTag = "Block";
    [SerializeField] private string dodgeTag = "Dodge";
    [SerializeField] private string hitTag = "Hit";

    [Header("Missing event failsafes: character gameplay seconds")]
    [Min(0.05f)][SerializeField] private float animationEnterTimeout = 0.6f;
    [Min(0.5f)][SerializeField] private float actionTimeout = 5f;

    public PlayerActionKind CurrentAction { get; private set; } = PlayerActionKind.Neutral;
    public bool BlockInputHeld { get; private set; }
    public bool IsDodging => CurrentAction == PlayerActionKind.Dodge;
    public Vector2 LatchedDodgeDirection { get; private set; } = Vector2.down;

    /// <summary>Notifies combo runtime when an attack ends or is interrupted.</summary>
    public event System.Action AttackSequenceExited;

    private Animator animator;
    private Health health;
    private CharacterCombat combat;
    private CombatResolver resolver;
    private CharacterTimeModifier timeModifier;
    private PlayerControls movement;
    private PlayerComboController comboController;

    private float elapsed;
    private bool hasEnteredAnimation;
    private bool warnedAboutMissingParameter;

    private static readonly int IsBlockingHash = Animator.StringToHash("IsBlocking");
    private static readonly int Light1Hash = Animator.StringToHash("1_LightAtk");
    private static readonly int Light2Hash = Animator.StringToHash("2_LightAtk");
    private static readonly int HeavyHash = Animator.StringToHash("1_HeavyAtk");
    private static readonly int IsFreeHash = Animator.StringToHash("IsFree");

    private void Awake()
    {
        animator = GetComponent<Animator>();
        health = GetComponent<Health>();
        combat = GetComponent<CharacterCombat>();
        resolver = GetComponent<CombatResolver>();
        timeModifier = GetComponent<CharacterTimeModifier>();
        movement = GetComponent<PlayerControls>();
        comboController = GetComponent<PlayerComboController>();
    }

    private void OnEnable()
    {
        if (resolver != null) resolver.HitReceived += OnHitReceived;
        if (health != null) health.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        if (resolver != null) resolver.HitReceived -= OnHitReceived;
        if (health != null) health.OnDeath -= HandleDeath;
        if (combat != null) combat.SetDodgeInvulnerable(false);
    }

    private void Update()
    {
        if (health != null && health.IsDead && CurrentAction != PlayerActionKind.Dead)
            HandleDeath();

        if (HasParameter("IsFree", AnimatorControllerParameterType.Bool))
            animator.SetBool(IsFreeHash, CurrentAction == PlayerActionKind.Neutral && !IsAnimatorBusy());

        if (CurrentAction == PlayerActionKind.Neutral ||
            CurrentAction == PlayerActionKind.Blocking ||
            CurrentAction == PlayerActionKind.Dead)
            return;

        float dt = timeModifier != null ? timeModifier.GameplayDeltaTime : Time.deltaTime;
        elapsed += dt;

        string expectedTag = ExpectedTag(CurrentAction);
        bool tagged = IsTaggedState(expectedTag);
        if (tagged) hasEnteredAnimation = true;

        if (hasEnteredAnimation && !tagged && !animator.IsInTransition(0))
        {
            FinishCurrentAction();
        }
        else if (!hasEnteredAnimation && elapsed >= animationEnterTimeout)
        {
            Debug.LogWarning($"[PlayerActionCoordinator] {name}: {CurrentAction} never entered Animator tag '{expectedTag}'. Check triggers/transitions/tags.", this);
            FinishCurrentAction();
        }
        else if (elapsed >= actionTimeout)
        {
            Debug.LogWarning($"[PlayerActionCoordinator] {name}: {CurrentAction} timed out; missing completion event or Animator transition.", this);
            FinishCurrentAction();
        }
    }

    public bool TryBeginLightAttack() => TryBeginAttack(PlayerActionKind.LightAttack);
    public bool TryBeginHeavyAttack() => TryBeginAttack(PlayerActionKind.HeavyAttack);

    /// <summary>
    /// A buffered, authored combo follow-up is the ONLY attack that may start
    /// while another attack is active. This does not bypass interruption
    /// rules for block, dodge or ordinary damage on heavy attacks.
    /// </summary>
    public bool TryContinueAttack(PlayerActionKind nextAttack)
    {
        if (!IsAttackAction(nextAttack) || !IsAttackAction(CurrentAction) ||
            !CanUseActions() || BlockInputHeld) return false;

        ChangeAction(nextAttack);
        return true;
    }

    /// <summary>Called when the last clip in a combo completes.</summary>
    public void CompleteAttackSequence()
    {
        if (IsAttackAction(CurrentAction)) FinishCurrentAction();
    }

    private bool TryBeginAttack(PlayerActionKind attack)
    {
        if (!CanStartFromNeutral() || BlockInputHeld) return false;
        ChangeAction(attack);
        return true;
    }

    /// <summary>Block button press and release. A held block forbids dodging.</summary>
    public bool RequestBlock(bool held)
    {
        BlockInputHeld = held;
        if (!held)
        {
            animator.SetBool(IsBlockingHash, false);
            if (CurrentAction == PlayerActionKind.Blocking)
                ChangeAction(PlayerActionKind.Neutral);
            return true;
        }

        if (!CanUseActions() || CurrentAction == PlayerActionKind.HeavyAttack ||
            CurrentAction == PlayerActionKind.Dodge || CurrentAction == PlayerActionKind.HitReaction ||
            CurrentAction == PlayerActionKind.Blocking ||
            (CurrentAction == PlayerActionKind.Neutral && IsAnimatorBusy()))
            return false;

        if (CurrentAction == PlayerActionKind.LightAttack)
            InterruptCurrentAction();

        ChangeAction(PlayerActionKind.Blocking);
        animator.SetBool(IsBlockingHash, true);
        return true;
    }

    /// <summary>
    /// Direction uses current movement INPUT (not the strafe blend-tree output).
    /// Focus: choose dominant axis (four ways), no input -> backward.
    /// Freelook: always request the forward-roll animation.
    /// </summary>
    public bool TryDodge(Vector2 movementInput)
    {
        if (!CanUseActions() || BlockInputHeld ||
            CurrentAction == PlayerActionKind.HeavyAttack ||
            CurrentAction == PlayerActionKind.Blocking ||
            CurrentAction == PlayerActionKind.Dodge ||
            CurrentAction == PlayerActionKind.HitReaction ||
            (CurrentAction == PlayerActionKind.Neutral && IsAnimatorBusy()))
            return false;

        bool focused = movement != null && movement.lockOnTarget != null;
        LatchedDodgeDirection = focused ? CardinalDirection(movementInput) : Vector2.up;

        string request = focused ? focusDodgeTrigger : freelookRollTrigger;
        if (!HasParameter(request, AnimatorControllerParameterType.Trigger) ||
            (focused && (!HasParameter(dodgeXParameter, AnimatorControllerParameterType.Float) ||
                         !HasParameter(dodgeZParameter, AnimatorControllerParameterType.Float))))
        {
            if (!warnedAboutMissingParameter)
            {
                Debug.LogWarning($"[PlayerActionCoordinator] {name}: configure Animator trigger '{request}'" +
                    (focused ? $" and float parameters '{dodgeXParameter}' / '{dodgeZParameter}'." : "."), this);
                warnedAboutMissingParameter = true;
            }
            return false;
        }

        if (CurrentAction == PlayerActionKind.LightAttack)
            InterruptCurrentAction();

        ChangeAction(PlayerActionKind.Dodge);
        if (focused)
        {
            // SetFloat without damping: animation selection must be latched.
            animator.SetFloat(dodgeXParameter, LatchedDodgeDirection.x);
            animator.SetFloat(dodgeZParameter, LatchedDodgeDirection.y);
        }
        animator.SetTrigger(request);
        return true;
    }

    /// <summary>Animation event on dodge clip at the FIRST invulnerable frame.</summary>
    public void BeginDodgeIFrames()
    {
        if (CurrentAction == PlayerActionKind.Dodge && CanUseActions())
            combat.SetDodgeInvulnerable(true);
    }

    /// <summary>Animation event on dodge clip at the LAST invulnerable frame.</summary>
    public void EndDodgeIFrames()
    {
        if (combat != null) combat.SetDodgeInvulnerable(false);
    }

    /// <summary>Animation event near END of every light/heavy attack clip.</summary>
    public void OnPlayerAttackFinished()
    {
        // Backward compatibility only. Phase 08 uses an ID-specific event on
        // PlayerComboController so an outgoing clip cannot end the new move.
        if (comboController != null) return;
        if (IsAttackAction(CurrentAction)) FinishCurrentAction();
    }

    /// <summary>Animation event on last frame of both roll and focus dodge clips.</summary>
    public void OnPlayerDodgeFinished()
    {
        if (CurrentAction == PlayerActionKind.Dodge)
            FinishCurrentAction();
    }

    /// <summary>Animation event near end of the player's single hit reaction.</summary>
    public void OnPlayerHitReactionFinished()
    {
        if (CurrentAction == PlayerActionKind.HitReaction)
            FinishCurrentAction();
    }

    private void OnHitReceived(CombatHitResult hit)
    {
        if (health == null || health.IsDead ||
            hit.Outcome != CombatHitOutcome.Damaged || hit.DamageApplied <= 0f ||
            hit.Defender == null || hit.Defender.gameObject != gameObject)
            return;

        // Heavy attacks have hyperarmor: damage applies but they do not flinch.
        // Fatal damage is handled separately by Health.OnDeath.
        if (CurrentAction == PlayerActionKind.HeavyAttack) return;

        // A hit outside the dodge's active i-frame window is interruptible.
        InterruptCurrentAction();

        if (HasParameter(hitTrigger, AnimatorControllerParameterType.Trigger))
        {
            ChangeAction(PlayerActionKind.HitReaction);
            animator.SetTrigger(hitTrigger);
        }
        else
        {
            Debug.LogWarning($"[PlayerActionCoordinator] Missing hit trigger '{hitTrigger}' on {name}; damage still applied.", this);
            ChangeAction(PlayerActionKind.Neutral);
        }
    }

    private void HandleDeath()
    {
        BlockInputHeld = false;
        InterruptCurrentAction();
        ChangeAction(PlayerActionKind.Dead);
    }

    public void OnCharacterDied() => HandleDeath();

    public void OnCharacterReset()
    {
        BlockInputHeld = false;
        InterruptCurrentAction();
        ChangeAction(PlayerActionKind.Neutral);
        LatchedDodgeDirection = Vector2.down;
        warnedAboutMissingParameter = false;
    }

    private void InterruptCurrentAction()
    {
        bool wasAttack = IsAttackAction(CurrentAction);
        // The same cleanup occurs for damage interrupts, light->block,
        // light->dodge, death and checkpoint restoration.
        if (combat != null)
        {
            combat.EndAttack();
            combat.SetComboMode(false);
            combat.SetDodgeInvulnerable(false);
            if (combat.IsBlocking)
                combat.DisableBlockbox();
        }

        animator.SetBool(IsBlockingHash, false);
        animator.ResetTrigger(Light1Hash);
        animator.ResetTrigger(Light2Hash);
        animator.ResetTrigger(HeavyHash);
        ResetOptionalTrigger(freelookRollTrigger);
        ResetOptionalTrigger(focusDodgeTrigger);
        ResetOptionalTrigger(hitTrigger);
        if (wasAttack) AttackSequenceExited?.Invoke();
    }

    private void FinishCurrentAction()
    {
        bool wasAttack = IsAttackAction(CurrentAction);
        if (CurrentAction == PlayerActionKind.Dodge && combat != null)
            combat.SetDodgeInvulnerable(false);
        ChangeAction(PlayerActionKind.Neutral);
        if (wasAttack) AttackSequenceExited?.Invoke();
    }

    private static bool IsAttackAction(PlayerActionKind action)
    {
        return action == PlayerActionKind.LightAttack || action == PlayerActionKind.HeavyAttack;
    }

    private bool CanStartFromNeutral() => CanUseActions() &&
        CurrentAction == PlayerActionKind.Neutral && !IsAnimatorBusy();

    private bool CanUseActions() => isActiveAndEnabled && health != null && !health.IsDead;

    private bool IsAnimatorBusy() => IsTaggedState(attackTag) || IsTaggedState(blockTag) ||
        IsTaggedState(dodgeTag) || IsTaggedState(hitTag);

    private bool IsTaggedState(string tag)
    {
        if (animator == null || animator.layerCount < 1 || string.IsNullOrEmpty(tag)) return false;
        if (animator.GetCurrentAnimatorStateInfo(0).IsTag(tag)) return true;
        return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag(tag);
    }

    private string ExpectedTag(PlayerActionKind kind)
    {
        switch (kind)
        {
            case PlayerActionKind.LightAttack:
            case PlayerActionKind.HeavyAttack: return attackTag;
            case PlayerActionKind.Dodge: return dodgeTag;
            case PlayerActionKind.HitReaction: return hitTag;
            default: return string.Empty;
        }
    }

    private void ChangeAction(PlayerActionKind next)
    {
        CurrentAction = next;
        elapsed = 0f;
        hasEnteredAnimation = false;
    }

    private static Vector2 CardinalDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.01f) return Vector2.down;
        if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
            return input.x >= 0 ? Vector2.right : Vector2.left;
        return input.y >= 0 ? Vector2.up : Vector2.down;
    }

    private bool HasParameter(string parameter, AnimatorControllerParameterType kind)
    {
        if (animator == null || string.IsNullOrEmpty(parameter)) return false;
        foreach (AnimatorControllerParameter item in animator.parameters)
            if (item.name == parameter && item.type == kind)
                return true;
        return false;
    }

    private void ResetOptionalTrigger(string parameter)
    {
        if (HasParameter(parameter, AnimatorControllerParameterType.Trigger))
            animator.ResetTrigger(parameter);
    }
}
