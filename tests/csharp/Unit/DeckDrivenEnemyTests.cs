using System;
using System.Collections.Generic;
using System.Linq;
using OdysseyCards.AI;
using OdysseyCards.Core;
using OdysseyCards.Roguelike;
using Xunit;

namespace OdysseyCards.Tests.Unit;

/// <summary>
/// 全员同构牌手门禁：
///   - 纯 C# 卡组生成器在 10~20 / 21~39 / 40 张三档厚度下都能稳定产出目标张数；
///   - 所有遭遇入口都返回 DeckDrivenCommander，旧意图怪只保留为死代码。
/// 测试全程不触碰 Godot 运行时——<see cref="DeckProfile"/> 与 stub 卡都是纯 C#。
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

	[Fact]
	public void Generate_DerivativeCoreCard_IsStillIncluded()
	{
		var profile = new DeckProfile
		{
			TargetDeckSize = 5,
			CoreCardIds = new[] { "token_core" },
		};
		var pool = CreatePool(8);
		pool.Add(new FakeDeckCard("token_core", CardType.Minion, 1, CardRarity.Derivative));

		var result = ThemedDeckGenerator.Generate(profile, pool, new Random(7));

		Assert.Contains("token_core", result.CardIds);
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
}
