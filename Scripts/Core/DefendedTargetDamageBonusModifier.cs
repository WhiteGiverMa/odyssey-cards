namespace OdysseyCards.Core;

/// <summary>
/// 对有防御力的目标造成额外伤害。
/// 用于表达“造成的伤害 +X”类来源修改器；该修改器不关心伤害类型，
/// 因此攻击、战吼、法术等只要有明确来源都会生效。
/// </summary>
public sealed class DefendedTargetDamageBonusModifier : IDamageModifier
{
    private readonly int _bonusDamage;
    private readonly int _minimumDefense;

    public DefendedTargetDamageBonusModifier(int bonusDamage, int minimumDefense = 1)
    {
        _bonusDamage = bonusDamage;
        _minimumDefense = minimumDefense;
    }

    public DamagePhase Phase => DamagePhase.ADDITIVE;

    public int ModifyDamageDealt(int currentDamage, DamageContext context)
    {
        if (context.Target == null || context.Target.Defense < _minimumDefense)
        {
            return currentDamage;
        }

        return currentDamage + _bonusDamage;
    }

    public int ModifyDamageTaken(int currentDamage, DamageContext context) => currentDamage;
}
