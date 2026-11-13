using System;
using System.Collections.Generic;
using UnityEngine;

public enum PlayerComboInput
{
    Light,
    Heavy
}

/// <summary>
/// Authoring data for the KNIGHT'S fixed five-attack move set.
/// This asset defines move identity, Animator requests and allowed follow-ups.
/// It does NOT hold mutable combo state, damage values, transition times or
/// Animator clips. Damage remains in CombatAttackSet; transition duration,
/// exit time and offset remain authored in the Animator Controller.
/// </summary>
[CreateAssetMenu(fileName = "PlayerComboConfig", menuName = "Soulslike/Player/Combo Config")]
public sealed class PlayerComboConfig : ScriptableObject
{
    [Serializable]
    public sealed class Move
    {
        [Tooltip("Must match CombatAttackSet's Attack ID and CharacterCombat.BeginAttack animation event string.")]
        [SerializeField] private string attackId;

        [Tooltip("Input category of this animation: Light or Heavy.")]
        [SerializeField] private PlayerComboInput input = PlayerComboInput.Light;

        [Tooltip("Animator trigger requesting this specific animation. Existing examples: 1_LightAtk, 2_LightAtk, 1_HeavyAtk.")]
        [SerializeField] private string animatorTrigger;

        [Tooltip("Attack ID requested by a LIGHT input during this move's authored combo window. Empty = no light follow-up.")]
        [SerializeField] private string nextLightAttackId;

        [Tooltip("Attack ID requested by a HEAVY input during this move's authored combo window. Empty = no heavy follow-up.")]
        [SerializeField] private string nextHeavyAttackId;

        public string AttackId => attackId;
        public PlayerComboInput Input => input;
        public string AnimatorTrigger => animatorTrigger;

        public string FollowUpId(PlayerComboInput button)
        {
            return button == PlayerComboInput.Light ? nextLightAttackId : nextHeavyAttackId;
        }
    }

    [Header("Neutral-state attack starters")]
    [Tooltip("Attack ID for the first standalone light attack.")]
    [SerializeField] private string firstLightOpenerId;

    [Tooltip("Optional SECOND standalone light opener. When populated, standalone light attacks alternate between the two, preserving the old prototype behaviour.")]
    [SerializeField] private string secondLightOpenerId;

    [Tooltip("Attack ID for the standalone heavy attack.")]
    [SerializeField] private string heavyOpenerId;

    [Header("Exactly five authored moves: three light, two heavy")]
    [SerializeField] private List<Move> moves = new List<Move>();

    public bool TryGetMove(string id, out Move move)
    {
        if (!string.IsNullOrEmpty(id))
        {
            foreach (Move candidate in moves)
            {
                if (candidate != null && string.Equals(candidate.AttackId, id, StringComparison.Ordinal))
                {
                    move = candidate;
                    return true;
                }
            }
        }
        move = null;
        return false;
    }

    public bool TryGetOpener(PlayerComboInput input, bool useSecondLight, out Move move)
    {
        string id = input == PlayerComboInput.Heavy ? heavyOpenerId :
            (useSecondLight && !string.IsNullOrEmpty(secondLightOpenerId) ? secondLightOpenerId : firstLightOpenerId);
        if (TryGetMove(id, out move) && move.Input == input)
            return true;
        move = null;
        return false;
    }

    public bool HasSecondLightOpener => !string.IsNullOrEmpty(secondLightOpenerId);
    public IReadOnlyList<Move> Moves => moves;

    private void OnValidate()
    {
        if (moves == null) return;
        if (moves.Count != 5)
            Debug.LogWarning($"[PlayerComboConfig] '{name}' has {moves.Count} moves; the project calls for exactly 5 (3 light, 2 heavy).", this);

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        int lights = 0, heavies = 0;
        foreach (Move move in moves)
        {
            if (move == null) continue;
            if (move.Input == PlayerComboInput.Light) lights++; else heavies++;
            if (string.IsNullOrWhiteSpace(move.AttackId) || string.IsNullOrWhiteSpace(move.AnimatorTrigger))
                Debug.LogWarning($"[PlayerComboConfig] '{name}' has a move missing its Attack ID or Animator trigger.", this);
            else if (!ids.Add(move.AttackId))
                Debug.LogWarning($"[PlayerComboConfig] '{name}' has a duplicate Attack ID '{move.AttackId}'.", this);
        }
        if (moves.Count == 5 && (lights != 3 || heavies != 2))
            Debug.LogWarning($"[PlayerComboConfig] '{name}' expects 3 light and 2 heavy moves; found {lights} light and {heavies} heavy.", this);

        foreach (Move move in moves)
        {
            if (move == null || string.IsNullOrEmpty(move.AttackId)) continue;
            ValidateFollowUp(move, PlayerComboInput.Light);
            ValidateFollowUp(move, PlayerComboInput.Heavy);
        }
        ValidateOpener(firstLightOpenerId, PlayerComboInput.Light, "First Light Opener");
        if (!string.IsNullOrEmpty(secondLightOpenerId))
            ValidateOpener(secondLightOpenerId, PlayerComboInput.Light, "Second Light Opener");
        ValidateOpener(heavyOpenerId, PlayerComboInput.Heavy, "Heavy Opener");
    }

    private void ValidateFollowUp(Move origin, PlayerComboInput input)
    {
        string id = origin.FollowUpId(input);
        if (string.IsNullOrEmpty(id)) return;
        if (string.Equals(id, origin.AttackId, StringComparison.Ordinal))
        {
            Debug.LogWarning($"[PlayerComboConfig] '{name}': immediate self-follow-up '{id}' is not supported by the per-clip ID completion events. Choose a different follow-up.", this);
            return;
        }
        if (!TryGetMove(id, out Move next) || next.Input != input)
            Debug.LogWarning($"[PlayerComboConfig] '{name}': {origin.AttackId}'s {input} follow-up '{id}' is missing or of the wrong input type.", this);
    }

    private void ValidateOpener(string id, PlayerComboInput input, string label)
    {
        if (!TryGetMove(id, out Move move) || move.Input != input)
            Debug.LogWarning($"[PlayerComboConfig] '{name}': {label} '{id}' is missing or has the wrong input type.", this);
    }
}
