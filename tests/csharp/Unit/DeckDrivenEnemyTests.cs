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
using OdysseyCards.Roguelike;
using Xunit;

namespace OdysseyCards.Tests.Unit;

/// <summary>
/// 全员同构牌手门禁：
///   - 纯 C# 卡组生成器在 10~20 / 21~39 / 40 张三档厚度下都能稳定产出目标张数；
///   - 主题卡组与敌方卡组严禁带入衍生牌（Derivative）——核心池「显式点名」也不例外；
///   - 所有遭遇入口都返回 DeckDrivenCommander，旧意图怪只保留为死代码。
/// 测试全程不触碰 Godot 运行时——<see cref="DeckProfile"/>、stub 卡与真实 .tres 文本解析都是纯 C#。
/// </summary>
public class DeckDrivenEnemyTests
{
	private sealed class FakeDeckCard(
		string id,
		CardType type,
		int cost,
		CardRarity rarity = CardRarity.Common,
		CardMechanicTag tags = CardMechanicTag.None) : IDeckCard
	{
		public string Id { get; } = id;
		public CardType Type { get; } = type;
		public int Cost { get; } = cost;
		public CardRarity Rarity { get; } = rarity;
		public string PackId { get; } = CardPackCatalog.PackCore;
		public bool HasMechanicTag(CardMechanicTag tag) => tags.HasFlag(tag);
		public bool HasKeyword(Keyword keyword) => false;
	}

	private static List<FakeDeckCard> CreatePool(int count)
		=> Enumerable.Range(0, count)
			.Select(i => new FakeDeckCard(
				$"card_{i}",
				i % 3 == 0 ? CardType.Minion : CardType.Spell,
				i % 6,
				CardRarity.Common,
				i % 2 == 0 ? CardMechanicTag.DirectDamage : CardMechanicTag.Armor))
			.ToList();

	[Theory]
	[InlineData(10, 6)]
	[InlineData(14, 8)]
	[InlineData(20, 10)]
	[InlineData(22, 11)]
	[InlineData(28, 10)]
	[InlineData(39, 10)]
	[InlineData(40, 10)]
	public void Generate_AllDeckThicknesses_ProducesExactTargetCount(int targetSize, int poolSize)
	{
		var profile = new DeckProfile
		{
			HeroId = "test",
			ThemeName = "测试牌组",
			TargetDeckSize = targetSize,
			MaxDuplicatesPerCard = 2,
			MaxDomainCards = 3,
		};

		var result = ThemedDeckGenerator.Generate(profile, CreatePool(poolSize), new Random(1234));

		Assert.Equal(targetSize, result.CardIds.Count);
		Assert.Equal(targetSize, result.Stats.TotalCards);
	}

	[Theory]
	[InlineData(14, 2)]
	[InlineData(20, 2)]
	[InlineData(21, 2)]
	[InlineData(28, 3)]
	[InlineData(39, 4)]
	[InlineData(40, 4)]
	public void ResolveMaxDuplicates_ThickDecks_ExpandAutomatically(int targetSize, int expectedMaxDuplicates)
	{
		var profile = new DeckProfile { TargetDeckSize = targetSize, MaxDuplicatesPerCard = 2 };

		Assert.Equal(expectedMaxDuplicates, ThemedDeckGenerator.ResolveMaxDuplicates(profile, targetSize));
	}

	[Theory]
	[InlineData(10)]
	[InlineData(20)]
	[InlineData(24)]
	[InlineData(40)]
	public void Generate_DerivativeAndStatusCoreCards_AreNeverIncluded(int targetSize)
	{
		var profile = new DeckProfile
		{
			TargetDeckSize = targetSize,
			MaxDuplicatesPerCard = 2,
			// 即使画像把衍生/特殊/状态牌写进招牌核心池，生成器也必须一律忽略。
			CoreCardIds = new[] { "legal_core", "token_core", "special_core", "status_token_core", "status_core" },
		};
		var pool = CreatePool(12);
		pool.Add(new FakeDeckCard("legal_core", CardType.Minion, 1, CardRarity.Common));
		pool.Add(new FakeDeckCard("token_core", CardType.Minion, 1, CardRarity.Derivative));
		pool.Add(new FakeDeckCard("special_core", CardType.Spell, 1, CardRarity.Special));
		pool.Add(new FakeDeckCard("status_token_core", CardType.Spell, 1, CardRarity.StatusToken));
		pool.Add(new FakeDeckCard("status_core", CardType.Status, 1, CardRarity.Common));

		var result = ThemedDeckGenerator.Generate(profile, pool, new Random(7));

		Assert.Equal(targetSize, result.CardIds.Count);
		Assert.Contains("legal_core", result.CardIds);
		Assert.DoesNotContain("token_core", result.CardIds);
		Assert.DoesNotContain("special_core", result.CardIds);
		Assert.DoesNotContain("status_token_core", result.CardIds);
		Assert.DoesNotContain("status_core", result.CardIds);
	}

