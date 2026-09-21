using System;
using UnityEngine;

[RequireComponent(typeof(CombatCollider))]
public class HitDetector : MonoBehaviour
{
    public float damage;
    public float chipDamagemultiplier = 0.1f;

    private CombatCollider self;

    private static readonly int ImpactHash = Animator.StringToHash("Impact");

    private void Awake()
    {
        self = GetComponent<CombatCollider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        //Debug.Log($"Trigger entered by {other.name}");

        var targetCC = other.GetComponent<CombatCollider>();

        if (targetCC == null)
        {
            //Debug.Log($"[HitDetector] No CombatCollider(Component) found");
            return;
        }

        if (targetCC.Owner == self.Owner)
        {
            return;
        }

        if (targetCC.type == ColliderType.Blockbox)
        {
            var defender = targetCC.Owner;

            if (defender.IsInParryWindow())
            {
                Debug.Log($"{defender.name} Parried");
                defender.ParryDistortion.Play();
            }
            else
            {
                Debug.Log($"{defender.name} Blocked took chip damage: {damage * chipDamagemultiplier}");
                defender.GetComponent<Animator>().SetTrigger(ImpactHash);
            }

            GetComponent<Collider>().enabled = false; 

        }
        else if (targetCC.type == ColliderType.Hurtbox)
        {
            Debug.Log($"{targetCC.Owner.name} took {damage} damage.");
            GetComponent<Collider>().enabled = false;
        }
    }
}
