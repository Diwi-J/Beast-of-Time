using System;
using UnityEngine;

[RequireComponent(typeof(CombatCollider))]
public class HitDetector : MonoBehaviour
{
    public float damage;
    public float parryWindow;

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
            bool IsParry = (Time.time - targetCC.Owner.BlockStartTime) <= parryWindow;
            if (IsParry)
            {
                Debug.Log($"{targetCC.Owner.name} Parried");
            }
            else
            {
                Debug.Log($"{targetCC.Owner.name} Blocked the Attack");
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
