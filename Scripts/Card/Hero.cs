using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.Character;
using OdysseyCards.Core;

namespace OdysseyCards.Card;

/// <summary>
/// 炉石传说风格的英雄类。
/// 包装 <see cref="CommanderCore"/>，添加护甲、英雄技能、武器系统和领域机制。
/// 纯 C# 类，不继承 Godot Node。
/// </summary>
public class Hero : IDamageTarget, IDamageSource
{
    /// <summary>
    /// 伤害修改器列表。动态列表，支持武器技能和状态效果注入修改器。
    /// </summary>
    private readonly List<IDamageModifier> _damageModifiers = new();

    /// <summary>
    /// 被包装的指挥官核心。
    /// </summary>
    private readonly CommanderCore _core;

    /// <summary>
    /// 当前展开的领域效果列表（key=DomainId, value=领域运行时数据）。
    /// </summary>
    private readonly Dictionary<string, ActiveDomain> _activeDomains = new();

    /// <summary>
    /// 状态效果列表（key=效果ID, value=状态效果运行时数据）。
    /// 用于武器禁用等持续性减益效果。
    /// </summary>
    private readonly Dictionary<string, StatusEffect> _statusEffects = new();

    /// <summary>
    /// 本回合已使用武器攻击的次数。
    /// </summary>
    private int _weaponAttacksThisTurn;

    /// <summary>
    /// 武器反击抑制标志。武器主动攻击目标后，目标的反击不应再次触发武器反击，
    /// 以避免无限循环。由 CombatManager 在武器攻击流程中控制。
    /// </summary>
    internal bool SuppressWeaponCounter { get; set; }

    // ===== 关键词属性（英雄也可拥有词条） =====

    /// <summary>
    /// 伏击：每回合第一次被攻击时，先于攻击者造成反击伤害。
    /// 若攻击者被伏击伤害消灭，则攻击被取消。
    /// </summary>
    public bool HasAmbush { get; set; }

    /// <summary>
    /// 冲击：攻击时抵消所有反击伤害（一次性消耗，类似圣盾）。
    /// </summary>
    public bool HasImpact { get; set; }

    /// <summary>
    /// 本回合伏击是否已被消耗。回合开始时重置。
    /// </summary>
    internal bool AmbushUsedThisTurn { get; set; }

    /// <summary>
    /// 重置伏击状态（新回合开始时调用）。
    /// </summary>
    internal void ResetAmbush()
    {
        AmbushUsedThisTurn = false;
    }

    // ===== 指挥官属性（来自 CommanderCore） =====

    /// <summary>
    /// 当前生命值。
    /// </summary>
    public int CurrentHealth => _core.CurrentHealth;

    /// <summary>
    /// 最大生命值。
    /// </summary>
    public int MaxHealth => _core.MaxHealth;

    /// <summary>
    /// 当前法力水晶。
    /// </summary>
    public int CurrentMana => _core.CurrentMana;

    /// <summary>
    /// 最大法力水晶。
    /// </summary>
    public int MaxMana => _core.MaxMana;

    /// <summary>
    /// 牌堆定义。
    /// </summary>
    public Deck Deck => _core.Deck;

    /// <summary>
    /// 战斗中的牌堆状态。
    /// </summary>
    public CombatDeckState DeckState => _core.CombatDeckState;

    /// <summary>
    /// 手牌列表。
    /// </summary>
    public IReadOnlyList<OdysseyCards.Card.Card> Hand => _core.Hand;

    // ===== 英雄独有属性 =====

    /// <summary>
    /// 当前护甲值。护甲在生命值之前吸收伤害。
    /// </summary>
    public int CurrentArmor { get; private set; }

    /// <summary>
    /// 防御力。影响受到伤害的数值：最终伤害 = max(0, 基础伤害 - 防御力)。
    /// 通过 DefenseModifier 在 DamageResolver ADDITIVE 阶段自动生效。
    /// 可为负值表示脆弱状态（受到额外伤害）。
    /// 注意：防御力在护甲之前计算——先减防御，剩余伤害再由护甲吸收。
    /// </summary>
    public int Defense { get; private set; }

    /// <summary>
    /// 修改防御力（正数为增加，负数为减少）。
    /// </summary>
    /// <param name="delta">增减量</param>
    public void ModifyDefense(int delta)
    {
        Defense += delta;
        GD.Print($"[Hero] 防御力变化 {delta:+0;-#}，当前：{Defense}");
    }

    /// <summary>
    /// 英雄技能。可为 null 表示该英雄没有英雄技能。
    /// </summary>
    public IHeroPower HeroPower { get; set; }

    /// <summary>
    /// 当前展开的领域效果（只读）。
    /// </summary>
    public IReadOnlyDictionary<string, ActiveDomain> ActiveDomains => _activeDomains;

