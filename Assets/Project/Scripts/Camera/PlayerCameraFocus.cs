using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

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

        if (playerCamera != null)
        {
            defaultLookAt = playerCamera.LookAt;
            orbitalFollow = playerCamera.GetComponent<CinemachineOrbitalFollow>();

            if (orbitalFollow != null)
                defaultRadialValue = orbitalFollow.RadialAxis.Value;
        }
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
        // If already locked on, unlock.
        if (currentTarget != null)
        {
            currentTarget = null;

            if (playerCamera != null)
                playerCamera.LookAt = defaultLookAt;

            if (orbitalFollow != null)
                orbitalFollow.RadialAxis.Value = defaultRadialValue;

            if (inputAxisController != null)
                inputAxisController.enabled = true;

            Debug.Log("Unlocked");
            return;
        }

        // Find the closest enemy.
        currentTarget = FindClosestEnemy();

        if (currentTarget != null)
        {
            // Look for the enemy's LockOnTarget child.
            Transform lockOnPoint = currentTarget.Find("LockOnTarget");

            if (lockOnPoint != null)
                currentTarget = lockOnPoint;

            // Switch the camera to use the lock-on LookAt target.
            if (playerCamera != null && lockOnCameraTarget != null)
                playerCamera.LookAt = lockOnCameraTarget;

            // Disable manual camera input while locked on.
            if (inputAxisController != null)
                inputAxisController.enabled = false;

            //Immediately snap the camera into a better combat framing angle.
            SnapLockOnCameraToTarget();

            Debug.Log("Locked onto: " + currentTarget.name);
        }
        else
        {
            Debug.Log("No enemy found");
        }
    }

    private Transform FindClosestEnemy()
    {
        Collider[] enemiesInRange = Physics.OverlapSphere(
            transform.position,
            lockOnRange,
            enemyLayer);

        Transform closestEnemy = null;
        float closestDistance = Mathf.Infinity;

        foreach (Collider enemy in enemiesInRange)
        {
            float distance = Vector3.Distance(
                transform.position,
                enemy.transform.position
            );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEnemy = enemy.transform;
            }
        }

        return closestEnemy;
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
