using UnityEngine;

/// <summary>
/// Put this on each prayer statue. The existing/future interaction system
/// calls Pray() when the player elects to rest. No input binding is assumed:
/// F is already owned by the externally developed focus system.
/// </summary>
[DisallowMultipleComponent]
public sealed class PrayerCheckpoint : MonoBehaviour
{
    [SerializeField] private WorldResetCoordinator worldResetCoordinator;
    [Tooltip("An empty child Transform marking where the player will face/spawn.")]
    [SerializeField] private Transform respawnMarker;

    public Vector3 RespawnPosition => respawnMarker != null ? respawnMarker.position : transform.position;
    public Quaternion RespawnRotation => respawnMarker != null ? respawnMarker.rotation : transform.rotation;

    /// <summary>Call this after a valid statue interaction.</summary>
    public void Pray()
    {
        if (worldResetCoordinator == null)
        {
            Debug.LogError("[PrayerCheckpoint] Assign WorldResetCoordinator.", this);
            return;
        }

        if (!worldResetCoordinator.RestAt(this))
            Debug.LogWarning("[PrayerCheckpoint] Rest was refused (possibly dead player or missing references).", this);
    }

    [ContextMenu("DEBUG / Pray At This Checkpoint")]
    private void DebugPray()
    {
        if (Application.isPlaying) Pray();
        else Debug.LogWarning("Enter Play mode to test praying.", this);
    }
}
