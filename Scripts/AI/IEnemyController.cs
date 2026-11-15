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
	IDecisionProvider DecisionProvider { get; }
	void InitializeDeck(Hero body, IReadOnlyList<CardData> pool, ThemeProfile profile, int seed);
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
