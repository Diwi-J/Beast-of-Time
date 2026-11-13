using UnityEngine;

/// <summary>
/// Add to EACH Animator STATE that plays an individual Melee_1 or Melee_2
/// clip, both standalone and inside a Melee_Combo Sub-State Machine.
/// The sequence order is authored in Animator and never hard-coded here.
///
/// This is a StateMachineBehaviour (not a component on the GameObject).
/// </summary>
public sealed class MobAttackSegmentState : StateMachineBehaviour
{
    public enum Segment { Melee1, Melee2 }

    [Tooltip("Select which actual melee clip this Animator state represents.")]
    [SerializeField] private Segment segment;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        MobAttackController controller = animator.GetComponent<MobAttackController>();
        if (controller != null)
            controller.EnterSegment(segment);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        MobAttackController controller = animator.GetComponent<MobAttackController>();
        if (controller != null)
            controller.ExitSegment(segment);
    }
}
