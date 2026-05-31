using Godot;
using OdysseyCards.Core;
using System;
using System.Collections.Generic;
using System.Linq;

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
    /// 伤害修改器列表（防御力修改器等）。
    /// </summary>
    private readonly List<IDamageModifier> _damageModifiers = new();

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
    /// 防御力。影响受到的伤害：最终伤害 = max(0, 基础伤害 - 防御力)。
    /// 通过 DefenseModifier 在 DamageResolver ADDITIVE 阶段自动生效。
    /// 可为负值表示脆弱（受到额外伤害）。
    /// </summary>
    public int Defense { get; private set; }

    /// <summary>
    /// 修改防御力（正数为增加，负数为减少）。
    /// 最大值不设上限，最小值不设下限（允许负防御表示脆弱状态）。
    /// </summary>
    /// <param name="delta">增减量</param>
    public void ModifyDefense(int delta)
    {
        Defense += delta;
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

    /// <summary>
    /// 计算此随从的目标标签掩码。
    /// 用于目标选择系统的合法性验证。
    /// </summary>
    public TargetTags GetTargetTags()
    {
        return (IsPlayerSide ? TargetTags.Friendly : TargetTags.Enemy) | TargetTags.Minion;
    }

    // ===== 关键词属性 =====

    /// <summary>
    /// 闪击：召唤的回合即可攻击。
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

    /// <summary>
    /// 伏击：每回合第一次被攻击时，先于攻击者造成反击伤害。
    /// 若攻击者被伏击伤害消灭，则攻击被取消。
    /// </summary>
    public bool HasAmbush { get; }

    /// <summary>
    /// 冲击：攻击时抵消所有反击伤害（一次性消耗，类似圣盾）。
    /// 冲击随从攻击伏击随从时，伏击的先手伤害也被免疫。
    /// </summary>
    public bool HasImpact { get; internal set; }

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
    /// 伤害修改器列表。包含防御力修改器等，由 DamageResolver 在各阶段调用。
    /// </summary>
    public IReadOnlyList<IDamageModifier> DamageModifiers => _damageModifiers;

    /// <summary>
    /// 随从的意图大脑。非 null 时，EnemyMinionsAttack 优先用此执行意图，
    /// 而不是默认攻击逻辑。机械小蠊等有自定义意图的随从设置此字段。
    /// </summary>
    public AI.IIntentActor? IntentBrain { get; set; }

    // ===== 状态效果系统 =====

    private readonly Dictionary<string, StatusEffect> _statusEffects = new();

    public IReadOnlyDictionary<string, StatusEffect> StatusEffects => _statusEffects;

    public void AddStatusEffect(StatusEffect effect)
    {
        if (_statusEffects.TryGetValue(effect.Id, out var existing))
        {
            existing.Stacks += effect.Stacks;
            GD.Print($"[Minion:{CardName}] 状态「{effect.Id}」叠加到 {existing.Stacks} 层");
        }
        else
        {
            _statusEffects[effect.Id] = effect;
            GD.Print($"[Minion:{CardName}] 获得状态「{effect.Id}」{effect.Stacks} 层");
            ApplyStatusEffectImmediate(effect);
        }
    }

    public void RemoveStatusEffect(string id)
    {
        if (_statusEffects.Remove(id))
        {
            GD.Print($"[Minion:{CardName}] 状态「{id}」已移除");
            OnStatusEffectRemoved(id);
        }
    }

    public void TickStatusEffects(TickTiming timing)
    {
        var expiredIds = _statusEffects
            .Where(kv => kv.Value.TickOn == timing)
            .Select(kv => { kv.Value.Tick(); return kv; })
            .Where(kv => kv.Value.IsExpired)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var id in expiredIds)
        {
            RemoveStatusEffect(id);
        }
    }

    public bool HasStatusEffect(string id)
    {
        return _statusEffects.ContainsKey(id) && !_statusEffects[id].IsExpired;
    }

    private void ApplyStatusEffectImmediate(StatusEffect effect)
    {
        switch (effect.Id)
        {
            case "attack_zero":
                Attack = 0;
                GD.Print($"[Minion:{CardName}] 攻击力被设为 0");
                break;
            case "meltdown":
                // 熔毁：防御力-1，最多叠加2层
                int currentStacks = HasStatusEffect("meltdown") ? _statusEffects["meltdown"].Stacks : 0;
                if (currentStacks <= 2) // 允许第1层和第2层触发
                {
                    ModifyDefense(-1);
                    GD.Print($"[Minion:{CardName}] 熔毁！防御力 {Defense}（已叠{currentStacks}层）");
                }
                break;
        }
    }

    private void OnStatusEffectRemoved(string id)
    {
        switch (id)
        {
            case "attack_zero":
                if (!HasStatusEffect("attack_zero"))
                {
                    Attack = Data.Attack; // 恢复到原始攻击力
                    GD.Print($"[Minion:{CardName}] 攻击力已恢复为 {Attack}");
                }
                break;
        }
    }

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

        // 复制防御力
        Defense = data.Defense;

        // 注册防御力修改器到 DamageModifiers
        _damageModifiers.Add(new OdysseyCards.Core.DefenseModifier(() => Defense));

        if (data.BonusDamageToDefendedTargets != 0)
        {
            _damageModifiers.Add(new DefendedTargetDamageBonusModifier(data.BonusDamageToDefendedTargets));
        }

        // 解析关键词
        HasCharge = data.HasKeyword(Keyword.Charge);
        HasTaunt = data.HasKeyword(Keyword.Taunt);
        HasBattlecry = data.HasKeyword(Keyword.Battlecry) || data.BattlecryEffects?.Count > 0;
        HasDeathrattle = data.HasKeyword(Keyword.Deathrattle) || data.DeathrattleEffects?.Count > 0;
        HasWindfury = data.HasKeyword(Keyword.Windfury);
        HasAmbush = data.HasKeyword(Keyword.Ambush);
        HasImpact = data.HasKeyword(Keyword.Impact);
    }

    // ===== 伤害与治疗 =====

    /// <summary>
    /// 受到来自某个伤害来源的基础伤害。
    /// 通过 DamageResolver 统一计算最终伤害值，然后应用。
    /// </summary>
    /// <param name="baseDamage">基础伤害值</param>
    /// <param name="source">伤害来源</param>
    public void TakeDamage(int baseDamage, IDamageSource? source)
    {
        TakeDamage(baseDamage, source, DamageKind.Attack);
    }

    /// <summary>
    /// 受到指定类型的基础伤害。
    /// </summary>
    /// <param name="baseDamage">基础伤害值</param>
    /// <param name="source">伤害来源</param>
    /// <param name="kind">伤害结算类型</param>
    public void TakeDamage(int baseDamage, IDamageSource? source, DamageKind kind)
    {
        int result = DamageResolver.ResolveDamage(baseDamage, source, this, kind);
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
        string defenseStr = Defense != 0 ? $" 防{Defense}" : "";
        return $"{CardName} | {Cost}费 {Attack}/{CurrentHealth}{defenseStr}";
    }
}
