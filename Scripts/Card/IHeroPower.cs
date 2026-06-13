namespace OdysseyCards.Card;

/// <summary>
/// 英雄技能接口。
/// 定义炉石传说风格的英雄技能契约。
/// 当前为占位接口，具体实现将在后续版本中完成。
/// </summary>
public interface IHeroPower
{
	/// <summary>
	/// 英雄技能名称。
	/// </summary>
	string Name { get; }

	/// <summary>
	/// 英雄技能的法力水晶消耗。
	/// </summary>
	int Cost { get; }

	/// <summary>
	/// 英雄技能的描述文本。
	/// </summary>
	string Description { get; }

	/// <summary>
	/// 检查英雄技能是否可以在当前状态下使用。
	/// </summary>
	/// <param name="hero">使用技能的英雄</param>
	/// <returns>可以使用时返回 true</returns>
	bool CanUse(Hero hero);

	/// <summary>
	/// 执行英雄技能效果。
	/// </summary>
	/// <param name="hero">使用技能的英雄</param>
	/// <param name="combatManager">
	/// 战斗管理器对象。因 CombatManager 尚未创建，使用 object 类型占位。
	/// </param>
	void Execute(Hero hero, object combatManager);
}

/// <summary>
/// 有冷却与可存储层数的技能。
/// </summary>
public interface IChargeCooldownSkill
{
	int Charges { get; }
	int MaxCharges { get; }
	int Cooldown { get; }
	int CurrentCooldown { get; }
	void TickChargeCooldown();
}
