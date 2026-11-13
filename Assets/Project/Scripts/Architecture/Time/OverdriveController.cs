using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Player-only owner of Overdrive activation and speed effects.
/// Charge/drain remain owned by Gauge; balance comes from OverdriveConfig.
/// All enemies are found through the ONE SceneActorRegistry (no second list).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterCore))]
[RequireComponent(typeof(CharacterTimeModifier))]
public sealed class OverdriveController : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private OverdriveConfig configuration;

    [Header("Existing scene references")]
    [SerializeField] private Gauge gauge;
    [SerializeField] private SceneActorRegistry actorRegistry;
    [SerializeField] private ParticleSystem timeDilationDistortion;

    private Controls controls;
    private Health health;
    private CharacterTimeModifier playerTime;
    private Animator animator;

    private static readonly int PoweredUpTrigger = Animator.StringToHash("PoweredUp");

    public bool IsActive => gauge != null && gauge.IsDraining;
    public Gauge Gauge => gauge;
    public OverdriveConfig Configuration => configuration;

    private void Awake()
    {
        var core = GetComponent<CharacterCore>();
        if (core != null && !core.IsPlayer)
            Debug.LogError("[OverdriveController] This component belongs on the player only.", this);

        health = GetComponent<Health>();
        playerTime = GetComponent<CharacterTimeModifier>();
        animator = GetComponent<Animator>();
        controls = new Controls();
        controls.Player.PowerUp.performed += OnActivationInput;

        if (gauge == null)
        {
            CharacterCombat combat = GetComponent<CharacterCombat>();
            if (combat != null)
                gauge = combat.gauge;
        }

        if (gauge != null && configuration != null)
            gauge.Configure(configuration);
    }

    private void OnEnable()
    {
        if (controls != null)
            controls.Enable();
        if (gauge != null)
            gauge.OnGaugeEmpty += HandleGaugeEmpty;
        if (health != null)
            health.OnDeath += CancelAndReset;
    }

    private void OnDisable()
    {
        CancelAndReset();
        if (controls != null)
            controls.Disable();
        if (gauge != null)
            gauge.OnGaugeEmpty -= HandleGaugeEmpty;
        if (health != null)
            health.OnDeath -= CancelAndReset;
    }

    private void OnDestroy()
    {
        if (controls != null)
        {
            controls.Player.PowerUp.performed -= OnActivationInput;
            controls.Dispose();
        }
    }

    private void OnActivationInput(InputAction.CallbackContext context)
    {
        TryActivate();
    }

    /// <summary>Also usable by a future player-action controller.</summary>
    public bool TryActivate()
    {
        if (health == null || health.IsDead || IsActive)
            return false;

        if (configuration == null || actorRegistry == null || gauge == null || playerTime == null)
        {
            Debug.LogError("[OverdriveController] Assign OverdriveConfig, Gauge and SceneActorRegistry.", this);
            return false;
        }

        if (!gauge.StartTimeDilation(configuration.MinimumActivationCharge))
            return false;

        // Apply effects first; the activation animation is purely presentation.
        playerTime.SetOverdriveMultiplier(configuration.PlayerSpeedMultiplier);
        SetEnemyMultipliers(configuration.EnemySpeedMultiplier, applyOnlyToLiving: true);

        if (animator != null)
            animator.SetTrigger(PoweredUpTrigger);
        if (timeDilationDistortion != null)
            timeDilationDistortion.Play();

        return true;
    }

    /// <summary>
    /// Call BEFORE triggering a player death animation, or during a rest/respawn.
    /// This explicitly removes every effect and empties the gauge.
    /// </summary>
    public void CancelAndReset()
    {
        EndEffects();
        if (gauge != null)
            gauge.ResetGauge();
    }

    private void HandleGaugeEmpty()
    {
        EndEffects();
    }

    private void EndEffects()
    {
        if (playerTime != null)
            playerTime.ClearOverdriveMultiplier();

        // Restore every registered enemy, including those already dead.
        SetEnemyMultipliers(1f, applyOnlyToLiving: false);

        if (gauge != null)
            gauge.StopTimeDilation();
        if (timeDilationDistortion != null)
            timeDilationDistortion.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void SetEnemyMultipliers(float multiplier, bool applyOnlyToLiving)
    {
        if (actorRegistry == null) return;

        foreach (CharacterCore enemy in actorRegistry.Enemies)
        {
            if (enemy == null) continue;
            // Inactive preplaced actors are still part of this level. Keep
            // their modifier prepared if they are activated while Overdrive runs.
            Health enemyHealth = enemy.GetComponent<Health>();
            if (applyOnlyToLiving && (enemyHealth == null || enemyHealth.IsDead))
                continue;

            CharacterTimeModifier modifier = enemy.GetComponent<CharacterTimeModifier>();
            if (modifier == null)
            {
                if (applyOnlyToLiving)
                    Debug.LogWarning($"[OverdriveController] {enemy.name} is missing CharacterTimeModifier.", enemy);
                continue;
            }

            modifier.SetOverdriveMultiplier(multiplier);
        }
    }
}
