using UnityEngine;

/// <summary>
/// Balance settings for the player ability. This asset contains definitions,
/// NEVER the mutable gauge amount or whether an activation is running.
/// </summary>
[CreateAssetMenu(fileName = "OverdriveConfig", menuName = "Soulslike/Combat/Overdrive Config")]
public sealed class OverdriveConfig : ScriptableObject
{
    [Header("Gauge (units)")]
    [SerializeField, Min(0.01f)] private float maximumCharge = 1f;
    [SerializeField, Min(0f)] private float chargePerParry = 0.34f;
    [SerializeField, Min(0.0001f)] private float drainPerRealSecond = 0.02f;
    [Tooltip("0 means any positive charge can be activated.")]
    [SerializeField, Min(0f)] private float minimumActivationCharge = 0f;

    [Header("Actor speed while active")]
    [Tooltip("1 = normal speed. Values below 1 slow enemies.")]
    [SerializeField, Range(0.01f, 1f)] private float enemySpeedMultiplier = 0.25f;
    [Tooltip("1 = normal speed. Values above 1 speed up the player.")]
    [SerializeField, Min(1f)] private float playerSpeedMultiplier = 1.25f;

    public float MaximumCharge => Mathf.Max(0.01f, maximumCharge);
    public float ChargePerParry => Mathf.Max(0f, chargePerParry);
    public float DrainPerRealSecond => Mathf.Max(0.0001f, drainPerRealSecond);
    public float MinimumActivationCharge => Mathf.Clamp(minimumActivationCharge, 0f, MaximumCharge);
    public float EnemySpeedMultiplier => Mathf.Clamp(enemySpeedMultiplier, 0.01f, 1f);
    public float PlayerSpeedMultiplier => Mathf.Max(1f, playerSpeedMultiplier);

    private void OnValidate()
    {
        maximumCharge = Mathf.Max(0.01f, maximumCharge);
        chargePerParry = Mathf.Max(0f, chargePerParry);
        drainPerRealSecond = Mathf.Max(0.0001f, drainPerRealSecond);
        minimumActivationCharge = Mathf.Clamp(minimumActivationCharge, 0f, maximumCharge);
        enemySpeedMultiplier = Mathf.Clamp(enemySpeedMultiplier, 0.01f, 1f);
        playerSpeedMultiplier = Mathf.Max(1f, playerSpeedMultiplier);
    }
}
