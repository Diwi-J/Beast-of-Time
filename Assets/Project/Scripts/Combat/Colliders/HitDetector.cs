using System;
using UnityEngine;

[RequireComponent(typeof(CombatCollider))]
public class HitDetector : MonoBehaviour
{
    public float damage = 10f;
    public float chipDamagemultiplier = 0.1f;

    private CombatCollider self;
    private PlayerSwordSound swordSound;
    private static readonly int ImpactHash = Animator.StringToHash("Impact");

    private void Awake()
    {
        self = GetComponent<CombatCollider>();
        swordSound = GetComponent<PlayerSwordSound>();
    }

    private void OnTriggerEnter(Collider other)
    {
        var targetCC = other.GetComponent<CombatCollider>();

        if (targetCC == null) return;
        if (targetCC.Owner == self.Owner) return;

        var defender = targetCC.Owner;
        if (defender == null) return;

        if (self.Owner != null && defender.CompareTag(self.Owner.tag)) return;

        if (targetCC.type == ColliderType.Blockbox)
        {
            ResolveBlockHit(defender);
            GetComponent<Collider>().enabled = false;
        }
        else if (targetCC.type == ColliderType.Hurtbox)
        {
            ResolveCleanHit(defender);
            GetComponent<Collider>().enabled = false;
        }
    }

    private void ResolveBlockHit(CharacterCombat defender)
    {
        if (defender.IsInParryWindow())
        {
            Debug.Log($"{defender.name} Parried");

            if (defender.ParryDistortion != null)
                defender.ParryDistortion.Play();

            var defenderSound = defender.GetComponent<PlayerSwordSound>();
            if (defenderSound != null)
                defenderSound.PlayClash();

            if (defender.gauge != null)
                defender.gauge.OnParrySuccess();

            // Parry deals zero damage and does not stagger the attacker.
            return;
        }

        float chip = damage * defender.blockDamageMultiplier;
        Debug.Log($"{defender.name} Blocked and took {chip} chip damage");

        ApplyDamage(defender, chip);

        // Skip the impact reaction if the chip damage killed the defender.
        if (defender.Health == null || !defender.Health.IsDead)
            defender.GetComponent<Animator>().SetTrigger(ImpactHash);
    }

    private void ResolveCleanHit(CharacterCombat defender)
    {
        Debug.Log($"{defender.name} took {damage} damage.");

        if (swordSound != null)
            swordSound.PlayHit();

        ApplyDamage(defender, damage);
    }

    private static void ApplyDamage(CharacterCombat defender, float amount)
    {
        if (defender.Health != null)
            defender.Health.TakeDamage(amount);
    }
}

