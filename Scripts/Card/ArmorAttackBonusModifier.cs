using System;
using OdysseyCards.Core;

namespace OdysseyCards.Card;

/// <summary>
/// 护甲攻击加成修改器。
/// 当随从拥有护甲时，在 ADDITIVE 阶段为其造成的伤害提供 +2 加成。
/// 适用于机甲枪骑兵 (Mech_Lancer) 等具有「有护甲时获得攻击力」被动的随从。
/// </summary>
internal sealed class ArmorAttackBonusModifier : IDamageModifier
{
    private readonly Minion _minion;

    public ArmorAttackBonusModifier(Minion minion)
    {
        _minion = minion ?? throw new ArgumentNullException(nameof(minion));
    }

    public DamagePhase Phase => DamagePhase.ADDITIVE;

    public int ModifyDamageDealt(int currentDamage, DamageContext context)
    {
        return _minion.HasArmor ? currentDamage + 2 : currentDamage;
    }

    public int ModifyDamageTaken(int currentDamage, DamageContext context) => currentDamage;
}
