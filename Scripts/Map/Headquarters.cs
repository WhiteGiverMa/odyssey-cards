using System;

namespace OdysseyCards.Map;

public class Headquarters
{
    public int MaxHealth { get; set; }
    public int CurrentHealth { get; set; }
    public NodeOwner Owner { get; set; }
    public int DeploymentNodeId { get; set; }

    public event Action<int, int> OnHealthChanged;
    public event Action OnDestroyed;

    public Headquarters(NodeOwner owner, int maxHealth = 8, int deploymentNodeId = -1)
    {
        Owner = owner;
        MaxHealth = maxHealth;
        CurrentHealth = maxHealth;
        DeploymentNodeId = deploymentNodeId;
    }

    public void TakeDamage(int damage)
    {
        int previousHealth = CurrentHealth;
        CurrentHealth -= damage;
        if (CurrentHealth < 0)
            CurrentHealth = 0;

        if (previousHealth != CurrentHealth)
        {
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        if (CurrentHealth <= 0 && previousHealth > 0)
        {
            OnDestroyed?.Invoke();
        }
    }

    public void Heal(int amount)
    {
        int previousHealth = CurrentHealth;
        CurrentHealth += amount;
        if (CurrentHealth > MaxHealth)
            CurrentHealth = MaxHealth;

        if (previousHealth != CurrentHealth)
        {
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }
    }

    public void IncreaseMaxHealth(int amount)
    {
        MaxHealth += amount;
        CurrentHealth += amount;
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    public void SetHealth(int currentHealth, int maxHealth)
    {
        MaxHealth = maxHealth;
        int previousHealth = CurrentHealth;
        CurrentHealth = Math.Clamp(currentHealth, 0, MaxHealth);

        if (previousHealth != CurrentHealth)
        {
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }
    }

    public bool IsDestroyed => CurrentHealth <= 0;
}
