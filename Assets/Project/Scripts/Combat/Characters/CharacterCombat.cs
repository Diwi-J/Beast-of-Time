using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phase 05: owns the character's combat colliders and existing parry window.
/// Attack damage / per-target hit tracking are delegated to the attack runtime
/// and CombatResolver. Overdrive remains in OverdriveController.
///
/// Existing Enablehitbox/Disablehitbox and block animation events are preserved.
/// Optional BeginAttack events select attack-specific SO data.
/// </summary>
public class CharacterCombat : MonoBehaviour
{
    private readonly Dictionary<string, CombatCollider> hitboxes = new Dictionary<string, CombatCollider>();
    private readonly Dictionary<string, CombatCollider> hurtboxes = new Dictionary<string, CombatCollider>();
    private CombatCollider blockbox;

    private Animator animator;
    private OverdriveController overdriveController;
    private CharacterTimeModifier timeModifier;
    private CombatAttackRuntime attackRuntime;
    private float blockWindowSpeedAtStart = 1f;
    private bool isDead;
    private bool dodgeInvulnerable;
    private float blockStartTime = float.NegativeInfinity;

    [Header("Parry timing (config SO migration later)")]
    public float parryStartUpDelay = 0.08f;
    public float parryWindowDuration = 0.2f;

    [Header("Block")]
    public float blockDamageMultiplier = 0.1f;

    [Header("Death")]
    [SerializeField] private string deathTrigger = "Killed";

    [Header("Existing feedback / inspector compatibility")]
    public Gauge gauge;
    public ParticleSystem ParryDistortion;

    // Compatibility event; NOT a living-enemy registry.
    public static event System.Action<CharacterCombat> CharacterDied;

    public Health Health { get; private set; }
    public bool IsBlocking { get; private set; }
    public bool IsDodgeInvulnerable => dodgeInvulnerable;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        Health = GetComponent<Health>();
        overdriveController = GetComponent<OverdriveController>();
        timeModifier = GetComponent<CharacterTimeModifier>();
        attackRuntime = GetComponent<CombatAttackRuntime>();

        if (gauge == null && overdriveController != null)
            gauge = overdriveController.Gauge;

