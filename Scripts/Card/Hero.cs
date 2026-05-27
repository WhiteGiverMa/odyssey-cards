using System;
using System.Collections.Generic;
using Godot;
using OdysseyCards.Character;
using OdysseyCards.Core;

namespace OdysseyCards.Card;

/// <summary>
/// 炉石传说风格的英雄类。
/// 包装 <see cref="CommanderCore"/>，添加护甲和英雄技能机制。
/// 纯 C# 类，不继承 Godot Node。
/// </summary>
public class Hero : IDamageTarget
{
    /// <summary>
    /// 空的伤害修改器列表，当前英雄未实现伤害修改。
    /// </summary>
    private static readonly IReadOnlyList<IDamageModifier> _emptyModifiers = Array.Empty<IDamageModifier>();

    /// <summary>
    /// 被包装的指挥官核心。
    /// </summary>
    private readonly CommanderCore _core;

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
    /// 英雄技能。可为 null 表示该英雄没有英雄技能。
    /// </summary>
    public IHeroPower HeroPower { get; set; }

    // ===== IDamageTarget 实现 =====

    /// <summary>
    /// 英雄的伤害修改器列表。当前返回空列表。
    /// </summary>
    public IReadOnlyList<IDamageModifier> DamageModifiers => _emptyModifiers;

    /// <summary>
    /// 英雄是否已死亡。
    /// </summary>
    public bool IsDead => _core.IsDead;

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
    }

    // ===== 伤害处理 =====

    /// <summary>
    /// 受到来自某个伤害来源的基础伤害。
    /// 护甲优先吸收伤害：护甲 > 0 时先消耗护甲，剩余伤害直接应用；
    /// 无护甲时通过 <see cref="DamageResolver"/> 计算最终伤害后应用。
    /// </summary>
    /// <param name="baseDamage">基础伤害值</param>
    /// <param name="source">伤害来源</param>
    public void TakeDamage(int baseDamage, IDamageSource source)
    {
        if (CurrentArmor > 0)
        {
            int absorbed = Math.Min(CurrentArmor, baseDamage);
            CurrentArmor -= absorbed;
            baseDamage -= absorbed;

            GD.Print($"[Hero] 护甲吸收了 {absorbed} 点伤害，剩余护甲：{CurrentArmor}");

            if (baseDamage <= 0)
                return;

            // 剩余伤害穿透护甲，直接应用（绕过伤害修改器）
        }
        else
        {
            // 无护甲 → 通过统一伤害解析器计算最终伤害
            baseDamage = DamageResolver.ResolveDamage(baseDamage, source, this);
        }

        ApplyDamage(baseDamage, source);
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
}
