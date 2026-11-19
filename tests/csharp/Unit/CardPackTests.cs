using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OdysseyCards.Core;
using Xunit;

namespace OdysseyCards.Tests.Unit;

/// <summary>
/// 卡包（系列）系统门禁：
///   - .tres 资源的 PackId 权威字段与 0.2.1 归类名单一致；
///   - 机械族标签的加减（40 系列去掉 / 墓碑加上）按位精确；
///   - CardPackCatalog 的启用/禁用过滤逻辑为纯 C#，无需 Godot 运行时即可直测。
/// </summary>
public class CardPackTests
{
	private static string Root()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null && !File.Exists(Path.Combine(directory.FullName, "project.godot")))
			directory = directory.Parent;
		return directory?.FullName ?? throw new DirectoryNotFoundException("找不到项目根目录");
	}

	private static string Read(string file) => File.ReadAllText(Path.Combine(Root(), "Resources", "Cards", file));

	private static string ReadPackId(string file)
	{
		var match = Regex.Match(Read(file), @"(?m)^PackId = ""([^""]+)""$");
		Assert.True(match.Success, $"{file} 缺少 PackId 字段");
		return match.Groups[1].Value;
	}

	private static int ReadMechanicTags(string file)
	{
		var match = Regex.Match(Read(file), @"(?m)^MechanicTags = (\d+)$");
		Assert.True(match.Success, $"{file} 缺少 MechanicTags 字段");
		return int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
	}

	// ===== 资源归类门禁 =====

	[Fact]
	public void AllCards_DeclareNonEmptyPackId()
	{
		var files = Directory.GetFiles(Path.Combine(Root(), "Resources", "Cards"), "*.tres");
		Assert.Equal(68, files.Length);
		foreach (var path in files)
		{
			var text = File.ReadAllText(path);
			var match = Regex.Match(text, @"(?m)^PackId = ""([^""]+)""$");
			Assert.True(match.Success, $"{Path.GetFileName(path)} 缺少 PackId");
			Assert.False(string.IsNullOrWhiteSpace(match.Groups[1].Value), $"{Path.GetFileName(path)} PackId 为空");
		}
	}

	[Theory]
	[InlineData("Minion_LianshuScout.tres", "unmanned_victory")]
	[InlineData("Minion_LianshuTransport.tres", "unmanned_victory")]
	[InlineData("Minion_SmartStinkyEgg.tres", "unmanned_victory")]
	[InlineData("Spell_BladeCrisis.tres", "unmanned_victory")]
	[InlineData("Minion_WhatTheDogDoing.tres", "unmanned_victory")]
	[InlineData("Spell_MambaMissile.tres", "unmanned_victory")]
	[InlineData("Minion_MingshanWalkingSupport.tres", "unmanned_victory")]
	[InlineData("Minion_40MainBattleTank.tres", "unmanned_victory")]
	[InlineData("Minion_40AMainBattleTank.tres", "unmanned_victory")]
	[InlineData("Minion_40BMainBattleTank.tres", "unmanned_victory")]
	[InlineData("Minion_Roach.tres", "future_hunt")]
	[InlineData("Minion_CentipedeAA.tres", "future_hunt")]
	[InlineData("Minion_CentipedeSiege.tres", "future_hunt")]
	[InlineData("Minion_Mech_Lancer.tres", "future_hunt")]
	[InlineData("Minion_Centurion.tres", "future_hunt")]
	[InlineData("Minion_Tombstone.tres", "future_hunt")]
	[InlineData("Spell_FragBullet.tres", "super_body_project")]
	[InlineData("Spell_SmokeRestore.tres", "super_body_project")]
	[InlineData("Domain_PreemptiveStrike.tres", "super_body_project")]
	[InlineData("Spell_093B_SSN.tres", "cutoff_current")]
	[InlineData("Spell_USS_Indiana.tres", "cutoff_current")]
	[InlineData("Spell_BeneathBlackWater.tres", "cutoff_current")]
	[InlineData("Spell_Ultimatum.tres", "cutoff_current")]
	[InlineData("Counter_OhioSSBN.tres", "cutoff_current")]
	[InlineData("Counter_Borei.tres", "cutoff_current")]
	public void ClassifiedCards_MatchDraftPack(string file, string expectedPack)
	{
		Assert.Equal(expectedPack, ReadPackId(file));
	}

	[Fact]
	public void UnlistedCards_DefaultToCore()
	{
		var classified = new HashSet<string>(StringComparer.Ordinal)
		{
			"Minion_LianshuScout.tres", "Minion_LianshuTransport.tres", "Minion_SmartStinkyEgg.tres",
			"Spell_BladeCrisis.tres", "Minion_WhatTheDogDoing.tres", "Spell_MambaMissile.tres",
			"Minion_MingshanWalkingSupport.tres", "Minion_40MainBattleTank.tres",
			"Minion_40AMainBattleTank.tres", "Minion_40BMainBattleTank.tres",
			"Minion_Roach.tres", "Minion_CentipedeAA.tres", "Minion_CentipedeSiege.tres",
			"Minion_Mech_Lancer.tres", "Minion_Centurion.tres", "Minion_Tombstone.tres",
			"Spell_FragBullet.tres", "Spell_SmokeRestore.tres", "Domain_PreemptiveStrike.tres",
			"Spell_093B_SSN.tres", "Spell_USS_Indiana.tres", "Spell_BeneathBlackWater.tres",
			"Spell_Ultimatum.tres", "Counter_OhioSSBN.tres", "Counter_Borei.tres",
		};

		var files = Directory.GetFiles(Path.Combine(Root(), "Resources", "Cards"), "*.tres");
		Assert.Equal(68, files.Length);
		Assert.Equal(25, classified.Count);
		foreach (var path in files)
		{
			var name = Path.GetFileName(path);
			if (!classified.Contains(name))
				Assert.Equal(CardPackCatalog.PackCore, ReadPackId(name));
		}
	}

	[Theory]
	[InlineData("Minion_40MainBattleTank.tres")]
	[InlineData("Minion_40AMainBattleTank.tres")]
	[InlineData("Minion_40BMainBattleTank.tres")]
	public void TankCards_HaveNoMechanicsTribeTag(string file)
	{
		Assert.Equal(0, ReadMechanicTags(file) & (int)CardMechanicTag.Mechanics);
	}

	[Fact]
	public void Tombstone_HasMechanicsTribeTag()
	{
		Assert.NotEqual(0, ReadMechanicTags("Minion_Tombstone.tres") & (int)CardMechanicTag.Mechanics);
	}

	[Fact]
	public void Mingshan_KeepsMechanicsTribeTagInUnmannedPack()
	{
		Assert.Equal("unmanned_victory", ReadPackId("Minion_MingshanWalkingSupport.tres"));
		Assert.NotEqual(0, ReadMechanicTags("Minion_MingshanWalkingSupport.tres") & (int)CardMechanicTag.Mechanics);
	}

	[Fact]
	public void SuperBodyNetaExtras_RemainInCore()
	{
		Assert.Equal(CardPackCatalog.PackCore, ReadPackId("Spell_SmokeDodge.tres"));
		Assert.Equal(CardPackCatalog.PackCore, ReadPackId("Spell_NaDaoFangYu.tres"));
		Assert.Equal(CardPackCatalog.PackCore, ReadPackId("Minion_RayanGe.tres"));
	}

	// ===== 纯 C# 目录逻辑 =====

	[Fact]
	public void Normalize_BlankBecomesCore_AndTrimsDiyIds()
	{
		Assert.Equal(CardPackCatalog.PackCore, CardPackCatalog.Normalize(null));
		Assert.Equal(CardPackCatalog.PackCore, CardPackCatalog.Normalize("   "));
		Assert.Equal(CardPackCatalog.PackCore, CardPackCatalog.Normalize("  core  "));
		Assert.Equal("my_pack", CardPackCatalog.Normalize("  my_pack  "));
	}

	[Fact]
	public void IsDefaultEnabled_ReserveDisabled_EverythingElseEnabled()
	{
		Assert.False(CardPackCatalog.IsDefaultEnabled(CardPackCatalog.PackReserve));
		Assert.True(CardPackCatalog.IsDefaultEnabled(CardPackCatalog.PackCore));
		foreach (var id in CardPackCatalog.BuiltInPackIds.Where(id => id != CardPackCatalog.PackReserve))
			Assert.True(CardPackCatalog.IsDefaultEnabled(id));
		Assert.True(CardPackCatalog.IsDefaultEnabled("future_diy_pack"));
	}

	[Fact]
	public void GetAllPackIds_KeepsBuiltInOrder_AppendsCustomSorted()
	{
		var ids = CardPackCatalog.GetAllPackIds(new[] { "zeta_pack", "core", "alpha_pack", "unmanned_victory", "zeta_pack" });
		Assert.Equal(CardPackCatalog.BuiltInPackIds.Length + 2, ids.Count);
		Assert.Equal(CardPackCatalog.BuiltInPackIds, ids.Take(CardPackCatalog.BuiltInPackIds.Length));
		Assert.Equal(new[] { "alpha_pack", "zeta_pack" }, ids.Skip(CardPackCatalog.BuiltInPackIds.Length));
	}

	[Fact]
	public void IsPackEnabled_CoreAlwaysTrue_OthersFollowDisabledSet()
	{
		var disabled = new HashSet<string>(CardPackCatalog.DefaultDisabledPackIds, StringComparer.OrdinalIgnoreCase);
		Assert.True(CardPackCatalog.IsPackEnabled(CardPackCatalog.PackCore, disabled));
		Assert.False(CardPackCatalog.IsPackEnabled(CardPackCatalog.PackReserve, disabled));
		Assert.True(CardPackCatalog.IsPackEnabled(CardPackCatalog.PackUnmannedVictory, disabled));
		Assert.True(CardPackCatalog.IsPackEnabled("brand_new_diy_pack", disabled)); // 新包默认启用
	}

	[Fact]
	public void SetPackEnabled_CannotDisableCore_ReportsActualChange()
	{
		var disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		Assert.False(CardPackCatalog.SetPackEnabled(disabled, CardPackCatalog.PackCore, false));
		Assert.Empty(disabled);

		Assert.True(CardPackCatalog.SetPackEnabled(disabled, CardPackCatalog.PackFutureHunt, false));
		Assert.False(CardPackCatalog.SetPackEnabled(disabled, CardPackCatalog.PackFutureHunt, false)); // 幂等
		Assert.True(CardPackCatalog.SetPackEnabled(disabled, CardPackCatalog.PackFutureHunt, true));
		Assert.Empty(disabled);
	}

	[Fact]
	public void FilterEnabledCards_RemovesDisabledPacks_KeepsOrder()
	{
		var disabled = new HashSet<string> { CardPackCatalog.PackReserve };
		var cards = new[]
		{
			"A|core", "B|unmanned_victory", "C|future_hunt", "D|reserve", "E|vanguard",
		};

		var result = CardPackCatalog.FilterEnabledCards(cards, c => c.Split('|')[1], disabled);

		Assert.Equal(new[] { "A|core", "B|unmanned_victory", "C|future_hunt", "E|vanguard" }, result);
	}

	[Fact]
	public void FilterEnabledCards_WhenEverythingDisabled_FallsBackToCore()
	{
		var disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			CardPackCatalog.PackUnmannedVictory, CardPackCatalog.PackFutureHunt, CardPackCatalog.PackReserve,
		};
		var cards = new[]
		{
			"core_card|core", "mech_card|future_hunt", "reserve_card|reserve",
		};

		var result = CardPackCatalog.FilterEnabledCards(cards, c => c.Split('|')[1], disabled);

		Assert.Single(result);
		Assert.Equal("core_card|core", result[0]);
	}

	[Fact]
	public void FilterEnabledCards_WithNoCoreCards_ReturnsEmptySafely()
	{
		var disabled = new HashSet<string> { "diy_only" };
		var cards = new[] { "diy_card|diy_only" };

		var result = CardPackCatalog.FilterEnabledCards(cards, c => c.Split('|')[1], disabled);

		Assert.Empty(result);
	}
}
