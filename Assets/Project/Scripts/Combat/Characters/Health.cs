using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;

    public float Max => maxHealth;
    public float Current { get; private set; }
    public float Normalized => maxHealth > 0f ? Current / maxHealth : 0f;
    public bool IsDead { get; private set; }

    public event Action<float> OnDamaged;
    public event Action<float, float> OnHealthChanged;
    public event Action OnDeath;

    private void Awake()
    {
        Current = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f) return;

        Current = Mathf.Max(0f, Current - amount);

        Debug.Log($"[Health] {name} took {amount} damage. {Current}/{maxHealth} left.");

        OnDamaged?.Invoke(amount);
        OnHealthChanged?.Invoke(Current, maxHealth);

        if (Current <= 0f)
        {
            IsDead = true;
            OnDeath?.Invoke();
        }
    }

    public void ResetHealth()
    {
        IsDead = false;
        Current = maxHealth;
        OnHealthChanged?.Invoke(Current, maxHealth);
    }
}
