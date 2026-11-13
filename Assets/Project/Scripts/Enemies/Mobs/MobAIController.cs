using UnityEngine;
using UnityEngine.AI;

public enum MobAIState
{
    Idle,
    Chasing,
    Attacking,
    ReturningHome,
    Dead
}

public enum MobAggressionPhase
{
    Basic,
    CombosUnlocked,
    Frenzy
}

/// <summary>
/// Single-mob AI for the three preplaced melee enemies.
/// NO group coordination, dynamic spawning, boss logic, or Mutant prototype.
/// NavMeshAgent moves the GameObject; animation is VISUAL ONLY (no root motion).
/// AI state is separate from Animator state and from combat reaction state.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterCore))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(MobAttackController))]
[RequireComponent(typeof(EnemyCombatReactions))]
public sealed class MobAIController : MonoBehaviour, ICharacterLifecycleParticipant
{
    [Header("Scene links")]
    [SerializeField] private SceneActorRegistry actorRegistry;
    [SerializeField] private MobAIConfig configuration;

    [Header("Debug (Play mode)")]
    [SerializeField] private bool logStateChanges;

    public MobAIState CurrentState { get; private set; } = MobAIState.Idle;
    public MobAggressionPhase CurrentPhase
    {
        get
        {
            if (configuration == null || health == null) return MobAggressionPhase.Basic;
            if (health.Normalized <= configuration.FrenzyHealth) return MobAggressionPhase.Frenzy;
            if (health.Normalized <= configuration.ComboUnlockHealth) return MobAggressionPhase.CombosUnlocked;
            return MobAggressionPhase.Basic;
        }
    }

    private CharacterCore core;
    private Health health;
    private NavMeshAgent agent;
    private Animator animator;
    private CharacterTimeModifier timeModifier;
    private EnemyCombatReactions reactions;
    private MobAttackController attacks;
    private Transform playerTransform;
    private Health playerHealth;

    private Vector3 homePosition;
    private Quaternion homeRotation;
    private float repathTimer;
    private float attackCooldown;
    private bool runMovement;
    private bool requestedReturn;
    private bool warnedNavMesh;
    private float initialAgentRadius;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private void Awake()
    {
        core = GetComponent<CharacterCore>();
        health = GetComponent<Health>();
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        timeModifier = GetComponent<CharacterTimeModifier>();
        reactions = GetComponent<EnemyCombatReactions>();
        attacks = GetComponent<MobAttackController>();

        homePosition = transform.position;
        homeRotation = transform.rotation;
        initialAgentRadius = agent.radius;

        if (core != null && core.Kind != CharacterKind.Mob)
            Debug.LogError($"[MobAIController] {name} must be classified as Mob in CharacterCore.", this);
        if (configuration == null)
            Debug.LogError($"[MobAIController] {name} needs a MobAIConfig asset.", this);
        if (actorRegistry == null)
            Debug.LogError($"[MobAIController] {name} needs the scene's SceneActorRegistry reference.", this);

        // The final mobs DO NOT use root motion. Navigation owns X/Z.
        agent.updatePosition = true;
        agent.updateRotation = false;
        agent.autoBraking = true;
        if (animator != null) animator.applyRootMotion = false;
    }

    private void OnEnable()
    {
        if (attacks != null)
        {
            attacks.AttackFinished += OnAttackFinished;
            attacks.AttackInterrupted += OnAttackInterrupted;
        }
    }

    private void OnDisable()
    {
        if (attacks != null)
        {
            attacks.AttackFinished -= OnAttackFinished;
            attacks.AttackInterrupted -= OnAttackInterrupted;
        }
    }

    private void Start()
    {
        if (actorRegistry != null && actorRegistry.Player != null)
        {
            playerTransform = actorRegistry.Player.transform;
            playerHealth = actorRegistry.Player.GetComponent<Health>();
        }
        // Some components may be added to disabled prefab objects; do not
        // assume we can set a destination without a baked NavMesh.
        if (agent.enabled && !agent.isOnNavMesh)
            WarnMissingNavMesh();
    }

