using System;
using UnityEngine;

/// <summary>
/// Enemy-only runtime poise. Subscribes to the Phase 05 CombatResolver events.
///
/// - Player parries an enemy attack: THAT enemy gains poise.
/// - Player lands a heavy hit on an enemy: THAT enemy gains poise.
/// - If poise was ALREADY full before a heavy hit, that heavy causes a break.
/// - The heavy hit which first fills poise does NOT break it.
/// - A successful parry alone NEVER breaks poise.
///
/// Resets poise to zero on break, death, or world reset. Reset-on-break is a
/// provisional balance policy; it can be revisited separately from architecture.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterCore))]
[RequireComponent(typeof(CombatResolver))]
[RequireComponent(typeof(Health))]
public sealed class EnemyPoise : MonoBehaviour, ICharacterLifecycleParticipant
{
    [SerializeField] private EnemyPoiseConfig configuration;
    [SerializeField] private bool logPoiseChanges;

    public float CurrentPoise { get; private set; }
    public float MaximumPoise => configuration != null ? configuration.MaximumPoise : 0f;
    public float Normalized => MaximumPoise > 0f ? CurrentPoise / MaximumPoise : 0f;
    public bool IsPrimed => MaximumPoise > 0f && CurrentPoise >= MaximumPoise;

    public event Action<float, float> PoiseChanged;
    public event Action<CombatHitResult> PoiseBroken;

    // This event lets the reaction component respond to heavy attacks AFTER
    // poise calculation, avoiding event-subscriber-order bugs on a break hit.
    public event Action<CombatHitResult, bool> HeavyHitProcessed;

    private CharacterCore core;
    private Health health;
    private CombatResolver resolver;

    private void Awake()
    {
        core = GetComponent<CharacterCore>();
        health = GetComponent<Health>();
        resolver = GetComponent<CombatResolver>();

        if (core != null && !core.IsEnemy)
            Debug.LogError("[EnemyPoise] This component is for mobs and bosses, not the player.", this);
        if (configuration == null)
            Debug.LogError($"[EnemyPoise] {name}: assign an EnemyPoiseConfig asset.", this);
    }

    private void OnEnable()
    {
        if (resolver == null) return;
        resolver.HitLanded += HandleOwnAttackOutcome;
        resolver.HitReceived += HandleReceivedHit;
    }

    private void OnDisable()
    {
        if (resolver == null) return;
        resolver.HitLanded -= HandleOwnAttackOutcome;
        resolver.HitReceived -= HandleReceivedHit;
    }

    private void HandleOwnAttackOutcome(CombatHitResult hit)
    {
        if (!CanAccumulate() || hit.Outcome != CombatHitOutcome.Parried) return;
        if (hit.Attacker == null || hit.Attacker.gameObject != gameObject) return;
        if (!IsPlayer(hit.Defender)) return;

        // This parry's gauge award belongs to the player; enemy poise belongs
        // to the attacker who was parried. Bosses gain poise even though they
        // do not play an ordinary parried reaction.
        AddPoise(configuration.GainWhenParried);
    }

    private void HandleReceivedHit(CombatHitResult hit)
    {
        if (!CanAccumulate() || hit.Outcome != CombatHitOutcome.Damaged ||
            hit.DamageApplied <= 0f) return;
        if (hit.Defender == null || hit.Defender.gameObject != gameObject) return;
        if (!hit.IsHeavyAttack || !IsPlayer(hit.Attacker)) return;

        bool broke = IsPrimed; // IMPORTANT: evaluated BEFORE applying this hit's gain.
        if (broke)
        {
            SetPoise(0f);
            if (logPoiseChanges) Debug.Log($"[EnemyPoise] {name}: poised heavy hit caused STAGGER.", this);
            PoiseBroken?.Invoke(hit);
        }
        else
        {
            AddPoise(configuration.GainFromHeavyHit);
        }

        HeavyHitProcessed?.Invoke(hit, broke);
    }

    private bool CanAccumulate()
    {
        return core != null && core.IsEnemy && configuration != null &&
               health != null && !health.IsDead;
    }

    private static bool IsPlayer(CharacterCombat character)
    {
        if (character == null) return false;
        CharacterCore characterCore = character.GetComponent<CharacterCore>();
        return characterCore != null && characterCore.IsPlayer;
    }

    private void AddPoise(float amount)
    {
        if (amount <= 0f) return;
        SetPoise(Mathf.Min(MaximumPoise, CurrentPoise + amount));
    }

    private void SetPoise(float value)
    {
        float next = Mathf.Clamp(value, 0f, MaximumPoise);
        if (Mathf.Approximately(next, CurrentPoise)) return;
        CurrentPoise = next;
        if (logPoiseChanges) Debug.Log($"[EnemyPoise] {name}: {CurrentPoise:0.##}/{MaximumPoise:0.##} (primed={IsPrimed}).", this);
        PoiseChanged?.Invoke(CurrentPoise, MaximumPoise);
    }

    public void ResetPoise()
    {
        SetPoise(0f);
    }

    public void OnCharacterDied() => ResetPoise();
    public void OnCharacterReset() => ResetPoise();

    [ContextMenu("DEBUG / Add Parry Poise (Play Mode)")]
    private void DebugGainFromParry()
    {
        if (Application.isPlaying && CanAccumulate())
            AddPoise(configuration.GainWhenParried);
    }

    [ContextMenu("DEBUG / Prime Poise (Play Mode)")]
    private void DebugPrime()
    {
        if (Application.isPlaying && CanAccumulate())
            SetPoise(MaximumPoise);
    }

    [ContextMenu("DEBUG / Print Poise (Play Mode)")]
    private void DebugPrintPoise()
    {
        if (Application.isPlaying)
            Debug.Log($"[EnemyPoise] {name}: {CurrentPoise:0.##}/{MaximumPoise:0.##}; primed={IsPrimed}.", this);
    }

    [ContextMenu("DEBUG / Reset Poise (Play Mode)")]
    private void DebugReset()
    {
        if (Application.isPlaying) ResetPoise();
    }
}
