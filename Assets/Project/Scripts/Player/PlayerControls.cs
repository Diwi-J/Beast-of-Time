using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerControls : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform; 

    [Header("Tuning")]
    [SerializeField] private float rotationSpeed = 10f; 
    [SerializeField] private float animatorDamping = 0.1f;

    private Animator animator;
    private Controls controls;
    private Vector2 moveInput;
    private bool isFocused;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveZHash = Animator.StringToHash("MoveZ");
    private static readonly int IsFocusedHash = Animator.StringToHash("IsFocused");

    private void Awake()
    {
        animator = GetComponent<Animator>();
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
        if (!isFocused)
        {
            UpdateFreelook();
        }
        else
        {
            UpdateFocus();
        }
    }

    private void UpdateFreelook()
    {
        float targetSpeed = moveInput.magnitude;

        animator.SetFloat(SpeedHash, targetSpeed, animatorDamping, Time.deltaTime);

        if (targetSpeed > 0.1f)
        {
            Vector3 camForward = FlattenAndNormalize(cameraTransform.forward);
            Vector3 camRight = FlattenAndNormalize(cameraTransform.right);
            Vector3 moveDirection = camForward * moveInput.y + camRight * moveInput.x;

            if (moveDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }

    private void UpdateFocus()
    {
        Vector3 localMove = transform.InverseTransformDirection(
            cameraTransform.forward * moveInput.y + cameraTransform.right * moveInput.x
        );

        animator.SetFloat(MoveXHash, localMove.x, animatorDamping, Time.deltaTime);
        animator.SetFloat(MoveZHash, localMove.z, animatorDamping, Time.deltaTime);
    }

    private static Vector3 FlattenAndNormalize(Vector3 v)
    {
        v.y = 0f;
        return v.normalized;
    }
}
