using System;
using UnityEngine;

/// <summary>
/// Lives on each character root. A weapon HitDetector forwards collision
/// contacts to its ATTACKER's resolver. This component decides the outcome,
/// applies health damage and emits actor-local results for future features.
///
/// Temporary compatibility feedback: parry VFX / gauge / block Impact are
/// still invoked here. Audio and VFX will become subscribers later.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterCombat))]
[RequireComponent(typeof(CombatAttackRuntime))]
public sealed class CombatResolver : MonoBehaviour
{
    [SerializeField] private bool logConfirmedHits;

    private CharacterCombat combat;
    private CharacterCore core;
    private CombatAttackRuntime attackRuntime;
    private static readonly int ImpactHash = Animator.StringToHash("Impact");

    public event Action<CombatHitResult> HitLanded;
    public event Action<CombatHitResult> HitReceived;

    private void Awake()
    {
        combat = GetComponent<CharacterCombat>();
        core = GetComponent<CharacterCore>();
        attackRuntime = GetComponent<CombatAttackRuntime>();
    }

    /// <summary>
    /// Returns false for invalid, friendly, dead or already-hit contacts.
    /// Multiple enemy targets are allowed in one attack; each target can
    /// receive a result only once until the next attack window.
    /// </summary>
    public bool TryResolve(CombatCollider attackingHitbox, CombatCollider target,
        float fallbackDamage, out CombatHitResult result)
    {
        result = default(CombatHitResult);

        if (attackingHitbox == null || target == null ||
            attackingHitbox.type != ColliderType.Hitbox ||
            (target.type != ColliderType.Hurtbox && target.type != ColliderType.Blockbox))
            return false;

        CharacterCombat attacker = attackingHitbox.Owner;
        CharacterCombat defender = target.Owner;
        if (attacker == null || defender == null || attacker != combat || defender == attacker)
            return false;

        if (combat.Health == null || combat.Health.IsDead ||
            defender.Health == null || defender.Health.IsDead)
            return false;

        if (AreAllies(defender)) return false;
        if (attackRuntime == null) return false;

        // Register AFTER validation but BEFORE taking damage, because
        // damage callbacks can disable colliders and change actor state.
        if (!attackRuntime.TryRegisterTarget(defender)) return false;

        float damage = attackRuntime.DamageOrFallback(fallbackDamage);
        CombatHitOutcome outcome;
        float appliedDamage = 0f;

        if (target.type == ColliderType.Blockbox)
        {
            if (defender.IsInParryWindow())
            {
                outcome = CombatHitOutcome.Parried;
                if (defender.ParryDistortion != null)
                    defender.ParryDistortion.Play();

                // Gauge owns Overdrive charge. During Overdrive, parries do
                // NOT replenish it. Gauge also plays the original clash SFX
                // when charging, so don't play that sound twice.
                if (defender.gauge != null && !defender.gauge.IsDraining)
                    defender.gauge.OnParrySuccess();
                else
                {
                    PlayerSwordSound sound = defender.GetComponent<PlayerSwordSound>();
                    if (sound != null) sound.PlayClash();
                }
            }
            else
            {
                outcome = CombatHitOutcome.Blocked;
                appliedDamage = ApplyHealthDamage(defender.Health,
                    damage * Mathf.Max(0f, defender.blockDamageMultiplier));

                if (!defender.Health.IsDead)
                {
                    Animator defenderAnimator = defender.GetComponent<Animator>();
                    if (defenderAnimator != null)
                        defenderAnimator.SetTrigger(ImpactHash);
                }
            }
        }
        else
        {
            outcome = CombatHitOutcome.Damaged;
            appliedDamage = ApplyHealthDamage(defender.Health, damage);
        }

        Collider sourceCollider = attackingHitbox.GetComponent<Collider>();
        Collider targetCollider = target.GetComponent<Collider>();
        Vector3 point = target.transform.position;
        if (sourceCollider != null && targetCollider != null)
            point = targetCollider.ClosestPoint(sourceCollider.bounds.center);

        Vector3 direction = defender.transform.position - attacker.transform.position;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : attacker.transform.forward;

        result = new CombatHitResult(attacker, defender, target, outcome,
            appliedDamage, attackRuntime.ActiveAttackId, attackRuntime.IsHeavyAttack,
            attackRuntime.IsComboAttack, point, direction);

        if (logConfirmedHits)
            Debug.Log($"[CombatResolver] {attacker.name} -> {defender.name}: " +
                      $"{outcome}, applied={appliedDamage:0.##}, " +
                      $"attack='{result.AttackId}', heavy={result.IsHeavyAttack}, combo={result.IsComboAttack}",
                      this);

        HitLanded?.Invoke(result);
        CombatResolver defenderResolver = defender.GetComponent<CombatResolver>();
        if (defenderResolver != null)
            defenderResolver.HitReceived?.Invoke(result);

        return true;
    }

    private bool AreAllies(CharacterCombat defender)
    {
        CharacterCore defenderCore = defender.GetComponent<CharacterCore>();
        if (core != null && defenderCore != null)
        {
            // No friendly fire: both mobs and bosses are on the enemy side.
            return core.IsEnemy == defenderCore.IsEnemy;
        }

        // Safety fallback during migration of existing prefabs.
        return defender.CompareTag(gameObject.tag);
    }

    private static float ApplyHealthDamage(Health health, float amount)
    {
        if (health == null || amount <= 0f) return 0f;
        float before = health.Current;
        health.TakeDamage(amount);
        return Mathf.Max(0f, before - health.Current);
    }
}
