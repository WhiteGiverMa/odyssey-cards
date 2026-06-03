using Godot;
using OdysseyCards.UI;
using Xunit;

namespace OdysseyCards.Tests.Unit;

/// <summary>
/// 单元测试 — CardFlyVfx.QuadraticBezier 二次贝塞尔插值数学。
/// 纯数学函数，无需 Godot 运行时。
/// </summary>
public class CardFlyVfxTests
{
	[Fact]
	public void QuadraticBezier_Start_ReturnsStart()
	{
		var start = new Vector2(10f, 20f);
		var ctrl = new Vector2(50f, 0f);
		var end = new Vector2(100f, 60f);

		var result = CardFlyVfx.QuadraticBezier(0f, start, ctrl, end);

		Assert.Equal(start.X, result.X);
		Assert.Equal(start.Y, result.Y);
	}

	[Fact]
	public void QuadraticBezier_End_ReturnsEnd()
	{
		var start = new Vector2(10f, 20f);
		var ctrl = new Vector2(50f, 0f);
		var end = new Vector2(100f, 60f);

		var result = CardFlyVfx.QuadraticBezier(1f, start, ctrl, end);

		Assert.Equal(end.X, result.X);
		Assert.Equal(end.Y, result.Y);
	}

	[Fact]
	public void QuadraticBezier_Halfway_ReturnsMidpoint()
	{
		var start = new Vector2(0f, 0f);
		var ctrl = new Vector2(10f, 0f);
		var end = new Vector2(20f, 0f);

		var result = CardFlyVfx.QuadraticBezier(0.5f, start, ctrl, end);

		Assert.Equal(10f, result.X, 4);
		Assert.Equal(0f, result.Y, 4);
	}

	[Fact]
	public void QuadraticBezier_Quarter_ReturnsCorrectValue()
	{
		var start = new Vector2(0f, 0f);
		var ctrl = new Vector2(0f, 10f);
		var end = new Vector2(10f, 10f);

		var result = CardFlyVfx.QuadraticBezier(0.25f, start, ctrl, end);

		// t=0.25: u=0.75
		// B = 0.75²·(0,0) + 2·0.75·0.25·(0,10) + 0.25²·(10,10)
		// B = (0,0) + 0.375·(0,10) + 0.0625·(10,10)
		// B = (0, 3.75) + (0.625, 0.625) = (0.625, 4.375)
		Assert.Equal(0.625f, result.X, 5);
		Assert.Equal(4.375f, result.Y, 5);
	}

	[Fact]
	public void QuadraticBezier_ThreeQuarter_ReturnsCorrectValue()
	{
		var start = new Vector2(0f, 0f);
		var ctrl = new Vector2(0f, 10f);
		var end = new Vector2(10f, 10f);

		var result = CardFlyVfx.QuadraticBezier(0.75f, start, ctrl, end);

		// t=0.75: u=0.25
		// B = 0.25²·(0,0) + 2·0.25·0.75·(0,10) + 0.75²·(10,10)
		// B = 0 + 0.375·(0,10) + 0.5625·(10,10)
		// B = (0, 3.75) + (5.625, 5.625) = (5.625, 9.375)
		Assert.Equal(5.625f, result.X, 5);
		Assert.Equal(9.375f, result.Y, 5);
	}
}
