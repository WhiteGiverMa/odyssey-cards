using Godot;

namespace OdysseyCards.Core;

public enum CardEffectType
{
    Damage,
    Heal,
    DrawCards,
    GainEnergy,
    GainMaxHealth,
    ApplyDebuff,
    ApplyBuff,
    Discard,
    ReturnToDeck,
    SummonUnit,
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
            CardEffectType.GainEnergy => $"获得{Value}点费用",
            CardEffectType.GainMaxHealth => $"总部获得+{Value}生命值",
            CardEffectType.ApplyDebuff => $"施加{TargetType}{Value}层",
            CardEffectType.ApplyBuff => $"获得{TargetType}{Value}层",
            CardEffectType.Discard => $"弃掉{Value}张牌",
            CardEffectType.ReturnToDeck => "返回抽牌堆",
            CardEffectType.SummonUnit => $"召唤{TargetType}",
            CardEffectType.Custom => CustomEffectName,
            _ => ""
        };
    }
}
