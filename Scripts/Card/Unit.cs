using Godot;
using System;
using System.Collections.Generic;
using OdysseyCards.Core;
using OdysseyCards.Card.Tags;
using OdysseyCards.Character;
using OdysseyCards.Map;

namespace OdysseyCards.Card;

/// <summary>
/// Represents a Unit card that can be deployed on the battle map.
/// Units have health, attack, range, and can perform move/attack actions.
/// </summary>
public partial class Unit : Card, IDamageSource, IDamageTarget
{
    private List<IDamageModifier> _damageModifiers = new();
    public IReadOnlyList<IDamageModifier> DamageModifiers => _damageModifiers.AsReadOnly();
    /// <summary>
    /// The resource data this unit was created from.
    /// </summary>
    public UnitData Data { get; private set; }

    /// <summary>
    /// Energy cost to deploy this unit.
    /// </summary>
    public int DeployCost { get; private set; }

    /// <summary>
    /// Energy cost for each action.
    /// </summary>
    public int ActionCost { get; private set; }

    /// <summary>
    /// Attack damage value.
    /// </summary>
    public int Attack { get; private set; }

    public int BaseAttack => Attack;

    /// <summary>
    /// Current health points.
    /// </summary>
    public int CurrentHealth { get; private set; }

    /// <summary>
    /// Maximum health points.
    /// </summary>
    public int MaxHealth { get; private set; }

    /// <summary>
    /// Attack range in nodes.
    /// </summary>
    public int Range { get; private set; }

    /// <summary>
    /// Current node ID on the battle map. -1 if not deployed.
    /// </summary>
    public int CurrentNode { get; set; } = -1;

    /// <summary>
    /// Owner of this unit (Player, Enemy, or None).
    /// </summary>
    public NodeOwner OwnerType { get; set; } = NodeOwner.None;

    /// <summary>
    /// Whether this unit has been deployed.
    /// </summary>
    public bool IsDeployed { get; private set; } = false;

    /// <summary>
    /// Whether this unit can act this turn.
    /// </summary>
    public bool CanActThisTurn { get; set; } = false;

    /// <summary>
    /// Number of move actions remaining.
    /// </summary>
    public int ActionsRemaining { get; set; } = 0;

    /// <summary>
    /// Number of attack actions remaining.
    /// </summary>
    public int AttacksRemaining { get; set; } = 0;

    /// <summary>
    /// Additional actions beyond the base 1.
    /// </summary>
    public int AdditionalActions { get; set; } = 0;

    /// <summary>
    /// Maximum attacks per turn.
    /// </summary>
    public int MaxAttacksPerTurn { get; set; } = 1;

    /// <summary>
    /// Defense value (reduces incoming damage).
    /// </summary>
    public int Defense { get; set; } = 0;

    /// <summary>
    /// Range for guard ability.
    /// </summary>
    public int GuardRange { get; set; } = 0;

    /// <summary>
    /// Whether this unit has the Ambush ability (strikes first).
    /// </summary>
    public bool HasAmbush { get; set; } = false;

    /// <summary>
    /// Whether this unit has the Impact ability (bonus effect on first attack).
    /// </summary>
    public bool HasImpact { get; set; } = false;

    /// <summary>
    /// Whether this unit is immune to damage.
    /// </summary>
    public bool IsImmune { get; set; } = false;

    /// <summary>
    /// Whether this unit is pinned (cannot move).
    /// </summary>
    public bool IsPinned { get; set; } = false;

    /// <summary>
    /// Whether this unit is suppressed.
    /// </summary>
    public bool IsSuppressed { get; set; } = false;

    /// <summary>
    /// Whether this unit is massive (cannot be moved by effects).
    /// </summary>
    public bool IsMassive { get; set; } = false;

    /// <summary>
    /// Whether this unit can infiltrate (bypass enemy units).
    /// </summary>
    public bool CanInfiltrate { get; set; } = false;

    /// <summary>
    /// Whether this unit should return to deck when destroyed.
    /// </summary>
    public bool ShouldReturnToDeck { get; set; } = false;

