using UnityEngine;

public class PlayerTargeting : MonoBehaviour
{
    EnemyHealthBar currentTarget;

    // Call when the player attacks / enters battle with an enemy
    public void SetTarget(EnemyHealthBar newTarget)
    {
        if (newTarget == currentTarget) return;

        if (currentTarget != null) currentTarget.SetVisible(false); // hide the old one first
        currentTarget = newTarget;
        if (currentTarget != null) currentTarget.SetVisible(true);
    }

    public void ClearTarget() => SetTarget(null); // call when battle ends
}