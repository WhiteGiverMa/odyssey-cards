namespace OdysseyCards.Core;

/// <summary>
/// 卡牌关键词（炉石传说风格）。
/// 取代旧的 CardTag 枚举（旧标签基于地图/移动设计，已废弃）。
/// </summary>
public enum Keyword
{
	/// <summary>
	/// 无关键词。
	/// </summary>
	None,

	/// <summary>
	/// 闪击：召唤的回合即可攻击。
	/// </summary>
	Charge,

	/// <summary>
	/// 嘲讽：敌方随从必须优先攻击具有嘲讽的随从。
	/// </summary>
	Taunt,

	/// <summary>
	/// 战吼：从手牌中打出时触发效果。
	/// </summary>
	Battlecry,

	/// <summary>
	/// 亡语：随从死亡时触发效果。
	/// </summary>
	Deathrattle,

	/// <summary>
	/// 风怒：每回合可以攻击两次。
	/// </summary>
	Windfury,

	/// <summary>
	/// 伏击：每回合第一次被攻击时，先于攻击者造成反击伤害。
	/// 若攻击者被伏击伤害消灭，则攻击被取消，攻击者不造成任何伤害。
	/// </summary>
	Ambush,

	/// <summary>
	/// 冲击：攻击时抵消所有反击伤害（一次性消耗，类似圣盾）。
	/// 冲击随从攻击伏击随从时，伏击的先手伤害也被免疫。
	/// </summary>
	Impact,

	/// <summary>
	/// 轮战：法术打出后回到抽牌堆底部（不进入弃牌堆）；
	/// 随从被击败后回到抽牌堆底部（不进入弃牌堆）。
	/// </summary>
	Recycle,

	/// <summary>
	/// 不可打出：此卡牌无法从手牌中主动打出。
	/// 只能通过弃牌、消耗或其他机制从手牌移除。
	/// </summary>
	Unplayable,

	/// <summary>
	/// 虚无：回合结束时自动消耗。
	/// 卡牌在回合结束阶段从手牌中强制消耗，不进入弃牌堆。
	/// </summary>
	Ethereal,

	/// <summary>
	/// 奇巧：被弃掉时将这张牌打出。
	/// 若是领域/法术则直接打出（0费），若是随从则召唤到最左侧空余槽位。
	/// 若没有空余槽位则消失（不进入弃牌堆）。
	/// 参考 STS2 的 Sly 关键词：CardKeyword.Sly → IsSlyThisTurn → AutoPlay。
	/// </summary>
	Qiqiao,
}
