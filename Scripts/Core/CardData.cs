using System.Collections.Generic;
using Godot;
using OdysseyCards.Localization;
using Loc = OdysseyCards.Localization.Localization;

namespace OdysseyCards.Core;

/// <summary>
/// 统一的卡牌数据资源。
/// 同时支持随从（Minion）和法术（Spell）两种类型，
/// 取代旧的 UnitData 和 OrderData。
/// </summary>
public partial class CardData : Resource, ICardData, ILocalizable
{
	// ===== 公共字段（所有卡牌类型共享） =====

	[Export] public CardType Type { get; set; } = CardType.Minion;
	[Export] public string Id { get; set; } = "";
	[Export] public string CardName { get; set; } = "Unnamed Card";
	[Export] public string Description { get; set; } = "";
	[Export] public CardRarity Rarity { get; set; } = CardRarity.Common;
	[Export] public Texture2D Artwork { get; set; }

	/// <summary>
	/// 法力值消耗（部署费用）。
	/// </summary>
	[Export] public int Cost { get; set; } = 1;

	/// <summary>
	/// 行动花费——随从攻击时额外消耗的法力值。
	/// 类似 KARDS，攻击从法力池中扣除。仅随从牌有效。
	/// </summary>
	[Export] public int ActionCost { get; set; } = 0;

	/// <summary>
	/// 领域标识（仅领域牌有效）。
	/// 同名领域叠加层数，不同领域独立存在。
	/// </summary>
	[Export] public string DomainId { get; set; } = "";

	// ===== 随从专属字段 =====

	/// <summary>
	/// 攻击力（仅随从）。
	/// </summary>
	[Export] public int Attack { get; set; } = 1;

	/// <summary>
	/// 生命值（仅随从）。
	/// </summary>
	[Export] public int Health { get; set; } = 1;

	/// <summary>
	/// 防御力（仅随从）。影响受到的伤害：伤害 = max(0, 伤害 - 防御力)。可为负值。
	/// </summary>
	[Export] public int Defense { get; set; } = 0;

	/// <summary>
	/// 对防御力不低于 1 的目标造成的额外伤害。
	/// 这是来源侧“造成的伤害”修改器，攻击、战吼和其他带来源的效果都会计算。
	/// </summary>
	[Export] public int BonusDamageToDefendedTargets { get; set; } = 0;

	/// <summary>
	/// 机制标签（多标签 [Flags] 系统）。描述「这张卡做什么」或「属于哪一类」的高层语义，
	/// 供主题卡组生成、ThemeProfile 画像、收藏过滤、卡牌效果目标过滤使用。
	/// 与 <see cref="Keywords"/>（战斗关键词）正交：一张卡可同时拥有任意机制标签组合 + 任意关键词组合。
	/// 历史上的 <c>CardTag</c>（种族/阵营）维度已合并入本字段（<c>Mechanics</c> 位=65536）。
	/// 详见 <see cref="CardMechanicTag"/> 的设计原则。
	/// </summary>
	[Export(PropertyHint.Flags, "DirectDamage:1,DamageOverTime:2,Heal:4,Armor:8,Draw:16,Discover:32,Summon:64,Buff:128,Silence:256,Discard:512,Domain:1024,WeaponSynergy:2048,ManaRamp:4096,StatusApply:8192,Shuffle:16384,Token:32768,Mechanics:65536,SuperBody:131072")]
	public CardMechanicTag MechanicTags { get; set; } = CardMechanicTag.None;

	/// <summary>
	/// 关键词列表。
	/// Battlecry / Deathrattle 是随从专属关键词；非随从不应配置这两个关键词，
	/// 主题卡组的 KeywordWeights 也按这个数据契约理解「战吼/亡语随从」偏好。
	/// </summary>
	[Export] public Godot.Collections.Array<Keyword> Keywords { get; set; } = new();

	/// <summary>
	/// 战吼效果（仅随从，打出时触发）。
	/// </summary>
	[Export] public Godot.Collections.Array<CardEffectData> BattlecryEffects { get; set; } = new();

	/// <summary>
	/// 亡语效果（仅随从，死亡时触发）。
	/// </summary>
	[Export] public Godot.Collections.Array<CardEffectData> DeathrattleEffects { get; set; } = new();

	// ===== 法术专属字段 =====

	/// <summary>
	/// 法术效果列表。
	/// </summary>
	[Export] public Godot.Collections.Array<CardEffectData> Effects { get; set; } = new();

	/// <summary>
	/// 是否需要选择目标。
	/// </summary>
	[Export] public bool RequiresTarget { get; set; } = false;

	/// <summary>
	/// 目标过滤条件（仅当 RequiresTarget=true 时有效）。
	/// 子集匹配规则：filter 是实体标签的子集即为合法目标。
	/// None 表示不限制，可对任意目标释放。
	/// </summary>
	[Export(PropertyHint.Flags, "Friendly:1,Enemy:2,Hero:4,Minion:8")]
	public TargetTags TargetFilter { get; set; } = TargetTags.None;

	/// <summary>
	/// 目标排除条件（仅当 RequiresTarget=true 时有效）。
	/// 排除规则：同时拥有所有排除标签的实体不可选。
	/// None 表示不排除任何目标。
	/// 例：ExcludeFilter=Friendly|Hero 表示"除己方英雄外均可选"。
	/// </summary>
	[Export(PropertyHint.Flags, "Friendly:1,Enemy:2,Hero:4,Minion:8")]
	public TargetTags ExcludeFilter { get; set; } = TargetTags.None;

	// ===== 本地化支持 =====

	public string LocalizationPrefix => "cards";

	public string LocalizationId => Id;

	public LocalStr Local(string field, Dictionary<string, object> parameters = null)
	{
		return new LocalStr($"cards.{Id}.{field}", parameters);
	}

	public bool HasLocal(string field)
	{
		return Loc.HasKey($"cards.{Id}.{field}");
	}

	public string GetLocalizedName()
	{
		return this.Local("name").Resolve();
	}

	public string GetLocalizedDescription(Dictionary<string, object> parameters = null)
	{
		return this.Local("description", parameters).Resolve();
	}

	// ===== 便捷查询方法 =====

	/// <summary>
	/// 检查随从是否拥有指定关键词。
	/// </summary>
	public bool HasKeyword(Keyword keyword)
	{
		return Keywords.Contains(keyword);
	}

	/// <summary>
	/// 检查是否拥有指定机制标签。
	/// </summary>
	public bool HasMechanicTag(CardMechanicTag tag)
	{
		return MechanicTags.HasFlag(tag);
	}

	/// <summary>
	/// 是否为随从牌。
	/// </summary>
	public bool IsMinion => Type == CardType.Minion;

	/// <summary>
	/// 是否为法术牌。
	/// </summary>
	public bool IsSpell => Type == CardType.Spell;
}
