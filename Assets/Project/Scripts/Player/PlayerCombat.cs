using UnityEngine;


[RequireComponent(typeof(Animator))]
public class PlayerCombat : MonoBehaviour
{
    private Animator animator;
    private Controls controls;

    private bool lightAttackToggle;

    private static readonly int LightAttack1Hash = Animator.StringToHash("1_LightAtk");
    private static readonly int LightAttack2Hash = Animator.StringToHash("2_LightAtk");
    private static readonly int HeavyAttackHash = Animator.StringToHash("1_HeavyAtk");
    private static readonly int IsBlockingHash = Animator.StringToHash("IsBlocking");

    private void Awake()
    {
        animator = GetComponent<Animator>();
        controls = new Controls();

        controls.Player.LightAttack.performed += _ => TryLightAttack();
        controls.Player.HeavyAttack.performed += _ => TryHeavyAttack();
        controls.Player.Block.performed += _ => animator.SetBool(IsBlockingHash, true);
        controls.Player.Block.canceled += _ => animator.SetBool(IsBlockingHash, false);
    }

    private void OnEnable() => controls.Enable();
    private void OnDisable() => controls.Disable();

    // True only while the player is in ordinary locomotion/idle - false during any attack or block state.
    // This is what enforces "nothing interrupts an attack or block until it's finished" without needing
    // extra bool parameters kept in sync between script and Animator.
    private bool IsFree()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        return !state.IsTag("Attack") && !state.IsTag("Block");
    }

    private void TryLightAttack()
    {
        if (!IsFree()) return;

        animator.SetTrigger(lightAttackToggle ? LightAttack2Hash : LightAttack1Hash);
        lightAttackToggle = !lightAttackToggle;
    }

    private void TryHeavyAttack()
    {
        if (!IsFree()) return;

        animator.SetTrigger(HeavyAttackHash);
    }
}
