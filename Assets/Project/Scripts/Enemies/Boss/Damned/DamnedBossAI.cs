using UnityEngine;
using UnityEngine.AI;

public enum DamnedBossState { Dormant, Pursuing, Attacking, ReturningHome, Reacting, Dead }

/// <summary>
/// Damned uses IN-PLACE animations. NavMeshAgent moves the character;
/// Animator only displays locomotion and authored attack/substate sequences.
/// Arena exit returns him home WITHOUT healing him.
/// CharacterLifecycle owns death and checkpoint restoration.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterCore))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(DamnedAttackController))]
public sealed class DamnedBossAI : MonoBehaviour, ICharacterLifecycleParticipant
{
    [SerializeField] private DamnedAIConfig config;
    [SerializeField] private SceneActorRegistry registry;
    [SerializeField] private bool activateAtStartForTesting;
    [SerializeField] private bool verboseLogging;

    public DamnedBossState State { get; private set; } = DamnedBossState.Dormant;
    public bool PlayerInsideArena { get; private set; }

    private Health health;
    private Animator animator;
    private NavMeshAgent agent;
    private DamnedAttackController attacks;
    private EnemyCombatReactions reactions;
    private CharacterTimeModifier characterTime;
    private Transform player;
    private Health playerHealth;
    private Vector3 homePosition;
    private Quaternion homeRotation;
    private float repathTimer;
    private float cooldownTimer;
    private float normalAgentAcceleration;
    private bool initialAgentEnabled;
    private bool warnedMissingNavMesh;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private void Awake()
    {
        health = GetComponent<Health>();
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        attacks = GetComponent<DamnedAttackController>();
        reactions = GetComponent<EnemyCombatReactions>();
        characterTime = GetComponent<CharacterTimeModifier>();
        homePosition = transform.position;
        homeRotation = transform.rotation;
        initialAgentEnabled = agent.enabled;
        normalAgentAcceleration = Mathf.Max(0.01f, agent.acceleration);

        // No root motion: the agent owns displacement, and script owns facing.
        agent.updatePosition = true;
        agent.updateRotation = false;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        attacks.Configure(config);
        PlayerInsideArena = activateAtStartForTesting;
    }

    private void OnEnable()
    {
        attacks.AttackCompleted += OnAttackCompleted;
        attacks.AttackCancelled += OnAttackCancelled;
        if (reactions != null) reactions.ReactionFinished += OnReactionFinished;
    }

    private void OnDisable()
    {
        attacks.AttackCompleted -= OnAttackCompleted;
        attacks.AttackCancelled -= OnAttackCancelled;
        if (reactions != null) reactions.ReactionFinished -= OnReactionFinished;
    }

    private void Start()
    {
        if (registry != null && registry.Player != null)
        {
            player = registry.Player.transform;
            playerHealth = registry.Player.GetComponent<Health>();
        }
        if (initialAgentEnabled && !agent.isOnNavMesh)
            WarnMissingNavMesh();
    }

    public void SetPlayerInsideArena(bool inside)
    {
        PlayerInsideArena = inside;
        if (!inside && State != DamnedBossState.Dead)
            BeginReturnHome();
    }

