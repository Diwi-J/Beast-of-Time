using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple input bridge for preplaced prayer statues in the single gameplay scene.
/// The player presses the Player/Pray input action when near a statue.
/// Checkpoint ownership and world resets remain in PrayerCheckpoint and
/// WorldResetCoordinator. Does not modify the existing focus/lock-on system.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Health))]
public sealed class PlayerPrayerInteractor : MonoBehaviour
{
    [Header("Prayer interaction")]
    [SerializeField, Min(0.1f)] private float interactionRange = 2.5f;

    private Health health;
    private Controls controls;
    private InputAction prayAction;
    private PrayerCheckpoint[] checkpoints;

    private void Awake()
    {
        health = GetComponent<Health>();
        controls = new Controls();

        // Look up by name to avoid a compile error until the Pray action
        // has been added to the project's Input Actions asset.
        prayAction = controls.asset.FindAction("Player/Pray", false);
        if (prayAction == null)
            Debug.LogError("[Prayer] Add a Button action named 'Pray' to the Player action map and bind it to R.", this);
    }

    private void Start()
    {
        // All statues are preplaced in one scene; discover them once.
        checkpoints = FindObjectsByType<PrayerCheckpoint>(FindObjectsSortMode.None);
    }

    private void OnEnable()
    {
        if (prayAction == null) return;
        prayAction.performed += OnPrayPressed;
        prayAction.Enable();
    }

    private void OnDisable()
    {
        if (prayAction == null) return;
        prayAction.performed -= OnPrayPressed;
        prayAction.Disable();
    }

    private void OnDestroy()
    {
        controls?.Dispose();
    }

    private void OnPrayPressed(InputAction.CallbackContext context)
    {
        if (health == null || health.IsDead || checkpoints == null) return;

        PrayerCheckpoint closest = null;
        float nearestDistanceSquared = interactionRange * interactionRange;

        foreach (PrayerCheckpoint checkpoint in checkpoints)
        {
            if (checkpoint == null || !checkpoint.isActiveAndEnabled) continue;

            // The checkpoint GameObject should be positioned near the statue's base.
            float distanceSquared = (checkpoint.transform.position - transform.position).sqrMagnitude;
            if (distanceSquared > nearestDistanceSquared) continue;

            nearestDistanceSquared = distanceSquared;
            closest = checkpoint;
        }

        if (closest != null)
            closest.Pray();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
