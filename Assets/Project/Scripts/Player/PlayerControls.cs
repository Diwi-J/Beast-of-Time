using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;

/// <summary>
/// Root-motion player controller.
/// Input only drives Animator parameters and scripted rotation.
/// Horizontal movement comes from the Animator (animator.deltaPosition).
/// Vertical movement (gravity) is scripted.
/// Everything is applied through a single CharacterController.Move in OnAnimatorMove.
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CharacterController))]
public class PlayerControls : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private CinemachineOrbitalFollow orbitalFollow;

    [Header("Rotation")]
    [SerializeField] private float freelookRotationSpeed = 10f;
    [SerializeField] private float focusRotationSpeed = 12f;

    [Header("Freelook 180 Turn")]
    [SerializeField] private float turnBackDotThreshold = -0.5f; // input opposes facing beyond this, trigger 180
    [SerializeField] private float turnTimeout = 1.5f;           // failsafe if OnTurnComplete never fires

    [Header("Animator Damping")]
    [SerializeField] private float speedDamping = 0.1f;
    [SerializeField] private float focusMoveDamping = 0.1f;

    [Header("Blend Tree Ranges")]
    [SerializeField] private float freelookRunThreshold = 2.93f;
    [SerializeField] private float focusWalkMagnitude = 1f;
    [SerializeField] private float focusRunMagnitude = 3f;

    [Header("Root Motion")]
    [Tooltip("Scales horizontal root motion. 1 = exactly what the clip does. Tuning knob only.")]
    [SerializeField] private float rootMotionMultiplier = 1f;

    [Header("Locked States")]
    [Tooltip("Animator state tags that block steering and rotation. Match these to PlayerCombat.IsFree().")]
    [SerializeField] private string[] lockedTags = { "Attack", "Block" };
    [Tooltip("If true, the clip's own root motion (lunges, steps) still moves the player while locked.")]
    [SerializeField] private bool allowRootMotionWhileLocked = true;

    [Header("Gravity")]
    [SerializeField] private float gravity = -15f;
    [SerializeField] private float groundedGravity = -2f;

    private Animator animator;
    private CharacterController controller;
    private Controls controls;

    private Vector2 moveInput;
    private bool isFocused;
    private bool isLocked;
    private bool isTurning;
    private float turnTimer;
    private float verticalVelocity;

    // Set via SetLockOnTarget / ClearLockOnTarget, or directly by other scripts.
    public Transform lockOnTarget;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveZHash = Animator.StringToHash("MoveZ");
    private static readonly int IsFocusedHash = Animator.StringToHash("IsFocused");
    private static readonly int TurnHash = Animator.StringToHash("180");

    private void Awake()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();
        controls = new Controls();

        // Root motion stops if the Animator is culled while off-screen.
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += ctx => moveInput = Vector2.zero;
    }

    private void OnEnable() => controls.Enable();
    private void OnDisable() => controls.Disable();

    public void SetLockOnTarget(Transform target)
    {
        // Store the selected enemy.
        lockOnTarget = target;

        // Update whether the player is focused.
        isFocused = target != null;

        // Update the Animator's focus parameter.
        animator.SetBool(IsFocusedHash, isFocused);

        // Reset strafe movement when leaving focus.
        if (!isFocused)
        {
            animator.SetFloat(MoveXHash, 0f);
            animator.SetFloat(MoveZHash, 0f);
        }
    }

    public void ClearLockOnTarget()
    {
        // Clear the target and exit focus mode.
        SetLockOnTarget(null);
    }

    // ---------------------------------------------------------------- Update

    private void Update()
    {
        isLocked = IsInLockedState();

        if (isTurning)
        {
            turnTimer += Time.deltaTime;
            if (turnTimer > turnTimeout)
                EndTurn();
        }

        if (isFocused)
            UpdateFocus();
        else
            UpdateFreelook();

        if (controller.isGrounded)
            verticalVelocity = groundedGravity;
        else
            verticalVelocity += gravity * Time.deltaTime;
    }

    private void UpdateFreelook()
    {
        float inputMagnitude = moveInput.magnitude;

        animator.SetFloat(SpeedHash, inputMagnitude * freelookRunThreshold, speedDamping, Time.deltaTime);

        if (isLocked || isTurning || inputMagnitude <= 0.1f)
            return;

        //Get the camera's horizontal orbit angle.
        float cameraYaw = orbitalFollow.HorizontalAxis.Value;

        //Convert the horizontal angle into a rotation.
        Quaternion yawRotation = Quaternion.Euler(0f, cameraYaw, 0f);

        //Calculate movement directions using the orbit angle.
        Vector3 camForward = yawRotation * Vector3.forward;
        Vector3 camRight = yawRotation * Vector3.right;

        Vector3 desiredDirection = (camForward * moveInput.y + camRight * moveInput.x).normalized;

        float facingDot = Vector3.Dot(transform.forward, desiredDirection);

        // Input wants a direction roughly opposite current facing: play the 180 clip.
        if (facingDot < turnBackDotThreshold)
        {
            animator.SetTrigger(TurnHash);
            isTurning = true;
            turnTimer = 0f;
            return;
        }

        // Face the input direction. Root motion then carries the character forward.
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(desiredDirection),
            freelookRotationSpeed * Time.deltaTime);
    }

    private void UpdateFocus()
    {
        if (lockOnTarget != null && !isLocked && !isTurning)
        {
            Vector3 toTarget = lockOnTarget.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(toTarget),
                    focusRotationSpeed * Time.deltaTime);
            }
        }

        float inputMagnitude = moveInput.magnitude;
        float blendMagnitude = Mathf.Lerp(focusWalkMagnitude, focusRunMagnitude, inputMagnitude);
        Vector2 scaledMove = moveInput.normalized * blendMagnitude * inputMagnitude;

        // The strafe clips carry their own direction, so these two values are all movement needs.
        animator.SetFloat(MoveXHash, inputMagnitude > 0.01f ? scaledMove.x : 0f, focusMoveDamping, Time.deltaTime);
        animator.SetFloat(MoveZHash, inputMagnitude > 0.01f ? scaledMove.y : 0f, focusMoveDamping, Time.deltaTime);
    }

    // ----------------------------------------------------------- Root motion

    // Runs after Update, once the Animator has evaluated this frame.
    // Defining this method makes Unity stop applying root motion automatically.
    private void OnAnimatorMove()
    {
        // The 180 clip rotates the root. Scripted rotation is paused while it plays.
        if (isTurning)
            transform.rotation = animator.deltaRotation * transform.rotation;

        Vector3 delta = animator.deltaPosition;

        if (isLocked && !allowRootMotionWhileLocked)
            delta = Vector3.zero;

        delta *= rootMotionMultiplier;
        delta.y = verticalVelocity * Time.deltaTime; // Y is always scripted

        controller.Move(delta);
    }

    // ----------------------------------------------------------------- Turn

    // Call this via an Animation Event at the end of the "180" clip.
    public void OnTurnComplete()
    {
        EndTurn();
    }

    private void EndTurn()
    {
        isTurning = false;
        turnTimer = 0f;
    }

    // --------------------------------------------------------- State gating

    private bool IsInLockedState()
    {
        for (int i = 0; i < animator.layerCount; i++)
        {
            if (HasLockedTag(animator.GetCurrentAnimatorStateInfo(i)))
                return true;

            if (animator.IsInTransition(i) && HasLockedTag(animator.GetNextAnimatorStateInfo(i)))
                return true;
        }
        return false;
    }

    private bool HasLockedTag(AnimatorStateInfo info)
    {
        for (int i = 0; i < lockedTags.Length; i++)
        {
            if (info.IsTag(lockedTags[i]))
                return true;
        }
        return false;
    }
}
