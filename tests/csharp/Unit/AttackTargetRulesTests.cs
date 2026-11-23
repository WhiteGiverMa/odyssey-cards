using System;
using System.Linq;
using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Combat;
using OdysseyCards.Core;
using Xunit;

namespace OdysseyCards.Tests.Unit;

public class AttackTargetRulesTests
{
	private static Minion Place(Board board, bool player, int slot = 0, UnitClass type = UnitClass.Vanguard, params Keyword[] keywords)
	{
		var unit = new Minion("unit", player, 2, 5, type, 0, keywords);
		Assert.True(board.PlaceMinion(unit, slot));
		return unit;
	}
	private static Hero Hero(bool player, WeaponRange range = WeaponRange.Melee) => new(new CommanderCore(), player)
	{
		Weapon = new Weapon("test", 1, 0) { Range = range },
	};

	[Theory]
	[InlineData(UnitClass.Vanguard, false)]
	[InlineData(UnitClass.Assault, false)]
	[InlineData(UnitClass.Support, true)]
	[InlineData(UnitClass.Skirmisher, true)]
	public void Reach_SupportToEnemySupport_OnlyRangedClasses(UnitClass type, bool expected)
	{
		var board = new Board();
		var attacker = Place(board, true, type: type);
		var defender = Place(board, false);
		Assert.Equal(expected, AttackTargetRules.CanAttackTarget(board, attacker, defender));
		Assert.Equal(expected, AttackTargetRules.CanAttackTarget(board, attacker, Hero(false)));
	}

	[Theory]
	[InlineData(UnitClass.Vanguard)]
	[InlineData(UnitClass.Assault)]
	public void Reach_MeleeSupportAttacksEnemyFrontline_ButCannotJumpRearTaunt(UnitClass type)
	{
		var board = new Board();
		var attacker = Place(board, true, type: type);
		var front = Place(board, false);
		board.MoveMinionToFrontline(front);
		var taunt = Place(board, false, keywords: new[] { Keyword.Taunt });
		Assert.True(AttackTargetRules.CanAttackTarget(board, attacker, front));
		Assert.False(AttackTargetRules.CanAttackTarget(board, attacker, taunt));
		Assert.False(AttackTargetRules.CanAttackTarget(board, attacker, Hero(false)));
	}

	[Fact]
	public void Reach_MeleeOnFrontline_CanAttackRearAndHq()
	{
		var board = new Board();
		var attacker = Place(board, true);
		board.MoveMinionToFrontline(attacker);
		Assert.True(AttackTargetRules.CanAttackTarget(board, attacker, Place(board, false)));
		Assert.True(AttackTargetRules.CanAttackTarget(board, attacker, Hero(false)));
	}

	[Theory]
	[InlineData(WeaponRange.Melee, false)]
	[InlineData(WeaponRange.Ranged, true)]
	[InlineData(WeaponRange.FriendlySupport, false)]
	public void Weapon_EnemyFrontline_BlocksOnlyMeleeRearAttacks(WeaponRange range, bool rearReachable)
	{
		var board = new Board();
		var front = Place(board, false);
		board.MoveMinionToFrontline(front);
		var rear = Place(board, false);
		var attacker = Hero(true, range);
		Assert.Equal(rearReachable, AttackTargetRules.CanAttackTarget(board, attacker, rear));
		Assert.Equal(rearReachable, AttackTargetRules.CanAttackTarget(board, attacker, Hero(false)));
		Assert.Equal(range != WeaponRange.FriendlySupport, AttackTargetRules.CanAttackTarget(board, attacker, front));
	}

	[Fact]
	public void Skirmisher_InSupportImmuneToMelee_ButInFrontlineExposed()
	{
		var board = new Board();
		var air = Place(board, false, type: UnitClass.Skirmisher);
		var hero = Hero(true);
		Assert.False(AttackTargetRules.CanAttackTarget(board, hero, air));
		Assert.True(AttackTargetRules.CanAttackTarget(board, Hero(true, WeaponRange.Ranged), air));
		board.MoveMinionToFrontline(air);
		Assert.True(AttackTargetRules.CanAttackTarget(board, hero, air));
	}

