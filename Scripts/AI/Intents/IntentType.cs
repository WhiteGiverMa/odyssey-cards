namespace OdysseyCards.AI.Intents;

/// <summary>
/// 意图类型枚举（扩展版）。
/// 参考《杀戮尖塔2》的意图系统设计，从原有的 4 种意图扩展到 15 种，
/// 覆盖攻击、防御、增益、减益、召唤、逃跑、睡眠、眩晕等全部敌人行为类别。
/// </summary>
public enum IntentType
{
	/// <summary>攻击：对单个目标造成伤害。</summary>
	Attack,

	/// <summary>多重攻击：对目标造成多次伤害。</summary>
	MultiAttack,

	/// <summary>斩杀攻击：对低生命值目标造成额外伤害。</summary>
	DeathBlow,

	/// <summary>增益：强化自身或友方单位。</summary>
	Buff,

	/// <summary>减益：削弱敌方单位。</summary>
	Debuff,

	/// <summary>强力减益：造成严重的负面效果。</summary>
	DebuffStrong,

	/// <summary>防御：获得护甲或格挡。</summary>
	Defend,

	/// <summary>治疗：恢复生命值。</summary>
	Heal,

	/// <summary>召唤：在战场上召唤随从。</summary>
	Summon,

	/// <summary>逃跑：脱离战斗。</summary>
	Escape,

	/// <summary>睡眠：本回合不行动，进入休眠状态。</summary>
	Sleep,

	/// <summary>眩晕：本回合无法行动。</summary>
	Stun,

	/// <summary>状态牌：向玩家牌堆中添加状态牌。</summary>
	StatusCard,

	/// <summary>隐藏意图：意图对玩家不可见。</summary>
	Hidden,

	/// <summary>施法：敌人打出一张具名卡牌/法术（本项目特有，塔2无）。</summary>
	SpellCast,

	/// <summary>未知意图：意图类型尚未确定。</summary>
	Unknown
}
