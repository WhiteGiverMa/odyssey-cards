using System;

namespace OdysseyCards.Core;

/// <summary>
/// 卡牌机制标签（[Flags]）。
/// 描述「这张卡做什么」的高层语义，供主题卡组生成、ThemeProfile 画像、收藏过滤使用。
/// 与 <see cref="CardTag"/>（种族/阵营）正交：一张卡可同时拥有种族标签和机制标签。
///
/// 设计原则：
///   - 此处只收录「机制类别」，不收录战斗关键词（闪击/嘲讽/亡语等见 <see cref="Keyword"/>）。
///   - 标签由人工填写，权威数据；不从 CardEffectType 自动推导——同一 EffectType 在不同 Value/Target
///     下语义可能不同（Damage 既可能是直伤也可能是 AOE 解场），自动推导会让 ThemeProfile 不可预测。
///   - None 是合法值，表示「通用卡」，ThemeProfile 计算时按 0 权重处理（稀释主题但不破坏主题）。
///
/// 当前已定义的标签（按位掩码递增，预留扩展空间）：
///   DirectDamage(1)       — 直伤：直接造成伤害的卡（含单体/AOE/英雄直击）
///   DamageOverTime(2)     — 持续伤害：以状态形式延迟结算的伤害（如理惠持续伤害状态）
///   Heal(4)               — 治疗：恢复生命/最大生命
///   Armor(8)              — 护甲：获得护甲值
///   Draw(16)              — 抽牌：从抽牌堆获取卡牌
///   Discover(32)          — 发现/检索：选择性获取卡牌（Discover/ChooseFromDiscard）
///   Summon(64)            — 召唤：直接召唤随从或将卡牌洗入抽牌堆
///   Buff(128)             — 增益：强化随从（+攻/+血/关键词授予）
///   Silence(256)          — 沉默/解场：移除关键词或处理敌方威胁
///   Discard(512)          — 弃牌：主动弃牌或弃牌联动
///   Domain(1024)          — 领域：持续性场地效果（CardType.Domain 或领域联动卡）
///   WeaponSynergy(2048)   — 武器协同：与武器攻击/武器技能联动
///   ManaRamp(4096)        — 法力增益：增加法力槽/突破上限/临时增益
///   StatusApply(8192)     — 状态施加：给目标挂 StatusEffect（含增益/减益）
///   Shuffle(16384)        — 洗牌：将某牌洗入抽牌堆（干洗/衍生洗入）
///   Token(32768)          — 衍生牌：将某指定牌加入手牌（Token 生成）
/// </summary>
[Flags]
public enum CardMechanicTag
{
	/// <summary>无机制标签（通用卡）。</summary>
	None = 0,

	/// <summary>直伤：直接造成伤害的卡（含单体/AOE/英雄直击）。</summary>
	DirectDamage = 1,

	/// <summary>持续伤害：以状态形式延迟结算的伤害。</summary>
	DamageOverTime = 2,

	/// <summary>治疗：恢复生命或最大生命值。</summary>
	Heal = 4,

	/// <summary>护甲：获得护甲值。</summary>
	Armor = 8,

	/// <summary>抽牌：从抽牌堆获取卡牌。</summary>
	Draw = 16,

	/// <summary>发现/检索：选择性获取卡牌（Discover/ChooseFromDiscard）。
	/// 语义上是 <see cref="Token"/> 的真子集——所有 Discover 都是 Token 的特例，
	/// 打此标签的卡应同时打 <see cref="Token"/>。</summary>
	Discover = 32,

	/// <summary>召唤：直接召唤随从或将卡牌洗入抽牌堆。</summary>
	Summon = 64,

	/// <summary>增益：强化随从（+攻/+血/关键词授予）。</summary>
	Buff = 128,

	/// <summary>沉默/解场：移除关键词或处理敌方威胁。</summary>
	Silence = 256,

	/// <summary>弃牌：主动弃牌或弃牌联动。</summary>
	Discard = 512,

	/// <summary>领域：持续性场地效果（CardType.Domain 或领域联动卡）。</summary>
	Domain = 1024,

	/// <summary>武器协同：与武器攻击/武器技能联动。</summary>
	WeaponSynergy = 2048,

	/// <summary>法力增益：增加法力槽/突破上限/临时增益。</summary>
	ManaRamp = 4096,

	/// <summary>状态施加：给目标挂 StatusEffect（含增益/减益）。</summary>
	StatusApply = 8192,

	/// <summary>洗牌：将某牌洗入抽牌堆（干洗/衍生洗入）。
	/// 与 <see cref="Summon"/> 的区别：Summon 强调「召唤随从到棋盘」，
	/// Shuffle 强调「洗入某牌到抽牌堆」。</summary>
	Shuffle = 16384,

	/// <summary>衍生牌：将某指定牌加入手牌（Token 生成）。
	/// <see cref="Discover"/> 是其真子集——所有 Discover 都是 Token 的特例。</summary>
	Token = 32768,
}
