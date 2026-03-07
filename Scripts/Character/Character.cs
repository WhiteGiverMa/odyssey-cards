using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OdysseyCards.Character;

/// <summary>
/// Base class for all characters (players and enemies) in the game.
/// Manages health, energy, block, and buff/debuff systems.
/// </summary>
public partial class Character : Node
{
    /// <summary>
    /// Natural maximum energy cap without bonuses.
    /// </summary>
    public const int NaturalMaxEnergyCap = 12;

    /// <summary>
    /// Hard maximum energy cap including all bonuses.
    /// </summary>
    public const int HardMaxEnergyCap = 24;

    /// <summary>
    /// Display name of the character.
    /// </summary>
    [Export] public string CharacterName { get; set; } = "Unnamed";

    /// <summary>
    /// Maximum health points.
    /// </summary>
    [Export] public int MaxHealth { get; set; } = 100;

    /// <summary>
    /// Maximum energy available per turn.
    /// </summary>
    [Export] public int MaxEnergy { get; set; } = 3;

    /// <summary>
    /// Whether this character is a headquarters.
    /// </summary>
    [Export] public bool IsHeadquarters { get; set; } = false;

    /// <summary>
    /// Additional tags for this character (e.g., Flying, Mechanical).
    /// </summary>
    [Export] public string[] Tags { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Current health points.
    /// </summary>
    public int CurrentHealth { get; protected set; }

    /// <summary>
    /// Current available energy.
    /// </summary>
    public int CurrentEnergy { get; protected set; }

    /// <summary>
    /// Current block value (absorbs damage).
    /// </summary>
    public int Block { get; protected set; }

    /// <summary>
    /// Whether the character is dead (health <= 0).
    /// </summary>
    public bool IsDead => CurrentHealth <= 0;

    private List<Buff> _buffs = new();
    private List<Debuff> _debuffs = new();

    /// <summary>
    /// Fired when health changes. Parameters: currentHealth, maxHealth.
    /// </summary>
    public event Action<int, int> OnHealthChanged;

    /// <summary>
    /// Fired when energy changes. Parameters: currentEnergy, maxEnergy.
    /// </summary>
    public event Action<int, int> OnEnergyChanged;

    /// <summary>
    /// Fired when block changes.
    /// </summary>
    public event Action<int> OnBlockChanged;

    /// <summary>
    /// Fired when the character dies.
    /// </summary>
    public event Action OnDeath;

    public override void _Ready()
    {
        CurrentHealth = MaxHealth;
        CurrentEnergy = MaxEnergy;
        Block = 0;
    }

    /// <summary>
    /// Applies damage to the character, accounting for block.
    /// </summary>
    /// <param name="amount">The amount of damage to apply.</param>
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

    /// <summary>
    /// Heals the character by the specified amount.
    /// </summary>
    /// <param name="amount">The amount to heal.</param>
    public virtual void Heal(int amount)
    {
        if (amount <= 0)
            return;

        CurrentHealth = Math.Min(MaxHealth, CurrentHealth + amount);
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    /// <summary>
    /// Gains block points.
    /// </summary>
    /// <param name="amount">The amount of block to gain.</param>
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

    /// <summary>
    /// Resets block to zero.
    /// </summary>
    public virtual void ResetBlock()
    {
        SetBlock(0);
    }

    /// <summary>
    /// Spends the specified amount of energy.
    /// </summary>
    /// <param name="amount">The amount of energy to spend.</param>
    public virtual void SpendEnergy(int amount)
    {
        CurrentEnergy = Math.Max(0, CurrentEnergy - amount);
        OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);
    }

    /// <summary>
    /// Gains the specified amount of energy.
    /// </summary>
    /// <param name="amount">The amount of energy to gain.</param>
    public virtual void GainEnergy(int amount)
    {
        CurrentEnergy = Math.Min(MaxEnergy + amount, CurrentEnergy + amount);
        OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);
    }

