using Godot;
using System.Collections.Generic;
using OdysseyCards.Core;
using OdysseyCards.Card.Tags;
using OdysseyCards.Character;

namespace OdysseyCards.Card;

/// <summary>
/// Represents an Order card that provides instant effects.
/// Orders are played directly from hand and can target characters or units.
/// </summary>
public partial class Order : Card
{
    /// <summary>
    /// The resource data this order was created from.
    /// </summary>
    public OrderData Data { get; private set; }

    /// <summary>
    /// Energy cost to play this order.
    /// </summary>
    public int Cost { get; private set; }

    /// <summary>
    /// Target type for this order.
    /// </summary>
    public CardTarget Target { get; private set; }

    /// <summary>
    /// Whether this order should return to draw pile after playing.
    /// Set by Rotation tag.
    /// </summary>
    public bool ShouldReturnToDeck { get; private set; } = false;

    /// <summary>
    /// Creates an Order instance from OrderData.
    /// </summary>
    /// <param name="data">The order data to create from.</param>
    /// <returns>A new Order instance.</returns>
    public static Order Create(OrderData data)
    {
        var order = new Order
        {
            Data = data,
            Id = data.Id,
            CardName = data.CardName,
            Description = data.Description,
            Rarity = data.Rarity,
            Artwork = data.Artwork,
            Type = CardType.Order,
            Tags = data.Tags,

            Cost = data.Cost,
            Target = data.Target
        };

        if (data.Effects != null)
        {
            foreach (var effect in data.Effects)
                order._effects.Add(effect);
        }

        return order;
    }

    /// <summary>
    /// Checks if this order can be played with the available energy.
    /// </summary>
    /// <param name="availableEnergy">The energy available.</param>
    /// <returns>True if the order can be played.</returns>
    public bool CanPlay(int availableEnergy)
    {
        return availableEnergy >= Cost;
    }

    /// <summary>
    /// Plays this order, executing its effects.
    /// </summary>
    /// <param name="caster">The character playing the order.</param>
    /// <param name="target">Optional target character.</param>
    public void Play(Character.Character caster, Character.Character target = null)
    {
        if (HasTag(CardTag.Rotation))
        {
            ShouldReturnToDeck = true;
        }

        foreach (var effect in _effects)
        {
            ExecuteEffect(effect, caster, target);
        }
    }

    private void ExecuteEffect(CardEffectData effect, Character.Character caster, Character.Character target)
    {
        switch (effect.EffectType)
        {
            case CardEffectType.Damage:
                if (target != null)
                {
                    target.TakeDamage(effect.Value);
                }
                break;
            case CardEffectType.Heal:
                caster?.Heal(effect.Value);
                break;
            case CardEffectType.DrawCards:
                GD.Print($"Draw {effect.Value} cards");
                break;
            case CardEffectType.GainEnergy:
                GD.Print($"Gain {effect.Value} energy");
                break;
            case CardEffectType.GainMaxHealth:
                GD.Print($"Headquarters +{effect.Value} HP");
                break;
            default:
                GD.Print($"Execute effect: {effect.GetDescription()}");
                break;
        }
    }

    /// <summary>
    /// Returns a formatted string with order info.
    /// </summary>
    /// <returns>Format: "Name | CostK"</returns>
    public override string GetCardInfo()
    {
        return $"{CardName} | {Cost}K";
    }
}
