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
