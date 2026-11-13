using UnityEngine;

/// <summary>
/// Reusable balance data only. Each EnemyPoise component owns its own current
/// poise. One asset can be shared by all three mobs; bosses may use different
/// assets. This asset never stores per-instance runtime state.
/// </summary>
[CreateAssetMenu(fileName = "EnemyPoiseConfig", menuName = "Soulslike/Combat/Enemy Poise Config")]
public sealed class EnemyPoiseConfig : ScriptableObject
{
    [Header("Threshold")]
    [Min(0.01f)] [SerializeField] private float maximumPoise = 4f;

    [Header("Poise gained by the enemy")]
    [Tooltip("Added to the ATTACKING enemy when the player parries its attack.")]
    [Min(0f)] [SerializeField] private float gainWhenParried = 1f;

    [Tooltip("Added to the DEFENDING enemy when a player heavy attack damages it.")]
    [Min(0f)] [SerializeField] private float gainFromHeavyHit = 1f;

    public float MaximumPoise => Mathf.Max(0.01f, maximumPoise);
    public float GainWhenParried => Mathf.Max(0f, gainWhenParried);
    public float GainFromHeavyHit => Mathf.Max(0f, gainFromHeavyHit);

    private void OnValidate()
    {
        maximumPoise = Mathf.Max(0.01f, maximumPoise);
        gainWhenParried = Mathf.Max(0f, gainWhenParried);
        gainFromHeavyHit = Mathf.Max(0f, gainFromHeavyHit);
    }
}
