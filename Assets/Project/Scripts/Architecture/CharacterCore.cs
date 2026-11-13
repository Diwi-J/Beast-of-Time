using UnityEngine;

/// <summary>
/// Stable identity and access point for components shared by all characters.
/// Does not make combat, AI, movement, death, or respawn decisions.
/// </summary>
public enum CharacterKind
{
    Player,
    Mob,
    Boss
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Animator))]
public sealed class CharacterCore : MonoBehaviour
{
    [SerializeField] private CharacterKind kind;

    public CharacterKind Kind => kind;
    public bool IsPlayer => kind == CharacterKind.Player;
    public bool IsEnemy => kind != CharacterKind.Player;
    public bool IsBoss => kind == CharacterKind.Boss;

    public Health Health { get; private set; }
    public Animator Animator { get; private set; }

    private void Awake()
    {
        Health = GetComponent<Health>();
        Animator = GetComponent<Animator>();
    }
}
