using Godot;
using OdysseyCards.Core;
using System;
using System.Collections.Generic;

namespace OdysseyCards.Card;

/// <summary>
/// 运行时随从类。
/// 代表战场上的一个随从单位，继承自 Card 基类，
/// 同时实现 IDamageSource（造成伤害）和 IDamageTarget（承受伤害）接口。
/// 遵循炉石传说风格的战斗模型，不继承 Godot Node。
/// </summary>
public class Minion : Card, IDamageSource, IDamageTarget
{
    /// <summary>
    /// 空的伤害修改器列表，用于满足接口要求。
    /// 后续可扩展为支持 Buff/光环等修改器。
    /// </summary>
    private static readonly IReadOnlyList<IDamageModifier> _emptyModifiers = Array.Empty<IDamageModifier>();

    // ===== 随从基础属性 =====

    /// <summary>
    /// 随从的攻击力。
    /// </summary>
    public int Attack { get; private set; }

    /// <summary>
    /// 修改攻击力（正数为增加，负数为减少，最小为 0）。
    /// </summary>
    /// <param name="delta">增减量</param>
    public void ModifyAttack(int delta)
    {
        Attack = Math.Max(0, Attack + delta);
    }

    /// <summary>
    /// 基础攻击力（等同于 Attack，用于接口兼容）。
    /// </summary>
    public int BaseAttack => Attack;

    /// <summary>
    /// 当前生命值。
    /// </summary>
    public int CurrentHealth { get; private set; }

    /// <summary>
    /// 最大生命值。
    /// </summary>
    public int MaxHealth { get; private set; }

    /// <summary>
    /// 在战场上的槽位索引。
    /// -1 表示尚未放置在战场上。
    /// </summary>
    public int BoardSlotIndex { get; set; } = -1;

    /// <summary>
    /// 是否属于玩家方。
    /// </summary>
    public bool IsPlayerSide { get; }

    /// <summary>
    /// 随从是否已死亡。
    /// </summary>
    public bool IsDead => CurrentHealth <= 0;

    // ===== 关键词属性 =====

    /// <summary>
    /// 冲锋：召唤的回合即可攻击。
    /// </summary>
    public bool HasCharge { get; }

    /// <summary>
    /// 嘲讽：敌方随从必须优先攻击此随从。
    /// </summary>
    public bool HasTaunt { get; }

    /// <summary>
    /// 战吼：从手牌中打出时触发效果。
    /// </summary>
    public bool HasBattlecry { get; }

    /// <summary>
    /// 亡语：随从死亡时触发效果。
    /// </summary>
    public bool HasDeathrattle { get; }

    /// <summary>
    /// 风怒：每回合可以攻击两次。
    /// </summary>
    public bool HasWindfury { get; }

    // ===== 效果访问器 =====

    /// <summary>
    /// 战吼效果列表（来源自卡牌数据）。
    /// </summary>
    public IReadOnlyList<CardEffectData> BattlecryEffects => Data.BattlecryEffects;

    /// <summary>
    /// 亡语效果列表（来源自卡牌数据）。
    /// </summary>
    public IReadOnlyList<CardEffectData> DeathrattleEffects => Data.DeathrattleEffects;

    // ===== IDamageSource 实现 =====

    /// <summary>
    /// 攻击方伤害修改器列表（当前返回空列表）。
    /// </summary>
    public IReadOnlyList<IDamageModifier> DamageModifiers => _emptyModifiers;

    // ===== 构造函数 =====

    /// <summary>
    /// 创建随从运行时实例。
    /// </summary>
    /// <param name="data">卡牌数据资源（必须是随从类型）</param>
    /// <param name="isPlayerSide">是否属于玩家方</param>
    public Minion(CardData data, bool isPlayerSide)
        : base(data)
    {
        IsPlayerSide = isPlayerSide;

        // 从卡牌数据复制战斗属性
        Attack = data.Attack;
        MaxHealth = data.Health;
        CurrentHealth = MaxHealth;

        // 解析关键词
        HasCharge = data.HasKeyword(Keyword.Charge);
        HasTaunt = data.HasKeyword(Keyword.Taunt);
        HasBattlecry = data.HasKeyword(Keyword.Battlecry) || data.BattlecryEffects?.Count > 0;
        HasDeathrattle = data.HasKeyword(Keyword.Deathrattle) || data.DeathrattleEffects?.Count > 0;
        HasWindfury = data.HasKeyword(Keyword.Windfury);
    }

    // ===== 伤害与治疗 =====

    /// <summary>
    /// 受到来自某个伤害来源的基础伤害。
    /// 通过 DamageResolver 统一计算最终伤害值，然后应用。
    /// </summary>
    /// <param name="baseDamage">基础伤害值</param>
    /// <param name="source">伤害来源</param>
    public void TakeDamage(int baseDamage, IDamageSource source)
    {
        int result = DamageResolver.ResolveDamage(baseDamage, source, this);
        ApplyDamage(result, source);
    }

    /// <summary>
    /// 应用最终计算后的伤害值。
    /// 实现 IDamageTarget 接口方法，扣除实际生命值并输出日志。
    /// </summary>
    /// <param name="finalDamage">最终伤害值（已通过所有修改器）</param>
    /// <param name="source">伤害来源</param>
    public void ApplyDamage(int finalDamage, IDamageSource source)
    {
        CurrentHealth -= finalDamage;
        GD.Print($"{CardName} 受到 {finalDamage} 点伤害，剩余生命值：{CurrentHealth}");

        // 未来可在此处触发受伤事件（如苦痛侍僧抽牌等）
    }

    /// <summary>
    /// 为随从恢复生命值，不超过最大生命值。
    /// </summary>
    /// <param name="amount">恢复量</param>
    public void Heal(int amount)
    {
        int beforeHeal = CurrentHealth;
        CurrentHealth = Math.Min(MaxHealth, CurrentHealth + amount);
        int healed = CurrentHealth - beforeHeal;

        if (healed > 0)
        {
            GD.Print($"{CardName} 恢复了 {healed} 点生命值，当前生命值：{CurrentHealth}");
        }
    }

    // ===== 信息方法 =====

    /// <summary>
    /// 获取随从信息摘要。
    /// 格式如「石拳食人魔 | 4费 4/5」。
    /// </summary>
    /// <returns>随从信息字符串</returns>
    public override string GetCardInfo()
    {
        return $"{CardName} | {Cost}费 {Attack}/{CurrentHealth}";
    }
}
