using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Combat;
using OdysseyCards.Core;
using Xunit;

namespace OdysseyCards.Tests.Unit;

public class FrontlineAiTests
{
	[Fact]
	public void EnemyMeleeIntent_PlayerFrontline_BlocksRearAndHq()
	{
		var board = new Board();
		var hq = new Hero(new CommanderCore(), true);
		var enemy = new Hero(new CommanderCore(), false) { Weapon = new Weapon("melee", 3, 0) };
		var front = new Minion("front", true, health: 100);
		var rear = new Minion("rear", true, health: 1);
		board.PlaceMinion(front, 0);
		board.MoveMinionToFrontline(front);
		board.PlaceMinion(rear, 0);
		Assert.Same(front, SmartTargetingSystem.SelectEnemyAttackTarget(board, hq, enemy, 3));
		Assert.False(AttackTargetRules.BaseReachable(board, enemy, rear));
		Assert.False(AttackTargetRules.BaseReachable(board, enemy, hq));
	}

	[Fact]
	public void EnemySpellDamage_CrossesFrontlineAndSmoke()
	{
		var board = new Board();
		var hqCore = new CommanderCore();
		hqCore.InitializeHealth(30, 1);
		var hq = new Hero(hqCore, true);
		hq.AddStatusEffect(new StatusEffect(StatusEffect.SmokescreenId, 1, TickTiming.PlayerTurnEnd));
		var enemy = new Hero(new CommanderCore(), false) { Weapon = new Weapon("melee", 3, 0) };
		var front = new Minion("front", true, health: 100);
		board.PlaceMinion(front, 0);
		board.MoveMinionToFrontline(front);
		Assert.Same(hq, SmartTargetingSystem.SelectEnemySpellTarget(board, hq, enemy, 3));
		Assert.Same(front, SmartTargetingSystem.SelectEnemyAttackTarget(board, hq, enemy, 3));
	}

	[Fact]
	public void UnreachableRearTaunt_DoesNotPreventEnemyMeleeFromAttackingFrontline()
	{
		var board = new Board();
		var front = new Minion("front", true, health: 20);
		board.PlaceMinion(front, 0);
		board.MoveMinionToFrontline(front);
		var taunt = new Minion("taunt", true, 1, 4, UnitClass.Vanguard, 0, Keyword.Taunt);
		board.PlaceMinion(taunt, 0);
		var attacker = new Minion("melee", false);
		board.PlaceMinion(attacker, 0);
		Assert.Same(front, SmartTargetingSystem.SelectEnemyAttackTarget(board, new Hero(new CommanderCore(), true), attacker, 1));
	}
}