    // ===== 武器系统 =====

    /// <summary>
    /// 英雄当前装备的武器。可为 null 表示无武器。
    /// </summary>
    public Weapon? Weapon { get; set; }

    /// <summary>
    /// 本回合已使用武器攻击的次数。
    /// </summary>
    public int WeaponAttacksThisTurn => _weaponAttacksThisTurn;

    /// <summary>
    /// 当前状态效果（只读）。
    /// </summary>
    public IReadOnlyDictionary<string, StatusEffect> StatusEffects => _statusEffects;

    // ===== IDamageTarget 实现 =====

    /// <summary>
    /// 英雄的伤害修改器列表（作为伤害目标）。
    /// </summary>
    public IReadOnlyList<IDamageModifier> DamageModifiers => _damageModifiers;

    /// <summary>
    /// 英雄是否已死亡。
    /// </summary>
    public bool IsDead => _core.IsDead;

    // ===== IDamageSource 实现 =====

    /// <summary>
    /// 英雄的基础攻击力（来自武器）。
    /// </summary>
    int IDamageSource.BaseAttack => Weapon?.Attack ?? 0;

    // ===== 事件 =====

    /// <summary>
    /// 法力值变化事件。参数为 (currentMana, maxMana)。
    /// </summary>
    public event Action<int, int> OnManaChanged
    {
        add => _core.OnManaChanged += value;
        remove => _core.OnManaChanged -= value;
    }

    // ===== 构造函数 =====

    /// <summary>
    /// 创建英雄实例，包装指定的指挥官核心。
    /// </summary>
    /// <param name="core">指挥官核心，不可为 null</param>
    /// <exception cref="ArgumentNullException">当 core 为 null 时抛出</exception>
    public Hero(CommanderCore core)
    {
        _core = core ?? throw new ArgumentNullException(nameof(core));

        // 注册防御力修改器
        _damageModifiers.Add(new DefenseModifier(() => Defense));
    }

    // ===== 伤害处理 =====

    /// <summary>
    /// 受到来自某个伤害来源的基础伤害。
    /// 护甲优先吸收伤害：护甲 > 0 时先消耗护甲，剩余伤害直接应用；
    /// 无护甲时通过 <see cref="DamageResolver"/> 计算最终伤害后应用。
    /// 伤害结算完成后，若英雄持有未被禁用的武器，对攻击者发动反击。
    /// </summary>
    /// <param name="baseDamage">基础伤害值</param>
    /// <param name="source">伤害来源</param>
    public void TakeDamage(int baseDamage, IDamageSource? source)
    {
        // Step 1: ALWAYS go through DamageResolver first (Defense modifier applies here)
        int resolvedDamage = DamageResolver.ResolveDamage(baseDamage, source, this);

        // Step 2: Armor absorbs remaining damage AFTER defense
        if (CurrentArmor > 0)
        {
            int absorbed = Math.Min(CurrentArmor, resolvedDamage);
            CurrentArmor -= absorbed;
            resolvedDamage -= absorbed;

            GD.Print($"[Hero] 护甲吸收了 {absorbed} 点伤害（防御力调整后），剩余护甲：{CurrentArmor}");

            if (resolvedDamage <= 0)
            {
                // 防御+护甲完全吸收，仍然触发武器反击
                CounterAttack(source);
                return;
            }
        }

        ApplyDamage(resolvedDamage, source);

        // Step 3: Weapon counter-attack after damage is settled
        CounterAttack(source);
    }

    /// <summary>
    /// 武器反击逻辑。英雄持有未被禁用的武器时，对攻击者造成等同于武器攻击力的伤害。
    /// 仅当攻击者可被伤害（实现 IDamageTarget）时生效。
    /// 当 SuppressWeaponCounter 为 true 时跳过，用于武器主动攻击时的互砍流程。
    /// </summary>
    /// <param name="source">发起攻击的伤害来源</param>
    private void CounterAttack(IDamageSource source)
    {
        if (SuppressWeaponCounter) return;
        if (Weapon == null || !Weapon.CanCounter) return;
        if (source is not IDamageTarget target) return;

        // 防止自我反击（如疲劳伤害等 source 为自身的情况）
        if (ReferenceEquals(target, this)) return;

        int counterDamage = Weapon.GetModifiedDamage(Weapon.Attack);
        if (counterDamage <= 0) return;

        GD.Print($"[Hero] ⚔ 武器反击！{Weapon.Name} 对攻击者造成 {counterDamage} 点伤害");
        target.ApplyDamage(counterDamage, this);
    }

