using Xunit;
using OdysseyCards.Core;

namespace OdysseyCards.Tests.Unit;

/// <summary>
/// 单元测试 — CardArtworkGenerator 卡面规格派生。
/// ResolveSpec 是纯函数（同输入恒同输出），不依赖 Godot 运行时，可直接测试。
/// </summary>
public class CardArtworkGeneratorTests
{
	[Fact]
	public void ResolveSpec_SameInput_ProducesIdenticalSpec()
	{
		var a = CardArtworkGenerator.ResolveSpec("minion_slime", CardType.Minion,
			CardRarity.Common, CardMechanicTag.None, "", null);
		var b = CardArtworkGenerator.ResolveSpec("minion_slime", CardType.Minion,
			CardRarity.Common, CardMechanicTag.None, "", null);

		Assert.Equal(a.Seed, b.Seed);
		Assert.Equal(a.SymbolIndex, b.SymbolIndex);
		Assert.Equal(a.StarCount, b.StarCount);
		Assert.Equal(a.BaseColor, b.BaseColor);
		Assert.Equal(a.SymbolRotation, b.SymbolRotation);
	}

	[Fact]
	public void ResolveSpec_DifferentIds_ProduceDifferentSeeds()
	{
		var a = CardArtworkGenerator.ResolveSpec("minion_slime", CardType.Minion,
			CardRarity.Common, CardMechanicTag.None, "", null);
		var b = CardArtworkGenerator.ResolveSpec("minion_roach", CardType.Minion,
			CardRarity.Common, CardMechanicTag.None, "", null);

		Assert.NotEqual(a.Seed, b.Seed);
	}

	[Fact]
	public void ResolveSpec_ManyIds_MostlyDistinctSymbols()
	{
		// 统计意义：100 张不同卡的符号索引不应全部撞车（符号库 12 种）
		var seen = new System.Collections.Generic.HashSet<int>();
		for (int i = 0; i < 100; i++)
		{
			var spec = CardArtworkGenerator.ResolveSpec($"card_{i}", CardType.Spell,
				CardRarity.Common, CardMechanicTag.None, "", null);
			seen.Add(spec.SymbolIndex);
		}

		Assert.True(seen.Count >= 6, $"符号多样性不足：100 张卡只用了 {seen.Count} 种符号");
	}

	[Fact]
	public void ResolveSpec_Minion_UsesWarmGoldPalette()
	{
		var spec = CardArtworkGenerator.ResolveSpec("minion_tank", CardType.Minion,
			CardRarity.Common, CardMechanicTag.None, "", null);

		// 暖金：R 通道显著高于 B 通道
		Assert.True(spec.AccentColor.R > spec.AccentColor.B,
			$"随从基调应为暖金色，实际 {spec.AccentColor}");
	}

	[Fact]
	public void ResolveSpec_Spell_UsesCyanBluePalette()
	{
		var spec = CardArtworkGenerator.ResolveSpec("spell_strike", CardType.Spell,
			CardRarity.Common, CardMechanicTag.None, "", null);

		// 青蓝：B 通道高于 R 通道
		Assert.True(spec.AccentColor.B > spec.AccentColor.R,
			$"法术基调应为青蓝色，实际 {spec.AccentColor}");
	}

	[Fact]
	public void ResolveSpec_DomainByMechanicTag_UsesVioletPalette()
	{
		var spec = CardArtworkGenerator.ResolveSpec("domain_infinite_fire", CardType.Spell,
			CardRarity.Common, CardMechanicTag.Domain, "", null);

		// 紫罗兰：R 与 B 均高，G 明显低
		Assert.True(spec.AccentColor.R > 0.6f && spec.AccentColor.B > 0.9f && spec.AccentColor.G < 0.75f,
			$"领域基调应为紫罗兰色，实际 {spec.AccentColor}");
	}

	[Fact]
	public void ResolveSpec_DomainByDomainId_UsesVioletPalette()
	{
		var spec = CardArtworkGenerator.ResolveSpec("domain_flying_away", CardType.Spell,
			CardRarity.Common, CardMechanicTag.None, "flying_away", null);

		Assert.True(spec.AccentColor.R > 0.6f && spec.AccentColor.B > 0.9f && spec.AccentColor.G < 0.75f,
			$"DomainId 非空应识别为领域（紫罗兰），实际 {spec.AccentColor}");
	}

	[Fact]
	public void ResolveSpec_MasterRarity_HasGoldGlow()
	{
		var spec = CardArtworkGenerator.ResolveSpec("spell_plan", CardType.Spell,
			CardRarity.Master, CardMechanicTag.None, "", null);

		Assert.True(spec.GlowColor.A > 0.5f, "大师级应有光晕");
		Assert.True(spec.GlowColor.R > 0.9f && spec.GlowColor.G > 0.7f, "大师级光晕应为金色");
	}

	[Fact]
	public void ResolveSpec_CommonRarity_HasNoGlow()
	{
		var spec = CardArtworkGenerator.ResolveSpec("spell_strike", CardType.Spell,
			CardRarity.Common, CardMechanicTag.None, "", null);

		Assert.True(spec.GlowColor.A < 0.01f, "一般级不应有光晕");
	}

	[Theory]
	[InlineData("ayame", ArtworkSymbolStyle.Rune)]
	[InlineData("rie", ArtworkSymbolStyle.Mecha)]
	[InlineData("sokou", ArtworkSymbolStyle.Mecha)]
	[InlineData("qimeng", ArtworkSymbolStyle.Abstract)]
	[InlineData(null, ArtworkSymbolStyle.Abstract)]
	public void ResolveSpec_HeroTheme_MapsToSymbolStyle(string? heroTheme, ArtworkSymbolStyle expected)
	{
		var spec = CardArtworkGenerator.ResolveSpec("any_card", CardType.Spell,
			CardRarity.Common, CardMechanicTag.None, "", heroTheme);

		Assert.Equal(expected, spec.Style);
	}

	[Fact]
	public void StableHash_SameString_SameValue()
	{
		Assert.Equal(CardArtworkGenerator.StableHash("minion_slime"),
			CardArtworkGenerator.StableHash("minion_slime"));
	}

	[Fact]
	public void StableHash_AlwaysNonNegative()
	{
		// FNV-1a 结果掩码到正数区间，可直接作为 Random 种子
		for (int i = 0; i < 50; i++)
		{
			Assert.True(CardArtworkGenerator.StableHash($"test_{i}") >= 0);
		}
	}

	[Fact]
	public void ResolveSpec_StarCount_WithinDesignedRange()
	{
		for (int i = 0; i < 30; i++)
		{
			var spec = CardArtworkGenerator.ResolveSpec($"card_{i}", CardType.Minion,
				CardRarity.Common, CardMechanicTag.None, "", null);
			Assert.InRange(spec.StarCount, 18, 32);
		}
	}
}