    private void Update()
    {
        if (config == null || health == null || health.IsDead || State == DamnedBossState.Dead) return;
        float dt = characterTime != null ? characterTime.GameplayDeltaTime : Time.deltaTime;
        cooldownTimer = Mathf.Max(0f, cooldownTimer - dt);
        repathTimer -= dt;

        if (reactions != null && reactions.IsReacting)
        {
            State = DamnedBossState.Reacting;
            PausePath();
            SetMovement(0f, dt);
            return;
        }

        if (State == DamnedBossState.Attacking && attacks.IsAttacking)
        {
            if (!PlayerInsideArena || !HasTarget()) { BeginReturnHome(); return; }
            if (FlatDistance(player.position, homePosition) > config.arenaLeashDistance)
            {
                BeginReturnHome();
                return;
            }

            if (attacks.RunningWindup)
            {
                // Running_Atk_start is an IN-PLACE looping animation.
                // Keep the agent pursuing until the finishing trigger is issued.
                TurnAndPlan(player.position, dt);
                SetMovement(1f, dt);
                if (FlatDistance(player.position, transform.position) <= config.runningAttackFinishRange)
                {
                    if (attacks.TryFinishRunning())
                    {
                        // Stop displacement when the run transitions to its strike.
                        PausePath();
                        SetMovement(0f, dt);
                    }
                }
            }
            else
            {
                // Every non-running attack, and the running finisher, is stationary.
                PausePath();
                SetMovement(0f, dt);
            }
            return;
        }

        if (State == DamnedBossState.ReturningHome)
        {
            ReturnHome(dt);
            return;
        }

        if (!PlayerInsideArena || !HasTarget())
        {
            State = DamnedBossState.Dormant;
            PausePath();
            SetMovement(0f, dt);
            return;
        }
        if (FlatDistance(player.position, homePosition) > config.arenaLeashDistance)
        {
            BeginReturnHome();
            return;
        }

        State = DamnedBossState.Pursuing;
        float distance = FlatDistance(player.position, transform.position);
        TurnAndPlan(player.position, dt);

        if (cooldownTimer <= 0f && IsFacing(player.position))
        {
            DamnedAttackKind? choice = ChooseAttack(distance);
            if (choice.HasValue && attacks.TryStart(choice.Value))
            {
                State = DamnedBossState.Attacking;
                if (attacks.RunningWindup)
                {
                    // Preserve the pursuit path; the running animation does not move Damned.
                    SetMovement(1f, dt);
                }
                else
                {
                    PausePath();
                    SetMovement(0f, dt);
                }
                return;
            }
        }

        if (distance > config.closeRange)
            SetMovement(distance > config.runWhenFartherThan ? 1f : 0.5f, dt);
        else
        {
            PausePath();
            SetMovement(0f, dt);
        }
    }

    private DamnedAttackKind? ChooseAttack(float distance)
    {
        // Distance eligibility first; weights only decide between eligible attacks.
        float[] weights = new float[9];
        if (distance <= config.closeRange)
        {
            weights[0] = weights[1] = weights[2] = config.basicMeleeWeight / 3f;
            weights[4] = config.meleeComboWeight;
            weights[5] = config.stompWeight;
        }
        if (distance > config.closeRange && distance <= config.mediumRange)
            weights[3] = config.mediumMeleeWeight;
        if (distance <= config.chokeRange)
            weights[8] = config.chokeWeight;
        if (distance <= config.bloodShoutRange && distance > config.closeRange)
            weights[7] = config.bloodShoutWeight;
        if (distance >= config.runningAttackMinDistance)
            weights[6] = config.runningAttackWeight;

        float total = 0f;
        foreach (float weight in weights) total += weight;
        if (total <= 0f) return null;
        float sample = Random.value * total;
        for (int i = 0; i < weights.Length; i++)
        {
            sample -= weights[i];
            if (sample <= 0f && weights[i] > 0f) return (DamnedAttackKind)i;
        }
        return DamnedAttackKind.Melee1;
    }

    private void BeginReturnHome()
    {
        if (State == DamnedBossState.Dead) return;
        if (attacks.IsAttacking) attacks.CancelAttack(true);
        State = DamnedBossState.ReturningHome;
        repathTimer = 0f;
        if (verboseLogging) Debug.Log("[Damned] Returning home without healing.", this);
    }

