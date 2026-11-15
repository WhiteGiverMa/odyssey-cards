using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Combat;
using OdysseyCards.Core;
using Xunit;

namespace OdysseyCards.Tests.Unit;

public class AttackCombatResolverTests
{
	[Fact]
	public void Ambush_SurvivingAttacker_TakesOnlyOneCounter()
	{
		var attacker = new Minion("attack", true, 2, 10);
		var defender = new Minion("defend", false, 3, 10, UnitClass.Vanguard, 0, Keyword.Ambush);
		Assert.True(AttackCombatResolver.Resolve(attacker, defender, 2));
		Assert.Equal(7, attacker.CurrentHealth);
		Assert.Equal(8, defender.CurrentHealth);
		Assert.True(defender.AmbushUsedThisTurn);
	}

	[Fact]
	public void Support_DoesNotTriggerOrConsumeAmbush_AndTakesNoCounter()
	{
		var attacker = new Minion("support", true, 2, 10, UnitClass.Support);
		var defender = new Minion("defend", false, 30, 10, UnitClass.Vanguard, 0, Keyword.Ambush);
		Assert.True(AttackCombatResolver.Resolve(attacker, defender, 2));
		Assert.Equal(10, attacker.CurrentHealth);
		Assert.Equal(8, defender.CurrentHealth);
		Assert.False(defender.AmbushUsedThisTurn);
	}

	[Fact]
	public void DisabledMinion_NoAmbushOrNormalCounter_EvenWhenAttackerIsHero()
	{
		var attacker = new Hero(new CommanderCore(), true) { Weapon = new Weapon("test", 2, 0) };
		var defender = new Minion("defend", false, 10, 10, UnitClass.Vanguard, 0, Keyword.Ambush);
		defender.AddStatusEffect(new StatusEffect(StatusEffect.IncapacitatedId, 1, TickTiming.EnemyTurnEnd));
		AttackCombatResolver.Resolve(attacker, defender, 2);
		Assert.Equal(30, attacker.CurrentHealth);
		Assert.False(defender.AmbushUsedThisTurn);
	}

	[Theory]
	[InlineData(true, false)]
	[InlineData(false, true)]
	public void HeroDefender_DisabledOrIgnored_CannotCounter(bool disabled, bool ignored)
	{
		var attacker = new Hero(new CommanderCore(), true) { Weapon = new Weapon("attack", 2, 0) { IgnoreCounterAttack = ignored } };
		var defender = new Hero(new CommanderCore(), false) { Weapon = new Weapon("defend", 4, 0), HasAmbush = true };
		if (disabled)
			defender.AddStatusEffect(new StatusEffect(StatusEffect.IncapacitatedId, 1, TickTiming.EnemyTurnEnd));
		AttackCombatResolver.Resolve(attacker, defender, 2);
		Assert.Equal(30, attacker.CurrentHealth);
		Assert.Equal(28, defender.CurrentHealth);
		Assert.False(defender.AmbushUsedThisTurn);
	}

	[Fact]
	public void Ambush_KillsAttacker_CancelsDamageButImpactPreventsIt()
	{
		var attacker = new Minion("attack", true, 4, 1);
		var defender = new Minion("defend", false, 2, 10, UnitClass.Vanguard, 0, Keyword.Ambush);
		Assert.False(AttackCombatResolver.Resolve(attacker, defender, 4));
		Assert.Equal(10, defender.CurrentHealth);
		attacker = new Minion("impact", true, 4, 1, UnitClass.Vanguard, 0, Keyword.Impact);
		defender.ResetAmbush();
		Assert.True(AttackCombatResolver.Resolve(attacker, defender, 4));
		Assert.Equal(1, attacker.CurrentHealth);
		Assert.False(attacker.HasImpact);
		Assert.True(defender.AmbushUsedThisTurn);
	}
}
