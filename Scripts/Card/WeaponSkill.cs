using Godot;
using OdysseyCards.Core;

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

    /// <summary>本地化 key —— 用于 UI 层通过 Localization.T() 查找名称翻译。空 = 使用 Name。</summary>
    string NameKey => string.Empty;

    /// <summary>本地化 key —— 用于 UI 层通过 Localization.T() 查找描述翻译。空 = 使用 Description。</summary>
    string DescKey => string.Empty;

    /// <summary>
    /// 修改武器攻击伤害。
    /// </summary>
    /// <param name="baseDamage">基础伤害值</param>
    /// <returns>修改后的伤害值</returns>
    int ModifyWeaponDamage(int baseDamage);

    /// <summary>
    /// 武器命中目标时触发（每次武器攻击/主动技能命中后）。
    /// 默认不执行任何操作。子类可重写以添加命中效果（如熔毁：目标防御-1，最多叠加2层）。
    /// </summary>
    /// <param name="target">被命中的目标</param>
    /// <param name="wielder">持有该武器的英雄</param>
    void OnWeaponHit(IDamageTarget target, Hero wielder) { }
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

    /// <summary>本地化 key —— 用于 UI 层通过 Localization.T() 查找名称翻译。空 = 使用 Name。</summary>
    string NameKey => string.Empty;

    /// <summary>本地化 key —— 用于 UI 层通过 Localization.T() 查找描述翻译。空 = 使用 Description。</summary>
    string DescKey => string.Empty;

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
/// 熔毁 — 离子手枪被动技能。
/// 武器攻击或主动技能命中时，使目标的防御力-1，最多可叠加2层（每个目标独立计数）。
/// 不修改武器攻击的基础伤害。
/// </summary>
public class MeltdownPassive : IWeaponPassive
{
    public string Name => "熔毁";
    public string Description => "武器攻击或主动技能命中时，目标防御力-1（最多2层）";
    public string NameKey => "weapon.passive.meltdown.name";
    public string DescKey => "weapon.passive.meltdown.desc";

    public int ModifyWeaponDamage(int baseDamage)
    {
        // 熔毁不修改武器伤害，仅触发命中效果
        return baseDamage;
    }

    public void OnWeaponHit(IDamageTarget target, Hero wielder)
    {
        if (target is Hero targetHero)
        {
            ApplyMeltdownToHero(targetHero);
        }
        else if (target is Minion targetMinion)
        {
            ApplyMeltdownToMinion(targetMinion);
        }
    }

    private static void ApplyMeltdownToHero(Hero hero)
    {
        hero.StatusEffects.TryGetValue("meltdown", out var existing);
        int currentStacks = existing?.Stacks ?? 0;
        if (currentStacks >= 2)
        {
            GD.Print($"[Meltdown] {hero} 的熔毁已满2层，不再叠加");
            return;
        }

        // 使用 StatusEffect 系统叠加熔毁层数（每层=防御-1，最多2层）
        hero.AddStatusEffect(new StatusEffect(
            id: "meltdown",
            stacks: 1, // 每次命中叠加1层
            tickOn: TickTiming.EnemyTurnEnd
        ));

        hero.ModifyDefense(-1);
        GD.Print($"[Meltdown] {hero} 的防御力-1（熔毁），当前防御力：{hero.Defense}");
    }

    private static void ApplyMeltdownToMinion(Minion minion)
    {
        minion.StatusEffects.TryGetValue("meltdown", out var existing);
        int currentStacks = existing?.Stacks ?? 0;
        if (currentStacks >= 2)
        {
            GD.Print($"[Meltdown] {minion.CardName} 的熔毁已满2层，不再叠加");
            return;
        }

        minion.AddStatusEffect(new StatusEffect(
            id: "meltdown",
            stacks: 1,
            tickOn: TickTiming.EnemyTurnEnd
        ));

        minion.ModifyDefense(-1);
        GD.Print($"[Meltdown] {minion.CardName} 的防御力-1（熔毁），当前防御力：{minion.Defense}");
    }
}

/// <summary>
/// 离子脉冲 — 离子手枪主动技能。
/// 4费，冷却3个友方回合。
/// - 对敌方英雄释放：禁用敌方武器，持续2个敌方回合。
/// - 对敌方随从释放：该随从攻击力变为0，持续2个敌方回合。
/// 目标选择由 CombatManager 通过 CombatManager.ActiveSkillTarget 属性控制。
/// </summary>
public class IonPulse : IWeaponActive
{
    public string Name => "离子脉冲";
    public string Description => "禁用敌人武器（对英雄）或使随从攻击力归零（对随从），持续2个敌方回合";
    public string NameKey => "weapon.skill.ion_pulse.name";
    public string DescKey => "weapon.skill.ion_pulse.desc";
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
        var target = combat.ActiveSkillTarget;

        if (target is Minion targetMinion)
        {
            // 对敌方随从：攻击力归零 2 回合
            targetMinion.AddStatusEffect(new StatusEffect(
                id: "attack_zero",
                stacks: 2,
                tickOn: TickTiming.EnemyTurnEnd
            ));

            GD.Print($"[IonPulse] {targetMinion.CardName} 的攻击力被设为 0（持续 2 个敌方回合）");
            // 注意：attack_zero 的立即应用由 Minion.ApplyStatusEffectImmediate 处理
        }
        else
        {
            // 对敌方英雄：禁用武器（原行为）
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

            GD.Print($"[IonPulse] {enemy} 的武器已被禁用 2 个敌方回合");
        }

        CurrentCooldown = Cooldown;
        wielder.SpendMana(Cost);

        GD.Print($"[IonPulse] 冷却 {Cooldown} 回合");
    }
}

/// <summary>
/// 离子手枪 — 玩家默认武器。
/// 攻击力 2，攻击花费 3 费。被动：熔毁（命中后目标防御-1，最多2层）。
/// 主动：离子脉冲（4 费，禁用敌方武器或使敌方随从攻击归零2回合，冷却 3 回合）。
/// </summary>
public class IonPistol : Weapon
{
    public IonPistol()
        : base(
            name: "离子手枪",
            attack: 2,
            attackCost: 3,
            passive: new MeltdownPassive(),
            active: new IonPulse())
    {
    }

    public override string NameKey => "weapon.ion_pistol.name";
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

    public override string NameKey => "weapon.rolling_log.name";
}