    /// <summary>
    /// 应用最终计算后的伤害值到英雄生命值。
    /// 实现 <see cref="IDamageTarget"/> 接口方法。
    /// </summary>
    /// <param name="finalDamage">最终伤害值</param>
    /// <param name="source">伤害来源</param>
    public void ApplyDamage(int finalDamage, IDamageSource source)
    {
        _core.ApplyDamage(finalDamage);
        GD.Print($"[Hero] 受到 {finalDamage} 点伤害，剩余生命值：{CurrentHealth}");
    }

    /// <summary>
    /// 为英雄增加护甲值。
    /// </summary>
    /// <param name="amount">护甲增加量</param>
    public void GainArmor(int amount)
    {
        CurrentArmor += amount;
        GD.Print($"[Hero] 获得 {amount} 点护甲，当前护甲：{CurrentArmor}");
    }

    /// <summary>
    /// 为英雄恢复生命值。
    /// </summary>
    /// <param name="amount">恢复量</param>
    public void Heal(int amount)
    {
        _core.Heal(amount);
        GD.Print($"[Hero] 恢复 {amount} 点生命值，当前生命：{CurrentHealth}");
    }

    // ===== 法力水晶管理（委托给 CommanderCore） =====

    /// <summary>
    /// 消耗法力水晶。
    /// </summary>
    /// <param name="amount">消耗量</param>
    public void SpendMana(int amount) => _core.SpendMana(amount);

    /// <summary>
    /// 获得法力水晶。
    /// </summary>
    /// <param name="amount">获得量</param>
    public void GainMana(int amount) => _core.GainMana(amount);

    /// <summary>
    /// 将当前法力水晶重置为最大值。
    /// </summary>
    public void ResetMana() => _core.ResetMana();

    /// <summary>
    /// 设置法力水晶的当前值和最大值。
    /// </summary>
    /// <param name="current">当前法力水晶</param>
    /// <param name="max">最大法力水晶</param>
    public void SetMana(int current, int max) => _core.SetMana(current, max);

    /// <summary>
    /// 检查是否有足够的法力水晶。
    /// </summary>
    /// <param name="amount">需要的法力水晶数量</param>
    /// <returns>足够时返回 true</returns>
    public bool CanSpendMana(int amount) => _core.CanSpendMana(amount);

    // ===== 牌堆操作（委托给 CommanderCore） =====

    /// <summary>
    /// 抽指定数量的卡牌。
    /// </summary>
    /// <param name="count">抽牌数量</param>
    public void DrawCards(int count) => _core.DrawCards(count);

    /// <summary>
    /// 弃掉指定卡牌。
    /// </summary>
    /// <param name="card">要弃掉的卡牌</param>
    public void DiscardCard(Card card) => _core.DiscardCard(card);

    /// <summary>
    /// 从手牌中移除指定卡牌（不进入弃牌堆）。
    /// </summary>
    /// <param name="card">要移除的卡牌</param>
    public void RemoveFromHand(Card card) => _core.RemoveFromHand(card);

    /// <summary>
    /// 将手牌中的卡牌洗回抽牌堆。
    /// </summary>
    /// <param name="card">要洗回的卡牌</param>
    public void ReturnToDrawPile(Card card) => _core.ReturnToDrawPile(card);

    /// <summary>
    /// 洗牌抽牌堆。
    /// </summary>
    public void ShuffleDrawPile() => _core.ShuffleDrawPile();

    /// <summary>
    /// 弃掉所有手牌。
    /// </summary>
    public void DiscardHand() => _core.DiscardHand();

    // ===== 领域管理 =====

    /// <summary>
    /// 展开一个领域效果。若同名领域已存在则叠加层数。
    /// </summary>
    /// <param name="domainId">领域标识</param>
    /// <param name="effectData">领域效果数据</param>
    public void AddDomain(string domainId, Core.CardEffectData effectData)
    {
        if (_activeDomains.TryGetValue(domainId, out var existing))
        {
            existing.StackCount++;
            GD.Print($"[Hero] 领域「{domainId}」叠加至 {existing.StackCount} 层");
        }
        else
        {
            _activeDomains[domainId] = new ActiveDomain(domainId, effectData);
            string desc = effectData.GetDescription();
            string descPart = string.IsNullOrWhiteSpace(desc) ? "" : $"：{desc}";
            GD.Print($"[Hero] 展开领域「{domainId}」{descPart}");
        }
    }

    /// <summary>
    /// 将一张卡牌插入抽牌堆的随机位置。
    /// </summary>
    /// <param name="card">要插入的卡牌实例</param>
    public void InsertCardToDrawPile(Card card)
    {
        _core.InsertCardToDrawPile(card);
    }

    // ===== 状态效果管理 =====

