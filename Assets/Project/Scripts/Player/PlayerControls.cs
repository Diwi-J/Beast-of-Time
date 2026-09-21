using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CharacterController))]
public class PlayerControls : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Freelook Speeds")]
    [SerializeField] private float freelookWalkSpeed = 2f;
    [SerializeField] private float freelookRunSpeed = 5f;
    [SerializeField] private float freelookRotationSpeed = 10f;
    [SerializeField] private float speedDamping = 0.1f;

    [Header("Freelook 180 Turn")]
    [SerializeField] private float turnBackDotThreshold = -0.5f; // input opposes facing beyond this, trigger 180
    [SerializeField] private PlayerCameraFocus cameraFocus;

    [Header("Focus Speeds")]
    [SerializeField] private float focusWalkSpeed = 2f;
    [SerializeField] private float focusRunSpeed = 4f;
    [SerializeField] private float focusRotationSpeed = 12f;
    [SerializeField] private float focusMoveDamping = 0.1f;

    [Header("Blend Tree Ranges")]
    [SerializeField] private float freelookWalkThreshold = 1.33f;
    [SerializeField] private float freelookRunThreshold = 2.93f;
    [SerializeField] private float focusWalkMagnitude = 1f;
    [SerializeField] private float focusRunMagnitude = 3f;

    [Header("Gravity")]
    [SerializeField] private float gravity = -15f;
    [SerializeField] private float groundedGravity = -2f;

    private Animator animator;
    private CharacterController controller;
    private Controls controls;

    private Vector2 moveInput;
    private bool isFocused;
    private float verticalVelocity;
    private bool isTurning;

    // externally settable by PlayerCameraFocus, or pulled directly if you prefer wiring it there
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

        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        controls.Player.Focus.performed += ctx =>
        {
            isFocused = !isFocused;
            animator.SetBool(IsFocusedHash, isFocused);
        };
    }

    private void OnEnable() => controls.Enable();
    private void OnDisable() => controls.Disable();

    private void Update()
    {
        Vector3 motion = isFocused ? UpdateFocus() : UpdateFreelook();

        if (controller.isGrounded)
            verticalVelocity = groundedGravity;
        else
            verticalVelocity += gravity * Time.deltaTime;

        motion.y = verticalVelocity;
        controller.Move(motion * Time.deltaTime);
    }

    private Vector3 UpdateFreelook()
    {
        float inputMagnitude = moveInput.magnitude;

        float targetSpeedParam = Mathf.Lerp(0f, freelookRunThreshold, inputMagnitude);
        animator.SetFloat(SpeedHash, targetSpeedParam, speedDamping, Time.deltaTime);

        if (inputMagnitude <= 0.1f || isTurning)
            return Vector3.zero;

        Vector3 camForward = Vector3.Scale(cameraTransform.forward, new Vector3(1, 0, 1)).normalized;
        Vector3 camRight = Vector3.Scale(cameraTransform.right, new Vector3(1, 0, 1)).normalized;
        Vector3 desiredDirection = (camForward * moveInput.y + camRight * moveInput.x).normalized;

        float facingDot = Vector3.Dot(transform.forward, desiredDirection);

        // Input wants a direction roughly opposite current facing -> trigger 180, don't move this frame
        if (facingDot < turnBackDotThreshold)
        {
            animator.SetTrigger(TurnHash);
            isTurning = true;
            return Vector3.zero;
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(desiredDirection), freelookRotationSpeed * Time.deltaTime);

        float speed = Mathf.Lerp(freelookWalkSpeed, freelookRunSpeed, inputMagnitude);
        return transform.forward * speed * inputMagnitude;
    }

    // Call this via an Animation Event at the end of the "180" clip
    public void OnTurnComplete()
    {
        isTurning = false;
        cameraFocus?.SnapCameraBehind();
    }

    private Vector3 UpdateFocus()
    {
        if (lockOnTarget != null)
        {
            Vector3 toTarget = lockOnTarget.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toTarget), focusRotationSpeed * Time.deltaTime);
        }

        float inputMagnitude = moveInput.magnitude;
        float blendMagnitude = Mathf.Lerp(focusWalkMagnitude, focusRunMagnitude, inputMagnitude);
        Vector2 scaledMove = moveInput.normalized * blendMagnitude * inputMagnitude;

        animator.SetFloat(MoveXHash, inputMagnitude > 0.01f ? scaledMove.x : 0f, focusMoveDamping, Time.deltaTime);
        animator.SetFloat(MoveZHash, inputMagnitude > 0.01f ? scaledMove.y : 0f, focusMoveDamping, Time.deltaTime);

        if (inputMagnitude <= 0.1f)
            return Vector3.zero;

        Vector3 localDirection = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
        float speed = Mathf.Lerp(focusWalkSpeed, focusRunSpeed, inputMagnitude);
        return localDirection * speed * inputMagnitude;
    }
}