    private void Update()
    {
        if (configuration == null || health == null || health.IsDead ||
            CurrentState == MobAIState.Dead)
            return;

        float dt = timeModifier != null ? timeModifier.GameplayDeltaTime : Time.deltaTime;
        attackCooldown = Mathf.Max(0f, attackCooldown - dt);
        repathTimer -= dt;

        // An ordinary hit reaction while idle, parry recoil or poise stun
        // pauses pursuit/attacking; it does not erase current HP/aggro.
        if (reactions != null && reactions.IsReacting)
        {
            StopAgent(clearPath: true);
            UpdateSpeedAnimator(0f, dt);
            return;
        }

        if (CurrentState == MobAIState.Attacking)
        {
            StopAgent(clearPath: false);
            UpdateSpeedAnimator(0f, dt);
            // MobAttackController raises AttackFinished/Interrupted when the
            // entire authored sequence ends. Never finish it per clip here.
            return;
        }

        if (CurrentState == MobAIState.ReturningHome)
        {
            UpdateReturning(dt);
            return;
        }

        if (!HasLivingPlayer())
        {
            // This is not a checkpoint reset. Keep current health.
            if (CurrentState != MobAIState.Idle)
                EnterReturningHome();
            else
            {
                StopAgent(clearPath: true);
                UpdateSpeedAnimator(0f, dt);
            }
            return;
        }

        // Territory is evaluated from the MOB'S SPAWN to the PLAYER.
        // This prevents a long pursuit that follows the player across level.
        if (HorizontalDistance(playerTransform.position, homePosition) > configuration.LeashRange)
        {
            EnterReturningHome();
            UpdateReturning(dt);
            return;
        }

        if (CurrentState == MobAIState.Idle)
        {
            StopAgent(clearPath: true);
            UpdateSpeedAnimator(0f, dt);
            if (HorizontalDistance(playerTransform.position, transform.position) <= configuration.DetectionRange)
                SetState(MobAIState.Chasing);
            return;
        }

        UpdateChasing(dt);
    }

    private void UpdateChasing(float dt)
    {
        if (!HasLivingPlayer())
        {
            EnterReturningHome();
            return;
        }
        if (!CanNavigate())
        {
            UpdateSpeedAnimator(0f, dt);
            return;
        }

        Vector3 toPlayer = Flat(playerTransform.position - transform.position);
        float distance = toPlayer.magnitude;

        if (distance <= configuration.AttackRange)
        {
            StopAgent(clearPath: true);
            UpdateSpeedAnimator(0f, dt);
            RotateTowards(toPlayer, dt);

            bool facing = toPlayer.sqrMagnitude < 0.001f ||
                Vector3.Angle(transform.forward, toPlayer) <= configuration.AttackFacingAngle;
            if (facing && attackCooldown <= 0f && attacks != null)
            {
                MobAttackChoice next = ChooseAttack();
                if (attacks.TryStartAttack(next))
                    SetState(MobAIState.Attacking);
            }
            return;
        }

        runMovement = distance > configuration.RunWhenFartherThan;
        MoveTowards(playerTransform.position, runMovement, dt,
            Mathf.Min(configuration.StoppingDistance, configuration.AttackRange - 0.1f));
    }

    private void UpdateReturning(float dt)
    {
        if (HorizontalDistance(transform.position, homePosition) <= configuration.HomeArrivalDistance)
        {
            StopAgent(clearPath: true);
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                homeRotation, configuration.TurnDegreesPerSecond * dt);
            UpdateSpeedAnimator(0f, dt);
            requestedReturn = false;
            SetState(MobAIState.Idle);
            return;
        }

        if (!CanNavigate())
        {
            UpdateSpeedAnimator(0f, dt);
            return;
        }

