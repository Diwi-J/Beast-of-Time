using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(CharacterCombat))]
public class EnemyController : MonoBehaviour
{
    private enum State { Idle, Chase, Attack, Dead }
    private enum AttackType { Swipe, Punch, Jump }

    [Header("Detection")]
    [SerializeField] private float aggroRange = 30f;

    [Header("Chase")]
    [Tooltip("Speed parameter values that match the walk and run points on the blend tree.")]
    [SerializeField] private float walkSpeedParam = 0.4f;
    [SerializeField] private float runSpeedParam = 1f;

    [Tooltip("Run when farther than this from the player, walk when closer.")]
    [SerializeField] private float runDistance = 6f;
    
    [Tooltip("Stop moving at this distance. Must be below Melee Range.")]
    [SerializeField] private float stopDistance = 1.6f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float speedDamping = 0.1f;
    [SerializeField] private float repathInterval = 0.2f;

    [Header("Melee")]
    [SerializeField] private float meleeRange = 2.2f;

    [Tooltip("Max angle between facing and the player to start an attack.")]
    [SerializeField] private float attackAngle = 35f;
    [SerializeField] private float attackCooldown = 1.5f;
    
    [Tooltip("Seconds of tracking the player after an attack starts. After this the facing is locked.")]
    [SerializeField] private float attackTrackTime = 0.3f;

    [Header("Attack Detection")]
    [Tooltip("Animator state tag on Swipe, Punch and JumpAtk states.")]
    [SerializeField] private string attackTag = "Attack";
    [SerializeField] private float attackEnterTimeout = 0.5f;
    [SerializeField] private float attackTimeout = 4f;

    [Header("Jump Attack")]
    [SerializeField] private float jumpMinDistance = 6f;
    [SerializeField] private float jumpMaxDistance = 14f;
    
    [Tooltip("How long the player must stay beyond Jump Min Distance before the enemy jumps.")]
    [SerializeField] private float jumpTriggerDelay = 2.5f;
    [SerializeField] private float jumpCooldown = 8f;
    [SerializeField] private float jumpTrackTime = 0.4f;
    
    [Tooltip("Scales root motion during the jump only. Use it to tune how far the jump travels.")]
    [SerializeField] private float jumpMotionMultiplier = 1f;
    [SerializeField] private float jumpDamage = 25f;
    [SerializeField] private float aoeRadius = 2.5f;
    [SerializeField] private float aoeHeightOffset = 1f;
    [SerializeField] private float aoeLifetime = 0.2f;
    [SerializeField] private GameObject jumpImpactVfx;

    [Tooltip("On: code moves the enemy between the takeoff and landing events. Off: old root motion jump.")]
    [SerializeField] private bool useCodeLeap = true;
    [Tooltip("Seconds between the takeoff and landing events. Frames 15 to 45 at 30 fps = 1.0.")]
    [SerializeField] private float jumpAirTime = 1f;
    [Tooltip("The enemy lands this far short of the player's position at takeoff.")]
    [SerializeField] private float jumpStandoff = 10f;

    [Header("Root Motion")]
    [SerializeField] private float rootMotionMultiplier = 1f;

    [Header("Death")]
    [SerializeField] private bool despawnAfterDeath = true;
    [SerializeField] private float despawnDelay = 5f;

    private Animator animator;
    private NavMeshAgent agent;
    private Health health;
    private CharacterCombat combat;

    private Transform player;
    private Health playerHealth;

    private State state = State.Idle;
    private AttackType currentAttack;

    private float attackCooldownTimer;
    private float jumpCooldownTimer;
    private float farTimer;
    private float repathTimer;
    private float attackTimer;
    private bool attackEntered;
    private Vector3 attackStartPosition;
    private int aoeLayer;

    private bool jumpLaunched;
    private bool leapActive;
    private float leapProgress;
    private Vector3 leapStart;
    private Vector3 leapEnd;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int SlowDownHash = Animator.StringToHash("SlowDown");
    private static readonly int SwipeHash = Animator.StringToHash("Swipe");
    private static readonly int PunchHash = Animator.StringToHash("Punch");
    private static readonly int JumpHash = Animator.StringToHash("JumpAtk");

    private bool HasTarget => player != null && (playerHealth == null || !playerHealth.IsDead);

    private float TimeScale
    {
        get
        {
            float s = animator.GetFloat(SlowDownHash);
            return s > 0.01f ? s : 1f;
        }
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        combat = GetComponent<CharacterCombat>();

        // Root motion stops if the Animator is culled while off-screen.
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // The agent plans the path. Root motion moves the enemy.
        agent.updatePosition = false;
        agent.updateRotation = false;

        // Put the AOE on the same layer as the enemy's own hitboxes so the collision matrix treats it the same.
        aoeLayer = gameObject.layer;
        foreach (var cc in GetComponentsInChildren<CombatCollider>(true))
        {
            if (cc.type == ColliderType.Hitbox)
            {
                aoeLayer = cc.gameObject.layer;
                break;
            }
        }
    }

