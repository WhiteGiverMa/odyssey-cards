using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OdysseyCards.AI;
using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Combat;
using OdysseyCards.Core;
using Xunit;
using RuntimeCard = OdysseyCards.Card.Card;

namespace OdysseyCards.Tests.Unit;

/// <summary>
/// M3 指挥官门禁：同步 AI 决策零卡死、宿敌池避开玩家英雄、
/// 情报左起翻开指挥官手牌、实时评分贪心优先抢线与斩杀。
/// </summary>
public class CommanderAiTests
{
	[Fact]
	public void AiDecisionProvider_Discover_PicksHighestScoredCard()
	{
		var provider = new AiDecisionProvider(card => card.Id switch
		{
			"best" => 30,
			"middle" => 20,
			_ => 1,
		});
		var options = new[] { new RuntimeCard("weak"), new RuntimeCard("best"), new RuntimeCard("middle") };
		IReadOnlyList<RuntimeCard>? chosen = null;

		provider.ChooseCards(new CardDecisionRequest(
			new Hero(new CommanderCore(), false), CardDecisionKind.Discover, options, 1, 1), cards => chosen = cards);

		Assert.NotNull(chosen);
		Assert.Same(options[1], Assert.Single(chosen));
	}

	[Fact]
	public void AiDecisionProvider_Discard_PicksLowestScoredCard()
	{
		var provider = new AiDecisionProvider(card => card.Id switch
		{
			"keep" => 30,
			"junk" => 1,
			_ => 10,
		});
		var options = new[] { new RuntimeCard("keep"), new RuntimeCard("junk") };
		IReadOnlyList<RuntimeCard>? chosen = null;

		provider.ChooseCards(new CardDecisionRequest(
			new Hero(new CommanderCore(), false), CardDecisionKind.Discard, options, 1, 1), cards => chosen = cards);

		Assert.NotNull(chosen);
		Assert.Same(options[1], Assert.Single(chosen));
	}

	[Fact]
	public void AiDecisionProvider_BladeCrisis_SelectsAtLeastTwoWhenAvailable()
	{
		var provider = new AiDecisionProvider(card => card.Id.Length);
		var options = Enumerable.Range(0, 5).Select(index => new RuntimeCard($"card_{index}")).ToArray();
		IReadOnlyList<RuntimeCard>? chosen = null;

		provider.ChooseCards(new CardDecisionRequest(
			new Hero(new CommanderCore(), false), CardDecisionKind.BladeCrisis, options, 0, 5), cards => chosen = cards);

		Assert.NotNull(chosen);
		Assert.InRange(chosen.Count, 2, 5);
	}

	[Fact]
	public void AiDecisionProvider_ChooseOption_PicksHighestScore()
	{
		var provider = new AiDecisionProvider();
		int chosen = -1;

		provider.ChooseOption(new OptionDecisionRequest("k", "fallback",
			new[] { "a", "b", "c" }, new[] { "", "", "" }, new double[] { 1, 9, 3 }), index => chosen = index);

		Assert.Equal(1, chosen);
	}

	[Theory]
	[InlineData(CardDecisionKind.Discover, 1, 1)]
	[InlineData(CardDecisionKind.RecoverDiscard, 2, 2)]
	[InlineData(CardDecisionKind.Discard, 0, 3)]
	[InlineData(CardDecisionKind.BladeCrisis, 0, 5)]
	[InlineData(CardDecisionKind.CopyHandFill, 1, 1)]
	[InlineData(CardDecisionKind.RestructureDiscard, 1, 1)]
	public void AiDecisionProvider_All68CardIds_AlwaysCompletesWithinBounds(CardDecisionKind kind, int min, int max)
	{
		var cards = LoadAllRuntimeCards();
		Assert.Equal(68, cards.Count);
		var provider = new AiDecisionProvider(card => (card.Id.GetHashCode() & 31) + 1);
		int calls = 0;
		IReadOnlyList<RuntimeCard>? chosen = null;

		provider.ChooseCards(new CardDecisionRequest(
			new Hero(new CommanderCore(), false), kind, cards, min, max), selected =>
		{
			calls++;
			chosen = selected;
		});

		Assert.Equal(1, calls);
		Assert.NotNull(chosen);
		Assert.InRange(chosen.Count, min, max);
		Assert.Equal(chosen.Count, chosen.Distinct().Count());
	}

	[Fact]
	public void EnemyRegistry_RivalPool_ExcludesPlayerHero()
	{
		Assert.Equal(new[] { "rie", "sokou" }, EnemyRegistry.GetRivalCommanderIds("ayame"));
		Assert.Equal(new[] { "ayame", "sokou" }, EnemyRegistry.GetRivalCommanderIds("rie"));
		Assert.Equal(new[] { "ayame", "rie" }, EnemyRegistry.GetRivalCommanderIds("sokou"));
		Assert.Equal(3, EnemyRegistry.GetRivalCommanderIds("redpantsu").Count);
	}