    /// <summary>
    /// 添加一个状态效果。若同名效果已存在则叠加层数。
    /// </summary>
    /// <param name="effect">要添加的状态效果</param>
    public void AddStatusEffect(StatusEffect effect)
    {
        if (_statusEffects.TryGetValue(effect.Id, out var existing))
        {
            existing.Stacks += effect.Stacks;
            GD.Print($"[Hero] 状态「{effect.Id}」叠加到 {existing.Stacks} 层");
        }
        else
        {
            _statusEffects[effect.Id] = effect;
            GD.Print($"[Hero] 获得状态「{effect.Id}」{effect.Stacks} 层");

            // 立即应用状态效果（如武器禁用）
            ApplyStatusEffectImmediate(effect);
        }
    }

    /// <summary>
    /// 移除指定 ID 的状态效果。
    /// </summary>
    /// <param name="id">效果标识符</param>
    public void RemoveStatusEffect(string id)
    {
        if (_statusEffects.Remove(id))
        {
            GD.Print($"[Hero] 状态「{id}」已移除");
            OnStatusEffectRemoved(id);
        }
    }

    /// <summary>
    /// 对指定触发时机的状态效果执行一次衰减计时。
    /// 层数归零的效果将被自动移除。
    /// </summary>
    /// <param name="timing">触发时机</param>
    public void TickStatusEffects(TickTiming timing)
    {
        var expiredIds = new List<string>();

        foreach (var (id, effect) in _statusEffects)
        {
            if (effect.TickOn != timing) continue;

            effect.Tick();
            if (effect.IsExpired)
            {
                expiredIds.Add(id);
            }
        }

        foreach (var id in expiredIds)
        {
            RemoveStatusEffect(id);
        }
    }

    /// <summary>
    /// 状态效果添加时的即时应用逻辑。
    /// 根据效果 ID 执行特定的即时行为（如武器禁用）。
    /// </summary>
    /// <param name="effect">新添加的状态效果</param>
    private void ApplyStatusEffectImmediate(StatusEffect effect)
    {
        switch (effect.Id)
        {
            case "weapon_disabled":
                if (Weapon != null)
                {
                    Weapon.IsDisabled = true;
                    GD.Print($"[Hero] 武器「{Weapon.Name}」已被禁用");
                }
                break;
        }
    }

    /// <summary>
    /// 状态效果移除时的清理逻辑。
    /// 根据效果 ID 执行特定的恢复行为。
    /// </summary>
    /// <param name="id">被移除的效果 ID</param>
    private void OnStatusEffectRemoved(string id)
    {
        switch (id)
        {
            case "weapon_disabled":
                if (Weapon != null && !HasStatusEffect("weapon_disabled"))
                {
                    Weapon.IsDisabled = false;
                    GD.Print($"[Hero] 武器「{Weapon.Name}」已恢复");
                }
                break;
        }
    }

    /// <summary>
    /// 检查是否持有指定 ID 的活跃状态效果。
    /// </summary>
    /// <param name="id">效果标识符</param>
    /// <returns>存在时返回 true</returns>
    public bool HasStatusEffect(string id)
    {
        return _statusEffects.ContainsKey(id) && !_statusEffects[id].IsExpired;
    }

    // ===== 武器攻击追踪 =====

    /// <summary>
    /// 检查英雄当前是否可以使用武器攻击。
    /// 条件：持有武器、武器未被禁用、本回合攻击次数未达上限。
    /// </summary>
    /// <returns>可以攻击返回 true</returns>
    public bool CanWeaponAttack()
    {
        if (Weapon == null) return false;
        if (Weapon.IsDisabled) return false;
        if (Weapon.AttacksPerTurn <= 0) return false;
        return _weaponAttacksThisTurn < Weapon.AttacksPerTurn;
    }

    /// <summary>
    /// 记录一次武器攻击。增加本回合攻击计数。
    /// </summary>
    public void RecordWeaponAttack()
    {
        _weaponAttacksThisTurn++;
        GD.Print($"[Hero] 武器攻击次数：{_weaponAttacksThisTurn}/{Weapon?.AttacksPerTurn ?? 0}");
    }

    /// <summary>
    /// 重置本回合武器攻击计数。回合开始时调用。
    /// </summary>
    public void ResetWeaponAttacks()
    {
        _weaponAttacksThisTurn = 0;
    }

    // ===== 武器主动技能冷却 =====

    /// <summary>
    /// 对武器主动技能执行一次冷却衰减。友方回合开始时调用。
    /// </summary>
    public void TickWeaponCooldown()
    {
        if (Weapon?.ActiveSkill == null) return;

        if (Weapon.ActiveSkill.CurrentCooldown > 0)
        {
            Weapon.ActiveSkill.CurrentCooldown--;
            GD.Print($"[Hero] 武器技能「{Weapon.ActiveSkill.Name}」冷却剩余 {Weapon.ActiveSkill.CurrentCooldown} 回合");
        }
    }
}
