using Godot;
using OdysseyCards.Character;
using OdysseyCards.Card.Effects;

namespace OdysseyCards.Card;

public class DamageEffect : CardEffect
{
    public DamageEffect()
    {
        Type = CardEffectType.Damage;
    }

    public DamageEffect(int damage) : this()
    {
        Value = damage;
    }

    public override void Execute(Character caster, Character target)
    {
        if (target == null) return;
        
        int damage = Value;
        
        for (int i = 0; i < Times; i++)
        {
            target.TakeDamage(damage);
        }
    }

    protected override void OnUpgraded()
    {
        Value = (int)(Value * 1.5f);
    }
}

public class GainBlockEffect : CardEffect
{
    public GainBlockEffect()
    {
        Type = CardEffectType.GainBlock;
    }

    public GainBlockEffect(int block) : this()
    {
        Value = block;
    }

    public override void Execute(Character caster, Character target)
    {
        if (caster == null) return;
        
        int block = Value;
        
        for (int i = 0; i < Times; i++)
        {
            caster.GainBlock(block);
        }
    }

    protected override void OnUpgraded()
    {
        Value = (int)(Value * 1.5f);
    }
}

public class GainEnergyEffect : CardEffect
{
    public GainEnergyEffect()
    {
        Type = CardEffectType.GainEnergy;
    }

    public GainEnergyEffect(int energy) : this()
    {
        Value = energy;
    }

    public override void Execute(Character caster, Character target)
    {
        if (caster == null) return;
        
        for (int i = 0; i < Times; i++)
        {
            caster.GainEnergy(Value);
        }
    }

    protected override void OnUpgraded()
    {
        Value += 1;
    }
}

public class DrawCardsEffect : CardEffect
{
    public DrawCardsEffect()
    {
        Type = CardEffectType.DrawCards;
    }

    public DrawCardsEffect(int count) : this()
    {
        Value = count;
    }

    public override void Execute(Character caster, Character target)
    {
        if (caster is not Player player) return;
        
        for (int i = 0; i < Times; i++)
        {
            player.DrawCards(Value);
        }
    }

    protected override void OnUpgraded()
    {
        Value += 1;
    }
}

public class ApplyDebuffEffect : CardEffect
{
    public ApplyDebuffEffect()
    {
        Type = CardEffectType.ApplyDebuff;
    }

    public ApplyDebuffEffect(string debuffType, int stacks) : this()
    {
        DebuffType = debuffType;
        Value = stacks;
    }

    public override void Execute(Character caster, Character target)
    {
        if (target == null) return;
        
        for (int i = 0; i < Times; i++)
        {
            ApplyDebuffToTarget(target, DebuffType, Value);
        }
    }

    private void ApplyDebuffToTarget(Character target, string debuffType, int stacks)
    {
        switch (debuffType.ToLower())
        {
            case "vulnerable":
                target.AddDebuff(new VulnerableDebuff { Stacks = stacks });
                break;
            case "weak":
                target.AddDebuff(new WeakDebuff { Stacks = stacks });
                break;
            case "poison":
                target.AddDebuff(new PoisonDebuff { Stacks = stacks });
                break;
        }
    }

    protected override void OnUpgraded()
    {
        Value += 1;
    }
}

public class HealEffect : CardEffect
{
    public HealEffect()
    {
        Type = CardEffectType.Heal;
    }

    public HealEffect(int amount) : this()
    {
        Value = amount;
    }

    public override void Execute(Character caster, Character target)
    {
        Character targetChar = target ?? caster;
        if (targetChar == null) return;
        
        targetChar.Heal(Value);
    }

    protected override void OnUpgraded()
    {
        Value = (int)(Value * 1.5f);
    }
}

public class VulnerableDebuff : Debuff
{
    public VulnerableDebuff() { Name = "Vulnerable"; }
}

public class WeakDebuff : Debuff
{
    public WeakDebuff() { Name = "Weak"; }
}

public class PoisonDebuff : Debuff
{
    public PoisonDebuff() { Name = "Poison"; }
}