	[Fact]
	public void EnemyRegistry_AllIds_ReturnOnlyDeckDrivenCommanders()
	{
		foreach (string id in EnemyRegistry.AllIds)
		{
			var encounters = EnemyRegistry.Create(id);
			Assert.NotEmpty(encounters);
			Assert.All(encounters, encounter => Assert.IsType<DeckDrivenCommander>(encounter));
		}
	}

	[Fact]
	public void EnemyDeckProfiles_TierDeckSizes_MatchHomogeneousContract()
	{
		var normals = new[]
		{
			EnemyDeckProfiles.CreateApprentice(), EnemyDeckProfiles.CreateGoutansha(),
			EnemyDeckProfiles.CreateSlimy(), EnemyDeckProfiles.CreateCultist(),
			EnemyDeckProfiles.CreateWolf(),
		};
		foreach (var monster in normals)
		{
			Assert.Equal("normal", monster.Role);
			Assert.InRange(monster.TargetDeckSize, EnemyDeckProfiles.NormalDeckMin, EnemyDeckProfiles.NormalDeckMax);
			Assert.InRange(monster.MaxHealth, 15, 26);
			Assert.NotNull(monster.DeckProfile);
		}

		var elites = new[]
		{
			EnemyDeckProfiles.CreateZhangLang(), EnemyDeckProfiles.CreateShanHu(),
			DeckDrivenCommander.CreateElite("ayame"),
		};
		foreach (var elite in elites)
		{
			Assert.Equal("elite", elite.Role);
			Assert.InRange(elite.TargetDeckSize, EnemyDeckProfiles.EliteDeckMin, EnemyDeckProfiles.EliteDeckMax);
		}

		// 宿敌少女精英走 38 HP 通用带；张郎 / 珊胡保留各自剧情 HP（24），卡组厚度仍属精英档。
		Assert.Equal(28, DeckDrivenCommander.CreateElite("rie").TargetDeckSize);
		Assert.Equal(38, DeckDrivenCommander.CreateElite("rie").MaxHealth);
		Assert.Equal(24, EnemyDeckProfiles.CreateZhangLang().MaxHealth);
		Assert.Equal(24, EnemyDeckProfiles.CreateShanHu().MaxHealth);

		var bosses = new[]
		{
			EnemyDeckProfiles.CreateCosmonaut(), EnemyDeckProfiles.CreateGuardian(),
			DeckDrivenCommander.CreateBoss("sokou"),
		};
		foreach (var boss in bosses)
		{
			Assert.Equal("boss", boss.Role);
			Assert.Equal(EnemyDeckProfiles.BossDeckSize, boss.TargetDeckSize);
			Assert.InRange(boss.MaxHealth, 55, 80);
		}
	}

	[Fact]
	public void Commander_OverridesExternalThemeProfile_WithoutMutatingSource()
	{
		var source = new DeckProfile
		{
			HeroId = "ayame",
			ThemeName = "原始主题",
			TargetDeckSize = 20,
			CoreCardIds = new[] { "spell_ukemi" },
			TagWeights = new Dictionary<CardMechanicTag, int> { [CardMechanicTag.Draw] = 3 },
		};
		var commander = new DeckDrivenCommander(new DeckDrivenCommanderSpec
		{
			Id = "ayame",
			DisplayName = "宿敌",
			MaxHealth = 38,
			TargetDeckSize = 40,
			CoreCardOverrides = new[] { "spell_expose" },
			TagWeightOverrides = new Dictionary<CardMechanicTag, int> { [CardMechanicTag.DirectDamage] = 6 },
			KeywordWeightOverrides = new Dictionary<Keyword, int> { [Keyword.Charge] = 5 },
		});

		var effective = commander.ResolveEffectiveProfile(source);

		Assert.Equal(40, effective.TargetDeckSize);
		Assert.Equal(new[] { "spell_expose" }, effective.CoreCardIds);
		Assert.True(effective.TagWeights.ContainsKey(CardMechanicTag.DirectDamage));
		Assert.Equal(5, effective.KeywordWeights[Keyword.Charge]);
		// 原画像未被污染。
		Assert.Equal(20, source.TargetDeckSize);
		Assert.Equal(new[] { "spell_ukemi" }, source.CoreCardIds);
	}

