namespace OdysseyCards.Card;

/// <summary>
/// 英雄武器。
/// 英雄出场自带，不可替换。使用武器攻击消耗法力水晶并造成武器攻击力伤害，
/// 攻击目标的反击伤害遵循「打谁即被谁攻击力反击」规则。
/// 纯 C# 类，不继承 Godot Node。
/// </summary>
public class Weapon
{
    /// <summary>
    /// 武器名称。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 基础攻击力。使用武器攻击时造成的基础伤害值。
    /// </summary>
    public int Attack { get; }

    /// <summary>
    /// 使用武器攻击消耗的法力水晶数量。
    /// 敌方武器此值为 0（敌人无费用概念）。
    /// </summary>
    public int AttackCost { get; }

    /// <summary>
    /// 每回合最大可攻击次数。默认 1。
    /// </summary>
    public int AttacksPerTurn { get; init; } = 1;

    /// <summary>
    /// 被动技能。可为 null 表示无被动效果。
    /// </summary>
    public IWeaponPassive? PassiveSkill { get; }

    /// <summary>
    /// 主动技能。可为 null 表示无主动技能。
    /// </summary>
    public IWeaponActive? ActiveSkill { get; }

    /// <summary>
    /// 武器是否被禁用。禁用时无法攻击也无法反击。
    /// 由 StatusEffect 系统控制（如 "weapon_disabled" 减益）。
    /// </summary>
    public bool IsDisabled { get; set; }

    /// <summary>
    /// 计算最终武器伤害。应用被动技能修改后返回。
    /// </summary>
    /// <param name="baseDamage">基础伤害（通常为 Attack）</param>
    /// <returns>修改后的伤害值</returns>
    public int GetModifiedDamage(int baseDamage)
    {
        if (IsDisabled) return 0;
        return PassiveSkill?.ModifyWeaponDamage(baseDamage) ?? baseDamage;
    }

    /// <summary>
    /// 武器是否可以造成反击伤害。
    /// 条件：未被禁用且攻击力大于 0。
    /// </summary>
    public bool CanCounter => !IsDisabled && Attack > 0;

    /// <summary>
    /// 创建武器实例。
    /// </summary>
    /// <param name="name">武器名称</param>
    /// <param name="attack">基础攻击力</param>
    /// <param name="attackCost">武器攻击法力消耗</param>
    /// <param name="passive">被动技能（可选）</param>
    /// <param name="active">主动技能（可选）</param>
    public Weapon(string name, int attack, int attackCost,
        IWeaponPassive? passive = null, IWeaponActive? active = null)
    {
        Name = name;
        Attack = attack;
        AttackCost = attackCost;
        PassiveSkill = passive;
        ActiveSkill = active;
    }
}
