using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-scoped reference list for the player and all preplaced enemies.
/// Explicit scene references intentionally remain available when a character
/// becomes inactive or its health reaches zero.
///
/// This registry does not spawn, destroy, revive, or reset characters.
/// </summary>
[DisallowMultipleComponent]
public sealed class SceneActorRegistry : MonoBehaviour
{
    [Header("Preplaced scene actors")]
    [SerializeField] private CharacterCore player;
    [SerializeField] private List<CharacterCore> enemies = new List<CharacterCore>();

    public CharacterCore Player => player;
    public IReadOnlyList<CharacterCore> Enemies => enemies;

    private void Awake()
    {
        ValidateAssignments();
    }

    [ContextMenu("Debug / Log Registered Actors")]
    private void LogRegisteredActors()
    {
        Debug.Log($"[SceneActorRegistry] Player: {(player != null ? player.name : "UNASSIGNED")}; " +
                  $"Enemy references: {enemies.Count}", this);

        for (int i = 0; i < enemies.Count; i++)
        {
            CharacterCore enemy = enemies[i];
            string description = enemy == null
                ? "NULL"
                : $"{enemy.name} ({enemy.Kind}, active={enemy.gameObject.activeInHierarchy})";
            Debug.Log($"[SceneActorRegistry] Enemy [{i}]: {description}", this);
        }
    }

    private void ValidateAssignments()
    {
        if (player == null)
            Debug.LogError("[SceneActorRegistry] Assign the player CharacterCore.", this);
        else if (!player.IsPlayer)
            Debug.LogError($"[SceneActorRegistry] {player.name} must be marked Player.", player);

        var seen = new HashSet<CharacterCore>();
        for (int i = 0; i < enemies.Count; i++)
        {
            CharacterCore enemy = enemies[i];
            if (enemy == null)
            {
                Debug.LogError($"[SceneActorRegistry] Enemy slot {i} is empty.", this);
                continue;
            }

            if (!enemy.IsEnemy)
                Debug.LogError($"[SceneActorRegistry] {enemy.name} is listed as an enemy but marked Player.", enemy);

            if (!seen.Add(enemy))
                Debug.LogError($"[SceneActorRegistry] {enemy.name} is assigned more than once.", this);

            if (enemy == player)
                Debug.LogError("[SceneActorRegistry] The player was also assigned as an enemy.", this);
        }
    }
}
