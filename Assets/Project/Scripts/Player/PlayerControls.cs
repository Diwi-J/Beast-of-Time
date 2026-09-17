using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Tuning")]
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float runSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;   // how fast the character turns to face movement (Freelook only)
    [SerializeField] private float animatorDamping = 0.1f; // smooths Animator float changes so blends aren't jittery

    [Header("Gravity")]
    [SerializeField] private float gravity = -15f;
    [SerializeField] private float groundedGravity = -2f; // small constant downward force while grounded, keeps isGrounded reliable

    private Animator animator;
    private CharacterController controller;
    private Controls controls;
    private Vector2 moveInput;
    private bool isFocused;
    private float verticalVelocity;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveZHash = Animator.StringToHash("MoveZ");
    private static readonly int IsFocusedHash = Animator.StringToHash("IsFocused");

    private void Awake()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();
        controls = new Controls();

        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        // Focus toggles on press rather than being held, so it matches a lock-on style press
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
        Vector3 moveDirection = isFocused ? UpdateFocus() : UpdateFreelook();
        ApplyGravity();

        Vector3 motion = moveDirection + Vector3.up * verticalVelocity;
        controller.Move(motion * Time.deltaTime);
    }

    private Vector3 UpdateFreelook()
    {
        // In Freelook the character always faces its movement direction, so we only need magnitude for Speed.
        float inputMagnitude = moveInput.magnitude; // 0 = idle, up to 1 = full run
        animator.SetFloat(SpeedHash, inputMagnitude, animatorDamping, Time.deltaTime);

        if (inputMagnitude <= 0.1f)
        {
            return Vector3.zero;
        }

        // Raw world-space direction - no camera basis involved.
        Vector3 direction = new Vector3(moveInput.x, 0f, moveInput.y);

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

        float speedMultiplier = Mathf.Lerp(walkSpeed, runSpeed, inputMagnitude);
        return direction.normalized * speedMultiplier * inputMagnitude;
    }

    private Vector3 UpdateFocus()
    {
        // In Focus, movement direction is relative to the character's current facing (which will later
        // be driven by the lock-on system aiming at the target). Facing itself isn't rotated here yet -
        // that'll be added once lock-on exists and gives this a target to face.
        Vector3 worldDirection = new Vector3(moveInput.x, 0f, moveInput.y);

        Vector3 localMove = transform.InverseTransformDirection(worldDirection);
        animator.SetFloat(MoveXHash, localMove.x, animatorDamping, Time.deltaTime);
        animator.SetFloat(MoveZHash, localMove.z, animatorDamping, Time.deltaTime);

        float speedMultiplier = Mathf.Lerp(walkSpeed, runSpeed, moveInput.magnitude);
        return worldDirection.normalized * speedMultiplier * moveInput.magnitude;
    }

    private void ApplyGravity()
    {
        if (controller.isGrounded)
        {
            // A small constant downward force rather than 0, so isGrounded stays reliably true on slopes/steps.
            verticalVelocity = groundedGravity;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
    }
}