    /// <summary>
    /// Resets energy to maximum.
    /// </summary>
    public virtual void ResetEnergy()
    {
        CurrentEnergy = MaxEnergy;
        OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);
    }

    /// <summary>
    /// Sets energy to specific values.
    /// </summary>
    /// <param name="current">The current energy value.</param>
    /// <param name="max">The maximum energy value.</param>
    public void SetEnergy(int current, int max)
    {
        MaxEnergy = max;
        CurrentEnergy = current;
        OnEnergyChanged?.Invoke(CurrentEnergy, MaxEnergy);
    }

    /// <summary>
    /// Increases maximum energy by the specified amount.
    /// </summary>
    /// <param name="amount">The amount to increase.</param>
    public void IncreaseMaxEnergy(int amount)
    {
        MaxEnergy = Mathf.Min(MaxEnergy + amount, HardMaxEnergyCap);
    }

    /// <summary>
    /// Called at the start of a turn. Resets energy and block, processes buffs.
    /// </summary>
    public virtual void StartTurn()
    {
        if (MaxEnergy < NaturalMaxEnergyCap)
        {
            MaxEnergy++;
        }
        CurrentEnergy = MaxEnergy;
        ResetBlock();
        ProcessTurnStartBuffs();
    }

    /// <summary>
    /// Called at the end of a turn. Processes end-of-turn buffs.
    /// </summary>
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

    /// <summary>
    /// Adds a buff to the character.
    /// </summary>
    /// <param name="buff">The buff to add.</param>
    public void AddBuff(Buff buff)
    {
        _buffs.Add(buff);
    }

    /// <summary>
    /// Adds a debuff to the character.
    /// </summary>
    /// <param name="debuff">The debuff to add.</param>
    public void AddDebuff(Debuff debuff)
    {
        _debuffs.Add(debuff);
    }

    /// <summary>
    /// Removes a buff from the character.
    /// </summary>
    /// <param name="buff">The buff to remove.</param>
    public void RemoveBuff(Buff buff)
    {
        _buffs.Remove(buff);
    }

    /// <summary>
    /// Removes a debuff from the character.
    /// </summary>
    /// <param name="debuff">The debuff to remove.</param>
    public void RemoveDebuff(Debuff debuff)
    {
        _debuffs.Remove(debuff);
    }

    /// <summary>
    /// Gets the static tags for this character (Unit or HQ + additional tags).
    /// </summary>
    /// <returns>Array of static tags.</returns>
    public string[] GetStaticTags()
    {
        var tags = new List<string>(Tags);
        tags.Add(IsHeadquarters ? Core.TargetTags.HQ : Core.TargetTags.Unit);
        return tags.ToArray();
    }

    /// <summary>
    /// Gets all tags for this character relative to a caster.
    /// Includes static tags plus Ally/Enemy based on relationship.
    /// </summary>
    /// <param name="caster">The character casting the card.</param>
    /// <returns>Array of all tags including dynamic Ally/Enemy.</returns>
    public string[] GetTagsRelativeTo(Character caster)
    {
        var tags = new List<string>(GetStaticTags());
        bool isAlly = IsAllyTo(caster);
        tags.Add(isAlly ? Core.TargetTags.Ally : Core.TargetTags.Enemy);
        return tags.ToArray();
    }

    /// <summary>
    /// Checks if this character is an ally to another character.
    /// Player and Player are allies, Enemy and Enemy are allies.
    /// </summary>
    /// <param name="other">The other character to check.</param>
    /// <returns>True if this character is an ally to the other.</returns>
    public bool IsAllyTo(Character other)
    {
        if (other == null) return false;

        bool thisIsPlayer = this is Player;
        bool otherIsPlayer = other is Player;

        return thisIsPlayer == otherIsPlayer;
    }

    /// <summary>
    /// Checks if this character matches all required tags.
    /// </summary>
    /// <param name="requiredTags">The tags required by the card.</param>
    /// <param name="caster">The character casting the card.</param>
    /// <returns>True if this character has all required tags.</returns>
    public bool MatchesTags(string[] requiredTags, Character caster)
    {
        if (requiredTags == null || requiredTags.Length == 0)
        {
            return true;
        }

        var myTags = GetTagsRelativeTo(caster);
        return requiredTags.All(tag => myTags.Contains(tag));
    }
}

/// <summary>
/// Base class for positive status effects (buffs).
/// Buffs provide beneficial effects that trigger at turn start/end.
/// </summary>
public abstract class Buff
{
    /// <summary>
    /// Name of the buff for display.
    /// </summary>
    public string Name { get; protected set; }

    /// <summary>
    /// Number of stacks (for stacking buffs).
    /// </summary>
    public int Stacks { get; set; }

    /// <summary>
    /// Called at the start of the owner's turn.
    /// </summary>
    /// <param name="owner">The character owning this buff.</param>
    public virtual void OnTurnStart(Character owner) { }

    /// <summary>
    /// Called at the end of the owner's turn.
    /// </summary>
    /// <param name="owner">The character owning this buff.</param>
    public virtual void OnTurnEnd(Character owner) { }
}

/// <summary>
/// Base class for negative status effects (debuffs).
/// Debuffs provide detrimental effects that trigger at turn start/end.
/// </summary>
public abstract class Debuff
{
    /// <summary>
    /// Name of the debuff for display.
    /// </summary>
    public string Name { get; protected set; }

    /// <summary>
    /// Number of stacks (for stacking debuffs).
    /// </summary>
    public int Stacks { get; set; }

    /// <summary>
    /// Called at the start of the owner's turn.
    /// </summary>
    /// <param name="owner">The character owning this debuff.</param>
    public virtual void OnTurnStart(Character owner) { }

    /// <summary>
    /// Called at the end of the owner's turn.
    /// </summary>
    /// <param name="owner">The character owning this debuff.</param>
    public virtual void OnTurnEnd(Character owner) { }
}
