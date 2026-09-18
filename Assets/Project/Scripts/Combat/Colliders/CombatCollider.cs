using UnityEngine;

public enum ColliderType {Hitbox, Hurtbox, Blockbox};

[RequireComponent(typeof(Collider))] 
public class CombatCollider : MonoBehaviour
{
    public ColliderType type;
    public string Id = "default";

    [HideInInspector] public CharacterCombat Owner;

    private void Awake()
    {
        if (string.IsNullOrEmpty(Id) || Id == "default")
        {
            string[] parts = gameObject.name.Split('_');
            Id = parts.Length > 1 ? parts[1] : gameObject.name;
        }       
    }

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }
}
