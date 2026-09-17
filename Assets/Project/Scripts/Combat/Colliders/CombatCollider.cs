using UnityEngine;

public enum ColliderType { Hitbox, Hurtbox, Blockbox};

[RequireComponent(typeof(Collider))] 
public class CombatCollider : MonoBehaviour
{
    public ColliderType type;
    public string Id = "default";

    [HideInInspector] public CharacterCombat Owner;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }
}
