using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using OdysseyCards.Core;

namespace OdysseyCards.Tests.Unit;

/// <summary>
/// 单元测试 — CardMechanicTag 枚举位值与 [Flags] 契约。
/// 锁定 Mechanics=65536（原 CardTag.Mechanics 并入后的新位），
/// 并保证所有位互不重叠、None=0、[Flags] 特性存在。
/// </summary>
public class CardMechanicTagTests
{
	[Fact]
	public void None_IsZero()
	{
		Assert.Equal(0, (int)CardMechanicTag.None);
	}

	[Fact]
	public void Mechanics_Equals65536()
	{
		// 原 CardTag.Mechanics=1 并入 CardMechanicTag 后的新位值。
		// 工具层（Tools/CardTagEditor）镜像此常量，改动会破坏迁移映射，必须锁定。
		Assert.Equal(65536, (int)CardMechanicTag.Mechanics);
	}

	[Fact]
	public void AllDefinedBits_AreDistinctPowersOfTwo()
	{
		var values = Enum.GetValues<CardMechanicTag>();
		var seen = new HashSet<int>();

		foreach (var v in values)
		{
			if (v == CardMechanicTag.None)
				continue;
			int iv = (int)v;
			// 每个命名位必须是 2 的幂（单一位）
			Assert.True((iv & (iv - 1)) == 0, $"{v} 不是 2 的幂：{iv}");
			Assert.True(seen.Add(iv), $"{v} 位值 {iv} 与已定义位重复");
		}
	}

	[Fact]
	public void Mechanics_DoesNotOverlapExistingBits()
	{
		// Mechanics=65536 不能与既有的 16 个位（1..32768）重叠
		int mechanics = (int)CardMechanicTag.Mechanics;
		Assert.True(mechanics > 32768, $"Mechanics={mechanics} 应大于既有最高位 Token=32768");
		Assert.Equal(0, mechanics & (int)CardMechanicTag.Token);
		Assert.Equal(0, mechanics & (int)CardMechanicTag.DirectDamage);
	}

	[Fact]
	public void Enum_HasFlagsAttribute()
	{
		var attr = typeof(CardMechanicTag).GetCustomAttribute<FlagsAttribute>();
		Assert.NotNull(attr);
	}

	[Fact]
	public void Mechanics_RecognizedAsSingleBit()
	{
		// 组合位掩码中 Mechanics 应能被 HasFlag 正确识别
		var combined = CardMechanicTag.Mechanics | CardMechanicTag.DirectDamage | CardMechanicTag.Heal;
		Assert.True(combined.HasFlag(CardMechanicTag.Mechanics));
		Assert.True(combined.HasFlag(CardMechanicTag.DirectDamage));
		Assert.True(combined.HasFlag(CardMechanicTag.Heal));
		Assert.False(combined.HasFlag(CardMechanicTag.Armor));
	}
}
