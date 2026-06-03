using System;
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

    // ===== 动态意图计算（延迟查询，每次调用重算） =====

    /// <summary>
    /// 攻击目标选择器——每次调用根据当前战场状态动态决定目标。
    /// 注入时机：<see cref="EnemyEncounter.GetCurrentIntent"/> 为 Attack 意图自动注入。
    /// 若为 null 则退化为无目标攻击（非 Attack 意图）。
    /// </summary>
    public Func<CombatManager, IDamageTarget>? TargetSelector;

    /// <summary>
    /// 伤害计算函数——每次调用重新走 DamageResolver 管线，反映当前力量/易伤等修饰。
    /// 注入时机：<see cref="EnemyEncounter.GetCurrentIntent"/> 为 Attack 意图自动注入。
    /// 若为 null 则返回静态 <see cref="Value"/>。
    /// </summary>
    public Func<CombatManager, int>? DamageCalc;

    // ===== 召唤意图的额外信息（供 UI 提前预览召唤物属性） =====

    /// <summary>召唤物名称（仅 Summon 意图时有效）。</summary>
    public string SummonMinionName;

    /// <summary>召唤物攻击力。</summary>
    public int SummonMinionAttack;

    /// <summary>召唤物生命值。</summary>
    public int SummonMinionHealth;

        /// <summary>召唤物是否具有闪击（入场即可攻击）。</summary>
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
        /// <param name="summonHasCharge">召唤物是否有闪击</param>
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

    // ===== 动态查询方法 =====

    /// <summary>
    /// 获取当前攻击目标（仅 Attack 意图有效）。
    /// 每次调用根据战场实时状态重新计算——若有嘲讽则指向嘲讽随从，反之指向英雄。
    /// </summary>
    /// <param name="combat">战斗管理器</param>
    /// <returns>攻击目标，若 TargetSelector 未注入则返回 null</returns>
    public readonly IDamageTarget? GetTarget(CombatManager combat)
    {
        return TargetSelector?.Invoke(combat);
    }

    /// <summary>
    /// 获取当前有效伤害值（经过所有伤害修饰后的预览值）。
    /// 每次调用重新走 DamageResolver 管线，用于 UI 实时预览和实际执行。
    /// </summary>
    /// <param name="combat">战斗管理器</param>
    /// <returns>有效伤害值</returns>
    public readonly int GetEffectiveDamage(CombatManager combat)
    {
        return DamageCalc?.Invoke(combat) ?? Value;
    }

    /// <summary>
    /// 获取带目标信息的动态意图描述文本（已本地化）。
    /// 根据意图类型和结构化数据动态生成本地化描述。
    /// </summary>
    /// <param name="combat">战斗管理器</param>
    /// <returns>意图 UI 显示文本</returns>
    public readonly string GetDisplayDescription(CombatManager combat)
    {
        return Type switch
        {
            IntentType.Attack => BuildAttackDescription(combat),
            IntentType.Defend => Localization.Localization.T("intent.defend_format", "获得 {amount} 点护甲")
                .Replace("{amount}", Value.ToString()),
            IntentType.Summon => BuildSummonDescription(),
            IntentType.Buff => Localization.Localization.T("intent.buff_format", "{desc}").Replace("{desc}", Description),
            _ => Description
        };
    }

    private readonly string BuildAttackDescription(CombatManager combat)
    {
        var target = TargetSelector?.Invoke(combat);
        int damage = DamageCalc?.Invoke(combat) ?? Value;
        string targetName = target switch
        {
            Hero => Localization.Localization.T("intent.target_hero", "英雄"),
            Minion m => m.GetLocalizedName(),
            _ => Localization.Localization.T("intent.target_unknown", "目标")
        };
        return Localization.Localization.T("intent.attack_format", "对{target}造成 {damage} 点伤害")
            .Replace("{target}", targetName)
            .Replace("{damage}", damage.ToString());
    }

    private readonly string BuildSummonDescription()
    {
        string format = SummonMinionHasCharge
                ? Localization.Localization.T("intent.summon_charge_format", "召唤 {name} ({atk}/{hp} 闪击)")
            : Localization.Localization.T("intent.summon_format", "召唤 {name} ({atk}/{hp})");
        return format
            .Replace("{name}", SummonMinionName)
            .Replace("{atk}", SummonMinionAttack.ToString())
            .Replace("{hp}", SummonMinionHealth.ToString());
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
    /// 敌人的攻击力。影响意图造成的伤害——意图伤害 = 意图基础值 + 攻击力。
    /// 攻击力也可被降低（如离子脉冲），攻击力降低会减少意图伤害。
    /// 最小为 0（攻击力为负会减少意图伤害，但不会让意图变为治疗）。
    /// </summary>
    public int Attack { get; set; }

    // ===== 意图系统 =====

    /// <summary>
    /// 循环意图序列。按顺序逐回合执行，到末尾后回到开头。
    /// </summary>
    protected EnemyIntent[] IntentPattern { get; init; }

    /// <summary>
    /// 当前意图在意图序列中的索引。
    /// </summary>
    public int CurrentPatternIndex { get; private set; }

    /// <summary>
    /// 当前 Attack 意图已解析的的目标缓存。
    /// 在 <see cref="GetCurrentIntent"/> 首次解析时锁定，
    /// <see cref="AdvanceIntent"/> 推进意图时清空。
    /// 保证意图显示和执行阶段攻击同一目标。
    /// </summary>
    protected IDamageTarget? _cachedAttackTarget;

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
        Attack = 0; // 默认无额外攻击力，子类可覆盖
        IntentPattern = intentPattern;
        CurrentPatternIndex = 0;
    }

    // ===== 意图操作 =====

    /// <summary>
    /// 获取当前回合的意图，并根据当前战场状态注入动态选择器。
    /// 对于 Attack 意图，自动注入 <see cref="ResolveAttackTarget"/> 和
    /// 基于 <see cref="DamageResolver.ResolvePreviewDamage"/> 的伤害计算函数。
    /// 调用者每次查询都会获得反映最新战场状态的意图。
    /// </summary>
    /// <param name="combat">战斗管理器，提供战场和目标信息</param>
    /// <returns>包含动态选择器的意图结构体</returns>
    public virtual EnemyIntent GetCurrentIntent(CombatManager combat, Hero self)
    {
        var intent = IntentPattern[CurrentPatternIndex];
        if (intent.Type == IntentType.Attack)
        {
            // 首次解析时锁定目标 — 保证意图显示和执行阶段攻击同一随从。
            _cachedAttackTarget ??= ResolveAttackTarget(combat);
            var cachedTarget = _cachedAttackTarget;
            intent.TargetSelector = _ => cachedTarget;
            intent.DamageCalc = (c) =>
            {
                int baseWithAttack = intent.Value + Attack;
                return DamageResolver.ResolvePreviewDamage(baseWithAttack, self, cachedTarget);
            };
        }
        return intent;
    }

    /// <summary>
    /// 默认攻击目标选择器。
    /// 根据战场实时状态：若玩家方有嘲讽随从则随机选择一个，否则攻击玩家英雄。
    /// 子类可重写以实现特殊的目标选择逻辑（如"总是攻击最左侧随从"）。
    /// </summary>
    /// <param name="combat">战斗管理器</param>
    /// <returns>攻击目标</returns>
    protected virtual IDamageTarget ResolveAttackTarget(CombatManager combat)
    {
        var taunts = combat.Board.GetTaunts(ofEnemy: false);
        if (taunts.Count > 0)
            return taunts[Random.Shared.Next(taunts.Count)];
        return combat.PlayerHero;
    }

    /// <summary>
    /// 将意图索引推进到序列的下一个位置。
    /// 到达序列末尾时循环回到开头。
    /// </summary>
    public virtual void AdvanceIntent()
    {
        _cachedAttackTarget = null;
        CurrentPatternIndex = (CurrentPatternIndex + 1) % IntentPattern.Length;
    }

    /// <summary>
    /// 清空当前攻击目标缓存。
    /// 战场随从、嘲讽或其他会影响合法目标的状态变化后调用，
    /// 让下一次意图刷新重新锁定显示/执行共用的目标。
    /// </summary>
    public void ResetCachedAttackTarget()
    {
        _cachedAttackTarget = null;
    }

    // ===== 意图执行辅助方法 =====

    /// <summary>
    /// 执行攻击意图——对目标造成伤害，若目标是随从则触发反击。
    /// 集中处理攻击流程，避免各敌人类别重复实现，同时确保反击逻辑一致性。
    /// </summary>
    /// <param name="combat">战斗管理器</param>
    protected void ExecuteAttackIntent(CombatManager combat, Hero self)
    {
        var intent = GetCurrentIntent(combat, self);
        var target = intent.GetTarget(combat);
        int effectiveDmg = intent.GetEffectiveDamage(combat);

        if (target is Minion minionTarget)
        {
            combat.TriggerBaitTacticsOnAttacked(minionTarget);

            // 伏击检查：随从有伏击且本回合未消耗时，先手伤害有击杀取消效果
            bool ambush = minionTarget.HasAmbush && !minionTarget.AmbushUsedThisTurn;
            if (ambush) minionTarget.AmbushUsedThisTurn = true;

            // 先造成反击伤害（正常反击 或 伏击先手）
            self.SuppressWeaponCounter = true;
            self.TakeDamage(minionTarget.Attack, minionTarget);
            self.SuppressWeaponCounter = false;
            string label = ambush ? "伏击先手" : "反击";
            GD.Print($"[{Name}] {minionTarget.CardName} {label}，对敌人造成 {minionTarget.Attack} 伤害");

            // 伏击击杀攻击者 → 攻击被取消
            if (ambush && self.IsDead)
            {
                GD.Print($"[{Name}] ☠ 被 {minionTarget.CardName} 伏击击杀，攻击被取消");
                return;
            }

            // 敌方英雄对随从造成伤害
            minionTarget.TakeDamage(effectiveDmg, null);
            GD.Print($"[{Name}] 攻击 {minionTarget.CardName}，造成 {effectiveDmg} 伤害");
        }
        else
        {
            // 敌方英雄攻击玩家英雄（或其他非随从目标）
            target?.TakeDamage(effectiveDmg, self);
        }
    }

    // ===== 抽象执行方法 =====

    /// <summary>
    /// 执行当前意图的具体行为。
    /// 由各具体敌人类实现，直接操作 CombatManager 暴露的 Hero 和 Board。
    /// 调用者应在调用前使用 <see cref="GetCurrentIntent"/> 获取当前意图，
    /// 调用后使用 <see cref="AdvanceIntent"/> 推进到下一意图。
    /// </summary>
    /// <param name="combat">战斗管理器，提供 Board 和 PlayerHero 访问</param>
    /// <param name="self">执行本意图的所属英雄身体</param>
    public abstract void ExecuteIntent(CombatManager combat, Hero self);
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
    public override void ExecuteIntent(CombatManager combat, Hero self)
    {
        var intent = GetCurrentIntent(combat, self);

        GD.Print($"[Cultist] 执行意图：{intent.Description}");

        switch (intent.Type)
        {
            case IntentType.Attack:
                ExecuteAttackIntent(combat, self);
                break;

            case IntentType.Defend:
                self.GainArmor(intent.Value);
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
            new(IntentType.Summon, 1, "召唤 软泥怪 (1/1 闪击)",
                summonName: "软泥怪", summonAttack: 1, summonHealth: 1, summonHasCharge: true),
            new(IntentType.Defend, 4, "获得 4 点护甲")
        })
    {
    }

    /// <inheritdoc />
    public override void ExecuteIntent(CombatManager combat, Hero self)
    {
        var intent = GetCurrentIntent(combat, self);

        GD.Print($"[SlimeBoss] 执行意图：{intent.Description}");

        switch (intent.Type)
        {
            case IntentType.Attack:
                ExecuteAttackIntent(combat, self);
                break;

            case IntentType.Summon:
                TrySummonSlime(combat);
                break;

            case IntentType.Defend:
                self.GainArmor(intent.Value);
                break;
        }
    }

    /// <summary>
        /// 尝试在敌方战场召唤一只 1/1 软泥怪随从（闪击）。
    /// 从 .tres 资源加载，与玩家卡牌同源。
    /// 若战场已满则不执行（最佳尝试策略）。
    /// </summary>
    /// <param name="combat">战斗管理器</param>
    private static void TrySummonSlime(CombatManager combat)
    {
        if (!combat.Board.CanPlaceMinion(isPlayerSide: false))
        {
            GD.Print("[SlimeBoss] 敌方战场已满，软泥怪无法召唤");
            return;
        }

        const string path = "res://Resources/Cards/Minion_Slime.tres";
        if (!ResourceLoader.Exists(path))
        {
            GD.PrintErr($"[SlimeBoss] 未找到软泥怪卡牌资源：{path}");
            return;
        }

        var slimeData = GD.Load<CardData>(path);
        if (slimeData == null)
        {
            GD.PrintErr("[SlimeBoss] 软泥怪卡牌资源加载失败");
            return;
        }

        var slime = new Minion(slimeData, isPlayerSide: false);
        int slot = combat.Board.GetEmptySlotIndex(isPlayerSide: false);
        combat.Board.PlaceMinion(slime, slot);

        GD.Print($"[SlimeBoss] 在敌方槽位 {slot} 召唤了软泥怪（{slime.Attack}/{slime.CurrentHealth}）");
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
    public override void ExecuteIntent(CombatManager combat, Hero self)
    {
        var intent = GetCurrentIntent(combat, self);

        GD.Print($"[WolfRider] 执行意图：{intent.Description}");

        switch (intent.Type)
        {
            case IntentType.Attack:
                ExecuteAttackIntent(combat, self);
                break;
        }
    }
}

