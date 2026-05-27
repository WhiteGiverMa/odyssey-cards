using Godot;
using OdysseyCards.Card;
using OdysseyCards.Combat;
using OdysseyCards.Core;

namespace OdysseyCards.AI;

// ====================================================================
// 意图类型与数据结构
// ====================================================================

/// <summary>
/// 敌人意图类型。
/// 参考《杀戮尖塔》的意图系统，用简单枚举表示敌人本回合的行为类别。
/// </summary>
public enum IntentType
{
    /// <summary>攻击：对玩家英雄造成伤害。</summary>
    Attack,

    /// <summary>防御：为敌方英雄增加护甲。</summary>
    Defend,

    /// <summary>召唤：在敌方战场召唤随从。</summary>
    Summon,

    /// <summary>增益：强化自身或随从。</summary>
    Buff
}

/// <summary>
/// 敌人意图结构体。
/// 描述敌人本回合将要执行的一个意图，包含类型、数值和显示文本。
/// </summary>
public struct EnemyIntent
{
    /// <summary>意图类型。</summary>
    public IntentType Type;

    /// <summary>意图数值（伤害量、护甲量、召唤数量等）。</summary>
    public int Value;

    /// <summary>意图描述文本，供 UI 展示。</summary>
    public string Description;

    // ===== 召唤意图的额外信息（供 UI 提前预览召唤物属性） =====

    /// <summary>召唤物名称（仅 Summon 意图时有效）。</summary>
    public string SummonMinionName;

    /// <summary>召唤物攻击力。</summary>
    public int SummonMinionAttack;

    /// <summary>召唤物生命值。</summary>
    public int SummonMinionHealth;

    /// <summary>召唤物是否具有冲锋（入场即可攻击）。</summary>
    public bool SummonMinionHasCharge;

    /// <summary>
    /// 创建敌人意图实例。
    /// </summary>
    /// <param name="type">意图类型</param>
    /// <param name="value">意图数值</param>
    /// <param name="description">意图描述文本</param>
    /// <param name="summonName">召唤物名称（仅 Summon 意图）</param>
    /// <param name="summonAttack">召唤物攻击力</param>
    /// <param name="summonHealth">召唤物生命值</param>
    /// <param name="summonHasCharge">召唤物是否有冲锋</param>
    public EnemyIntent(IntentType type, int value, string description,
        string summonName = "", int summonAttack = 0, int summonHealth = 0, bool summonHasCharge = false)
    {
        Type = type;
        Value = value;
        Description = description;
        SummonMinionName = summonName;
        SummonMinionAttack = summonAttack;
        SummonMinionHealth = summonHealth;
        SummonMinionHasCharge = summonHasCharge;
    }
}

// ====================================================================
// 敌人遭遇抽象基类
// ====================================================================

/// <summary>
/// 敌人遭遇抽象基类。
/// 定义敌人的基础属性（名称、生命值）、循环意图模式和执行接口。
/// 纯 C# 类，不继承 Godot Node——英雄由 CombatManager 管理。
/// 参考《杀戮尖塔》的 Monster/Intent 架构设计。
/// </summary>
public abstract class EnemyEncounter
{
    // ===== 基础属性 =====

    /// <summary>
    /// 敌人名称。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 最大生命值。
    /// </summary>
    public int MaxHealth { get; }

    /// <summary>
    /// 当前生命值。随战斗进程变化。
    /// </summary>
    public int CurrentHealth { get; set; }

    /// <summary>
    /// 是否已死亡。
    /// </summary>
    public bool IsDead => CurrentHealth <= 0;

    // ===== 意图系统 =====

    /// <summary>
    /// 循环意图序列。按顺序逐回合执行，到末尾后回到开头。
    /// </summary>
    protected EnemyIntent[] IntentPattern { get; init; }

    /// <summary>
    /// 当前意图在意图序列中的索引。
    /// </summary>
    public int CurrentPatternIndex { get; private set; }

    // ===== 构造函数 =====

    /// <summary>
    /// 创建敌人遭遇实例。
    /// </summary>
    /// <param name="name">敌人名称</param>
    /// <param name="maxHealth">最大生命值</param>
    /// <param name="intentPattern">循环意图序列</param>
    protected EnemyEncounter(string name, int maxHealth, EnemyIntent[] intentPattern)
    {
        Name = name;
        MaxHealth = maxHealth;
        CurrentHealth = maxHealth;
        IntentPattern = intentPattern;
        CurrentPatternIndex = 0;
    }

    // ===== 意图操作 =====

    /// <summary>
    /// 获取当前回合的意图。
    /// </summary>
    /// <returns>当前意图结构体</returns>
    public EnemyIntent GetCurrentIntent()
    {
        return IntentPattern[CurrentPatternIndex];
    }

    /// <summary>
    /// 将意图索引推进到序列的下一个位置。
    /// 到达序列末尾时循环回到开头。
    /// </summary>
    public void AdvanceIntent()
    {
        CurrentPatternIndex = (CurrentPatternIndex + 1) % IntentPattern.Length;
    }

    // ===== 抽象执行方法 =====

    /// <summary>
    /// 执行当前意图的具体行为。
    /// 由各具体敌人类实现，直接操作 CombatManager 暴露的 Hero 和 Board。
    /// 调用者应在调用前使用 <see cref="GetCurrentIntent"/> 获取当前意图，
    /// 调用后使用 <see cref="AdvanceIntent"/> 推进到下一意图。
    /// </summary>
    /// <param name="combat">战斗管理器，提供 PlayerHero、EnemyHero 和 Board 访问</param>
    public abstract void ExecuteIntent(CombatManager combat);
}

// ====================================================================
// 具体敌人类型
// ====================================================================