	[Fact]
	public void EnemyDeckProfiles_ZhangShanDuo_EachHasOwnDeckAndHealth()
	{
		var encounters = EnemyRegistry.Create("zhangshan");

		Assert.Equal(2, encounters.Count);
		var commanders = encounters.Cast<DeckDrivenCommander>().ToList();
		Assert.Equal(new[] { "zhanglang", "shanhu" }, commanders.Select(c => c.HeroId));
		Assert.All(commanders, c =>
		{
			Assert.Equal(22, c.MaxHealth);
			Assert.Equal(22, c.TargetDeckSize);
		});
	}

	[Fact]
	public void EnemyDeckProfiles_Passives_AreDeclaredOnCommander()
	{
		var zhanglang = EnemyDeckProfiles.CreateZhangLang();
		var shanhu = EnemyDeckProfiles.CreateShanHu();

		Assert.Equal(3, zhanglang.DamageCap);
		Assert.False(zhanglang.Unbreakable);
		Assert.Null(shanhu.DamageCap);
		Assert.True(shanhu.Unbreakable);
	}

	[Fact]
	public void EnemyDeckProfiles_Goutansha_KeepsNegativeStartingDefense()
	{
		Assert.Equal(-1, EnemyDeckProfiles.CreateGoutansha().StartingDefense);
	}

	// ===== 真实资源门禁：主题画像与敌方卡组严禁带入衍生牌 =====

	[Fact]
	public void RealResources_ThemeAndEnemyProfiles_NeverGenerateDerivativeCards()
	{
		var pool = LoadResourcePool();
		Assert.Equal(81, pool.Count);
		var byId = pool.ToDictionary(card => card.Id, StringComparer.Ordinal);
		Assert.Contains(pool, card => card.Rarity == CardRarity.Derivative);

		var profiles = LoadThemeProfiles().Concat(LoadEnemyProfiles()).ToList();
		Assert.True(profiles.Count >= 10, $"画像过少：{profiles.Count}");

		foreach (var profile in profiles)
		{
			foreach (string coreId in profile.CoreCardIds)
			{
				Assert.True(byId.TryGetValue(coreId, out var core), $"{profile.HeroId} 的核心卡 {coreId} 不在卡池中");
				Assert.NotEqual(CardRarity.Derivative, core!.Rarity);
				Assert.NotEqual(CardRarity.Special, core.Rarity);
				Assert.NotEqual(CardRarity.StatusToken, core.Rarity);
				Assert.NotEqual(CardType.Status, core.Type);
			}

			var generated = ThemedDeckGenerator.Generate(profile, pool, new Random(2026));

			Assert.Equal(profile.TargetDeckSize, generated.CardIds.Count);
			foreach (string id in generated.CardIds)
			{
				Assert.True(byId.TryGetValue(id, out var card), $"{profile.HeroId} 生成了不存在的卡 {id}");
				Assert.NotEqual(CardRarity.Derivative, card!.Rarity);
				Assert.NotEqual(CardType.Status, card.Type);
			}
		}
	}

	private sealed class ResourceDeckCard(
		string id,
		CardType type,
		int cost,
		CardRarity rarity,
		CardMechanicTag tags,
		IReadOnlyList<int> keywordValues) : IDeckCard
	{
		public string Id { get; } = id;
		public CardType Type { get; } = type;
		public int Cost { get; } = cost;
		public CardRarity Rarity { get; } = rarity;
		public string PackId { get; } = CardPackCatalog.PackCore;
		public bool HasMechanicTag(CardMechanicTag tag) => tags.HasFlag(tag);
		public bool HasKeyword(Keyword keyword) => keywordValues.Contains((int)keyword);
	}

	/// <summary>不实例化 Godot Resource，直接从 .tres 文本还原真实 81 张启用卡池。</summary>
	private static List<ResourceDeckCard> LoadResourcePool()
	{
		string cardsDirectory = Path.Combine(Root(), "Resources", "Cards");
		return Directory.GetFiles(cardsDirectory, "*.tres")
			.OrderBy(path => path, StringComparer.Ordinal)
			.Select(path =>
			{
				string text = File.ReadAllText(path);
				string fileName = Path.GetFileName(path);
				string id = MatchString(text, "Id") ?? throw new InvalidOperationException($"{fileName} 缺少 Id");
				int type = MatchInt(text, "Type") ?? InferType(fileName);
				return new ResourceDeckCard(
					id,
					(CardType)type,
					MatchInt(text, "Cost") ?? 0,
					(CardRarity)(MatchInt(text, "Rarity") ?? (int)CardRarity.Common),
					(CardMechanicTag)(MatchInt(text, "MechanicTags") ?? 0),
					ReadKeywordValues(text));
			})
			.ToList();
	}