/// <summary>
/// 实习机械师 — 召唤型敌人，会召唤机械静螳并为其提供护甲。
/// 意图模式：召唤(1)→增益(5)→增益(5)→增益(5)→...（若机械静螳死亡则重新召唤）。
/// 生命值 20，武器为棍木（攻击力 1）。
/// </summary>
public class ApprenticeMechanic : EnemyEncounter
{
    private const string MechLancerPath = "res://Resources/Cards/Minion_Mech_Lancer.tres";

    /// <summary>自上次召唤以来已执行的增益次数。</summary>
    private int _buffCountSinceLastSummon;

    /// <summary>当前意图是否为增益（用于 AdvanceIntent 计数追踪）。</summary>
    private bool _currentIntentIsBuff;

    /// <summary>
    /// 创建实习机械师遭遇实例。
    /// </summary>
    public ApprenticeMechanic()
        : base("实习机械师", 20, new EnemyIntent[]
        {
            new(IntentType.Summon, 1, "召唤 机械静螳 (4/3 嘲讽 伏击)",
                summonName: "机械静螳", summonAttack: 4, summonHealth: 3)
        })
    {
        Attack = 1; // 棍木武器
    }

    /// <inheritdoc />
    public override EnemyIntent GetCurrentIntent(CombatManager combat, Hero self)
    {
        if (HasFriendlyMechLancer(combat))
        {
            // 意图 B：增益 — 为机械静螳增加 5 点护甲
            _currentIntentIsBuff = true;
            return new EnemyIntent(IntentType.Buff, 5, "使机械静螳获得5点护甲");
        }

        // 意图 A：召唤 — 机械静螳死亡后重新召唤
        _currentIntentIsBuff = false;
        _buffCountSinceLastSummon = 0;
        return IntentPattern[0];
    }

