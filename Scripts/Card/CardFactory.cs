using Godot;
using System.Collections.Generic;
using Godot.Collections;
using OdysseyCards.Core;
using OdysseyCards.Character;
using OdysseyCards.Card.Effects;

namespace OdysseyCards.Card;

public static class CardFactory
{
    public static Card CreateStrike()
    {
        var data = new CardData
        {
            CardName = "Strike",
            Description = "Deal 6 damage.",
            Type = CardType.Attack,
            Rarity = CardRarity.Common,
            Target = CardTarget.SingleEnemy,
            Cost = 1
        };

        var damageEffect = new CardEffectData
        {
            EffectType = CardEffectType.Damage,
            Value = 6
        };
        
        data.Effects = new Godot.Collections.Array<CardEffectData> { damageEffect };

        return Card.Create(data);
    }

    public static Card CreateDefend()
    {
        var data = new CardData
        {
            CardName = "Defend",
            Description = "Gain 5 Block.",
            Type = CardType.Skill,
            Rarity = CardRarity.Common,
            Target = CardTarget.Self,
            Cost = 1
        };

        var blockEffect = new CardEffectData
        {
            EffectType = CardEffectType.GainBlock,
            Value = 5
        };
        
        data.Effects = new Godot.Collections.Array<CardEffectData> { blockEffect };

        return Card.Create(data);
    }

    public static Card CreateBash()
    {
        var data = new CardData
        {
            CardName = "Bash",
            Description = "Deal 8 damage. Apply 2 Vulnerable.",
            Type = CardType.Attack,
            Rarity = CardRarity.Common,
            Target = CardTarget.SingleEnemy,
            Cost = 2
        };

        var damageEffect = new CardEffectData
        {
            EffectType = CardEffectType.Damage,
            Value = 8
        };
        
        var vulnerableEffect = new CardEffectData
        {
            EffectType = CardEffectType.ApplyDebuff,
            Value = 2,
            DebuffType = "vulnerable"
        };
        
        data.Effects = new Godot.Collections.Array<CardEffectData> { damageEffect, vulnerableEffect };

        return Card.Create(data);
    }

    public static Card CreateCleave()
    {
        var data = new CardData
        {
            CardName = "Cleave",
            Description = "Deal 8 damage to ALL enemies.",
            Type = CardType.Attack,
            Rarity = CardRarity.Common,
            Target = CardTarget.AllEnemies,
            Cost = 1
        };

        var damageEffect = new CardEffectData
        {
            EffectType = CardEffectType.Damage,
            Value = 8,
            Times = 1
        };
        
        data.Effects = new Godot.Collections.Array<CardEffectData> { damageEffect };
        
        return Card.Create(data);
    }

    public static Card CreateIronWave()
    {
        var data = new CardData
        {
            CardName = "Iron Wave",
            Description = "Gain 5 Block. Deal 5 damage.",
            Type = CardType.Attack,
            Rarity = CardRarity.Common,
            Target = CardTarget.SingleEnemy,
            Cost = 1
        };

        var blockEffect = new CardEffectData
        {
            EffectType = CardEffectType.GainBlock,
            Value = 5
        };
        
        var damageEffect = new CardEffectData
        {
            EffectType = CardEffectType.Damage,
            Value = 5
        };
        
        data.Effects = new Godot.Collections.Array<CardEffectData> { blockEffect, damageEffect };

        return Card.Create(data);
    }

    public static List<CardData> GetStarterDeck()
    {
        var deck = new List<CardData>();
        
        for (int i = 0; i < 5; i++)
            deck.Add(CreateStrike().Data);
        
        for (int i = 0; i < 4; i++)
            deck.Add(CreateDefend().Data);
        
        deck.Add(CreateBash().Data);
        
        return deck;
    }
}
