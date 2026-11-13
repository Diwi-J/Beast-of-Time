using UnityEngine;

/// <summary>Authoring data only; live combat/AI state stays on the boss GameObject.</summary>
[CreateAssetMenu(fileName = "DamnedAIConfig", menuName = "Soulslike/Boss/Damned AI Config")]
public sealed class DamnedAIConfig : ScriptableObject
{
    [Header("Arena and navigation")]
    [Min(1f)] public float arenaLeashDistance = 30f;
    [Min(0.05f)] public float homeArrivalDistance = 0.65f;
    [Min(0.05f)] public float repathInterval = 0.2f;
    [Min(0.1f)] public float turnDegreesPerSecond = 300f;
    [Min(0.1f)] public float locomotionWalkSpeed = 1.8f;
    [Min(0.1f)] public float locomotionRunSpeed = 3.5f;
    [Min(0.1f)] public float runWhenFartherThan = 5f;
    [Min(0.01f)] public float speedDamping = 0.12f;

    [Header("Attack distances measured from boss to player")]
    [Min(0.2f)] public float closeRange = 2.4f;
    [Min(0.2f)] public float mediumRange = 4.8f;
    [Min(0.2f)] public float chokeRange = 6f;
    [Min(0.2f)] public float bloodShoutRange = 10f;
    [Min(0.2f)] public float runningAttackMinDistance = 7f;
    [Min(0.2f)] public float runningAttackFinishRange = 2.4f;
    [Range(0f, 180f)] public float attackFacingAngle = 50f;

    [Header("Health based attack pace")]
    [Min(0f)] public float fullHealthCooldown = 2.0f;
    [Min(0f)] public float zeroHealthCooldown = 0.25f;

    [Header("Attack selection chances (relative weights)")]
    [Min(0f)] public float basicMeleeWeight = 3f;
    [Min(0f)] public float meleeComboWeight = 3f;
    [Min(0f)] public float stompWeight = 1.5f;
    [Min(0f)] public float mediumMeleeWeight = 2f;
    [Min(0f)] public float chokeWeight = 1.5f;
    [Min(0f)] public float bloodShoutWeight = 1.5f;
    [Min(0f)] public float runningAttackWeight = 2f;

    [Header("Safety / authored sequence timing (character seconds)")]
    [Min(0.1f)] public float attackEntryTimeout = 1f;
    [Min(0.2f)] public float attackMaxDuration = 16f;
    [Min(0.2f)] public float runningAttackMaxDuration = 9f;

    public float CooldownAtHealth(float normalizedHealth)
    {
        return Mathf.Lerp(zeroHealthCooldown, fullHealthCooldown, Mathf.Clamp01(normalizedHealth));
    }

    private void OnValidate()
    {
        arenaLeashDistance = Mathf.Max(1f, arenaLeashDistance);
        mediumRange = Mathf.Max(closeRange, mediumRange);
        chokeRange = Mathf.Max(mediumRange, chokeRange);
        bloodShoutRange = Mathf.Max(chokeRange, bloodShoutRange);
        runningAttackFinishRange = Mathf.Max(0.2f, runningAttackFinishRange);
        runningAttackMinDistance = Mathf.Max(closeRange, runningAttackMinDistance);
        attackMaxDuration = Mathf.Max(attackEntryTimeout + 0.1f, attackMaxDuration);
        runningAttackMaxDuration = Mathf.Max(attackEntryTimeout + 0.1f, runningAttackMaxDuration);
    }
}
