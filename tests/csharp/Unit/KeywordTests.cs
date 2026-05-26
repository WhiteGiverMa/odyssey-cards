using System;
using System.Collections.Generic;
using Xunit;
using OdysseyCards.Core;

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
    }

    [Fact]
    public void KeywordCount_AtLeastFive()
    {
        var values = Enum.GetValues<Keyword>();
        Assert.True(values.Length >= 5, $"Expected at least 5 keywords, got {values.Length}");
    }
}
