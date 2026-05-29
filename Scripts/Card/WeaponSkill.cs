using Godot;

namespace OdysseyCards.Card;

// ====================================================================
// 武器技能接口
// ====================================================================

/// <summary>
/// 武器被动技能接口。
/// 被动效果常驻生效，无需手动触发。
/// </summary>
public interface IWeaponPassive
{
    /// <summary>技能名称。</summary>
    string Name { get; }

    /// <summary>技能描述文本。</summary>
    string Description { get; }

    /// <summary>
    /// 修改武器攻击伤害。
    /// </summary>
    /// <param name="baseDamage">基础伤害值</param>
    /// <returns>修改后的伤害值</returns>
    int ModifyWeaponDamage(int baseDamage);
}

/// <summary>
/// 武器主动技能接口。
/// 主动技能需要手动触发，有法力消耗和冷却时间。
/// </summary>
public interface IWeaponActive
{
    /// <summary>技能名称。</summary>
    string Name { get; }

    /// <summary>技能描述文本。</summary>
    string Description { get; }

    /// <summary>法力消耗。</summary>
    int Cost { get; }

    /// <summary>冷却回合数。使用后需等待此数量的友方回合才能再次使用。</summary>
    int Cooldown { get; }

    /// <summary>当前剩余冷却回合数。0 表示可用。</summary>
    int CurrentCooldown { get; set; }

    /// <summary>
    /// 检查技能是否可在当前状态下使用。
    /// </summary>
    /// <param name="wielder">使用该武器的英雄</param>
    /// <returns>可以使用时返回 true</returns>
    bool CanUse(Hero wielder);

    /// <summary>
    /// 执行技能效果。
    /// </summary>
    /// <param name="wielder">使用该武器的英雄</param>
    /// <param name="combat">战斗管理器</param>
    void Execute(Hero wielder, Combat.CombatManager combat);
}

// ====================================================================
// 玩家默认武器：离子手枪
// ====================================================================

/// <summary>
/// 功率放大 — 离子手枪被动技能。
/// 武器攻击的伤害 +50%。
/// </summary>
public class PowerAmplifier : IWeaponPassive
{
    public string Name => "功率放大";
    public string Description => "武器攻击的伤害+50%";

    public int ModifyWeaponDamage(int baseDamage)
    {
        int modified = (int)(baseDamage * 1.5);
        GD.Print($"[PowerAmplifier] {baseDamage} → {modified}（+50%）");
        return modified;
    }
}

/// <summary>
/// 离子脉冲 — 离子手枪主动技能。
/// 禁用敌人的武器，持续 2 个敌方回合。冷却 3 个友方回合。
/// </summary>
public class IonPulse : IWeaponActive
{
    public string Name => "离子脉冲";
    public string Description => "禁用敌人的武器，持续2个敌方回合";
    public int Cost => 4;
    public int Cooldown => 3;
    public int CurrentCooldown { get; set; }

    public bool CanUse(Hero wielder)
    {
        if (CurrentCooldown > 0) return false;
        if (wielder.CurrentMana < Cost) return false;
        return true;
    }

    public void Execute(Hero wielder, Combat.CombatManager combat)
    {
        var enemy = combat.EnemyHero;
        enemy.AddStatusEffect(new StatusEffect(
            id: "weapon_disabled",
            stacks: 2,
            tickOn: TickTiming.EnemyTurnEnd
        ));

        // 立即应用禁用状态
        if (enemy.Weapon != null)
        {
            enemy.Weapon.IsDisabled = true;
        }

        CurrentCooldown = Cooldown;
        wielder.SpendMana(Cost);

        GD.Print($"[IonPulse] {enemy} 的武器已被禁用 2 个敌方回合（冷却 {Cooldown} 回合）");
    }
}

/// <summary>
/// 离子手枪 — 玩家默认武器。
/// 攻击力 2，攻击花费 3 费。被动：功率放大（+50% 武器伤害）。
/// 主动：离子脉冲（4 费，禁用敌方武器 2 回合，冷却 3 回合）。
/// </summary>
public class IonPistol : Weapon
{
    public IonPistol()
        : base(
            name: "离子手枪",
            attack: 2,
            attackCost: 3,
            passive: new PowerAmplifier(),
            active: new IonPulse())
    {
    }
}

// ====================================================================
// 敌方默认武器：棍木
// ====================================================================

/// <summary>
/// 棍木 — 敌方默认武器。
/// 攻击力 1，无攻击花费，纯被动（仅用于反击伤害）。
/// 无主动/被动技能。
/// </summary>
public class RollingLog : Weapon
{
    public RollingLog()
        : base(
            name: "棍木",
            attack: 1,
            attackCost: 0)
    {
    }
}
