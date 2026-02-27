using Godot;
using OdysseyCards.Card.Effects;

namespace OdysseyCards.Core;

public partial class CardEffectData : Resource
{
    [Export] public CardEffectType EffectType { get; set; } = CardEffectType.Damage;
    [Export] public int Value { get; set; } = 6;
    [Export] public int Times { get; set; } = 1;
    [Export] public string DebuffType { get; set; } = "";
    [Export] public bool Upgraded { get; set; } = false;

    public CardEffect CreateEffect()
    {
        CardEffect effect = EffectType switch
        {
            CardEffectType.Damage => new Card.DamageEffect(Value),
            CardEffectType.GainBlock => new Card.GainBlockEffect(Value),
            CardEffectType.GainEnergy => new Card.GainEnergyEffect(Value),
            CardEffectType.DrawCards => new Card.DrawCardsEffect(Value),
            CardEffectType.ApplyDebuff => new Card.ApplyDebuffEffect(DebuffType, Value),
            CardEffectType.Heal => new Card.HealEffect(Value),
            _ => null
        };

        if (effect != null && Upgraded)
        {
            effect.Upgrade();
        }

        return effect;
    }
}
