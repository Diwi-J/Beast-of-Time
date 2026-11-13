using UnityEngine;

/// <summary>
/// Per-archetype MOB tuning only. This asset never stores an enemy's current
/// target, health phase, path, cooldown, or attack state.
/// Can be shared by all three mobs, or varied per mob for balancing.
/// </summary>
[CreateAssetMenu(fileName = "MobAIConfig", menuName = "Soulslike/Enemy/Mob AI Config")]
public sealed class MobAIConfig : ScriptableObject
{
    [Header("Awareness / home territory (metres)")]
    [Min(0.1f)] [SerializeField] private float detectionRange = 10f;
    [Tooltip("Distance from the MOB'S ORIGINAL SPAWN to the PLAYER that ends pursuit.")]
    [Min(0.1f)] [SerializeField] private float leashRange = 16f;
    [Min(0.05f)] [SerializeField] private float homeArrivalDistance = 0.4f;

    [Header("Navigation (metres / second)")]
    [Min(0.1f)] [SerializeField] private float walkSpeed = 1.6f;
    [Min(0.1f)] [SerializeField] private float runSpeed = 3.4f;
    [Min(0f)] [SerializeField] private float runWhenFartherThan = 5f;
    [Min(0.1f)] [SerializeField] private float acceleration = 12f;
    [Min(0f)] [SerializeField] private float turnDegreesPerSecond = 360f;
    [Min(0.05f)] [SerializeField] private float repathInterval = 0.2f;
    [Min(0f)] [SerializeField] private float stoppingDistance = 1.5f;

    [Header("Attacking")]
    [Min(0.1f)] [SerializeField] private float attackRange = 2.2f;
    [Range(0f, 180f)] [SerializeField] private float attackFacingAngle = 40f;
    [Tooltip("Health fractions: above this only Melee_1 / Melee_2. At or below, combos unlock.")]
    [Range(0.01f, 1f)] [SerializeField] private float comboUnlockHealth = 0.6f;
    [Tooltip("Health fraction at/below which the mob becomes almost relentless.")]
    [Range(0f, 1f)] [SerializeField] private float frenzyHealth = 0.2f;
    [Min(0f)] [SerializeField] private float basicCooldown = 1.2f;
    [Min(0f)] [SerializeField] private float comboPhaseCooldown = 0.55f;
    [Min(0f)] [SerializeField] private float frenzyCooldown = 0.05f;
    [Range(0f, 1f)] [SerializeField] private float comboChanceAfterUnlock = 0.65f;
    [Range(0f, 1f)] [SerializeField] private float comboChanceDuringFrenzy = 0.85f;

    [Header("Animator response")]
    [Tooltip("Movement blend tree: 0 idle, 0.5 walk, 1 run.")]
    [Min(0f)] [SerializeField] private float speedDamping = 0.12f;

    public float DetectionRange => detectionRange;
    public float LeashRange => leashRange;
    public float HomeArrivalDistance => homeArrivalDistance;
    public float WalkSpeed => walkSpeed;
    public float RunSpeed => runSpeed;
    public float RunWhenFartherThan => runWhenFartherThan;
    public float Acceleration => acceleration;
    public float TurnDegreesPerSecond => turnDegreesPerSecond;
    public float RepathInterval => repathInterval;
    public float StoppingDistance => stoppingDistance;
    public float AttackRange => attackRange;
    public float AttackFacingAngle => attackFacingAngle;
    public float ComboUnlockHealth => comboUnlockHealth;
    public float FrenzyHealth => frenzyHealth;
    public float BasicCooldown => basicCooldown;
    public float ComboPhaseCooldown => comboPhaseCooldown;
    public float FrenzyCooldown => frenzyCooldown;
    public float ComboChanceAfterUnlock => comboChanceAfterUnlock;
    public float ComboChanceDuringFrenzy => comboChanceDuringFrenzy;
    public float SpeedDamping => speedDamping;

    private void OnValidate()
    {
        detectionRange = Mathf.Max(0.1f, detectionRange);
        leashRange = Mathf.Max(detectionRange + 0.1f, leashRange);
        homeArrivalDistance = Mathf.Max(0.05f, homeArrivalDistance);
        walkSpeed = Mathf.Max(0.1f, walkSpeed);
        runSpeed = Mathf.Max(walkSpeed, runSpeed);
        repathInterval = Mathf.Max(0.05f, repathInterval);
        stoppingDistance = Mathf.Max(0f, stoppingDistance);
        attackRange = Mathf.Max(stoppingDistance + 0.1f, attackRange);
        frenzyHealth = Mathf.Min(frenzyHealth, comboUnlockHealth);
    }
}
