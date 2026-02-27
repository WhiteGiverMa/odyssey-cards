using Godot;
using OdysseyCards.Character;

namespace OdysseyCards.Card.Effects;

public enum CardEffectType
{
    Damage,
    GainBlock,
    GainEnergy,
    DrawCards,
    ApplyDebuff,
    Heal,
    SelfDamage,
    GainStrength,
    GainDexterity,
    DoubleBlock,
    CopyCard
}

public abstract class CardEffect
{
    public CardEffectType Type { get; protected set; }
    public int Value { get; set; }
    public int Times { get; set; } = 1;
    public string DebuffType { get; set; }
    public bool IsUpgraded { get; set; }

    public abstract void Execute(Character caster, Character target);

    public virtual void Upgrade()
    {
        IsUpgraded = true;
        OnUpgraded();
    }

    protected virtual void OnUpgraded() { }
}
