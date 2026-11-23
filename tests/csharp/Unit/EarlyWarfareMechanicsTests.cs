using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OdysseyCards.AI;
using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Combat;
using OdysseyCards.Core;
using Xunit;
using RuntimeCard = OdysseyCards.Card.Card;

namespace OdysseyCards.Tests.Unit;

/// <summary>
/// 0.2.3「早期战争」纯 C# 机制门禁：开局指挥点、热力值退役、支援线守护护英雄、
/// 机会~、烟遁/拉烟哥、诱饵战术、亡语抽牌归属、骑士I型冲击攻击、星途精神、
/// 完美格挡、爆炸反应装甲、M10 布克动态行动花费与目标型部署规则。
/// 不实例化任何 Godot Resource / Node：需要 .tres 的接线由游戏内 QA 覆盖。
/// </summary>
public class EarlyWarfareMechanicsTests
{
	private static string Root()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null && !File.Exists(Path.Combine(directory.FullName, "project.godot")))
			directory = directory.Parent;
		return directory?.FullName ?? throw new DirectoryNotFoundException("找不到项目根目录");
	}

	private static Hero CreateHero(bool isPlayerSide, int health = 30)
	{
		var core = new CommanderCore();
		core.InitializeHealth(health, health);
		core.SetMana(10, 10);
		core.MaxHandSize = 10;
		return new Hero(core, isPlayerSide);
	}

	private static Minion CreateMinion(string id, bool isPlayerSide, int attack = 3, int health = 5,
		UnitClass unitClass = UnitClass.Vanguard, int actionCost = 1, params Keyword[] keywords)
		=> new(id, isPlayerSide, attack, health, unitClass, actionCost, keywords);

	[Fact]
	public void OpeningMana_FirstTurnIsOneOfOne()
	{
		var state = new GameState();
		state.StartGame();
		Assert.Equal(0, state.PlayerMana);
		Assert.Equal(0, state.PlayerMaxMana);

		state.StartPlayerTurn(GameState.MaxManaCrystals);

		Assert.Equal(1, state.TurnCount);
		Assert.Equal(1, state.PlayerMana);
		Assert.Equal(1, state.PlayerMaxMana);
	}

	[Fact]
	public void HeatPipeline_NoLongerAmplifiesDamage()
	{
		var attacker = CreateMinion("attacker", true, attack: 10, health: 10);
		var defender = CreateMinion("defender", false, attack: 1, health: 30);

		defender.TakeDamage(10, attacker);

		Assert.Equal(20, defender.CurrentHealth);

		// 热力值只保留为死代码：CombatManager 不再注册倍率修改器，也不再喂出牌/回合事件。
		string manager = File.ReadAllText(Path.Combine(Root(), "Scripts", "Combat", "CombatManager.cs"));
		Assert.DoesNotContain("new HeatDamageModifier", manager, StringComparison.Ordinal);
		Assert.DoesNotContain("ModifyHeatSystem", manager, StringComparison.Ordinal);
		Assert.DoesNotContain("_heatSystem.OnCardPlayed", manager, StringComparison.Ordinal);
		Assert.DoesNotContain("_heatSystem.OnManaSpent", manager, StringComparison.Ordinal);
		Assert.DoesNotContain("_heatSystem.OnEnemyTurnEnd", manager, StringComparison.Ordinal);
	}

	[Fact]
	public void SupportGuard_ProtectsHero_UntilRemovedOrIncapacitated()
	{
		var board = new Board();
		var attacker = CreateMinion("attacker", true);
		Assert.True(board.PlaceMinion(attacker, 0));
		Assert.True(board.MoveMinionToFrontline(attacker));
		var guard = CreateMinion("guard", false, 3, 5, UnitClass.Vanguard, 1, Keyword.Guard);
		Assert.True(board.PlaceMinion(guard, 1));
		var hero = CreateHero(false);

		Assert.False(AttackTargetRules.CanAttackTarget(board, attacker, hero));
		Assert.True(AttackTargetRules.CanAttackTarget(board, attacker, guard));

		guard.AddStatusEffect(new StatusEffect(StatusEffect.IncapacitatedId, 1, TickTiming.EnemyTurnEnd));
		Assert.True(AttackTargetRules.CanAttackTarget(board, attacker, hero));

		board.RemoveMinion(guard);
		Assert.True(AttackTargetRules.CanAttackTarget(board, attacker, hero));
	}

	[Fact]
	public void Chansu_RequiresMoreThanTenManaSlots_AndFetchesFromPiles()
	{
		Assert.False(EarlyWarfareRules.ShouldFetchChansu(true, new DamageEventInfo(1, 0, false), 10));
		Assert.True(EarlyWarfareRules.ShouldFetchChansu(true, new DamageEventInfo(1, 0, false), 11));
		Assert.False(EarlyWarfareRules.ShouldFetchChansu(false, new DamageEventInfo(1, 0, false), 11));
		Assert.False(EarlyWarfareRules.ShouldFetchChansu(true, new DamageEventInfo(0, 0, true), 11));

		var core = new CommanderCore();
		var discardCard = new RuntimeCard("spell_chansu", CardType.Spell);
		core.DiscardPile.Add(discardCard);
		Assert.True(EarlyWarfareRules.TryFetchCardToHand(core, "spell_chansu"));
		Assert.Contains(discardCard, core.Hand);
		Assert.Empty(core.DiscardPile);

		var drawCard = new RuntimeCard("spell_chansu", CardType.Spell);
		core.DrawPile.Add(drawCard);
		Assert.True(EarlyWarfareRules.TryFetchCardToHand(core, "spell_chansu"));
		Assert.Contains(drawCard, core.Hand);
		Assert.Empty(core.DrawPile);

		Assert.False(EarlyWarfareRules.TryFetchCardToHand(core, "spell_chansu"));
	}

	[Fact]
	public void SmokeDodge_OnlySupportLineWithoutTauntOrGuard_DecaysOnOpponentTurnEnd()
	{
		var board = new Board();
		var normal = CreateMinion("normal", true);
		var guard = CreateMinion("guard", true, 3, 5, UnitClass.Vanguard, 1, Keyword.Guard);
		var taunt = CreateMinion("taunt", true, 3, 5, UnitClass.Vanguard, 1, Keyword.Taunt);
		var front = CreateMinion("front", true);
		Assert.True(board.PlaceMinion(normal, 0));
		Assert.True(board.PlaceMinion(guard, 1));
		Assert.True(board.PlaceMinion(taunt, 2));
		Assert.True(board.PlaceMinion(front, 3));
		Assert.True(board.MoveMinionToFrontline(front));

		int applied = EarlyWarfareRules.ApplySmokeDodge(board.GetPlayerMinions(), true, 1, TickTiming.EnemyTurnEnd);

		Assert.Equal(1, applied);
		Assert.True(normal.HasSmokeScreen);
		Assert.False(guard.HasSmokeScreen);
		Assert.False(taunt.HasSmokeScreen);
		Assert.False(front.HasSmokeScreen);

		// 玩家回合结束不衰减，完整覆盖接下来的敌方回合。
		normal.TickStatusEffects(TickTiming.PlayerTurnEnd);
		Assert.True(normal.HasSmokeScreen);

		normal.TickStatusEffects(TickTiming.EnemyTurnEnd);
		Assert.False(normal.HasSmokeScreen);
	}

	[Fact]
	public void RayanGe_FirstMoveGrantsSmoke_AndLaterActionsDoNotRepeat()
	{
		var hero = CreateHero(true);
		var runner = CreateMinion("minion_rayan_ge", true, attack: 3, health: 4, UnitClass.Assault,
			actionCost: 1, Keyword.Charge);
		runner.OwnerHero = hero;

		// 首次行动（移动）即触发。
		Assert.True(EarlyWarfareRules.TryGrantFirstActionSmoke(runner, hero));
		Assert.True(hero.HasSmokeScreen);
		Assert.Equal(1, hero.StatusEffects[StatusEffect.SmokescreenId].Stacks);
		Assert.Equal(TickTiming.EnemyTurnEnd, hero.StatusEffects[StatusEffect.SmokescreenId].TickOn);

		// 后续攻击不再重复触发。
		Assert.False(EarlyWarfareRules.TryGrantFirstActionSmoke(runner, hero));
		Assert.Equal(1, hero.StatusEffects[StatusEffect.SmokescreenId].Stacks);
	}

	[Fact]
	public void BaitTactics_DiscardsRandomCardFromCasterOpponents()
	{
		var enemyHero = CreateHero(false);
		enemyHero.Core.MaxHandSize = 10;
		enemyHero.Core.AddToHand(new RuntimeCard("enemy_card_a", CardType.Spell));
		enemyHero.Core.AddToHand(new RuntimeCard("enemy_card_b", CardType.Spell));
		var playerTarget = CreateMinion("player_target", true);
		playerTarget.GrantBaitTactics(casterIsPlayerSide: true);

		Assert.True(EarlyWarfareRules.TryTriggerBaitTactics(playerTarget, new[] { enemyHero }));
		Assert.Single(enemyHero.Hand);

		var playerHero = CreateHero(true);
		playerHero.Core.AddToHand(new RuntimeCard("player_card_a", CardType.Spell));
		var enemyTarget = CreateMinion("enemy_target", false);
		enemyTarget.GrantBaitTactics(casterIsPlayerSide: false);

		Assert.True(EarlyWarfareRules.TryTriggerBaitTactics(enemyTarget, new[] { playerHero }));
		Assert.Empty(playerHero.Hand);

		var plain = CreateMinion("plain", true);
		Assert.False(EarlyWarfareRules.TryTriggerBaitTactics(plain, new[] { enemyHero }));
	}

	[Fact]
	public void DeathrattleDrawOwner_IsCasterSide_AbsoluteNotMinionOwner()
	{
		Assert.Equal("owner_player", EarlyWarfareRules.OwnerDrawTargetType(casterIsPlayerSide: true));
		Assert.Equal("owner_enemy", EarlyWarfareRules.OwnerDrawTargetType(casterIsPlayerSide: false));

		// 敌意 / 纳米散尸术生成的亡语必须固化施法方阵营，不能沿用随从拥有者。
		string dispatcher = File.ReadAllText(Path.Combine(Root(), "Scripts", "Combat", "CardEffectDispatcher.cs"));
		int uses = dispatcher.Split("EarlyWarfareRules.OwnerDrawTargetType").Length - 1;
		Assert.True(uses >= 2, $"亡语抽牌归属应同时用于敌意与纳米散尸术，实际 {uses} 处");
	}

	[Fact]
	public void KnightType1_ImpactGrantsPlusTwoAttack_AndConsumingItReverts()
	{
		var knight = CreateMinion("minion_knight_type1", true, attack: 4, health: 2, UnitClass.Vanguard,
			actionCost: 1, Keyword.Charge, Keyword.Impact);

		Assert.True(knight.HasImpact);
		Assert.True(knight.HasImpactAttackBonus);
		Assert.Equal(6, knight.Attack);
		Assert.Equal(6, knight.BaseAttack);

		// 冲击被消耗（攻击后由 AttackCombatResolver 置 false）。
		knight.HasImpact = false;
		Assert.Equal(4, knight.Attack);

		// 再次被「诱饵战术」授予冲击后恢复 6 攻。
		knight.HasImpact = true;
		Assert.Equal(6, knight.Attack);

		// 增益叠加在基础攻击之上，不会重复计算冲击加成。
		knight.GainStats(2, 0);
		Assert.Equal(8, knight.Attack);

		knight.Silence();
		Assert.False(knight.HasImpactAttackBonus);
		Assert.Equal(6, knight.Attack);
	}

	[Fact]
	public void SutaraitoSpiritNextTurn_DrawsWithoutGainingMana()
	{
		var allyA = CreateMinion("ally_a", true, attack: 2, health: 2);
		var allyB = CreateMinion("ally_b", true, attack: 1, health: 1);
		EarlyWarfareRules.BuffFriendlyMinions(new[] { allyA, allyB }, 2, 2);
		Assert.Equal(4, allyA.Attack);
		Assert.Equal(4, allyA.CurrentHealth);
		Assert.Equal(3, allyB.Attack);
		Assert.Equal(3, allyB.CurrentHealth);

		string dispatcher = File.ReadAllText(Path.Combine(Root(), "Scripts", "Combat", "CardEffectDispatcher.cs"));
		Assert.Contains("hero.DrawCards((effect.SecondaryValue > 0 ? effect.SecondaryValue : 2) * status.Stacks);",
			dispatcher, StringComparison.Ordinal);
		Assert.DoesNotContain("hero.GainMana((effect.Value > 0 ? effect.Value : 2) * status.Stacks);",
			dispatcher, StringComparison.Ordinal);
	}

	[Fact]
	public void PerfectBlock_GrantsArmorAndUkemiOnlyWhenAttackDealsNoLifeDamage()
	{
		var hero = CreateHero(true);
		int granted = 0;
		EarlyWarfareRules.ApplyPerfectBlock(hero, 3, () => granted++);
		Assert.Equal(3, hero.CurrentArmor);

		var enemy = CreateMinion("enemy", false, attack: 3, health: 3);
		hero.TakeDamage(3, enemy);
		Assert.Equal(1, granted);
		Assert.Equal(30, hero.CurrentHealth);
		Assert.Equal(0, hero.CurrentArmor);

		// 监听已注销：下一次攻击即使造成生命伤害也不会给牌。
		hero.TakeDamage(3, enemy);
		Assert.Equal(1, granted);
		Assert.Equal(27, hero.CurrentHealth);

		// 两次完美格挡各自独立，下一次受击同时触发并各自注销。
		var guardedHero = CreateHero(true);
		int doubleGranted = 0;
		EarlyWarfareRules.ApplyPerfectBlock(guardedHero, 3, () => doubleGranted++);
		EarlyWarfareRules.ApplyPerfectBlock(guardedHero, 3, () => doubleGranted++);
		Assert.Equal(6, guardedHero.CurrentArmor);
		guardedHero.TakeDamage(2, enemy);
		Assert.Equal(2, doubleGranted);
		guardedHero.TakeDamage(2, enemy);
		Assert.Equal(2, doubleGranted);
		Assert.Equal(30, guardedHero.CurrentHealth);
		Assert.Equal(2, guardedHero.CurrentArmor);
	}

	[Fact]
	public void ExplosiveReactiveArmor_BlocksLifeDamageAndSkipsArmorOnlyHits()
	{
		var (hero, _, _) = CreatePlayerState();
		var enemy = CreateMinion("enemy", false, attack: 3, health: 3);
		hero.GainArmor(5);
		hero.AddDomain("explosive_reactive_armor", null);

		// 纯护甲挡住的攻击不消耗层数。
		hero.TakeDamage(3, enemy);
		Assert.True(hero.HasDomain("explosive_reactive_armor"));
		Assert.Equal(30, hero.CurrentHealth);

		// 护甲被打穿后的生命伤害被抵消，并消耗 1 层。
		hero.TakeDamage(7, enemy);
		Assert.Equal(30, hero.CurrentHealth);
		Assert.False(hero.HasDomain("explosive_reactive_armor"));

		// 层数耗尽后不再抵挡。
		hero.TakeDamage(3, enemy);
		Assert.Equal(27, hero.CurrentHealth);
	}

	[Fact]
	public void BookerActionCost_IsZeroWithFriendlyVanguard_AndReturnsToOneAfterItDies()
	{
		var (_, board, _) = CreatePlayerState();
		var booker = CreateMinion("minion_m10_booker", true, attack: 3, health: 2, UnitClass.Assault,
			actionCost: 1, Keyword.Charge);
		Assert.True(board.PlaceMinion(booker, 0));
		Assert.Equal(1, booker.ActionCost);

		var vanguard = CreateMinion("vanguard", true, attack: 2, health: 3, UnitClass.Vanguard);
		Assert.True(board.PlaceMinion(vanguard, 1));
		bool hasVanguard = EarlyWarfareRules.HasLivingVanguard(board.GetAllMinions(), isPlayerSide: true);
		Assert.True(hasVanguard);
		EarlyWarfareRules.ApplyBookerActionCost(new[] { booker }, hasVanguard);
		Assert.Equal(0, booker.ActionCost);

		// 先锋死亡后光环消失，行动花费恢复为 1。
		vanguard.ApplyDevDamage(99);
		board.RemoveMinion(vanguard);
		hasVanguard = EarlyWarfareRules.HasLivingVanguard(board.GetAllMinions(), isPlayerSide: true);
		Assert.False(hasVanguard);
		EarlyWarfareRules.ApplyBookerActionCost(new[] { booker }, hasVanguard);
		Assert.Equal(1, booker.ActionCost);

		// 手牌实例同样按光环取值。
		var handCard = new RuntimeCard("minion_m10_booker", CardType.Minion, actionCost: 1);
		EarlyWarfareRules.ApplyBookerActionCost(new[] { handCard }, hasFriendlyVanguard: true);
		Assert.Equal(0, handCard.ActionCost);
		EarlyWarfareRules.ApplyBookerActionCost(new[] { handCard }, hasFriendlyVanguard: false);
		Assert.Equal(1, handCard.ActionCost);
	}

	[Fact]
	public void TargetDeployment_MoveToFrontlineTargetsRequireReachableFrontline()
	{
		var (_, board, _) = CreatePlayerState();
		var support = CreateMinion("support_target", true);
		Assert.True(board.PlaceMinion(support, 0));

		Assert.True(EarlyWarfareRules.IsValidMoveToFrontlineTarget(board, support));

		var frontline = CreateMinion("frontline_target", true);
		Assert.True(board.PlaceMinion(frontline, 1));
		Assert.True(board.MoveMinionToFrontline(frontline));
		// 已在前线的单位不能再被「移动至前线」。
		Assert.False(EarlyWarfareRules.IsValidMoveToFrontlineTarget(board, frontline));

		// 敌方已占领前线时，友方支援线随从无法推进。
		var enemyFront = CreateMinion("enemy_front", false);
		var other = new Board();
		Assert.True(other.PlaceMinion(enemyFront, 0));
		Assert.True(other.MoveMinionToFrontline(enemyFront));
		var otherSupport = CreateMinion("other_support", true);
		Assert.True(other.PlaceMinion(otherSupport, 0));
		Assert.False(EarlyWarfareRules.IsValidMoveToFrontlineTarget(other, otherSupport));

		// 无合法目标时随从依然裸拍上场（战吼跳过）；有目标时先进入目标选择。
		Assert.False(EarlyWarfareRules.NeedsBattlecryTargetSelection(cardRequiresTarget: true, hasAnyValidTarget: false));
		Assert.True(EarlyWarfareRules.NeedsBattlecryTargetSelection(cardRequiresTarget: true, hasAnyValidTarget: true));
		Assert.False(EarlyWarfareRules.NeedsBattlecryTargetSelection(cardRequiresTarget: false, hasAnyValidTarget: true));
	}

	private static (Hero Hero, Board Board, GameState State) CreatePlayerState()
	{
		var core = new CommanderCore();
		core.InitializeHealth(30, 30);
		var hero = new Hero(core, true);
		var board = new Board();
		var state = new GameState(core);
		state.StartGame();
		state.StartPlayerTurn(GameState.MaxManaCrystals);
		return (hero, board, state);
	}
}
