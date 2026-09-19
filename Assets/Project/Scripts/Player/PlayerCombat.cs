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
    private static readonly int PoweredUpHash = Animator.StringToHash("PoweredUp");
    private static readonly int IsFreeHash = Animator.StringToHash("IsFree");

    private void Awake()
    {
        animator = GetComponent<Animator>();
        controls = new Controls();

        controls.Player.LightAttack.performed += _ => TryLightAttack();
        controls.Player.HeavyAttack.performed += _ => TryHeavyAttack();
        controls.Player.Block.performed += _ => animator.SetBool(IsBlockingHash, true);
        controls.Player.Block.canceled += _ => animator.SetBool(IsBlockingHash, false);
        controls.Player.PowerUp.performed += _ => animator.SetTrigger(PoweredUpHash);
    }

    private void OnEnable() => controls.Enable();
    private void OnDisable() => controls.Disable();

    private bool IsFree()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

        if (!state.IsTag("Attack") && !state.IsTag("Block") && state.IsTag("Focus"))
        {
            animator.SetBool(IsFreeHash, true);
            return true;
        }
        else
        {
            animator.SetBool(IsFreeHash, false);
            return false;
        }
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