    /// <inheritdoc />
    public override void ExecuteIntent(CombatManager combat, Hero self)
    {
        var intent = GetCurrentIntent(combat, self);

        GD.Print($"[ApprenticeMechanic] 执行意图：{intent.Description} （已增益 {_buffCountSinceLastSummon} 次）");

        switch (intent.Type)
        {
            case IntentType.Summon:
                TrySummonMechLancer(combat);
                break;

            case IntentType.Buff:
                BuffMechLancer(combat);
                break;
        }
    }

    /// <inheritdoc />
    public override void AdvanceIntent()
    {
        ResetCachedAttackTarget();
        if (_currentIntentIsBuff)
        {
            _buffCountSinceLastSummon++;
        }
    }

    /// <summary>
    /// 检查敌方战场上是否存在存活的我方机械静螳。
    /// </summary>
    /// <param name="combat">战斗管理器</param>
    /// <returns>存在机械静螳返回 true</returns>
    private static bool HasFriendlyMechLancer(CombatManager combat)
    {
        foreach (var minion in combat.Board.GetEnemyMinions())
        {
            if (minion.Id == "minion_Mech_Lancer")
                return true;
        }
        return false;
    }

    /// <summary>
    /// 尝试在敌方战场召唤机械静螳（4/3 嘲讽 伏击）。
    /// 若战场已满则跳过（最佳尝试策略）。
    /// 召唤的随从在当前敌方回合不可攻击（由 _enemyMinionsCanAttack 快照机制保证）。
    /// </summary>
    /// <param name="combat">战斗管理器</param>
    private static void TrySummonMechLancer(CombatManager combat)
    {
        if (!combat.Board.CanPlaceMinion(isPlayerSide: false))
        {
            GD.Print("[ApprenticeMechanic] 敌方战场已满，机械静螳无法召唤");
            return;
        }

        if (!ResourceLoader.Exists(MechLancerPath))
        {
            GD.PrintErr($"[ApprenticeMechanic] 未找到机械静螳卡牌资源：{MechLancerPath}");
            return;
        }

        var data = GD.Load<CardData>(MechLancerPath);
        if (data == null)
        {
            GD.PrintErr("[ApprenticeMechanic] 机械静螳卡牌资源加载失败");
            return;
        }

        var mechLancer = new Minion(data, isPlayerSide: false);
        mechLancer.HasTaunt = true; // 召唤时赋予嘲讽（基础卡牌仅有伏击）
        int slot = combat.Board.GetEmptySlotIndex(isPlayerSide: false);
        combat.Board.PlaceMinion(mechLancer, slot);

        GD.Print($"[ApprenticeMechanic] 在敌方槽位 {slot} 召唤了机械静螳（{mechLancer.Attack}/{mechLancer.CurrentHealth} 嘲讽 伏击）");
    }

    /// <summary>
    /// 为战场上所有存活的机械静螳增加 5 点护甲。
    /// </summary>
    /// <param name="combat">战斗管理器</param>
    private static void BuffMechLancer(CombatManager combat)
    {
        bool found = false;
        foreach (var minion in combat.Board.GetEnemyMinions())
        {
            if (minion.Id == "minion_Mech_Lancer" && !minion.IsDead)
            {
                minion.GainArmor(5);
                GD.Print($"[ApprenticeMechanic] 机械静螳获得 5 点护甲，当前护甲：{minion.CurrentArmor}");
                found = true;
            }
        }

        if (!found)
        {
            GD.Print("[ApprenticeMechanic] 战场上没有机械静螳可增益");
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
    public override void ExecuteIntent(CombatManager combat, Hero self)
    {
        var intent = GetCurrentIntent(combat, self);

        GD.Print($"[GuardianBoss] 执行意图：{intent.Description}");

        switch (intent.Type)
        {
            case IntentType.Attack:
                ExecuteAttackIntent(combat, self);
                break;

            case IntentType.Defend:
                self.GainArmor(intent.Value);
                break;
        }
    }
}
