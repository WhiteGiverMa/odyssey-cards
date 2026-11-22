using System.Collections.Generic;
using OdysseyCards.Card;
using OdysseyCards.Combat;
using OdysseyCards.Core;

namespace OdysseyCards.AI;

/// <summary>敌方独立 actor 的控制轨道；身体和牌堆仍由 EnemyUnit.Body 持有。</summary>
public interface IEnemyController
{
	void ExecuteTurn(CombatManager combat, EnemyUnit unit);
}

public interface IIntentDrivenEnemy : IEnemyController, IIntentActor
{
}

public interface IDeckDrivenCommander : IEnemyController
{
	string HeroId { get; }

	/// <summary>目标卡组厚度（普通 10~20 / 精英 21~39 / Boss 40）。</summary>
	int TargetDeckSize { get; }

	/// <summary>牌手自带卡组画像；null 表示由 CombatManager 回退加载主题资源。</summary>
	DeckProfile? DeckProfile { get; }

	IDecisionProvider DecisionProvider { get; }
	EnemyStepKind LastActionKind { get; }
	void InitializeDeck(Hero body, IReadOnlyList<CardData> pool, DeckProfile profile, int seed);
	void BeginTurn(Hero body);
	bool TryExecuteSingleBestAction(CombatManager combat, EnemyUnit unit);
	void ArmRemainingCountermeasures(CombatManager combat, Hero body);
}

/// <summary>原意图大脑的控制轨道，不持有独立血量或手牌。</summary>
internal sealed class IntentDrivenEnemy(EnemyUnit unit) : IIntentDrivenEnemy
{
	public Hero? OwnerHero => unit.Body;
	public bool HasMoveStates => unit.Brain.HasMoveStates;
	public EnemyIntent GetCurrentIntent(CombatManager combat) => unit.Brain.GetCurrentIntent(combat, unit.Body);
	public void ExecuteIntent(CombatManager combat) => unit.Brain.ExecuteIntent(combat, unit.Body);
	public void AdvanceIntent() => unit.Brain.AdvanceIntent();
	public MoveState? GetCurrentMove(CombatManager combat) => unit.Brain.GetCurrentMove(combat, unit.Body);
	public void AdvanceMove() => unit.Brain.AdvanceMove();
	public void ExecuteTurn(CombatManager combat, EnemyUnit owner)
	{
		ExecuteIntent(combat);
		AdvanceMove();
	}
}

/// <summary>
/// 敌方意图可见性判定——纯 C#，供战斗规则、UI 与 xUnit 共用。
/// 同构牌手（<see cref="IDeckDrivenCommander"/>）不暴露静态意图：
/// 英雄身份卡只保留顶部手牌栏，指挥官随从由评分贪心控制器行动，
/// 因此既不能画静态意图箭头，也不能写槽位意图文字。
/// 旧意图敌人（非 <see cref="IDeckDrivenCommander"/>）与无主随从保持兼容。
/// </summary>
public static class EnemyIntentVisibility
{
	/// <summary>英雄是否应显示静态意图（攻击/增益箭头与意图文字）。</summary>
	/// <param name="controller">敌方单位的控制轨道；null 视为旧意图怪，保持可见。</param>
	public static bool ShouldShowHeroIntent(IEnemyController? controller)
		=> controller is not IDeckDrivenCommander;

	/// <summary>
	/// 随从是否归同构牌手所有——<see cref="Minion.OwnerHero"/> 与某个
	/// <see cref="IDeckDrivenCommander"/> 的身体是同一实例。
	/// </summary>
	/// <param name="minion">待判定随从；null 或未归属时返回 false。</param>
	/// <param name="enemyUnits">当前战斗的敌方单位集合。</param>
	public static bool IsCommanderMinion(Minion? minion, IEnumerable<EnemyUnit>? enemyUnits)
	{
		Hero? owner = minion?.OwnerHero;
		if (owner == null || enemyUnits == null)
			return false;

		foreach (var unit in enemyUnits)
		{
			if (ReferenceEquals(unit.Body, owner) && unit.Controller is IDeckDrivenCommander)
				return true;
		}

		return false;
	}

	/// <summary>随从是否应显示静态意图箭头与槽位文字；指挥官随从一律隐藏。</summary>
	/// <param name="minion">待判定随从。</param>
	/// <param name="enemyUnits">当前战斗的敌方单位集合。</param>
	public static bool ShouldShowMinionIntent(Minion? minion, IEnumerable<EnemyUnit>? enemyUnits)
		=> !IsCommanderMinion(minion, enemyUnits);
}
