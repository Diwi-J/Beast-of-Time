using System;
using UnityEngine;

[RequireComponent(typeof(CombatCollider))]
public class HitDetector : MonoBehaviour
{
    public float damage;
    public float chipDamagemultiplier = 0.1f;

    private CombatCollider self;

    private void Awake()
    {
        self = GetComponent<CombatCollider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        var targetCC = other.GetComponent<CombatCollider>();

        if (targetCC != null)
        {
            Debug.Log($"[HitDetector] No CombatCollider(Component) found");
            return;
        }

        if (targetCC == self.Owner)
        {
            return;
        }

        if (targetCC.type == ColliderType.Blockbox)
        {
            var defender = targetCC.Owner;

            if (defender.IsInParryWindow())
            {
                Debug.Log($"{defender.name} Parried");
            }
            else
            {
                Debug.Log($"{defender.name} Blocked took chip damage: {damage * chipDamagemultiplier}");
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
