using System;
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
/// M3 施法者相对化门禁：同一目标过滤条件在玩家/指挥官两侧必须得到镜像结果，
/// 资源（手牌、法力、弃牌堆）只能由施法者自身 CommanderCore 承担。
/// </summary>
public class ActorContextTests
{
	[Fact]
	public void GetRelativeTags_PlayerCaster_EnemyMinionIsEnemy()
	{
		var player = new Hero(new CommanderCore(), true);
		var enemyMinion = new Minion("enemy_minion", false);
		var actor = new EffectActorContext(player, player, null, new AiDecisionProvider());

		var tags = actor.GetRelativeTags(enemyMinion);

		Assert.True(tags.HasFlag(TargetTags.Enemy));
		Assert.True(tags.HasFlag(TargetTags.Minion));
		Assert.False(tags.HasFlag(TargetTags.Friendly));
	}

	[Fact]
	public void GetRelativeTags_EnemyCaster_PlayerMinionIsEnemy()
	{
		var enemy = new Hero(new CommanderCore(), false);
		var playerMinion = new Minion("player_minion", true);
		var actor = new EffectActorContext(enemy, enemy, null, new AiDecisionProvider());

		var tags = actor.GetRelativeTags(playerMinion);

		Assert.True(tags.HasFlag(TargetTags.Enemy));
		Assert.True(tags.HasFlag(TargetTags.Minion));
	}

	[Fact]
	public void SameEnemyMinionFilter_ValidatesMirrorSides_WithoutChangingTresData()
	{
		var require = TargetTags.Enemy | TargetTags.Minion;
		var player = new Hero(new CommanderCore(), true);
		var enemy = new Hero(new CommanderCore(), false);
		var playerMinion = new Minion("same_id", true);
		var enemyMinion = new Minion("same_id", false);
		var playerActor = new EffectActorContext(player, player, null, new AiDecisionProvider());
		var enemyActor = new EffectActorContext(enemy, enemy, null, new AiDecisionProvider());

		Assert.True(TargetTagsHelper.IsValidTarget(playerActor.GetRelativeTags(enemyMinion), require, TargetTags.None));
		Assert.False(TargetTagsHelper.IsValidTarget(playerActor.GetRelativeTags(playerMinion), require, TargetTags.None));
		Assert.True(TargetTagsHelper.IsValidTarget(enemyActor.GetRelativeTags(playerMinion), require, TargetTags.None));
		Assert.False(TargetTagsHelper.IsValidTarget(enemyActor.GetRelativeTags(enemyMinion), require, TargetTags.None));
	}

	[Fact]
	public void EffectActorContext_RejectsDamageSourceFromOppositeSide()
	{
		var player = new Hero(new CommanderCore(), true);
		var enemy = new Hero(new CommanderCore(), false);

		Assert.Throws<ArgumentException>(() =>
			new EffectActorContext(player, enemy, null, new AiDecisionProvider()));
	}

	[Fact]
	public void GameState_PlayerMana_IsReadOnlyMirrorOfCommanderCore()
	{
		var core = new CommanderCore();
		core.InitializeHealth(30);
		core.SetMana(7, 9);
		var state = new GameState(core);

		Assert.Equal(7, state.PlayerMana);
		Assert.Equal(9, state.PlayerMaxMana);

		core.SetMana(2, 4);
		Assert.Equal(2, state.PlayerMana);
		Assert.Equal(4, state.PlayerMaxMana);

		state.StartPlayerTurn(GameState.MaxManaCrystals);
		Assert.Equal(5, state.PlayerMaxMana);
		Assert.Equal(5, state.PlayerMana);
		Assert.Equal(5, core.MaxMana);
	}

	[Fact]
	public void GameState_GainManaSlot_DoesNotFillCurrentMana()
	{
		var core = new CommanderCore();
		core.InitializeHealth(30);
		core.SetMana(3, 3);
		var state = new GameState(core);

		state.GainManaSlot(2);

		Assert.Equal(5, state.PlayerMaxMana);
		Assert.Equal(3, state.PlayerMana);
	}

	[Fact]
	public void SameCardId_PlayedByEitherSide_OnlyCasterHandManaAndPileMove()
	{
		var playerCore = new CommanderCore();
		playerCore.InitializeHealth(30);
		playerCore.SetMana(6, 6);
		var enemyCore = new CommanderCore();
		enemyCore.InitializeHealth(38);
		enemyCore.SetMana(6, 6);
		var player = new Hero(playerCore, true);
		var enemy = new Hero(enemyCore, false);

		var playerCard = new RuntimeCard("spell_strike");
		var enemyCard = new RuntimeCard("spell_strike");
		playerCore.AddToHand(playerCard);
		enemyCore.AddToHand(enemyCard);

		player.SpendMana(1);
		player.DiscardCard(playerCard);

		Assert.Equal(5, playerCore.CurrentMana);
		Assert.Equal(6, enemyCore.CurrentMana);
		Assert.Empty(playerCore.Hand);
		Assert.Single(playerCore.DiscardPile);
		Assert.Single(enemyCore.Hand);
		Assert.Empty(enemyCore.DiscardPile);
		Assert.Same(enemyCard, enemyCore.Hand.Single());
	}

	[Fact]
	public void MinionOwnerHero_RemainsAttachedAfterLeavingBoard_ForDeathPileRouting()
	{
		var board = new Board();
		var enemyCore = new CommanderCore();
		var enemy = new Hero(enemyCore, false);
		var minion = new Minion("minion_18th_regiment", false) { OwnerHero = enemy };
		board.PlaceMinion(minion, 0);
		board.RemoveMinion(minion);

		Assert.Same(enemy, minion.OwnerHero);
		Assert.Empty(board.GetAllMinions());
	}
}
