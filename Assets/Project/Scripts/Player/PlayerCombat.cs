using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Input bridge ONLY. PlayerComboController chooses which of five authored
/// moves to request, PlayerActionCoordinator owns action/interruption gates.
/// The combat hit window and damage remain in CharacterCombat/CombatAttackRuntime.
/// External PlayerCameraFocus and OverdriveController remain untouched.
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerActionCoordinator))]
[RequireComponent(typeof(PlayerComboController))]
public class PlayerCombat : MonoBehaviour, ICharacterLifecycleParticipant
{
    [Header("Input")]
    [SerializeField] private string dodgeActionName = "Dodge";

    [Header("Existing audio")]
    [SerializeField] private PlayerSwordSound playerSwordSound;
    [Min(0.1f)][SerializeField] private float swingSoundWaitTimeout = 1.5f;

    private Controls controls;
    private PlayerActionCoordinator actions;
    private PlayerComboController combos;
    private CombatAttackRuntime attackRuntime;
    private InputAction dodgeAction;
    private Coroutine pendingSwingSound;

    private void Awake()
    {
        actions = GetComponent<PlayerActionCoordinator>();
        combos = GetComponent<PlayerComboController>();
        attackRuntime = GetComponent<CombatAttackRuntime>();
        controls = new Controls();

        controls.Player.LightAttack.performed += OnLightAttack;
        controls.Player.HeavyAttack.performed += OnHeavyAttack;
        controls.Player.Block.performed += OnBlockPressed;
        controls.Player.Block.canceled += OnBlockReleased;

        dodgeAction = controls.asset.FindAction("Player/" + dodgeActionName, false);
        if (dodgeAction != null)
            dodgeAction.performed += OnDodge;
        else
            Debug.LogWarning($"[PlayerCombat] No 'Player/{dodgeActionName}' input action found; configure the Dodge action.", this);
    }

    private void OnEnable()
    {
        if (combos != null) combos.AttackRequested += OnAttackRequested;
        if (controls != null) controls.Enable();
    }

    private void OnDisable()
    {
        StopSwingSoundWait();
        if (combos != null) combos.AttackRequested -= OnAttackRequested;
        if (controls != null) controls.Disable();
        if (actions != null) actions.RequestBlock(false);
    }

    private void OnDestroy()
    {
        if (controls == null) return;
        controls.Player.LightAttack.performed -= OnLightAttack;
        controls.Player.HeavyAttack.performed -= OnHeavyAttack;
        controls.Player.Block.performed -= OnBlockPressed;
        controls.Player.Block.canceled -= OnBlockReleased;
        if (dodgeAction != null) dodgeAction.performed -= OnDodge;
        controls.Dispose();
    }

    private void OnLightAttack(InputAction.CallbackContext context)
    {
        combos.TryRequestAttack(PlayerComboInput.Light);
    }

    private void OnHeavyAttack(InputAction.CallbackContext context)
    {
        combos.TryRequestAttack(PlayerComboInput.Heavy);
    }

    private void OnBlockPressed(InputAction.CallbackContext context) => actions.RequestBlock(true);
    private void OnBlockReleased(InputAction.CallbackContext context) => actions.RequestBlock(false);

    private void OnDodge(InputAction.CallbackContext context)
    {
        actions.TryDodge(controls.Player.Move.ReadValue<Vector2>());
    }

    private void OnAttackRequested(string attackId)
    {
        StopSwingSoundWait();
        if (playerSwordSound != null)
            pendingSwingSound = StartCoroutine(PlaySoundWhenAttackClipBegins(attackId));
    }

    private IEnumerator PlaySoundWhenAttackClipBegins(string id)
    {
        // This waits for the Phase 05 BeginAttack animation event, so the
        // sound doesn't fire early while a previous combo clip is still playing.
        float elapsed = 0f;
        while (elapsed < swingSoundWaitTimeout)
        {
            if (combos == null || !combos.IsSequenceActive)
                break;

            if (attackRuntime != null && attackRuntime.IsActive &&
                attackRuntime.ActiveAttackId == id)
            {
                playerSwordSound.PlaySlice();
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        pendingSwingSound = null;
    }

    private void StopSwingSoundWait()
    {
        if (pendingSwingSound == null) return;
        StopCoroutine(pendingSwingSound);
        pendingSwingSound = null;
    }

    public void OnCharacterDied() => StopSwingSoundWait();
    public void OnCharacterReset() => StopSwingSoundWait();
}
