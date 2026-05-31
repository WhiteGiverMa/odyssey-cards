namespace OdysseyCards.Core;

/// <summary>
/// 伤害结算类型。
/// Attack 表示随从/英雄/武器的攻击伤害，会正常受到目标防御力影响；
/// Effect 表示战吼、法术、亡语等效果伤害，不受目标防御力减免，
/// 但仍会计算来源的“造成的伤害”修改器。
/// </summary>
public enum DamageKind
{
    Attack,
    Effect,
}
