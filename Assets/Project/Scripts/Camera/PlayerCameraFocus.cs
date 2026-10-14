using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using System.Collections.Generic;

public class PlayerCameraFocus : MonoBehaviour
{
    [Header("Lock-On Settings")]
    [SerializeField] private float lockOnRange = 10f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float lockOnRotationSpeed = 300f;

    [Header("Dynamic Lock-On Distance")]
    [SerializeField] private float minTargetDistance = 2f;
    [SerializeField] private float maxTargetDistance = 10f;

    [SerializeField] private float minCameraDistance = 4f;
    [SerializeField] private float maxCameraDistance = 8f;

    [SerializeField] private float cameraDistanceSmoothTime = 0.2f;

    [Header("Lock-On Framing")]
    [Range(0f, 1f)]
    [SerializeField] private float lockOnLookAtWeight = 0.5f;

    [Header("Lock-On Camera Position")]
    [Range(-60f, 60f)]
    [SerializeField] private float lockOnSideAngle = 40f;

    [Header("Focus Switching")]
    [SerializeField] private PlayerControls playerControls;

    [SerializeField] private float switchPressThreshold = 0.65f;
    [SerializeField] private float switchReleaseThreshold = 0.3f;

    private bool isFocused;
    private bool switchReady = true;

    [Header("Camera")]
    [SerializeField] private CinemachineCamera playerCamera;
    [SerializeField] private CinemachineInputAxisController inputAxisController;
    [SerializeField] private Transform lockOnCameraTarget;

    private Transform defaultLookAt;
    private Transform currentTarget;

    private Controls controls;
    private CinemachineOrbitalFollow orbitalFollow;

    private float cameraDistanceVelocity;
    private float defaultRadialValue;

    private void Awake()
    {
        controls = new Controls();

        if (playerControls == null)
            playerControls = GetComponentInParent<PlayerControls>();

        if (playerCamera != null)
        {
            defaultLookAt = playerCamera.LookAt;
            orbitalFollow = playerCamera.GetComponent<CinemachineOrbitalFollow>();

            if (orbitalFollow != null)
                defaultRadialValue = orbitalFollow.RadialAxis.Value;
        }
    }

    private void Update()
    {
        // Focus switching only works while locked on.
        if (!isFocused)
        {
            switchReady = true;
            return;
        }

        // Unlock if the selected target is destroyed.
        if (currentTarget == null)
        {
            UnlockFocus();
            return;
        }

        // NEW: Read horizontal switching input.
        float switchInput =
            controls.Player.SwitchTarget.ReadValue<float>();

        // Allow another switch once the input is released.
        if (Mathf.Abs(switchInput) <= switchReleaseThreshold)
        {
            switchReady = true;
            return;
        }

        // Ignore weak input and repeated input while held.
        if (!switchReady ||
            Mathf.Abs(switchInput) < switchPressThreshold)
            return;

        switchReady = false;

        // Positive = right, negative = left.
        int direction = switchInput > 0f ? 1 : -1;

        SwitchTarget(direction);
    }

    private void LateUpdate()
    {
        if (currentTarget != null)
        {
            UpdateLockOnCamera();
            UpdateLockOnCameraTarget();
            UpdateLockOnCameraDistance();
        }
    }

    private void OnEnable()
    {
        controls.Player.Enable();
        controls.Player.Focus.performed += OnFocusPerformed;
    }

    private void OnDisable()
    {
        controls.Player.Focus.performed -= OnFocusPerformed;
        controls.Player.Disable();
    }

    private void OnFocusPerformed(InputAction.CallbackContext context)
    {
        ToggleFocus();
    }

    private void ToggleFocus()
    {
        // If already focused, unlock.
        if (isFocused)
        {
            UnlockFocus();
            return;
        }

        // Find the nearest available enemy.
        Transform target = FindClosestEnemy();

        if (target == null)
        {
            Debug.Log("No enemy found");
            return;
        }

        if (playerCamera == null ||
            orbitalFollow == null ||
            lockOnCameraTarget == null)
        {
            Debug.LogWarning("Lock-on camera references are missing.");
            return;
        }

        // Start lock-on.
        currentTarget = target;
        isFocused = true;
        switchReady = true;

        // Position the LookAt point before changing targets.
        UpdateLockOnCameraTarget();

        playerCamera.LookAt = lockOnCameraTarget;

        // Disable manual camera rotation.
        if (inputAxisController != null)
            inputAxisController.enabled = false;

        // Keep the initial framing behaviour we implemented earlier.
        SnapLockOnCameraToTarget();

        // NEW: Tell the movement controller which enemy is selected.
        if (playerControls != null)
            playerControls.SetLockOnTarget(currentTarget);

        Debug.Log("Locked onto: " + currentTarget.name);
    }