	private static int InferType(string fileName)
	{
		if (fileName.StartsWith("Minion_", StringComparison.Ordinal))
			return (int)CardType.Minion;
		if (fileName.StartsWith("Domain_", StringComparison.Ordinal))
			return (int)CardType.Domain;
		if (fileName.StartsWith("Status_", StringComparison.Ordinal))
			return (int)CardType.Status;
		return (int)CardType.Spell;
	}

	/// <summary>还原 Resources/Themes/ThemeProfile_*.tres 的玩家主题画像。</summary>
	private static List<DeckProfile> LoadThemeProfiles()
	{
		string themesDirectory = Path.Combine(Root(), "Resources", "Themes");
		return Directory.GetFiles(themesDirectory, "ThemeProfile_*.tres")
			.OrderBy(path => path, StringComparer.Ordinal)
			.Select(path =>
			{
				string text = File.ReadAllText(path);
				return new DeckProfile
				{
					HeroId = MatchString(text, "HeroId") ?? "",
					ThemeName = MatchString(text, "ThemeName") ?? "",
					TargetDeckSize = MatchInt(text, "TargetDeckSize") ?? 20,
					MaxDuplicatesPerCard = MatchInt(text, "MaxDuplicatesPerCard") ?? 2,
					MaxDomainCards = MatchInt(text, "MaxDomainCards") ?? 3,
					TagWeights = ReadWeightBlock<CardMechanicTag>(text, "TagWeights"),
					KeywordWeights = ReadWeightBlock<Keyword>(text, "KeywordWeights"),
					CoreCardIds = ReadPackedStrings(text, "CoreCardIds"),
				};
			})
			.ToList();
	}

	/// <summary>收集 EnemyRegistry 全部敌人画像（宿敌指挥官走主题资源，画像为 null，由主题门禁覆盖）。</summary>
	private static List<DeckProfile> LoadEnemyProfiles()
		=> EnemyRegistry.AllIds
			.SelectMany(EnemyRegistry.Create)
			.OfType<DeckDrivenCommander>()
			.Select(commander => commander.DeckProfile)
			.OfType<DeckProfile>()
			.ToList();