    /// <summary>
    /// Whether this unit has attacked this turn.
    /// </summary>
    public bool HasAttackedThisTurn { get; set; } = false;

    private List<CardEffectData> _deployEffects = new();
    private List<CardEffectData> _lastWordsEffects = new();

    /// <summary>
    /// Creates a Unit instance from UnitData.
    /// </summary>
    /// <param name="data">The unit data to create from.</param>
    /// <returns>A new Unit instance.</returns>
    public static Unit Create(UnitData data)
    {
        var unit = new Unit
        {
            Data = data,
            _data = data,
            Id = data.Id,
            Rarity = data.Rarity,
            Artwork = data.Artwork,
            Type = CardType.Unit,
            Tags = data.Tags,

            DeployCost = data.DeployCost,
            ActionCost = data.ActionCost,
            Attack = data.Attack,
            MaxHealth = data.MaxHealth,
            CurrentHealth = data.MaxHealth,
            Range = data.Range
        };

        if (data.Effects != null)
        {
            foreach (var effect in data.Effects)
                unit._effects.Add(effect);
        }

        if (data.DeployEffects != null)
        {
            foreach (var effect in data.DeployEffects)
                unit._deployEffects.Add(effect);
        }

        if (data.LastWordsEffects != null)
        {
            foreach (var effect in data.LastWordsEffects)
                unit._lastWordsEffects.Add(effect);
        }

        unit.ApplyPassiveTags();

        unit._damageModifiers.Add(new DefenseModifier(unit));
        unit._damageModifiers.Add(new ImmuneModifier(unit));
        return unit;
    }

    private void ApplyPassiveTags()
    {
        if (Tags == null)
            return;

        foreach (var tag in Tags)
        {
            var tagDef = TagFactory.CreateTag(tag);
            if (tagDef != null && tagDef.TriggerType == TagTriggerType.Passive)
            {
                var context = new TagContext(this, null, GetTagCount(tag));
                tagDef.ApplyPassiveEffect(context);
            }
        }
    }

    /// <summary>
    /// Called when this unit is deployed on the map.
    /// </summary>
    public void OnDeploy()
    {
        IsDeployed = true;
        CanActThisTurn = false;
        ActionsRemaining = 1 + AdditionalActions;
        AttacksRemaining = MaxAttacksPerTurn;
        HasAttackedThisTurn = false;

        if (Tags != null)
        {
            foreach (var tag in Tags)
            {
                var tagDef = TagFactory.CreateTag(tag);
                if (tagDef != null)
                {
                    var context = new TagContext(this, null, GetTagCount(tag));
                    tagDef.OnDeploy(context);
                }
            }
        }

        foreach (var effect in _deployEffects)
        {
            ExecuteEffect(effect);
        }
    }

    /// <summary>
    /// Called at the start of each turn.
    /// </summary>
    public void OnTurnStart()
    {
        if (IsPinned)
        {
            IsPinned = false;
        }

        CanActThisTurn = true;
        ActionsRemaining = 1 + AdditionalActions;
        AttacksRemaining = MaxAttacksPerTurn;
        HasAttackedThisTurn = false;

        if (Tags != null)
        {
            foreach (var tag in Tags)
            {
                var tagDef = TagFactory.CreateTag(tag);
                if (tagDef != null)
                {
                    var context = new TagContext(this, null, GetTagCount(tag));
                    tagDef.OnTurnStart(context);
                }
            }
        }
    }

    /// <summary>
    /// Called at the end of each turn.
    /// </summary>
    public void OnTurnEnd()
    {
        if (Tags != null)
        {
            foreach (var tag in Tags)
            {
                var tagDef = TagFactory.CreateTag(tag);
                if (tagDef != null)
                {
                    var context = new TagContext(this, null, GetTagCount(tag));
                    tagDef.OnTurnEnd(context);
                }
            }
        }
    }

    /// <summary>
    /// Called when this unit is destroyed.
    /// </summary>
    public void OnDeath()
    {
        if (Tags != null)
        {
            foreach (var tag in Tags)
            {
                var tagDef = TagFactory.CreateTag(tag);
                if (tagDef != null)
                {
                    var context = new TagContext(this, null, GetTagCount(tag));
                    tagDef.OnDeath(context);
                }
            }
        }

        foreach (var effect in _lastWordsEffects)
        {
            ExecuteEffect(effect);
        }
    }

