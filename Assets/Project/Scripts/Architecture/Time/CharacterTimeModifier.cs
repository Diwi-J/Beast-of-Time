using UnityEngine;

/// <summary>
/// Per-character source of truth for gameplay/animation time scaling.
/// DO NOT additionally multiply root-motion deltaPosition by this multiplier:
/// Animator.speed already affects the root-motion samples.
/// Future AI should use GameplayDeltaTime for cooldowns, tracking and timers.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterCore))]
[RequireComponent(typeof(Animator))]
public sealed class CharacterTimeModifier : MonoBehaviour, ICharacterLifecycleParticipant
{
    [Header("Normal-time tuning")]
    [Tooltip("Normally 1. Separate from Overdrive so future effects can be composed here.")]
    [SerializeField, Min(0.01f)] private float baseMultiplier = 1f;

    private float overdriveMultiplier = 1f;
    private Animator animator;

    private static readonly int OldPlayerPowerUp = Animator.StringToHash("PowerUp");
    private static readonly int OldEnemySlowDown = Animator.StringToHash("SlowDown");

    public float EffectiveMultiplier => Mathf.Max(0.01f, baseMultiplier * overdriveMultiplier);
    public float GameplayDeltaTime => Time.deltaTime * EffectiveMultiplier;
    public float GameplayFixedDeltaTime => Time.fixedDeltaTime * EffectiveMultiplier;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        ApplyMultiplier();
    }

    private void OnEnable()
    {
        ApplyMultiplier();
    }

    public void SetOverdriveMultiplier(float multiplier)
    {
        overdriveMultiplier = Mathf.Max(0.01f, multiplier);
        ApplyMultiplier();
    }

    public void ClearOverdriveMultiplier()
    {
        overdriveMultiplier = 1f;
        ApplyMultiplier();
    }

    public void OnCharacterDied()
    {
        // Enemy death animations remain affected until the player's Overdrive
        // ends. The player is explicitly restored BEFORE its death animation.
    }

    public void OnCharacterReset()
    {
        ClearOverdriveMultiplier();
    }

    private void ApplyMultiplier()
    {
        if (animator == null) return;

        // Phase 02 used these Animator float parameters as the speed source.
        // Their existing Controller speed-multiplier bindings can remain;
        // neutralising them prevents multiplying the slowdown/boost TWICE.
        ResetLegacySpeedParameter(OldPlayerPowerUp);
        ResetLegacySpeedParameter(OldEnemySlowDown);

        // Unlike a per-state speed parameter, Animator.speed affects every
        // state, attack, combo substate and root-motion clip consistently.
        animator.speed = EffectiveMultiplier;
    }

    private void ResetLegacySpeedParameter(int hash)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == hash &&
                parameter.type == AnimatorControllerParameterType.Float)
            {
                animator.SetFloat(hash, 1f);
                return;
            }
        }
    }

    private void OnValidate()
    {
        baseMultiplier = Mathf.Max(0.01f, baseMultiplier);
    }
}
