using Godot;

namespace OdysseyCards.Core;

public enum CardEffectType
{
    /// <summary>
    /// 造成伤害。
    /// </summary>
    Damage,

    /// <summary>
    /// 恢复生命值。
    /// </summary>
    Heal,

    /// <summary>
    /// 抽牌。
    /// </summary>
    DrawCards,

    /// <summary>
    /// 获得法力水晶（仅本回合）。
    /// </summary>
    GainEnergy,

    /// <summary>
    /// 获得最大生命值。
    /// </summary>
    GainMaxHealth,

    /// <summary>
    /// 对目标随从造成伤害。
    /// </summary>
    DealDamageToTarget,

    /// <summary>
    /// 对所有敌方随从造成伤害。
    /// </summary>
    DealDamageToAllEnemies,

    /// <summary>
    /// 对敌方英雄造成伤害。
    /// </summary>
    DealDamageToEnemyHero,

    /// <summary>
    /// 召唤随从。
    /// </summary>
    SummonMinion,

    /// <summary>
    /// 强化随从（+攻击/+生命）。
    /// </summary>
    BuffMinion,

    /// <summary>
    /// 回复生命值。
    /// </summary>
    RestoreHealth,

    /// <summary>
    /// 沉默：移除所有关键词和效果。
    /// </summary>
    Silence,

    /// <summary>
    /// 获得护甲值。
    /// </summary>
    GainArmor,

    // 保留旧值以兼容现有数据
    /// <summary>
    /// [已废弃] 施加减益。
    /// </summary>
    ApplyDebuff,

    /// <summary>
    /// [已废弃] 施加增益。
    /// </summary>
    ApplyBuff,

    /// <summary>
    /// [已废弃] 弃牌。
    /// </summary>
    Discard,

    /// <summary>
    /// [已废弃] 返回牌库。
    /// </summary>
    ReturnToDeck,

    /// <summary>
    /// 自定义效果。
    /// </summary>
    Custom
}

public partial class CardEffectData : Resource
{
    [Export] public CardEffectType EffectType { get; set; } = CardEffectType.Damage;
    [Export] public int Value { get; set; } = 0;
    [Export] public int SecondaryValue { get; set; } = 0;
    [Export] public string TargetType { get; set; } = "default";
    [Export] public string CustomEffectName { get; set; } = "";

    public string GetDescription()
    {
        return EffectType switch
        {
            CardEffectType.Damage => $"造成{Value}点伤害",
            CardEffectType.Heal => $"恢复{Value}点生命值",
            CardEffectType.DrawCards => $"抽{Value}张牌",
            CardEffectType.GainEnergy => $"获得{Value}点法力值",
            CardEffectType.GainMaxHealth => $"获得+{Value}最大生命值",
            CardEffectType.DealDamageToTarget => $"对一个随从造成{Value}点伤害",
            CardEffectType.DealDamageToAllEnemies => $"对所有敌方随从造成{Value}点伤害",
            CardEffectType.DealDamageToEnemyHero => $"对敌方英雄造成{Value}点伤害",
            CardEffectType.SummonMinion => $"召唤{TargetType}",
            CardEffectType.BuffMinion => $"使一个随从获得+{Value}/+{SecondaryValue}",
            CardEffectType.RestoreHealth => $"恢复{Value}点生命值",
            CardEffectType.Silence => "沉默一个随从",
            CardEffectType.GainArmor => $"获得{Value}点护甲",
            CardEffectType.ApplyDebuff => $"施加{TargetType}{Value}层",
            CardEffectType.ApplyBuff => $"获得{TargetType}{Value}层",
            CardEffectType.Discard => $"弃掉{Value}张牌",
            CardEffectType.ReturnToDeck => "返回抽牌堆",
            CardEffectType.Custom => CustomEffectName,
            _ => ""
        };
    }
}
