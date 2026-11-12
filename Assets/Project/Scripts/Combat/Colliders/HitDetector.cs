using UnityEngine;

/// <summary>
/// Detects only physics contacts. Damage / parry / block rules live in the
/// attacker's CombatResolver; per-attack deduplication lives in
/// CombatAttackRuntime. Never disables its own collider on contact.
/// </summary>
[RequireComponent(typeof(CombatCollider))]
public class HitDetector : MonoBehaviour
{
    [Tooltip("Used by older clips until a CombatAttackSet is wired via BeginAttack.")]
    public float damage = 10f;

    // Retain the old serialized field for prefab compatibility, but the source
    // of truth for chip damage is defender.CharacterCombat.blockDamageMultiplier.
    [HideInInspector] public float chipDamagemultiplier = 0.1f;

    private CombatCollider self;
    private PlayerSwordSound swordSound;
    private CombatResolver resolver;
    private CharacterCombat cachedOwner;
    private bool reportedMissingResolver;

    private void Awake()
    {
        self = GetComponent<CombatCollider>();
        // Preserve the existing hit sound without designing the audio system now.
        swordSound = GetComponent<PlayerSwordSound>();
        if (swordSound == null)
            swordSound = GetComponentInParent<PlayerSwordSound>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (self == null || self.type != ColliderType.Hitbox)
            return;

        CombatCollider target = other.GetComponent<CombatCollider>();
        if (target == null || self.Owner == null) return;

        if (cachedOwner != self.Owner || resolver == null)
        {
            cachedOwner = self.Owner;
            resolver = cachedOwner.GetComponent<CombatResolver>();
        }

        if (resolver == null)
        {
            if (!reportedMissingResolver)
            {
                reportedMissingResolver = true;
                Debug.LogError($"[HitDetector] Add CombatResolver to attack owner '{self.Owner.name}'.", self.Owner);
            }
            return;
        }

        if (!resolver.TryResolve(self, target, damage, out CombatHitResult result))
            return;

        // Temporary legacy sound bridge until the dedicated audio/VFX phase.
        if (result.Outcome == CombatHitOutcome.Damaged && swordSound != null)
            swordSound.PlayHit();
    }
}
