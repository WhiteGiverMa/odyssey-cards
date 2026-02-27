using System;
using System.Collections.Generic;
using Godot;

namespace OdysseyCards.Character;

public partial class Character : Node
{
    [Export] public string CharacterName { get; set; } = "Unnamed";
    [Export] public int MaxHealth { get; set; } = 100;
    [Export] public int MaxEnergy { get; set; } = 3;
    
    public int CurrentHealth { get; protected set; }
    public int CurrentEnergy { get; protected set; }
    public int Block { get; protected set; }
    public bool IsDead => CurrentHealth <= 0;

    private List<Buff> _buffs = new();
    private List<Debuff> _debuffs = new();

    public event Action<int, int> OnHealthChanged;
    public event Action<int, int> OnEnergyChanged;
    public event Action<int> OnBlockChanged;
    public event Action OnDeath;

    public override void _Ready()
    {
        CurrentHealth = MaxHealth;
        CurrentEnergy = MaxEnergy;
        Block = 0;
    }

    public virtual void TakeDamage(int amount)
    {
        if (amount <= 0)
            return;

        int actualDamage = amount;
        
        if (Block > 0)
        {
            if (Block >= amount)
            {
                SetBlock(Block - amount);
                return;
            }
            else
            {
                actualDamage = amount - Block;
                SetBlock(0);
            }
        }

        CurrentHealth = Math.Max(0, CurrentHealth - actualDamage);
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);

        if (IsDead)
        {
            Die();
        }
    }

    public virtual void Heal(int amount)
    {
        if (amount <= 0)
            return;

        CurrentHealth = Math.Min(MaxHealth, CurrentHealth + amount);
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    public virtual void GainBlock(int amount)
    {
        if (amount <= 0)
            return;

        SetBlock(Block + amount);
    }

    protected void SetBlock(int value)
    {
        Block = Math.Max(0, value);
        OnBlockChanged?.Invoke(Block);
    }

    public virtual void ResetBlock()
    {
        SetBlock(0);
    }

    public virtual void SpendEnergy(int amount)
    {
        CurrentEnergy = Math.Max(0, CurrentEnergy - amount);
        OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);
    }

    public virtual void GainEnergy(int amount)
    {
        CurrentEnergy = Math.Min(MaxEnergy + amount, CurrentEnergy + amount);
        OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);
    }

    public virtual void ResetEnergy()
    {
        CurrentEnergy = MaxEnergy;
        OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);
    }

    public virtual void StartTurn()
    {
        ResetBlock();
        ProcessTurnStartBuffs();
    }

    public virtual void EndTurn()
    {
        ProcessTurnEndBuffs();
    }

    protected virtual void Die()
    {
        OnDeath?.Invoke();
    }

    protected virtual void ProcessTurnStartBuffs()
    {
        foreach (var buff in _buffs)
            buff.OnTurnStart(this);
        foreach (var debuff in _debuffs)
            debuff.OnTurnStart(this);
    }

    protected virtual void ProcessTurnEndBuffs()
    {
        foreach (var buff in _buffs)
            buff.OnTurnEnd(this);
        foreach (var debuff in _debuffs)
            debuff.OnTurnEnd(this);
    }

    public void AddBuff(Buff buff)
    {
        _buffs.Add(buff);
    }

    public void AddDebuff(Debuff debuff)
    {
        _debuffs.Add(debuff);
    }

    public void RemoveBuff(Buff buff)
    {
        _buffs.Remove(buff);
    }

    public void RemoveDebuff(Debuff debuff)
    {
        _debuffs.Remove(debuff);
    }
}

public abstract class Buff
{
    public string Name { get; protected set; }
    public int Stacks { get; set; }
    
    public virtual void OnTurnStart(Character owner) { }
    public virtual void OnTurnEnd(Character owner) { }
}

public abstract class Debuff
{
    public string Name { get; protected set; }
    public int Stacks { get; set; }
    
    public virtual void OnTurnStart(Character owner) { }
    public virtual void OnTurnEnd(Character owner) { }
}
