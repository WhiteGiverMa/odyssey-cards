using OdysseyCards.Core;
using Xunit;

namespace OdysseyCards.Tests.Unit;

/// <summary>
/// 单元测试 — 单位速度数据契约。
/// </summary>
public class SpeedRulesTests
{
	[Fact]
	public void DefaultSpeed_IsTwo()
	{
		Assert.Equal(2, SpeedRules.Default);
	}

	[Fact]
	public void ClampSpeed_OutOfRange_UsesSupportedBounds()
	{
		Assert.Equal(SpeedRules.Min, SpeedRules.Clamp(0));
		Assert.Equal(SpeedRules.Max, SpeedRules.Clamp(6));
	}

	[Fact]
	public void TauntSpeed_IsThree()
	{
		Assert.Equal(3, SpeedRules.Taunt);
	}
}
