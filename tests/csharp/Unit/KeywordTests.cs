using System;
using System.Collections.Generic;
using OdysseyCards.Core;
using Xunit;

namespace OdysseyCards.Tests.Unit;

/// <summary>
/// 单元测试 — Keyword 枚举值
/// </summary>
public class KeywordTests
{
	[Fact]
	public void KeywordValues_AreDistinct()
	{
		var values = Enum.GetValues<Keyword>();
		var distinct = new HashSet<Keyword>(values);

		Assert.Equal(values.Length, distinct.Count);
	}

	[Fact]
	public void AllStandardKeywords_HaveDefinedValues()
	{
		var values = Enum.GetValues<Keyword>();

		Assert.Contains(Keyword.Charge, values);
		Assert.Contains(Keyword.Taunt, values);
		Assert.Contains(Keyword.Battlecry, values);
		Assert.Contains(Keyword.Deathrattle, values);
		Assert.Contains(Keyword.Windfury, values);
		Assert.Contains(Keyword.Ambush, values);
		Assert.Contains(Keyword.Impact, values);
	}

	[Fact]
	public void KeywordCount_AtLeastSeven()
	{
		var values = Enum.GetValues<Keyword>();
		Assert.True(values.Length >= 7, $"Expected at least 7 keywords, got {values.Length}");
	}
}
