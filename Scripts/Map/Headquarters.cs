namespace OdysseyCards.Map;

public class Headquarters
{
    public int MaxHealth { get; set; }
    public int CurrentHealth { get; set; }
    public NodeOwner Owner { get; set; }
    public int DeploymentNodeId { get; set; }

    public Headquarters(NodeOwner owner, int maxHealth = 8, int deploymentNodeId = -1)
    {
        Owner = owner;
        MaxHealth = maxHealth;
        CurrentHealth = maxHealth;
        DeploymentNodeId = deploymentNodeId;
    }

    public void TakeDamage(int damage)
    {
        CurrentHealth -= damage;
        if (CurrentHealth < 0) CurrentHealth = 0;
    }

    public void Heal(int amount)
    {
        CurrentHealth += amount;
        if (CurrentHealth > MaxHealth) CurrentHealth = MaxHealth;
    }

    public void IncreaseMaxHealth(int amount)
    {
        MaxHealth += amount;
        CurrentHealth += amount;
    }

    public bool IsDestroyed => CurrentHealth <= 0;
}