        foreach (CombatCollider cc in GetComponentsInChildren<CombatCollider>(true))
        {
            cc.Owner = this; // Preserve CombatCollider.Owner for all existing prefabs.
            Collider collider = cc.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;

            switch (cc.type)
            {
                case ColliderType.Hitbox:
                    if (hitboxes.ContainsKey(cc.Id))
                        Debug.LogWarning($"[CharacterCombat] Duplicate hitbox ID '{cc.Id}' on {name}.", cc);
                    hitboxes[cc.Id] = cc;
                    break;
                case ColliderType.Hurtbox:
                    if (hurtboxes.ContainsKey(cc.Id))
                        Debug.LogWarning($"[CharacterCombat] Duplicate hurtbox ID '{cc.Id}' on {name}.", cc);
                    hurtboxes[cc.Id] = cc;
                    break;
                case ColliderType.Blockbox:
                    if (blockbox != null)
                        Debug.LogWarning($"[CharacterCombat] More than one blockbox on {name}.", cc);
                    blockbox = cc;
                    break;
            }
        }
        EnableAllHurtboxes();
    }

    private void OnEnable()
    {
        if (Health != null)
            Health.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        if (Health != null)
            Health.OnDeath -= HandleDeath;
    }

    private void HandleDeath()
    {
        if (isDead) return;
        isDead = true;
        IsBlocking = false;
        dodgeInvulnerable = false;

        // Clear the player's Overdrive BEFORE the death animation starts.
        if (overdriveController != null)
            overdriveController.CancelAndReset();

        DisableEveryHitbox();
        if (attackRuntime != null)
            attackRuntime.ResetAllAttackState();
        DisableAllHurtboxes();
        if (blockbox != null)
            blockbox.GetComponent<Collider>().enabled = false;

        if (animator != null)
            animator.SetTrigger(deathTrigger);
        CharacterDied?.Invoke(this);
    }

    /// <summary>
    /// OPTIONAL animation event: put just BEFORE Enablehitbox on each attack
    /// clip. The string must match a CombatAttackSet entry exactly.
    /// </summary>
    public void BeginAttack(string attackId)
    {
        if (isDead) return;
        DisableEveryHitbox();
        if (attackRuntime != null)
            attackRuntime.BeginAuthoredAttack(attackId);
    }

    /// <summary>
    /// Future combo controller / AI calls this when entering or leaving a
    /// combo. The SAME animation clip can then be used both ways.
    /// </summary>
    public void SetComboMode(bool enabled)
    {
        if (attackRuntime != null)
            attackRuntime.SetComboMode(enabled);
    }

    /// <summary>
    /// Existing animation-event signature preserved. The first enabled hitbox
    /// begins the hit window; additional enabled hitboxes share its hit list.
    /// </summary>
    public void Enablehitbox(string id)
    {
        if (isDead) return;
        if (!hitboxes.TryGetValue(id, out CombatCollider cc))
        {
            Debug.LogWarning($"[CharacterCombat] {name} has no hitbox ID '{id}'.", this);
            return;
        }

        if (attackRuntime != null && !AnyHitboxEnabled())
            attackRuntime.BeginLegacyWindowIfNeeded();

        cc.GetComponent<Collider>().enabled = true;
    }

    /// <summary>
    /// Existing animation-event signature preserved. Ends the current attack
    /// window only after every active weapon hitbox has been closed.
    /// </summary>
    public void Disablehitbox(string id)
    {
        if (hitboxes.TryGetValue(id, out CombatCollider cc))
            cc.GetComponent<Collider>().enabled = false;

        if (!AnyHitboxEnabled() && attackRuntime != null)
            attackRuntime.EndAttack();
    }

    /// <summary>Optional animation event for early recovery/interrupt cleanup.</summary>
    public void EndAttack()
    {
        DisableEveryHitbox();
        if (attackRuntime != null)
            attackRuntime.EndAttack();
    }

    public void EnableAllHurtboxes()
    {
        // All invulnerability/defense flags are checked here: a block
        // release must not accidentally restore hurtboxes during dodge.
        if (isDead || IsBlocking || dodgeInvulnerable) return;
        foreach (CombatCollider cc in hurtboxes.Values)
            cc.GetComponent<Collider>().enabled = true;
    }

    public void DisableAllHurtboxes()
    {
        foreach (CombatCollider cc in hurtboxes.Values)
            cc.GetComponent<Collider>().enabled = false;
    }

    /// <summary>
    /// Authoritative dodge i-frame switch, driven by events on dodge clips.
    /// Clearing it restores hurtboxes ONLY when alive and not blocking.
    /// </summary>
    public void SetDodgeInvulnerable(bool enabled)
    {
        if (isDead && enabled) return;
        dodgeInvulnerable = enabled;
        if (enabled) DisableAllHurtboxes();
        else EnableAllHurtboxes();
    }

    public void EnableBlockbox()
    {
        if (isDead) return;
        IsBlocking = true;
        blockStartTime = Time.time;
        blockWindowSpeedAtStart = timeModifier != null ? timeModifier.EffectiveMultiplier : 1f;
        DisableAllHurtboxes();
        if (blockbox != null)
            blockbox.GetComponent<Collider>().enabled = true;
    }

    public void DisableBlockbox()
    {
        IsBlocking = false;
        if (blockbox != null)
            blockbox.GetComponent<Collider>().enabled = false;
        EnableAllHurtboxes();
    }

    public bool IsInParryWindow()
    {
        if (!IsBlocking || isDead) return false;
        float elapsed = (Time.time - blockStartTime) * blockWindowSpeedAtStart;
        return elapsed >= parryStartUpDelay &&
               elapsed <= parryStartUpDelay + parryWindowDuration;
    }

    /// <summary>Used by Phase 03 CharacterLifecycle on rest / respawn.</summary>
    public void ResetCombatState()
    {
        isDead = false;
        IsBlocking = false;
        dodgeInvulnerable = false;
        blockStartTime = float.NegativeInfinity;
        blockWindowSpeedAtStart = 1f;

        if (overdriveController != null)
            overdriveController.CancelAndReset();

        DisableEveryHitbox();
        if (attackRuntime != null)
            attackRuntime.ResetAllAttackState();
        if (blockbox != null)
            blockbox.GetComponent<Collider>().enabled = false;
        EnableAllHurtboxes();
    }

    private bool AnyHitboxEnabled()
    {
        foreach (CombatCollider cc in hitboxes.Values)
            if (cc != null && cc.GetComponent<Collider>().enabled)
                return true;
        return false;
    }

    private void DisableEveryHitbox()
    {
        foreach (CombatCollider cc in hitboxes.Values)
            if (cc != null)
                cc.GetComponent<Collider>().enabled = false;
    }
}
