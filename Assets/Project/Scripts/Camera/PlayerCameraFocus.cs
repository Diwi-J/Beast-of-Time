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

    [Header("Camera")]
    [SerializeField] private CinemachineCamera playerCamera;
    [SerializeField] private CinemachineInputAxisController inputAxisController;
    [SerializeField] private Transform lockOnCameraTarget;

    private Transform defaultLookAt;
    private Transform currentTarget;

    private CameraControls cameraControls;
    private CinemachineOrbitalFollow orbitalFollow;

    private float cameraDistanceVelocity;
    private float defaultRadialValue;

    private void Awake()
    {
        cameraControls = new CameraControls();

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
        cameraControls.Camera.Enable();
        cameraControls.Camera.Focus.performed += OnFocusPerformed;
    }

    private void OnDisable()
    {
        cameraControls.Camera.Focus.performed += OnFocusPerformed;
        cameraControls.Camera.Disable();
    }

    private void OnFocusPerformed(InputAction.CallbackContext context)
    {
        ToggleFocus();
    }

    private void ToggleFocus()
    {
        // If already locked on, unlock
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

        //Find an enemy
        currentTarget = FindClosestEnemy();

        if (currentTarget != null)
        {
            //Look for the enemy's LockOnTarget child
            Transform lockOnPoint = currentTarget.Find("LockOnTarget");

            if (lockOnPoint != null)
                currentTarget = lockOnPoint;

            if (playerCamera != null && lockOnCameraTarget != null)
                playerCamera.LookAt = lockOnCameraTarget;

            if (inputAxisController != null)
                inputAxisController.enabled = false;

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

        Vector3 directionToTarget = currentTarget.position - transform.position;

        // We only care about horizontal direction.
        directionToTarget.y = 0f;

        if (directionToTarget.sqrMagnitude < 0.001f)
            return;


        // Convert the direction to the enemy into an angle.
        float targetAngle = Mathf.Atan2(
            directionToTarget.x,
            directionToTarget.z)
            * Mathf.Rad2Deg;

        // Current horizontal position of the orbital camera.
        float currentAngle = orbitalFollow.HorizontalAxis.Value;

        // Gradually rotate around the player toward the desired angle.
        float newAngle = Mathf.MoveTowardsAngle(
            currentAngle,
            targetAngle,
            lockOnRotationSpeed * Time.deltaTime
        );

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

        lockOnCameraTarget.position =
            Vector3.Lerp(playerPoint, enemyPoint, 0.1f);
    }

    private void UpdateLockOnCameraDistance()
    {
        if (currentTarget == null || orbitalFollow == null)
            return;

        Vector3 playerPosition = transform.position;
        Vector3 enemyPosition = currentTarget.position;

        // Ignore height difference.
        playerPosition.y = 0f;
        enemyPosition.y = 0f;

        float targetDistance =
            Vector3.Distance(playerPosition, enemyPosition);

        // Convert player-enemy distance into a 0-1 value.
        float distancePercent = Mathf.InverseLerp(
            minTargetDistance,
            maxTargetDistance,
            targetDistance
        );

        // Use that value to choose how far the camera should be.
        float desiredCameraDistance = Mathf.Lerp(
            minCameraDistance,
            maxCameraDistance,
            distancePercent
        );

        // Smoothly move toward the new distance.
        float newDistance = Mathf.SmoothDamp(
            orbitalFollow.RadialAxis.Value,
            desiredCameraDistance,
            ref cameraDistanceVelocity,
            cameraDistanceSmoothTime
        );

        orbitalFollow.RadialAxis.Value = newDistance;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, lockOnRange);
    }

}