    private void Start()
    {
        if (!agent.isOnNavMesh)
            Debug.LogWarning($"[EnemyController] {name} is not on a NavMesh. Bake the arena and check the spawn point.");
    }

    private void OnEnable() => health.OnDeath += HandleDeath;
    private void OnDisable() => health.OnDeath -= HandleDeath;


    #region Update
    private void Update()
    {
        if (state == State.Dead) return;

        ResolveTarget();

        float scaledDt = Time.deltaTime * TimeScale;

        attackCooldownTimer -= scaledDt;
        jumpCooldownTimer -= scaledDt;

        switch (state)
        {
            case State.Idle: UpdateIdle(scaledDt); break;
            case State.Chase: UpdateChase(scaledDt); break;
            case State.Attack: UpdateAttack(scaledDt); break;
        }
    }

    private void ResolveTarget()
    {
        if (player != null) return;

        var p = CharacterCombat.PlayerInstance;
        if (p == null) return;

        player = p.transform;
        playerHealth = p.Health;
    }

    private void UpdateIdle(float dt)
    {
        SetLocomotion(0f, dt);

        if (!HasTarget) return;

        if (FlatDistance(player.position) <= aggroRange)
        {
            state = State.Chase;
            farTimer = 0f;
            repathTimer = 0f;
        }
    }

    private void UpdateChase(float dt)
    {
        if (!HasTarget)
        {
            state = State.Idle;
            return;
        }

        Vector3 toPlayer = Flat(player.position - transform.position);
        float dist = toPlayer.magnitude;

        // Path planning.
        repathTimer -= dt;
        if (repathTimer <= 0f && agent.isOnNavMesh)
        {
            agent.SetDestination(player.position);
            repathTimer = repathInterval;
        }

        // Steer along the path. Close in, face the player directly.
        Vector3 steerDir = toPlayer;
        if (dist > meleeRange && agent.hasPath && !agent.pathPending)
        {
            Vector3 toCorner = Flat(agent.steeringTarget - transform.position);
            if (toCorner.sqrMagnitude > 0.01f)
                steerDir = toCorner;
        }

        RotateToward(steerDir, dt);

        // Locomotion. Slow down while still turning so the enemy does not walk off the path.
        float targetSpeed = 0f;
        if (dist > stopDistance)
        {
            float alignment = Mathf.Clamp01(Vector3.Dot(transform.forward, steerDir.normalized));
            targetSpeed = (dist > runDistance ? runSpeedParam : walkSpeedParam) * alignment;
        }
        SetLocomotion(targetSpeed, dt);

        // Melee.
        if (dist <= meleeRange && attackCooldownTimer <= 0f && IsFacing(toPlayer))
        {
            StartAttack(Random.value < 0.5f ? AttackType.Swipe : AttackType.Punch);
            return;
        }

        // Jump attack for a player who keeps their distance.
        if (dist > jumpMinDistance) farTimer += dt;
        else farTimer = 0f;

        if (farTimer >= jumpTriggerDelay
            && dist <= jumpMaxDistance
            && jumpCooldownTimer <= 0f
            && attackCooldownTimer <= 0f
            && IsFacing(toPlayer)
            && HasClearLineToPlayer())
        {
            StartAttack(AttackType.Jump);
        }
    }

    private void UpdateAttack(float dt)
    {
        attackTimer += dt;

        SetLocomotion(0f, dt);

        bool tracking = currentAttack == AttackType.Jump
            ? !jumpLaunched
            : attackTimer <= attackTrackTime;

        if (tracking && HasTarget)
            RotateToward(Flat(player.position - transform.position), dt);

        if (leapActive)
            leapProgress = Mathf.Clamp01(leapProgress + dt / jumpAirTime);

        bool inAttackAnim = InAttackAnimation();

        if (!attackEntered)
        {
            if (inAttackAnim)
                attackEntered = true;
            else if (attackTimer > attackEnterTimeout)
                AbortAttack();
        }
        else if (!inAttackAnim)
        {
            FinishAttack();
        }

        // Failsafe so the enemy can never stay stuck in the Attack state.
        if (state == State.Attack && attackTimer > attackTimeout)
            FinishAttack();
    }
    #endregion

    #region Attacks
    private void StartAttack(AttackType type)
    {
        currentAttack = type;
        state = State.Attack;
        attackTimer = 0f;
        attackEntered = false;
        attackStartPosition = transform.position;

        jumpLaunched = false;
        leapActive = false;

        switch (type)
        {
            case AttackType.Swipe: animator.SetTrigger(SwipeHash); break;
            case AttackType.Punch: animator.SetTrigger(PunchHash); break;
            case AttackType.Jump: animator.SetTrigger(JumpHash); break;
        }
    }

    private void AbortAttack()
    {
        ResetAttackTriggers();
        state = State.Chase;
        attackCooldownTimer = 0.25f;
        leapActive = false;
    }

    private void FinishAttack()
    {
        state = State.Chase;
        attackCooldownTimer = attackCooldown;

        if (currentAttack == AttackType.Jump)
            jumpCooldownTimer = jumpCooldown;

        farTimer = 0f;
        repathTimer = 0f;

        leapActive = false;
    }