	private static int? MatchInt(string text, string key)
	{
		var match = Regex.Match(text, $"(?m)^{key} = (\\d+)$");
		return match.Success
			? int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)
			: null;
	}

	private static string? MatchString(string text, string key)
	{
		var match = Regex.Match(text, $"(?m)^{key} = \"([^\"]*)\"$");
		return match.Success ? match.Groups[1].Value : null;
	}

	private static IReadOnlyList<string> ReadPackedStrings(string text, string key)
	{
		var block = Regex.Match(text, $"(?m)^{key} = PackedStringArray\\(([^)]*)\\)");
		return block.Success
			? Regex.Matches(block.Groups[1].Value, "\"([^\"]+)\"")
				.Select(match => match.Groups[1].Value)
				.ToList()
			: Array.Empty<string>();
	}

	private static Dictionary<TKey, int> ReadWeightBlock<TKey>(string text, string key)
		where TKey : struct, Enum
	{
		var result = new Dictionary<TKey, int>();
		var block = Regex.Match(text, $"(?m)^{key} = \\{{([^}}]*)\\}}");
		if (!block.Success)
			return result;
		foreach (Match pair in Regex.Matches(block.Groups[1].Value, "(\\d+)\\s*:\\s*(-?\\d+)"))
		{
			int rawKey = int.Parse(pair.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
			int rawWeight = int.Parse(pair.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
			result[(TKey)Enum.ToObject(typeof(TKey), rawKey)] = rawWeight;
		}
		return result;
	}

	private static IReadOnlyList<int> ReadKeywordValues(string text)
	{
		var line = Regex.Match(text, "(?m)^Keywords = ([^\r\n]*)");
		return line.Success
			? Regex.Matches(line.Groups[1].Value, "\\d+")
				.Select(match => int.Parse(match.Value, System.Globalization.CultureInfo.InvariantCulture))
				.ToList()
			: Array.Empty<int>();
	}

	private static string Root()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null && !File.Exists(Path.Combine(directory.FullName, "project.godot")))
			directory = directory.Parent;
		return directory?.FullName ?? throw new DirectoryNotFoundException("找不到项目根目录");
	}

	// ===== 意图可见性契约：同构牌手不暴露静态意图 =====

	private static Hero CreateEnemyBody() => new(new CommanderCore(), false);

	private static EnemyUnit CreateCommanderUnit(DeckDrivenCommander commander)
		=> new(CreateEnemyBody(), commander);

	[Fact]
	public void EnemyIntentVisibility_AllCommanderTiers_HideHeroIntent()
	{
		var commanders = new[]
		{
			EnemyDeckProfiles.CreateGoutansha(),
			DeckDrivenCommander.CreateElite("rie"),
			DeckDrivenCommander.CreateBoss("sokou"),
		};

		foreach (var commander in commanders)
		{
			var unit = CreateCommanderUnit(commander);
			var controller = Assert.IsType<DeckDrivenCommander>(unit.Controller);
			Assert.False(EnemyIntentVisibility.ShouldShowHeroIntent(controller));
		}
	}

	[Fact]
	public void EnemyIntentVisibility_CommanderMinions_HideIntentOnBothRows()
	{
		var commander = CreateCommanderUnit(EnemyDeckProfiles.CreateGoutansha());
		var units = new[] { commander };
		var board = new Board();
		var support = new Minion("support", false, 1, 2) { OwnerHero = commander.Body };
		var frontline = new Minion("frontline", false, 1, 2) { OwnerHero = commander.Body };

		Assert.True(board.PlaceMinion(support, 0));
		Assert.True(board.PlaceMinion(frontline, 1));
		Assert.True(board.MoveMinionToFrontline(frontline, 0));
		Assert.Equal(BoardRow.EnemySupport, support.BoardRow!.Value);
		Assert.Equal(BoardRow.Frontline, frontline.BoardRow!.Value);

		Assert.True(EnemyIntentVisibility.IsCommanderMinion(support, units));
		Assert.False(EnemyIntentVisibility.ShouldShowMinionIntent(support, units));
		Assert.True(EnemyIntentVisibility.IsCommanderMinion(frontline, units));
		Assert.False(EnemyIntentVisibility.ShouldShowMinionIntent(frontline, units));
	}

	[Fact]
	public void EnemyIntentVisibility_DuoCommanders_HideOnlyOwnMinions()
	{
		var encounters = EnemyRegistry.Create("zhangshan");
		var units = encounters
			.Select(encounter => CreateCommanderUnit(Assert.IsType<DeckDrivenCommander>(encounter)))
			.ToList();
		var secondMinion = new Minion("shanhu_minion", false, 1, 1) { OwnerHero = units[1].Body };
		var orphan = new Minion("orphan", false, 1, 1);

		Assert.All(units, unit => Assert.False(EnemyIntentVisibility.ShouldShowHeroIntent(unit.Controller)));
		Assert.False(EnemyIntentVisibility.ShouldShowMinionIntent(secondMinion, units));
		Assert.True(EnemyIntentVisibility.ShouldShowMinionIntent(orphan, units));
	}

	[Fact]
	public void EnemyIntentVisibility_LegacyEncounterAndOwnerlessMinion_KeepIntent()
	{
		var legacy = new EnemyUnit(CreateEnemyBody(), new Cultist());
		var legacyMinion = new Minion("legacy", false, 1, 1) { OwnerHero = legacy.Body };
		var units = new[] { legacy };

		Assert.True(EnemyIntentVisibility.ShouldShowHeroIntent(legacy.Controller));
		Assert.True(EnemyIntentVisibility.ShouldShowHeroIntent(null));
		Assert.False(EnemyIntentVisibility.IsCommanderMinion(legacyMinion, units));
		Assert.True(EnemyIntentVisibility.ShouldShowMinionIntent(legacyMinion, units));
		Assert.True(EnemyIntentVisibility.ShouldShowMinionIntent(new Minion("orphan", false, 1, 1), units));
		Assert.True(EnemyIntentVisibility.ShouldShowMinionIntent(null, units));
		Assert.False(EnemyIntentVisibility.IsCommanderMinion(null, units));
	}

	[Fact]
	public void EnemyIntentVisibility_MixedBattle_KeepsLegacyMinionVisible()
	{
		var commander = CreateCommanderUnit(EnemyDeckProfiles.CreateGoutansha());
		var legacy = new EnemyUnit(CreateEnemyBody(), new Cultist());
		var units = new[] { commander, legacy };
		var commanderMinion = new Minion("commander_minion", false, 1, 1) { OwnerHero = commander.Body };
		var legacyMinion = new Minion("legacy_minion", false, 1, 1) { OwnerHero = legacy.Body };

		Assert.False(EnemyIntentVisibility.ShouldShowMinionIntent(commanderMinion, units));
		Assert.True(EnemyIntentVisibility.ShouldShowMinionIntent(legacyMinion, units));
	}
}
