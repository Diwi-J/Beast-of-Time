using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds one character's CURRENT attack and the characters contacted during it.
/// A shared CombatAttackSet asset never contains mutable runtime state.
///
/// CharacterCombat starts/ends the active window through its existing
/// Enablehitbox/Disablehitbox animation events; BeginAttack
/// can additionally choose an authored attack definition.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterCombat))]
public sealed class CombatAttackRuntime : MonoBehaviour, ICharacterLifecycleParticipant
{
    [SerializeField] private CombatAttackSet attackSet;

    private readonly HashSet<CharacterCombat> hitTargets = new HashSet<CharacterCombat>();
    private CombatAttackSet.AttackEntry activeDefinition;
    private string activeAttackId = string.Empty;
    private bool active;
    private bool comboAttack;
    private bool comboModeEnabled;

    public string ActiveAttackId => activeAttackId;
    public bool IsActive => active;
    public bool IsComboAttack => active && comboAttack;
    public bool ComboModeEnabled => comboModeEnabled;
    public bool IsHeavyAttack => active && activeDefinition != null && activeDefinition.IsHeavy;
    public bool HasAuthoredDamage => active && activeDefinition != null;

    /// <summary>Called at the beginning of an attack CLIP, not on button press.</summary>
    public void BeginAuthoredAttack(string attackId)
    {
        EndAttack();
        active = true;
        // A shared clip may play alone or inside an authored combo substate.
        // The ACTION/AI controller owns this mode, not the animation clip.
        comboAttack = comboModeEnabled;
        activeAttackId = attackId ?? string.Empty;

        if (attackSet == null)
        {
            Debug.LogWarning($"[CombatAttackRuntime] {name}: no CombatAttackSet assigned. HitDetector fallback damage will be used.", this);
            return;
        }

        if (!attackSet.TryGetAttack(activeAttackId, out activeDefinition))
            Debug.LogWarning($"[CombatAttackRuntime] {name}: attack ID '{activeAttackId}' not found in '{attackSet.name}'. Using fallback damage.", this);
    }

    /// <summary>
    /// Called by a FUTURE player combo controller or enemy AI when a combo
    /// starts/ends. Do not bake combo-only events into shared animation clips.
    /// </summary>
    public void SetComboMode(bool enabled)
    {
        comboModeEnabled = enabled;
    }

    /// <summary>
    /// Backwards-compatible for clips that ONLY contain Enablehitbox/Disablehitbox.
    /// All enabled hitboxes share this one hit-target set until the last one closes.
    /// </summary>
    public void BeginLegacyWindowIfNeeded()
    {
        if (active) return;
        EndAttack();
        active = true;
    }

    /// <summary>True only the first time this attack contacts this character.</summary>
    public bool TryRegisterTarget(CharacterCombat target)
    {
        if (target == null) return false;
        BeginLegacyWindowIfNeeded();
        return hitTargets.Add(target);
    }

    public float DamageOrFallback(float detectorFallback)
    {
        return activeDefinition != null && active
            ? activeDefinition.CalculateDamage(comboAttack)
            : Mathf.Max(0f, detectorFallback);
    }

    public void EndAttack()
    {
        active = false;
        comboAttack = false;
        activeAttackId = string.Empty;
        activeDefinition = null;
        hitTargets.Clear();
    }

    public void ResetAllAttackState()
    {
        EndAttack();
        comboModeEnabled = false;
    }

    [ContextMenu("DEBUG / Enable Combo Mode (Play Mode)")]
    private void DebugEnableCombo()
    {
        if (Application.isPlaying) SetComboMode(true);
    }

    [ContextMenu("DEBUG / Disable Combo Mode (Play Mode)")]
    private void DebugDisableCombo()
    {
        if (Application.isPlaying) SetComboMode(false);
    }

    public void OnCharacterDied() => ResetAllAttackState();
    public void OnCharacterReset() => ResetAllAttackState();

    private void OnDisable() => ResetAllAttackState();
}
