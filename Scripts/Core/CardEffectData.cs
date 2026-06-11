using Godot;

namespace OdysseyCards.Core;

public enum CardEffectType
{
	/// <summary>
	/// 造成伤害。
	/// </summary>
	Damage = 0,

	/// <summary>
	/// 恢复生命值（上限内）。
	/// </summary>
	Heal = 1,

	/// <summary>
	/// 抽牌。
	/// </summary>
	DrawCards = 2,

	/// <summary>
	/// 获得法力水晶（仅本回合）。
	/// </summary>
	GainEnergy = 3,

	/// <summary>
	/// 获得最大生命值（同步回复等量生命值）。
	/// </summary>
	GainMaxHealth = 4,

	/// <summary>
	/// 对目标随从造成伤害。
	/// </summary>
	DealDamageToTarget = 5,

	/// <summary>
	/// 对所有敌方随从造成伤害。
	/// </summary>
	DealDamageToAllEnemies = 6,

	/// <summary>
	/// 对敌方英雄造成伤害。
	/// </summary>
	DealDamageToEnemyHero = 7,

	/// <summary>
	/// 召唤随从。
	/// </summary>
	SummonMinion = 8,

	/// <summary>
	/// 强化随从（+攻击/+生命）。
	/// </summary>
	BuffMinion = 9,

	/// <summary>
	/// [已废弃] 回复生命值。保留以兼容 .tres 数据，新卡牌请使用 Heal。
	/// </summary>
	RestoreHealth = 10,

	/// <summary>
	/// 沉默：移除所有关键词和效果。
	/// </summary>
	Silence = 11,

	/// <summary>
	/// 获得护甲值。
	/// </summary>
	GainArmor = 12,

	/// <summary>
	/// [已废弃] 施加减益。
	/// </summary>
	ApplyDebuff = 13,

	/// <summary>
	/// [已废弃] 施加增益。
	/// </summary>
	ApplyBuff = 14,

	/// <summary>
	/// 自定义效果。
	/// </summary>
	Custom = 17,

	/// <summary>
	/// 获得额外的法力水晶槽（永久增加法力上限）。
	/// Value = 增加的槽数。只增加上限，不增加当前法力。
	/// 可突破自然增长上限，但不超过硬上限(30)。
	/// </summary>
	GainManaSlot = 18,

	/// <summary>
	/// 解除自然增长的水晶槽上限。
	/// 使每回合开始的自动法力增长不再受自然上限(12)限制，可持续增长至硬上限(30)。
	/// Value 不使用。
	/// </summary>
	RemoveNaturalManaCap = 19,

	/// <summary>
	/// 发现：从 N 张随机卡牌中选取一张加入手牌。
	/// Value = 选项数量（默认 3），TargetType = 稀有度过滤（可选，"0"=仅衍生卡，"all"=全部）。
	/// </summary>
	Discover = 20,

	/// <summary>
	/// 对友方英雄造成伤害。
	/// </summary>
	DealDamageToFriendlyHero = 21,

	/// <summary>
	/// 替换目标随从的亡语为「抽 N 张牌」。
	/// Value = 抽牌数量。
	/// </summary>
	ReplaceDeathrattleWithDraw = 22,

	/// <summary>
	/// 使所有玩家区域中的随从获得「被攻击后 +N/+N」。
	/// Value = 攻击和生命成长值。
	/// </summary>
	GrantIdolTwilight = 23,

	/// <summary>
	/// 从弃牌堆中展示 N 张，选择 M 张加入手牌。
	/// Value = 展示数量，SecondaryValue = 选择数量。
	/// </summary>
	ChooseFromDiscard = 24,

	/// <summary>
	/// 随机弃掉手牌。
	/// Value = 弃牌数量。
	/// </summary>
	DiscardRandom = 25,

	/// <summary>
	/// 选择弃掉指定数量的手牌。
	/// Value = 必须弃掉的数量（精确值）。
	/// </summary>
	DiscardChoose = 26,

	/// <summary>
	/// 选择弃掉最多指定数量的手牌（0 ~ N 均可）。
	/// Value = 最大可弃数量。
	/// </summary>
	DiscardChooseUpTo = 27,

	/// <summary>
	/// 将 N 张随机指定标签的卡牌洗入抽牌堆。
	/// Value = 洗入数量，TargetType = CardTag 枚举值名称（如 "Mechanics"）。
	/// </summary>
	ShuffleTribeCards = 28
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
			CardEffectType.DealDamageToFriendlyHero => $"对友方英雄造成{Value}点伤害",
			CardEffectType.ReplaceDeathrattleWithDraw => $"使一个随从失去亡语，获得亡语：抽{Value}张牌",
			CardEffectType.GrantIdolTwilight => $"所有友方随从获得被攻击后+{Value}/+{Value}",
			CardEffectType.ChooseFromDiscard => $"从弃牌堆{Value}张牌中选择{SecondaryValue}张加入手牌",
			CardEffectType.DiscardRandom => $"随机弃掉{Value}张手牌",
			CardEffectType.DiscardChoose => $"选择弃掉{Value}张手牌",
			CardEffectType.DiscardChooseUpTo => $"选择弃掉最多{Value}张手牌",
			CardEffectType.ShuffleTribeCards => $"将{Value}张随机{TargetType}卡牌洗入抽牌堆",
			CardEffectType.SummonMinion => $"召唤{TargetType}",
			CardEffectType.BuffMinion => $"使一个随从获得+{Value}/+{SecondaryValue}",
			CardEffectType.RestoreHealth => $"恢复{Value}点生命值",
			CardEffectType.Silence => "沉默一个随从",
			CardEffectType.GainArmor => $"获得{Value}点护甲",
			CardEffectType.ApplyDebuff => $"施加{TargetType}{Value}层",
			CardEffectType.ApplyBuff => $"获得{TargetType}{Value}层",
			CardEffectType.GainManaSlot => $"获得{Value}个额外的法力水晶槽",
			CardEffectType.RemoveNaturalManaCap => "解除自然增长的水晶槽上限",
			CardEffectType.Discover => $"发现：从{Value}张卡牌中选1张",
			CardEffectType.Custom => CustomEffectName,
			_ => ""
		};
	}
}