	[Fact]
	public void Taunt_InterceptsSameLineAndHq_ButNotAnotherLine()
	{
		var board = new Board();
		var attacker = Place(board, true, type: UnitClass.Support);
		var front = Place(board, false);
		board.MoveMinionToFrontline(front);
		var taunt = Place(board, false, 0, keywords: new[] { Keyword.Taunt });
		var rear = Place(board, false, 1);
		Assert.True(AttackTargetRules.CanAttackTarget(board, attacker, front));
		Assert.True(AttackTargetRules.CanAttackTarget(board, attacker, taunt));
		Assert.False(AttackTargetRules.CanAttackTarget(board, attacker, rear));
		Assert.False(AttackTargetRules.CanAttackTarget(board, attacker, Hero(false)));
	}

	[Fact]
	public void Guard_OnlyAdjacentOrdinaryUnits_AndSupportGuardProtectsHq()
	{
		var board = new Board();
		var attacker = Place(board, true, type: UnitClass.Support);
		var guard = Place(board, false, 1, keywords: new[] { Keyword.Guard });
		var guarded = Place(board, false, 0);
		var guard2 = Place(board, false, 2, keywords: new[] { Keyword.Guard });
		var distant = Place(board, false, 3);
		Assert.False(AttackTargetRules.CanAttackTarget(board, attacker, guarded));
		Assert.True(AttackTargetRules.CanAttackTarget(board, attacker, guard));
		Assert.True(AttackTargetRules.CanAttackTarget(board, attacker, guard2));
		Assert.False(AttackTargetRules.CanAttackTarget(board, attacker, distant));
		// 0.2.3 暂时调整：支援阵线的守护同样守护英雄 HQ。
		Assert.False(AttackTargetRules.CanAttackTarget(board, attacker, Hero(false)));
		board.RemoveMinion(guard);
		board.RemoveMinion(guard2);
		Assert.True(AttackTargetRules.CanAttackTarget(board, attacker, Hero(false)));
	}

	[Fact]
	public void Guard_InSupportProtectsHq_ButIncapacitatedGuardDoesNot()
	{
		var board = new Board();
		var attacker = Place(board, true);
		board.MoveMinionToFrontline(attacker);
		var guard = Place(board, false, 1, keywords: new[] { Keyword.Guard });

		Assert.False(AttackTargetRules.CanAttackTarget(board, attacker, Hero(false)));
		// 守护本身仍可被拆除，防守方不会形成无解防线。
		Assert.True(AttackTargetRules.CanAttackTarget(board, attacker, guard));

		guard.AddStatusEffect(new StatusEffect(StatusEffect.IncapacitatedId, 1, TickTiming.EnemyTurnEnd));
		Assert.True(guard.IsIncapacitated);
		Assert.True(AttackTargetRules.CanAttackTarget(board, attacker, Hero(false)));

		board.RemoveMinion(guard);
		Assert.True(AttackTargetRules.CanAttackTarget(board, attacker, Hero(false)));
	}

	[Fact]
	public void Escort_GroundGuardProtectingSkirmisher_IsExposedAndBreaksLock()
	{
		var board = new Board();
		var attacker = Place(board, true, type: UnitClass.Skirmisher);
		var guard = Place(board, false, 0, keywords: new[] { Keyword.Guard });
		var escort = Place(board, false, 1, UnitClass.Skirmisher);
		var ordinary = Place(board, false, 3);
		Assert.True(AttackTargetRules.CanAttackTarget(board, attacker, guard));
		Assert.False(AttackTargetRules.CanAttackTarget(board, attacker, escort));
		Assert.False(AttackTargetRules.CanAttackTarget(board, attacker, ordinary));
		Assert.False(AttackTargetRules.CanAttackTarget(board, attacker, Hero(false)));
		board.RemoveMinion(guard);
		Assert.True(AttackTargetRules.CanAttackTarget(board, attacker, escort));
		board.RemoveMinion(escort);
		Assert.True(AttackTargetRules.CanAttackTarget(board, attacker, ordinary));
		Assert.True(AttackTargetRules.CanAttackTarget(board, attacker, Hero(false)));
	}

