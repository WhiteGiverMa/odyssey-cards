using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Combat;
using OdysseyCards.Core;
using Xunit;

namespace OdysseyCards.Tests.Unit;

public class ActionTrackerTests
{
	[Theory]
	[InlineData(UnitClass.Vanguard)]
	[InlineData(UnitClass.Assault)]
	[InlineData(UnitClass.Support)]
	[InlineData(UnitClass.Skirmisher)]
	public void Deployment_WithoutCharge_CannotActUntilNextTurn(UnitClass type)
	{
		var tracker = new AttackTracker();
		var board = new Board();
		var minion = new Minion("unit", true, unitClass: type);
		board.PlaceMinion(minion, 0);
		tracker.RegisterDeployment(minion);
		Assert.False(tracker.CanMove(minion));
		Assert.False(tracker.CanAttack(minion));
		tracker.Reset();
		tracker.AddCharged(minion);
		Assert.True(tracker.CanMove(minion));
		Assert.True(tracker.CanAttack(minion));
	}

	[Theory]
	[InlineData(UnitClass.Vanguard, false)]
	[InlineData(UnitClass.Assault, true)]
	[InlineData(UnitClass.Support, false)]
	[InlineData(UnitClass.Skirmisher, false)]
	public void Charge_MoveAndAttack_OnlyAssaultCanDoBoth(UnitClass type, bool both)
	{
		var tracker = new AttackTracker();
		var board = new Board();
		var minion = new Minion("unit", true, 1, 1, type, 0, Keyword.Charge);
		board.PlaceMinion(minion, 0);
		tracker.RegisterDeployment(minion);
		Assert.True(tracker.CanMove(minion));
		tracker.RecordMove(minion);
		Assert.False(tracker.CanMove(minion));
		Assert.Equal(both, tracker.CanAttack(minion));
		tracker.Reset();
		tracker.AddCharged(minion);
		tracker.RecordAttack(minion);
		Assert.Equal(both, tracker.CanMove(minion));
		Assert.False(tracker.CanAttack(minion));
	}

	[Fact]
	public void Assault_Windfury_HasOneMoveAndTwoAttacks()
	{
		var board = new Board();
		var tracker = new AttackTracker();
		var unit = new Minion("tank", true, 2, 2, UnitClass.Assault, 0, Keyword.Charge, Keyword.Windfury);
		board.PlaceMinion(unit, 0);
		tracker.RegisterDeployment(unit);
		tracker.RecordMove(unit);
		Assert.True(tracker.CanAttack(unit));
		tracker.RecordAttack(unit);
		Assert.True(tracker.CanAttack(unit));
		tracker.RecordAttack(unit);
		Assert.False(tracker.CanAttack(unit));
		Assert.False(tracker.CanMove(unit));
	}

	[Fact]
	public void IncapacitatedOrRemoved_CannotUseEitherQuota()
	{
		var board = new Board();
		var tracker = new AttackTracker();
		var unit = new Minion("tank", true);
		board.PlaceMinion(unit, 0);
		tracker.AddCharged(unit);
		unit.AddStatusEffect(new StatusEffect(StatusEffect.IncapacitatedId, 1, TickTiming.PlayerTurnEnd));
		Assert.False(tracker.CanMove(unit));
		Assert.False(tracker.CanAttack(unit));
		unit.TickStatusEffects(TickTiming.PlayerTurnEnd);
		Assert.True(tracker.CanMove(unit));
		board.RecallMinion(unit);
		Assert.False(tracker.CanMove(unit));
		Assert.False(tracker.CanAttack(unit));
	}

	[Fact]
	public void OneStepAssault_PaysTwoCosts_MovesBeforeFirstAttackAndCombat()
	{
		var board = new Board();
		var tracker = new AttackTracker();
		var core = new CommanderCore();
		core.SetMana(4, 4);
		var owner = new Hero(core, true);
		var actions = new MinionActionService(board, tracker, owner);
		var unit = new Minion("tank", true, 2, 4, UnitClass.Assault, 2, Keyword.Charge);
		board.PlaceMinion(unit, 0);
		tracker.RegisterDeployment(unit);
		var target = new Hero(new CommanderCore(), false);
		int phase = 0;
		Assert.True(actions.CanAttack(unit, target, out bool advances));
		Assert.True(advances);
		Assert.True(actions.Attack(unit, target, () =>
		{
			Assert.Equal(1, phase);
			Assert.Equal(0, owner.CurrentMana);
			Assert.Equal(BoardRow.Frontline, unit.BoardRow);
			phase = 2;
		}, () => phase++));
		Assert.Equal(2, phase);
		Assert.False(tracker.CanMove(unit));
		Assert.False(tracker.CanAttack(unit));
	}

	[Theory]
	[InlineData(1, false, false)]
	[InlineData(3, false, false)]
	[InlineData(4, true, false)]
	[InlineData(4, false, true)]
	public void OneStepAssault_InvalidManaSmokeOrEnemyFrontline_HasNoSideEffects(int mana, bool smoke, bool enemyFront)
	{
		var board = new Board();
		var tracker = new AttackTracker();
		var core = new CommanderCore();
		core.SetMana(mana, mana);
		var owner = new Hero(core, true);
		var actions = new MinionActionService(board, tracker, owner);
		var unit = new Minion("tank", true, 2, 4, UnitClass.Assault, 2, Keyword.Charge);
		board.PlaceMinion(unit, 0);
		tracker.RegisterDeployment(unit);
		var target = new Hero(new CommanderCore(), false);
		if (smoke)
			target.AddStatusEffect(new StatusEffect(StatusEffect.SmokescreenId, 1, TickTiming.EnemyTurnEnd));
		if (enemyFront)
		{
			var blocker = new Minion("blocker", false);
			board.PlaceMinion(blocker, 0);
			board.MoveMinionToFrontline(blocker);
		}
		int effects = 0;
		Assert.False(actions.Attack(unit, target, () => effects++, () => effects++));
		Assert.Equal(mana, owner.CurrentMana);
		Assert.Equal(0, effects);
		Assert.Equal(BoardRow.PlayerSupport, unit.BoardRow);
		Assert.True(tracker.CanAttack(unit));
	}
}
