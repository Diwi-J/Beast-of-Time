using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Put on a separate GameObject with a trigger collider and kinematic Rigidbody.
/// Reports player presence to Damned, without touching lock-on or player scripts.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class DamnedArenaVolume : MonoBehaviour
{
    [SerializeField] private DamnedBossAI damned;
    private readonly HashSet<Collider> playerColliders = new HashSet<Collider>();

    private void Awake()
    {
        Collider trigger = GetComponent<Collider>();
        if (!trigger.isTrigger) Debug.LogWarning("[DamnedArenaVolume] Collider must be Is Trigger.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        CharacterCore core = other.GetComponentInParent<CharacterCore>();
        if (core == null || !core.IsPlayer || damned == null) return;
        if (playerColliders.Add(other)) damned.SetPlayerInsideArena(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (playerColliders.Remove(other) && playerColliders.Count == 0 && damned != null)
            damned.SetPlayerInsideArena(false);
    }

    private void OnDisable()
    {
        playerColliders.Clear();
        if (damned != null) damned.SetPlayerInsideArena(false);
    }
}