    private void UnlockFocus()
    {
        isFocused = false;
        currentTarget = null;
        switchReady = true;

        // Restore normal camera LookAt.
        if (playerCamera != null)
            playerCamera.LookAt = defaultLookAt;

        // Restore the original radial value.
        if (orbitalFollow != null)
            orbitalFollow.RadialAxis.Value = defaultRadialValue;

        // Restore manual camera input.
        if (inputAxisController != null)
            inputAxisController.enabled = true;

        // NEW: Tell the movement controller focus has ended.
        if (playerControls != null)
            playerControls.ClearLockOnTarget();

        Debug.Log("Unlocked");
    }

    private Transform FindClosestEnemy()
    {
        List<Transform> targets = GetAvailableTargets();

        Transform closestEnemy = null;
        float closestDistance = Mathf.Infinity;

        foreach (Transform target in targets)
        {
            float distance = Vector3.Distance(
                transform.position,
                target.position
            );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEnemy = target;
            }
        }

        return closestEnemy;
    }

    private List<Transform> GetAvailableTargets()
    {
        // Find all enemy colliders within lock-on range.
        Collider[] enemiesInRange = Physics.OverlapSphere(
            transform.position,
            lockOnRange,
            enemyLayer
        );

        List<Transform> targets = new List<Transform>();

        foreach (Collider enemy in enemiesInRange)
        {
            // Find the correct target point.
            Transform targetPoint = ResolveLockOnTarget(enemy);

            if (targetPoint == null)
                continue;

            // Avoid duplicate targets from multiple colliders.
            if (!targets.Contains(targetPoint))
                targets.Add(targetPoint);
        }

        return targets;
    }

    private Transform ResolveLockOnTarget(Collider enemyCollider)
    {
        Transform candidate = enemyCollider.transform;

        // Search upward through the enemy hierarchy.
        while (candidate != null)
        {
            // The collider may already be on the target point.
            if (candidate.name == "LockOnTarget")
                return candidate;

            // Search for a direct child named LockOnTarget.
            Transform targetPoint = candidate.Find("LockOnTarget");

            if (targetPoint != null)
                return targetPoint;

            candidate = candidate.parent;
        }

        // Fallback if no dedicated target point exists.
        return enemyCollider.transform;
    }

    private Transform FindNextTarget(int direction)
    {
        if (currentTarget == null)
            return null;

        // Use the actual Main Camera for screen positioning.
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogWarning("Main Camera not found.");
            return null;
        }

        List<Transform> targets = GetAvailableTargets();

        // Find the selected enemy's screen position.
        Vector3 currentScreenPosition =
            mainCamera.WorldToViewportPoint(currentTarget.position);

        Transform bestTarget = null;
        float bestScore = Mathf.Infinity;

        foreach (Transform candidate in targets)
        {
            if (candidate == null || candidate == currentTarget)
                continue;

            // Convert enemy position to screen coordinates.
            Vector3 candidateScreenPosition =
                mainCamera.WorldToViewportPoint(candidate.position);

            // Ignore enemies behind the camera.
            if (candidateScreenPosition.z <= 0f)
                continue;

            // Only consider enemies currently within the view.
            if (candidateScreenPosition.x < 0f ||
                candidateScreenPosition.x > 1f ||
                candidateScreenPosition.y < 0f ||
                candidateScreenPosition.y > 1f)
                continue;

            // Compare horizontal screen positions.
            float horizontalDifference =
                candidateScreenPosition.x - currentScreenPosition.x;

            // Q: candidate must be left of current target.
            // E: candidate must be right of current target.
            if (horizontalDifference * direction <= 0.03f)
                continue;

            // Prioritise horizontally nearby enemies.
            // A small vertical penalty helps resolve ties.
            float score =
                Mathf.Abs(horizontalDifference) +
                Mathf.Abs(
                    candidateScreenPosition.y - currentScreenPosition.y
                ) * 0.1f;

            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = candidate;
            }
        }

        return bestTarget;
    }

    private void SwitchTarget(int direction)
    {
        if (!isFocused || currentTarget == null)
            return;

        Transform nextTarget = FindNextTarget(direction);

        if (nextTarget == null)
        {
            Debug.Log("No enemy available in that direction.");
            return;
        }

        // Change the selected enemy.
        currentTarget = nextTarget;

        // Update the camera's LookAt position.
        UpdateLockOnCameraTarget();

        // Tell the player movement system about the new target.
        if (playerControls != null)
            playerControls.SetLockOnTarget(currentTarget);

        Debug.Log("Switched focus to: " + currentTarget.name);
    }

    private void UpdateLockOnCamera()
    {
        if (currentTarget == null || orbitalFollow == null)
            return;

        // Get the direction from the player to the enemy.
        Vector3 directionToTarget =
            currentTarget.position - transform.position;

        // Ignore vertical differences.
        directionToTarget.y = 0f;

        if (directionToTarget.sqrMagnitude < 0.001f)
            return;

        // Calculate the horizontal angle toward the enemy.
        float targetAngle = Mathf.Atan2(
            directionToTarget.x,
            directionToTarget.z
        ) * Mathf.Rad2Deg;

        // Offset the camera to one side of the player.
        float desiredCameraAngle = targetAngle + lockOnSideAngle;

        // Read the current horizontal orbit angle.
        float currentAngle = orbitalFollow.HorizontalAxis.Value;

        // Smoothly rotate toward the offset angle.
        float newAngle = Mathf.MoveTowardsAngle(
            currentAngle,
            desiredCameraAngle,
            lockOnRotationSpeed * Time.deltaTime
        );

        // Apply the angle.
        orbitalFollow.HorizontalAxis.Value = newAngle;
    }

    private void UpdateLockOnCameraTarget()
    {
        if (currentTarget == null || lockOnCameraTarget == null)
            return;

        Vector3 playerPoint;

        if (defaultLookAt != null)
        {
            playerPoint = defaultLookAt.position;
        }
        else
        {
            playerPoint = transform.position;
        }

        Vector3 enemyPoint = currentTarget.position;

        // CHANGED: Calculate an adjustable point between
        // the player and enemy.
        Vector3 desiredLookAtPosition = Vector3.Lerp(
            playerPoint,
            enemyPoint,
            lockOnLookAtWeight
        );

        // Update the camera's LookAt target.
        lockOnCameraTarget.position = desiredLookAtPosition;
    }

    private void UpdateLockOnCameraDistance()
    {
        if (currentTarget == null || orbitalFollow == null)
            return;

        Vector3 playerPosition = transform.position;
        Vector3 enemyPosition = currentTarget.position;

        playerPosition.y = 0f;
        enemyPosition.y = 0f;

        float targetDistance = Vector3.Distance(
            playerPosition,
            enemyPosition
        );

        float distancePercent = Mathf.InverseLerp(
            minTargetDistance,
            maxTargetDistance,
            targetDistance
        );

        float desiredCameraDistance = Mathf.Lerp(
            minCameraDistance,
            maxCameraDistance,
            distancePercent
        );

        // Convert world distance to Cinemachine radial multiplier.
        float desiredRadialValue =
            desiredCameraDistance /
            Mathf.Max(0.01f, orbitalFollow.Radius);

        // CHANGED: Correct SmoothDamp argument usage.
        float newRadialValue = Mathf.SmoothDamp(
            orbitalFollow.RadialAxis.Value,
            desiredRadialValue,
            ref cameraDistanceVelocity,
            cameraDistanceSmoothTime
        );

        orbitalFollow.RadialAxis.Value = newRadialValue;
    }

    private void SnapLockOnCameraToTarget()
    {
        if (currentTarget == null || orbitalFollow == null)
            return;

        // Get direction from player to enemy.
        Vector3 directionToTarget =
            currentTarget.position - transform.position;

        directionToTarget.y = 0f;

        if (directionToTarget.sqrMagnitude < 0.001f)
            return;

        // Calculate the enemy's horizontal direction.
        float targetAngle = Mathf.Atan2(
            directionToTarget.x,
            directionToTarget.z
        ) * Mathf.Rad2Deg;

        // Include the side-angle offset.
        float desiredCameraAngle =
            targetAngle + lockOnSideAngle;

        // Immediately set the desired orbital angle.
        orbitalFollow.HorizontalAxis.Value = desiredCameraAngle;

        // Update the LookAt target.
        UpdateLockOnCameraTarget();

        // Update the camera distance.
        UpdateLockOnCameraDistance();
    }

    public void SnapCameraBehind()
    {
        if (orbitalFollow != null)
            orbitalFollow.HorizontalAxis.Value += 180f;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, lockOnRange);
    }

}