    private void ReturnHome(float dt)
    {
        if (FlatDistance(transform.position, homePosition) <= config.homeArrivalDistance)
        {
            PausePath();
            SetMovement(0f, dt);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, homeRotation,
                config.turnDegreesPerSecond * dt);
            if (Quaternion.Angle(transform.rotation, homeRotation) <= 2f)
                State = DamnedBossState.Dormant;
            return;
        }
        TurnAndPlan(homePosition, dt);
        SetMovement(FlatDistance(transform.position, homePosition) > config.runWhenFartherThan ? 1f : 0.5f, dt);
    }

    private void TurnAndPlan(Vector3 destination, float dt)
    {
        if (agent.enabled && agent.isOnNavMesh && repathTimer <= 0f)
        {
            agent.isStopped = false;
            if (!agent.SetDestination(destination) && verboseLogging)
                Debug.LogWarning("[Damned] NavMeshAgent rejected destination.", this);
            repathTimer = config.repathInterval;
        }
        else if (!agent.enabled || !agent.isOnNavMesh)
        {
            WarnMissingNavMesh();
        }

        Vector3 targetDirection = destination - transform.position;
        if (agent.enabled && agent.isOnNavMesh && agent.hasPath && !agent.pathPending)
            targetDirection = agent.steeringTarget - transform.position;
        targetDirection.y = 0f;
        if (targetDirection.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(targetDirection), config.turnDegreesPerSecond * dt);
    }

    /// <summary>
    /// Assign movement speed in world units/second. NavMeshAgent movement does not
    /// inherit Animator.speed, so scale speed AND acceleration for Overdrive.
    /// Speed parameter values 0 / 0.5 / 1 select the authored movement blend tree.
    /// </summary>
    private void SetMovement(float speedBlend, float dt)
    {
        speedBlend = Mathf.Clamp01(speedBlend);
        float multiplier = characterTime != null ? characterTime.EffectiveMultiplier : 1f;
        float normalSpeed = speedBlend < 0.01f ? 0f
            : speedBlend >= 0.75f ? config.locomotionRunSpeed : config.locomotionWalkSpeed;

        if (agent != null && agent.enabled)
        {
            agent.speed = normalSpeed * multiplier;
            agent.acceleration = normalAgentAcceleration * multiplier * multiplier;
        }

        // Avoid presenting a walk/run while the NavMesh is unavailable.
        float animatorBlend = agent != null && agent.enabled && agent.isOnNavMesh
            ? speedBlend : 0f;
        animator.SetFloat(SpeedHash, animatorBlend, config.speedDamping, dt);
    }

    private void PausePath()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;
        agent.isStopped = true;
        agent.ResetPath();
    }

    private bool IsFacing(Vector3 target)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;
        return direction.sqrMagnitude < 0.001f ||
            Vector3.Angle(transform.forward, direction) <= config.attackFacingAngle;
    }

    private bool HasTarget() => player != null && playerHealth != null && !playerHealth.IsDead;
    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = b.y;
        return Vector3.Distance(a, b);
    }

    private void WarnMissingNavMesh()
    {
        if (warnedMissingNavMesh) return;
        warnedMissingNavMesh = true;
        Debug.LogWarning($"[DamnedBossAI] {name} is not on a baked NavMesh. Navigation paused.", this);
    }

    private void OnAttackCompleted(DamnedAttackKind kind)
    {
        cooldownTimer = config.CooldownAtHealth(health.Normalized);
        State = PlayerInsideArena && HasTarget() ? DamnedBossState.Pursuing : DamnedBossState.ReturningHome;
    }

    private void OnAttackCancelled()
    {
        if (State == DamnedBossState.Attacking)
            State = PlayerInsideArena && HasTarget() ? DamnedBossState.Pursuing : DamnedBossState.ReturningHome;
    }

    private void OnReactionFinished(EnemyReactionKind kind)
    {
        if (State == DamnedBossState.Reacting)
            State = PlayerInsideArena && HasTarget() ? DamnedBossState.Pursuing : DamnedBossState.ReturningHome;
    }

    // Intentionally NO OnAnimatorMove: NavMeshAgent owns world-space movement.
    public void OnCharacterDied()
    {
        State = DamnedBossState.Dead;
        PausePath();
        if (animator != null) animator.SetFloat(SpeedHash, 0f);
    }

    public void OnCharacterReset()
    {
        PlayerInsideArena = false;
        State = DamnedBossState.Dormant;
        repathTimer = cooldownTimer = 0f;
        warnedMissingNavMesh = false;
        if (agent != null)
        {
            agent.updatePosition = true;
            agent.updateRotation = false;
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.Warp(homePosition);
                agent.ResetPath();
                agent.isStopped = true;
            }
        }
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.SetFloat(SpeedHash, 0f);
        }
        SetMovement(0f, 0f);
    }
}
