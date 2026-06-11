using OdysseyCards.Card;
using OdysseyCards.Combat;

namespace OdysseyCards.AI;

/// <summary>
/// 敌方战斗单位——将 Hero 身体和 EnemyEncounter 大脑配对。
/// 每个 EnemyUnit 代表战斗中的一个敌方 actor，拥有独立 HP、武器、意图状态机。
/// 多敌人战斗即为多个 EnemyUnit 实例。
/// </summary>
public class EnemyUnit : IIntentActor
{
	/// <summary>
	/// 敌方英雄身体（HP、护甲、武器、状态效果等）。
	/// </summary>
	public Hero Body { get; }

	/// <summary>
	/// 敌方 AI 大脑（意图模式、目标选择、意图执行）。
	/// </summary>
	public EnemyEncounter Brain { get; }

	Hero? IIntentActor.OwnerHero => Body;

	public bool HasMoveStates => Brain.HasMoveStates;

	/// <summary>
	/// 创建敌方战斗单位。
	/// </summary>
	/// <param name="body">敌方英雄身体实例</param>
	/// <param name="brain">敌方 AI 遭遇定义</param>
	public EnemyUnit(Hero body, EnemyEncounter brain)
	{
		Body = body ?? throw new System.ArgumentNullException(nameof(body));
		Brain = brain ?? throw new System.ArgumentNullException(nameof(brain));
	}

	public EnemyIntent GetCurrentIntent(CombatManager combat)
		=> Brain.GetCurrentIntent(combat, Body);

	public void ExecuteIntent(CombatManager combat)
		=> Brain.ExecuteIntent(combat, Body);

	public void AdvanceIntent()
		=> Brain.AdvanceIntent();

	/// <summary>
	/// 获取当前 MoveState（新意图系统）。
	/// 委托给 Brain 查询。
	/// </summary>
	/// <param name="combat">战斗管理器</param>
	/// <returns>当前 MoveState</returns>
	public MoveState? GetCurrentMove(CombatManager combat)
		=> Brain.GetCurrentMove(combat, Body);

	public void AdvanceMove()
		=> Brain.AdvanceMove();
}