    private void ResetAttackTriggers()
    {
        animator.ResetTrigger(SwipeHash);
        animator.ResetTrigger(PunchHash);
        animator.ResetTrigger(JumpHash);
    }

    // True while the Animator is in, or moving into, a state tagged as an attack.
    private bool InAttackAnimation()
    {
        AnimatorStateInfo info = animator.IsInTransition(0)
            ? animator.GetNextAnimatorStateInfo(0)
            : animator.GetCurrentAnimatorStateInfo(0);

        return info.IsTag(attackTag);
    }

    public void SpawnJumpAOE()
    {
        if (state == State.Dead) return;

        if (leapActive)
        {
            Vector3 landing = new Vector3(leapEnd.x, transform.position.y, leapEnd.z);
            transform.position = landing;

            if (agent.enabled && agent.isOnNavMesh)
                agent.nextPosition = landing;

            leapActive = false;
        }

        Debug.Log($"[EnemyController] Jump attack travelled {Vector3.Distance(attackStartPosition, transform.position):F2} m");

        var go = new GameObject("JumpAOE");
        go.layer = aoeLayer;
        go.transform.position = transform.position + Vector3.up * aoeHeightOffset;

        var sphere = go.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = aoeRadius;

        // Trigger events need a Rigidbody on at least one of the two objects.
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var cc = go.AddComponent<CombatCollider>();
        cc.type = ColliderType.Hitbox;
        cc.Id = "JumpAOE";
        cc.Owner = combat;

        var hit = go.AddComponent<HitDetector>();
        hit.damage = jumpDamage;

        Destroy(go, aoeLifetime);

        if (jumpImpactVfx != null)
            Destroy(Instantiate(jumpImpactVfx, transform.position, Quaternion.identity), 3f);
    }

    public void JumpTakeoff()
    {
        if (!useCodeLeap || state != State.Attack || currentAttack != AttackType.Jump) return;

        jumpLaunched = true;
        if (!HasTarget) return;

        Vector3 toPlayer = Flat(player.position - transform.position);
        float distance = Mathf.Min(toPlayer.magnitude, jumpMaxDistance);
        float travel = Mathf.Max(0f, distance - jumpStandoff);

        if (toPlayer.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(toPlayer);

        leapStart = transform.position;
        leapEnd = leapStart + toPlayer.normalized * travel;

        // Stop at the NavMesh edge if something is in the way.
        if (agent.enabled && agent.isOnNavMesh && agent.Raycast(leapEnd, out NavMeshHit hit))
            leapEnd = hit.position;

        leapEnd.y = leapStart.y;
        leapProgress = 0f;
        leapActive = true;
    }
    #endregion

    #region Death
    private void HandleDeath()
    {
        if (state == State.Dead) return;
        state = State.Dead;

        ResetAttackTriggers();

        if (agent.enabled)
        {
            if (agent.isOnNavMesh) agent.ResetPath();
            agent.enabled = false;
        }

        if (despawnAfterDeath)
            Destroy(gameObject, despawnDelay);

        leapActive = false;
    }
    #endregion 

    // Runs after Update. Defining it stops Unity applying root motion automatically.
    private void OnAnimatorMove()
    {
        Vector3 position;

        if (leapActive)
        {
            // Code drives X/Z during the leap. The clip's horizontal motion is ignored.
            position = Vector3.Lerp(leapStart, leapEnd, leapProgress);
        }
        else
        {
            Vector3 delta = animator.deltaPosition;
            delta.y = 0f;

            float multiplier = rootMotionMultiplier;
            if (state == State.Attack && currentAttack == AttackType.Jump && !useCodeLeap)
                multiplier *= jumpMotionMultiplier;

            position = transform.position + delta * multiplier;
        }

        if (agent.enabled && agent.isOnNavMesh)
        {
            // Height comes from the NavMesh. Keep the agent in step with the transform.
            position.y = agent.nextPosition.y;
            transform.position = position;
            agent.nextPosition = position;
        }
        else
        {
            // Dead, or off the NavMesh: apply the clip's movement as it is.
            position.y = transform.position.y;
            transform.position = position;
        }
    }

    #region Helpers
    private void SetLocomotion(float target, float dt)
    {
        animator.SetFloat(SpeedHash, target, speedDamping, dt);
    }

    private void RotateToward(Vector3 flatDirection, float dt)
    {
        if (flatDirection.sqrMagnitude < 0.0001f) return;

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(flatDirection),
            rotationSpeed * dt);
    }

    private bool IsFacing(Vector3 flatDirectionToTarget)
    {
        return Vector3.Angle(transform.forward, flatDirectionToTarget) <= attackAngle;
    }

    // The jump travels in a straight line, so only jump when nothing blocks the way.
    private bool HasClearLineToPlayer()
    {
        if (!agent.isOnNavMesh) return true;
        return !agent.Raycast(player.position, out NavMeshHit hit);
    }

    private float FlatDistance(Vector3 point) => Flat(point - transform.position).magnitude;

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, jumpMinDistance);
        Gizmos.DrawWireSphere(transform.position, jumpMaxDistance);
    }
    #endregion
}