        float distToHome = HorizontalDistance(transform.position, homePosition);
        runMovement = distToHome > configuration.RunWhenFartherThan;
        MoveTowards(homePosition, runMovement, dt, 0f);
    }

    private void EnterReturningHome()
    {
        requestedReturn = true;
        if (CurrentState == MobAIState.Attacking)
            return; // Let the authored attack finish; don't cut a sub-state mid-clip.
        SetState(MobAIState.ReturningHome);
        repathTimer = 0f;
    }

    private void MoveTowards(Vector3 destination, bool running, float dt, float stoppingDistance)
    {
        if (!CanNavigate()) return;
        float multiplier = timeModifier != null ? timeModifier.EffectiveMultiplier : 1f;
        float targetSpeed = running ? configuration.RunSpeed : configuration.WalkSpeed;

        // The Animator is already scaled by CharacterTimeModifier. NavMeshAgent
        // updates in Unity real time, so BOTH its speed and acceleration must
        // be adjusted when the enemy is slowed by Overdrive.
        agent.speed = targetSpeed * multiplier;
        agent.acceleration = configuration.Acceleration * multiplier * multiplier;
        agent.stoppingDistance = stoppingDistance;
        agent.isStopped = false;

        if (repathTimer <= 0f)
        {
            if (!agent.SetDestination(destination))
                Debug.LogWarning($"[MobAIController] {name}: SetDestination rejected (check NavMesh connectivity).", this);
            repathTimer = configuration.RepathInterval;
        }

        Vector3 direction = Flat(agent.desiredVelocity);
        if (direction.sqrMagnitude > 0.001f)
            RotateTowards(direction, dt);

        // Idle=0, walk=0.5, run=1. Scale by ACTUAL measured velocity so
        // turning/braking does not leave the mob animating a full run in place.
        float speedRatio = targetSpeed * multiplier > 0.001f
            ? Mathf.Clamp01(Flat(agent.velocity).magnitude / (targetSpeed * multiplier))
            : 0f;
        UpdateSpeedAnimator((running ? 1f : 0.5f) * speedRatio, dt);
    }

    private void StopAgent(bool clearPath)
    {
        if (!CanNavigate()) return;
        agent.isStopped = true;
        if (clearPath && agent.hasPath) agent.ResetPath();
        repathTimer = 0f;
    }

    private void UpdateSpeedAnimator(float speed, float dt)
    {
        if (animator != null && configuration != null)
            animator.SetFloat(SpeedHash, Mathf.Clamp01(speed), configuration.SpeedDamping, dt);
    }

    private void RotateTowards(Vector3 direction, float dt)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;
        Quaternion facing = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation,
            facing, configuration.TurnDegreesPerSecond * dt);
    }

    private MobAttackChoice ChooseAttack()
    {
        MobAggressionPhase phase = CurrentPhase;
        float comboChance = phase == MobAggressionPhase.Frenzy
            ? configuration.ComboChanceDuringFrenzy
            : configuration.ComboChanceAfterUnlock;

        if (phase != MobAggressionPhase.Basic && Random.value < comboChance)
            return MobAttackChoice.Combo;
        return Random.value < 0.5f ? MobAttackChoice.Melee1 : MobAttackChoice.Melee2;
    }

    private float RecoveryForCurrentPhase()
    {
        switch (CurrentPhase)
        {
            case MobAggressionPhase.Frenzy: return configuration.FrenzyCooldown;
            case MobAggressionPhase.CombosUnlocked: return configuration.ComboPhaseCooldown;
            default: return configuration.BasicCooldown;
        }
    }

    private void OnAttackFinished()
    {
        attackCooldown = RecoveryForCurrentPhase();
        TransitionAfterAttack();
    }

    private void OnAttackInterrupted()
    {
        attackCooldown = RecoveryForCurrentPhase();
        TransitionAfterAttack();
    }

    private void TransitionAfterAttack()
    {
        if (health == null || health.IsDead) return;

        if (requestedReturn || !HasLivingPlayer() ||
            HorizontalDistance(playerTransform.position, homePosition) > configuration.LeashRange)
        {
            requestedReturn = true;
            SetState(MobAIState.ReturningHome);
            repathTimer = 0f;
            return;
        }

        SetState(MobAIState.Chasing);
        repathTimer = 0f;
    }

    private void SetState(MobAIState next)
    {
        if (CurrentState == next) return;
        if (logStateChanges)
            Debug.Log($"[MobAIController] {name}: {CurrentState} -> {next} (phase={CurrentPhase})", this);
        CurrentState = next;
    }

    private bool HasLivingPlayer()
    {
        return playerTransform != null && playerHealth != null && !playerHealth.IsDead;
    }

    private bool CanNavigate()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh) return true;
        WarnMissingNavMesh();
        return false;
    }

    private void WarnMissingNavMesh()
    {
        if (warnedNavMesh || agent == null || !agent.enabled) return;
        warnedNavMesh = true;
        Debug.LogWarning($"[MobAIController] {name} is not on an active baked NavMesh. Navigation paused.", this);
    }

    private static Vector3 Flat(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        return Flat(a - b).magnitude;
    }

    public void OnCharacterDied()
    {
        requestedReturn = false;
        SetState(MobAIState.Dead);
        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            // Corpse stays visible, but does not push other NavMeshAgents.
            agent.enabled = false;
        }
        // The death Animator is deliberately left running by CharacterLifecycle.
    }

    public void OnCharacterReset()
    {
        requestedReturn = false;
        repathTimer = 0f;
        attackCooldown = 0f;
        runMovement = false;
        warnedNavMesh = false;

        if (agent != null)
        {
            // CharacterLifecycle has already restored the starting transform
            // and agent enabled state, warped it, and cleared the old path.
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.stoppingDistance = configuration != null ? configuration.StoppingDistance : 0f;
            }
            agent.radius = initialAgentRadius;
        }
        if (animator != null) animator.SetFloat(SpeedHash, 0f);
        SetState(MobAIState.Idle);
    }

    private void OnDrawGizmosSelected()
    {
        if (configuration == null) return;
        Vector3 home = Application.isPlaying ? homePosition : transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, configuration.DetectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(home, configuration.LeashRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, configuration.AttackRange);
    }
}
