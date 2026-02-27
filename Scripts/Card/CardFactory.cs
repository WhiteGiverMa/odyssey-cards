using Godot;
using System.Collections.Generic;
using OdysseyCards.Core;
using OdysseyCards.Character;

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

        var card = Card.Create(data);
        card.AddEffect((caster, target) =>
        {
            if (target != null)
                target.TakeDamage(6);
        });

        return card;
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

        var card = Card.Create(data);
        card.AddEffect((caster, target) =>
        {
            caster.GainBlock(5);
        });

        return card;
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

        var card = Card.Create(data);
        card.AddEffect((caster, target) =>
        {
            if (target != null)
            {
                target.TakeDamage(8);
            }
        });

        return card;
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

        var card = Card.Create(data);
        card.AddEffect((caster, target) =>
        {
            if (Combat.CombatManager.Instance != null)
            {
                foreach (var enemy in Combat.CombatManager.Instance.Enemies)
                {
                    if (!enemy.IsDead)
                        enemy.TakeDamage(8);
                }
            }
        });

        return card;
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

        var card = Card.Create(data);
        card.AddEffect((caster, target) =>
        {
            caster.GainBlock(5);
            if (target != null)
                target.TakeDamage(5);
        });

        return card;
    }

    public static List<CardData> GetStarterDeck()
    {
        return new List<CardData>
        {
            CreateStrike().Data,
            CreateStrike().Data,
            CreateStrike().Data,
            CreateStrike().Data,
            CreateStrike().Data,
            CreateDefend().Data,
            CreateDefend().Data,
            CreateDefend().Data,
            CreateDefend().Data,
            CreateBash().Data
        };
    }
}
