using UnityEngine;

public enum CombatHitOutcome
{
    Damaged,
    Blocked,
    Parried
}

/// <summary>
/// Describes a confirmed contact AFTER combat rules have been applied.
/// Future poise, sound and VFX listeners can use this without reading
/// collider- or health-script internals.
/// </summary>
public readonly struct CombatHitResult
{
    public CharacterCombat Attacker { get; }
    public CharacterCombat Defender { get; }
    public CombatCollider DefenderCollider { get; }
    public CombatHitOutcome Outcome { get; }
    public float DamageApplied { get; }
    public string AttackId { get; }
    public bool IsHeavyAttack { get; }
    public bool IsComboAttack { get; }
    public Vector3 ImpactPoint { get; }
    public Vector3 ImpactDirection { get; }

    public CombatHitResult(CharacterCombat attacker, CharacterCombat defender,
        CombatCollider defenderCollider, CombatHitOutcome outcome,
        float damageApplied, string attackId, bool isHeavyAttack,
        bool isComboAttack, Vector3 impactPoint, Vector3 impactDirection)
    {
        Attacker = attacker;
        Defender = defender;
        DefenderCollider = defenderCollider;
        Outcome = outcome;
        DamageApplied = damageApplied;
        AttackId = attackId;
        IsHeavyAttack = isHeavyAttack;
        IsComboAttack = isComboAttack;
        ImpactPoint = impactPoint;
        ImpactDirection = impactDirection;
    }
}
