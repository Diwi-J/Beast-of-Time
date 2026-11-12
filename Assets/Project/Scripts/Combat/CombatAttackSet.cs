using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authoring data only. Create one asset per character/archetype as needed.
/// The current attack and its already-hit targets NEVER live in this asset.
/// </summary>
[CreateAssetMenu(fileName = "CombatAttackSet", menuName = "Soulslike/Combat/Attack Set")]
public sealed class CombatAttackSet : ScriptableObject
{
    [Serializable]
    public sealed class AttackEntry
    {
        [Tooltip("Must exactly match the string passed by the BeginAttack animation event.")]
        [SerializeField] private string attackId;
        [Min(0f)] [SerializeField] private float baseDamage = 10f;
        [Tooltip("For later poise and action-interruption rules; no poise is applied in Phase 05.")]
        [SerializeField] private bool heavyAttack;
        [Tooltip("Applied only when BeginComboAttack is used. 1 means no bonus.")]
        [Min(1f)] [SerializeField] private float comboDamageMultiplier = 1.1f;

        public string AttackId => attackId;
        public bool IsHeavy => heavyAttack;
        public float BaseDamage => Mathf.Max(0f, baseDamage);
        public float ComboDamageMultiplier => Mathf.Max(1f, comboDamageMultiplier);
        public float CalculateDamage(bool isComboAttack)
        {
            return BaseDamage * (isComboAttack ? ComboDamageMultiplier : 1f);
        }
    }

    [SerializeField] private List<AttackEntry> attacks = new List<AttackEntry>();

    public bool TryGetAttack(string attackId, out AttackEntry entry)
    {
        if (!string.IsNullOrEmpty(attackId))
        {
            foreach (AttackEntry candidate in attacks)
            {
                if (candidate != null &&
                    string.Equals(candidate.AttackId, attackId, StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            }
        }

        entry = null;
        return false;
    }

    private void OnValidate()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (AttackEntry attack in attacks)
        {
            if (attack == null) continue;
            if (string.IsNullOrWhiteSpace(attack.AttackId))
            {
                Debug.LogWarning($"[CombatAttackSet] Empty attack ID in {name}.", this);
                continue;
            }

            if (!seen.Add(attack.AttackId))
                Debug.LogWarning($"[CombatAttackSet] Duplicate attack ID '{attack.AttackId}' in {name}.", this);
        }
    }
}