	[Fact]
	public void CommanderHand_IntelRevealsLeftToRight_AndOverflowHealsThenArmor()
	{
		var core = new CommanderCore();
		core.InitializeHealth(38, 36);
		var commander = new Hero(core, false);
		var hand = Enumerable.Range(0, 4).Select(index => new RuntimeCard($"cmd_card_{index}")).ToArray();
		core.Hand.AddRange(hand);
		var intel = new IntelService();

		var first = intel.Resolve(new Hero(new CommanderCore(), true), 2, core.Hand);

		Assert.Equal(new IntelResult(2, 0), first);
		Assert.True(hand[0].IsRevealedByIntel);
		Assert.True(hand[1].IsRevealedByIntel);
		Assert.False(hand[2].IsRevealedByIntel);

		var second = intel.Resolve(commander, 5, core.Hand);

		Assert.Equal(new IntelResult(2, 3), second);
		Assert.True(hand[3].IsRevealedByIntel);
		Assert.Equal(38, commander.CurrentHealth);
		Assert.Equal(1, commander.CurrentArmor);
	}

	[Fact]
	public void GreedyScoring_LethalHqOutranksEveryNormalAttack()
	{
		var board = new Board();
		var player = new Hero(new CommanderCore(), true);
		player.Core.InitializeHealth(30, 3);
		var attacker = new Minion("raider", false, 3, 3, UnitClass.Assault, 0);
		board.PlaceMinion(attacker, 0);
		int lethal = player.CurrentHealth + player.CurrentArmor;

		double lethalScore = DeckDrivenCommander.ScoreAttack(attacker, player, lethal);
		double chipScore = DeckDrivenCommander.ScoreAttack(attacker, player, lethal + 20);

		Assert.True(lethalScore >= 9000);
		Assert.True(lethalScore > chipScore);
	}

	[Fact]
	public void GreedyScoring_FavorableTradeOutranksSuicideTrade()
	{
		var board = new Board();
		var strong = new Minion("strong", false, 5, 5, UnitClass.Assault, 0);
		var weak = new Minion("weak", false, 1, 1, UnitClass.Assault, 0);
		var smallDefender = new Minion("small", true, 2, 2);
		var bigDefender = new Minion("big", true, 6, 6);
		board.PlaceMinion(smallDefender, 0);
		board.PlaceMinion(bigDefender, 1);

		double favorable = DeckDrivenCommander.ScoreAttack(strong, smallDefender, 30);
		double suicide = DeckDrivenCommander.ScoreAttack(weak, bigDefender, 30);

		Assert.True(favorable > 0);
		Assert.True(favorable > suicide);
	}

	[Fact]
	public void GreedyScoring_EmptyFrontlineMoveOutranksReinforcingOwnFrontline()
	{
		var board = new Board();
		var commander = new Hero(new CommanderCore(), false);
		var raider = new Minion("raider", false, 3, 3, UnitClass.Assault, 1);
		board.PlaceMinion(raider, 0);

		double emptyLine = DeckDrivenCommander.ScoreMove(board, commander, raider);
		Assert.True(emptyLine > 0);

		Assert.True(board.MoveMinionToFrontline(raider));
		double ownedLine = DeckDrivenCommander.ScoreMove(board, commander, raider);

		Assert.True(emptyLine > ownedLine);
	}

	[Fact]
	public void MultiTurnGreedy_CapturesFrontlineThenDamagesPlayerHq()
	{
		var board = new Board();
		var commander = new Hero(new CommanderCore(), false);
		var player = new Hero(new CommanderCore(), true);
		player.Core.InitializeHealth(30);
		var raider = new Minion("raider", false, 3, 3, UnitClass.Assault, 1);
		board.PlaceMinion(raider, 0);

		// 第 1 回合：空前线净收益最高 → 推进抢线。
		Assert.True(DeckDrivenCommander.ScoreMove(board, commander, raider) > 0);
		Assert.True(board.MoveMinionToFrontline(raider));
		Assert.Equal(FrontlineOwner.Enemy, board.FrontlineOwner);

		// 第 2 回合：已占前线，跨线攻击玩家 HQ 的收益为正并真实造成伤害。
		double hqScore = DeckDrivenCommander.ScoreAttack(raider, player, player.CurrentHealth + player.CurrentArmor);
		Assert.True(hqScore > 0);
		int damage = DamageResolver.ResolvePreviewDamage(raider.Attack, raider, player);
		player.TakeDamage(damage, raider, DamageKind.Attack);
		Assert.True(player.CurrentHealth < player.MaxHealth);
	}

	private static List<RuntimeCard> LoadAllRuntimeCards()
	{
		string root = Root();
		return Directory.GetFiles(Path.Combine(root, "Resources", "Cards"), "*.tres")
			.OrderBy(path => path, StringComparer.Ordinal)
			.Select(path =>
			{
				string text = File.ReadAllText(path);
				string id = Regex.Match(text, "(?m)^Id = \"([^\"]+)\"").Groups[1].Value;
				CardType type = Path.GetFileName(path).StartsWith("Minion_", StringComparison.Ordinal) ? CardType.Minion
					: Path.GetFileName(path).StartsWith("Domain_", StringComparison.Ordinal) ? CardType.Domain
					: CardType.Spell;
				return new RuntimeCard(id, type);
			})
			.ToList();
	}

	private static string Root()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null && !File.Exists(Path.Combine(directory.FullName, "project.godot")))
			directory = directory.Parent;
		return directory?.FullName ?? throw new DirectoryNotFoundException("找不到项目根目录");
	}
}