    /// <summary>
    /// Checks if this unit can move.
    /// </summary>
    /// <returns>True if the unit can move this turn.</returns>
    public bool CanMove()
    {
        return CanActThisTurn && ActionsRemaining > 0 && !IsPinned;
    }

    /// <summary>
    /// Checks if this unit can attack.
    /// </summary>
    /// <returns>True if the unit can attack this turn.</returns>
    public bool CanAttack()
    {
        return CanActThisTurn && AttacksRemaining > 0 && !IsPinned;
    }

    /// <summary>
    /// Consumes a move action.
    /// </summary>
    public void UseMoveAction()
    {
        ActionsRemaining--;
    }

    /// <summary>
    /// Consumes an attack action.
    /// </summary>
    public void UseAttackAction()
    {
        AttacksRemaining--;
        ActionsRemaining--;
        HasAttackedThisTurn = true;

        if (HasImpact)
        {
            HasImpact = false;
        }
    }

    /// <summary>
    /// Applies damage to this unit using the unified damage pipeline.
    /// </summary>
    /// <param name="baseDamage">The base damage amount.</param>
    /// <param name="source">The source of the damage.</param>
    public void TakeDamage(int baseDamage, IDamageSource source)
    {
        int finalDamage = DamageResolver.ResolveDamage(baseDamage, source, this);
        ApplyDamage(finalDamage, source);
    }

    /// <summary>
    /// Applies the final damage to this unit after all modifiers.
    /// </summary>
    /// <param name="finalDamage">The final damage amount after all modifiers.</param>
    /// <param name="source">The source of the damage.</param>
    public void ApplyDamage(int finalDamage, IDamageSource source)
    {
        if (finalDamage <= 0)
            return;
        CurrentHealth -= finalDamage;
        if (CurrentHealth <= 0)
        {
            OnDeath();
        }
    }

    [Obsolete("Use TakeDamage(int baseDamage, IDamageSource source) instead.")]
    public void TakeDamage(int amount)
    {
        if (IsImmune)
            return;
        int actualDamage = System.Math.Max(0, amount - Defense);
        CurrentHealth -= actualDamage;

        if (CurrentHealth <= 0)
        {
            OnDeath();
        }
    }

    /// <summary>
    /// Heals this unit by the specified amount.
    /// </summary>
    /// <param name="amount">The amount to heal.</param>
    public void Heal(int amount)
    {
        CurrentHealth = System.Math.Min(MaxHealth, CurrentHealth + amount);
    }

    private void ExecuteEffect(CardEffectData effect)
    {
        GD.Print($"Unit {CardName} executing effect: {effect.GetDescription()}");
    }

    /// <summary>
    /// Returns a formatted string with unit stats.
    /// </summary>
    /// <returns>Format: "Name | Cost/ActionCost/Attack/Health/Range"</returns>
    public override string GetCardInfo()
    {
        return $"{CardName} | {DeployCost}/{ActionCost}/{Attack}/{MaxHealth}/{Range}";
    }
}

public class DefenseModifier : IDamageModifier
{
    private readonly Unit _unit;

    public DefenseModifier(Unit unit) => _unit = unit;

    public DamagePhase Phase => DamagePhase.ADDITIVE;

    public int ModifyDamageDealt(int currentDamage, DamageContext context) => currentDamage;

    public int ModifyDamageTaken(int currentDamage, DamageContext context)
    {
        return currentDamage - _unit.Defense;
    }
}

public class ImmuneModifier : IDamageModifier
{
    private readonly Unit _unit;

    public ImmuneModifier(Unit unit) => _unit = unit;

    public DamagePhase Phase => DamagePhase.CAPPING;

    public int ModifyDamageDealt(int currentDamage, DamageContext context) => currentDamage;

    public int ModifyDamageTaken(int currentDamage, DamageContext context)
    {
        return _unit.IsImmune ? 0 : currentDamage;
    }
}