/// <summary>
/// 邪教徒 — 基础教学敌人。
/// 意图模式：攻击(6) → 攻击(6) → 防御(5) → 循环。
/// 生命值 20，攻击较高但防御薄弱，适合作为第一个遭遇战。
/// </summary>
public class Cultist : EnemyEncounter
{
    /// <summary>
    /// 创建邪教徒遭遇实例。
    /// </summary>
    public Cultist()
        : base("邪教徒", 20, new EnemyIntent[]
        {
            new(IntentType.Attack, 6, "造成 6 点伤害"),
            new(IntentType.Attack, 6, "造成 6 点伤害"),
            new(IntentType.Defend, 5, "获得 5 点护甲")
        })
    {
    }

    /// <inheritdoc />
    public override void ExecuteIntent(CombatManager combat)
    {
        var intent = GetCurrentIntent();

        GD.Print($"[Cultist] 执行意图：{intent.Description}");

        switch (intent.Type)
        {
            case IntentType.Attack:
                combat.PlayerHero.TakeDamage(intent.Value, null);
                break;

            case IntentType.Defend:
                combat.EnemyHero.GainArmor(intent.Value);
                break;
        }
    }
}

/// <summary>
/// 史莱姆首领 — 召唤型敌人。
/// 意图模式：攻击(8) → 召唤(1) → 防御(4) → 循环。
/// 生命值 40，会定期召唤 1/1 软泥怪随从铺场。
/// </summary>
public class SlimeBoss : EnemyEncounter
{
    /// <summary>
    /// 创建史莱姆首领遭遇实例。
    /// </summary>
    public SlimeBoss()
        : base("史莱姆首领", 40, new EnemyIntent[]
        {
            new(IntentType.Attack, 8, "造成 8 点伤害"),
            new(IntentType.Summon, 1, "召唤 软泥怪 (1/1 冲锋)",
                summonName: "软泥怪", summonAttack: 1, summonHealth: 1, summonHasCharge: true),
            new(IntentType.Defend, 4, "获得 4 点护甲")
        })
    {
    }

    /// <inheritdoc />
    public override void ExecuteIntent(CombatManager combat)
    {
        var intent = GetCurrentIntent();

        GD.Print($"[SlimeBoss] 执行意图：{intent.Description}");

        switch (intent.Type)
        {
            case IntentType.Attack:
                combat.PlayerHero.TakeDamage(intent.Value, null);
                break;

            case IntentType.Summon:
                TrySummonSlime(combat);
                break;

            case IntentType.Defend:
                combat.EnemyHero.GainArmor(intent.Value);
                break;
        }
    }

    /// <summary>
    /// 尝试在敌方战场召唤一只 1/1 软泥怪随从。
    /// 若战场已满则不执行（最佳尝试策略）。
    /// </summary>
    /// <param name="combat">战斗管理器</param>
    private void TrySummonSlime(CombatManager combat)
    {
        if (!combat.Board.CanPlaceMinion(isPlayerSide: false))
        {
            GD.Print("[SlimeBoss] 敌方战场已满，软泥怪无法召唤");
            return;
        }

        var slimeData = new CardData
        {
            Id = "slime",
            CardName = "软泥怪",
            Cost = 0,
            Type = CardType.Minion,
            Attack = 1,
            Health = 1,
            Keywords = new Godot.Collections.Array<Keyword> { Keyword.Charge }
        };

        var slime = new Minion(slimeData, isPlayerSide: false);
        int slot = combat.Board.GetEmptySlotIndex(isPlayerSide: false);
        combat.Board.PlaceMinion(slime, slot);

        GD.Print($"[SlimeBoss] 在敌方槽位 {slot} 召唤了软泥怪（1/1）");
    }
}

/// <summary>
/// 狼骑兵 — 速攻型敌人。
/// 意图模式：攻击(5) → 循环（每回合攻击）。
/// 生命值仅 12，但每回合稳定输出，考验玩家的爆发击杀能力。
/// </summary>
public class WolfRider : EnemyEncounter
{
    /// <summary>
    /// 创建狼骑兵遭遇实例。
    /// </summary>
    public WolfRider()
        : base("狼骑兵", 12, new EnemyIntent[]
        {
            new(IntentType.Attack, 5, "造成 5 点伤害")
        })
    {
    }

    /// <inheritdoc />
    public override void ExecuteIntent(CombatManager combat)
    {
        var intent = GetCurrentIntent();

        GD.Print($"[WolfRider] 执行意图：{intent.Description}");

        switch (intent.Type)
        {
            case IntentType.Attack:
                combat.PlayerHero.TakeDamage(intent.Value, null);
                break;
        }
    }
}

/// <summary>
/// 守护者 — 第一位面 Boss。
/// 意图模式：攻击(12) → 防御(8) → 攻击(12) → 循环。
/// 生命值 60，高伤害高耐久，考验玩家的资源管理和爆发能力。
/// </summary>
public class GuardianBoss : EnemyEncounter
{
    /// <summary>
    /// 创建守护者 Boss 遭遇实例。
    /// </summary>
    public GuardianBoss()
        : base("守护者", 60, new EnemyIntent[]
        {
            new(IntentType.Attack, 12, "造成 12 点伤害"),
            new(IntentType.Defend, 8, "获得 8 点护甲"),
            new(IntentType.Attack, 12, "造成 12 点伤害")
        })
    {
    }

    /// <inheritdoc />
    public override void ExecuteIntent(CombatManager combat)
    {
        var intent = GetCurrentIntent();

        GD.Print($"[GuardianBoss] 执行意图：{intent.Description}");

        switch (intent.Type)
        {
            case IntentType.Attack:
                combat.PlayerHero.TakeDamage(intent.Value, null);
                break;

            case IntentType.Defend:
                combat.EnemyHero.GainArmor(intent.Value);
                break;
        }
    }
}
