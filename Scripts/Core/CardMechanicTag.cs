using System;

namespace OdysseyCards.Core;

/// <summary>
/// 卡牌机制标签（[Flags]）。
/// 描述「这张卡做什么」或「这张卡属于哪一类」的高层语义，供主题卡组生成、ThemeProfile 画像、收藏过滤、
/// 卡牌效果目标过滤（如种族洗牌）使用。
///
/// 与 <see cref="Keyword"/>（战斗关键词）正交：一张卡可同时拥有任意机制标签组合 + 任意关键词组合。
/// 历史上曾存在正交的 <c>CardTag</c>（种族/阵营）维度，仅 <c>Mechanics</c> 一个位；
/// 该维度已合并到本枚举作为 <see cref="Mechanics"/>=65536，<c>CardTag</c> 已删除。
///
/// 设计原则：
///   - 此处收录「机制类别」与「种族/阵营类别」两类语义，统一为单一位掩码维度。
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
///   Mechanics(65536)      — 机械族：原种族标签维度并入后的位；供种族洗牌效果、主题偏好使用
///   SuperBody(131072)     — 超体：neta 和平精英「超体对抗」
///   Intel(262144)         — 情报：揭露暗牌；未用完点数直接溢出且无额外收益
///   PackCore(524288)      — 系列「核心」（1&lt;&lt;19，卡包系列标签位起点）
///   PackUnmanned(1048576) — 系列「无人智胜」
///   PackFutureHunt(2097152)    — 系列「未来追杀」
///   PackSuperBody(4194304)     — 系列「超体计划」
///   PackReserve(8388608)       — 系列「预备」
///   PackVanguard(16777216)     — 系列「先锋」
///   PackCutoffCurrent(33554432) — 系列「断流」（潜艇战主题）
///
/// 系列标签位由 <see cref="CardPackCatalog"/> 维护：查询时按 <c>CardData.PackId</c> 自动匹配，
/// 无需在 .tres 的 MechanicTags 里重复打位。DIY 自定义 PackId 无预置位，需用字符串字段表达。
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

	/// <summary>机械族：原正交维度 <c>CardTag.Mechanics</c> 并入后的位。
	/// 语义上表示「这张卡属于机械族」，供种族洗牌效果（<see cref="Combat.CardEffectDispatcher"/>）过滤、
	/// ThemeProfile 种族偏好加权使用。原 <c>CardTag</c> 枚举已删除。</summary>
	Mechanics = 65536,

	/// <summary>超体：neta 和平精英「超体对抗」玩法。一类机制标签，同直伤/token一样属人工权威标注。
	/// 供「超体插件盒」藏品检索与未来超体联动效果使用。</summary>
	SuperBody = 131072,

	/// <summary>情报：揭露敌方暗牌，剩余点数回复生命或转为格挡。</summary>
	Intel = 262144,

	/// <summary>系列「核心」（卡包机制标签位起点 1&lt;&lt;19）。由 PackId 自动授予。</summary>
	PackCore = 524288,

	/// <summary>系列「无人智胜」。由 PackId 自动授予。</summary>
	PackUnmanned = 1048576,

	/// <summary>系列「未来追杀」（neta 环世界）。由 PackId 自动授予。</summary>
	PackFutureHunt = 2097152,

	/// <summary>系列「超体计划」（neta 和平精英超体对抗）。由 PackId 自动授予。</summary>
	PackSuperBody = 4194304,

	/// <summary>系列「预备」——默认禁用。由 PackId 自动授予。</summary>
	PackReserve = 8388608,

	/// <summary>系列「先锋」——新卡默认归属。由 PackId 自动授予。</summary>
	PackVanguard = 16777216,

	/// <summary>系列「断流」（潜艇战主题，0.2.2）。由 PackId 自动授予。</summary>
	PackCutoffCurrent = 33554432,
}