	[Fact]
	public void Escort_TauntOverridesEscort_AndDisabledEscortDoesNotBlock()
	{
		var board = new Board();
		var attacker = Place(board, true, type: UnitClass.Skirmisher);
		var taunt = Place(board, false, 0, keywords: new[] { Keyword.Taunt, Keyword.Guard });
		var escort = Place(board, false, 1, UnitClass.Skirmisher);
		Assert.True(AttackTargetRules.CanAttackTarget(board, attacker, taunt));
		Assert.False(AttackTargetRules.CanAttackTarget(board, attacker, escort));
		board.RemoveMinion(taunt);
		escort.AddStatusEffect(new StatusEffect(StatusEffect.IncapacitatedId, 1, TickTiming.EnemyTurnEnd));
		Assert.True(AttackTargetRules.CanAttackTarget(board, attacker, Hero(false)));
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void Smoke_AllClassesAndWeaponsCannotAttack_Symmetric(bool player)
	{
		foreach (var type in Enum.GetValues<UnitClass>())
		{
			var board = new Board();
			var attacker = Place(board, player, type: type);
			board.MoveMinionToFrontline(attacker);
			var defender = Place(board, !player);
			defender.AddStatusEffect(new StatusEffect(StatusEffect.SmokescreenId, 1, TickTiming.EnemyTurnEnd));
			var hq = Hero(!player);
			hq.AddStatusEffect(new StatusEffect(StatusEffect.SmokescreenId, 1, TickTiming.EnemyTurnEnd));
			Assert.False(AttackTargetRules.CanAttackTarget(board, attacker, defender));
			Assert.False(AttackTargetRules.CanAttackTarget(board, attacker, hq));
			Assert.False(AttackTargetRules.CanAttackTarget(board, Hero(player, WeaponRange.Ranged), defender));
			Assert.True(AttackTargetRules.CanTargetFrontlineSpell(board, player, defender));
		}
	}

	[Theory]
	[InlineData(Keyword.Taunt)]
	[InlineData(Keyword.Guard)]
	public void Smoke_TauntAndGuard_ForceMutualExclusion(Keyword keyword)
	{
		var unit = new Minion("unit", true);
		unit.AddStatusEffect(new StatusEffect(StatusEffect.SmokescreenId, 1, TickTiming.PlayerTurnEnd));
		if (keyword == Keyword.Guard)
			unit.HasGuard = true;
		else
			unit.HasTaunt = true;
		Assert.False(unit.HasSmokeScreen);
		Assert.False(unit.HasStatusEffect(StatusEffect.SmokescreenId));
		unit.AddStatusEffect(new StatusEffect(StatusEffect.SmokescreenId, 2, TickTiming.PlayerTurnEnd));
		Assert.False(unit.HasStatusEffect(StatusEffect.SmokescreenId));
	}

	[Fact]
	public void FrontlineSpell_EnemyOccupancyRestricts_WithoutApplyingAttackFilters()
	{
		var board = new Board();
		var front = Place(board, false);
		board.MoveMinionToFrontline(front);
		var rear = Place(board, false);
		Assert.True(AttackTargetRules.CanTargetFrontlineSpell(board, true, front));
		Assert.False(AttackTargetRules.CanTargetFrontlineSpell(board, true, rear));
		Assert.False(AttackTargetRules.CanTargetFrontlineSpell(board, true, Hero(false)));
		board.RecallMinion(front);
		Assert.True(AttackTargetRules.CanTargetFrontlineSpell(board, true, rear));
	}

	[Fact]
	public void LegalTargets_OffBoardDeadFriendly_AreRejected()
	{
		var board = new Board();
		var attacker = Place(board, true, type: UnitClass.Support);
		var dead = Place(board, false);
		dead.ApplyDevDamage(99);
		Assert.False(AttackTargetRules.CanAttackTarget(board, attacker, dead));
		Assert.False(AttackTargetRules.CanAttackTarget(board, attacker, attacker));
		Assert.False(AttackTargetRules.CanAttackTarget(board, attacker, new Minion("outside", false)));
		Assert.Empty(AttackTargetRules.GetLegalAttackTargets(board, attacker, new IDamageTarget[] { dead, attacker }));
	}

	[Fact]
	public void DefensePriority_AllFourSlotClassKeywordCombinations_NeverDeadlock()
	{
		// 每格六种形状（地面/游击 × 普通/守护/嘲讽），穷举 6^4 种支援线。
		for (int layout = 0; layout < 1296; layout++)
		{
			var board = new Board();
			var attacker = Place(board, true, type: UnitClass.Skirmisher);
			int remaining = layout;
			for (int slot = 0; slot < 4; slot++)
			{
				int shape = remaining % 6;
				remaining /= 6;
				var type = shape >= 3 ? UnitClass.Skirmisher : UnitClass.Vanguard;
				var keyword = (shape % 3) switch { 1 => Keyword.Guard, 2 => Keyword.Taunt, _ => Keyword.None };
				Place(board, false, slot, type, keyword);
			}
			var candidates = board.GetEnemyMinions().Cast<IDamageTarget>().ToList();
			Assert.NotEmpty(AttackTargetRules.GetLegalAttackTargets(board, attacker, candidates));
		}
	}
}
