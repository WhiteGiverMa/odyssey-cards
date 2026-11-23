using System;
using System.Collections.Generic;
using System.Linq;

namespace OdysseyCards.Core;

/// <summary>
/// 卡包（扩展包 / 系列）目录。
/// <para>
/// 纯 C# 静态契约，不依赖 Godot，便于 xUnit 与 Web 工具层镜像同一套 ID。
/// <see cref="CardData.PackId"/> 是资源上的权威字段；本目录提供 ID 常量、规范化、
/// 启用/禁用过滤与枚举辅助。
/// </para>
/// <para>
/// 卡包是与机制标签（<see cref="CardMechanicTag"/>）、关键词（<see cref="Keyword"/>）
/// 正交的独立单值维度：卡包不占机制标签位，机制标签也不反推卡包。
/// </para>
/// </summary>
public static class CardPackCatalog
{
	/// <summary>核心包——保底卡包，永远启用。</summary>
	public const string PackCore = "core";

	/// <summary>无人智胜。</summary>
	public const string PackUnmannedVictory = "unmanned_victory";

	/// <summary>未来追杀（neta 环世界）。</summary>
	public const string PackFutureHunt = "future_hunt";

	/// <summary>超体计划（neta 和平精英超体对抗）。</summary>
	public const string PackSuperBodyProject = "super_body_project";

	/// <summary>预备包——默认禁用，存放待重做/有已知问题的卡。</summary>
	public const string PackReserve = "reserve";

	/// <summary>先锋包——新卡默认归属，当前为空。</summary>
	public const string PackVanguard = "vanguard";

	/// <summary>断流——潜艇战主题（0.2.2）。默认启用。</summary>
	public const string PackCutoffCurrent = "cutoff_current";

	/// <summary>内置卡包 ID（稳定顺序，供 UI 枚举）。</summary>
	public static readonly string[] BuiltInPackIds =
	{
		PackCore,
		PackUnmannedVictory,
		PackFutureHunt,
		PackSuperBodyProject,
		PackCutoffCurrent,
		PackReserve,
		PackVanguard,
	};

	/// <summary>默认禁用的卡包——新存档只禁用「预备」。</summary>
	public static readonly string[] DefaultDisabledPackIds = { PackReserve };

	/// <summary>规范化 PackId：空白视为核心包。</summary>
	public static string Normalize(string? packId)
		=> string.IsNullOrWhiteSpace(packId) ? PackCore : packId.Trim();

	/// <summary>是否为内置卡包（大小写不敏感）。</summary>
	public static bool IsBuiltInPack(string? packId)
	{
		var id = Normalize(packId);
		foreach (var builtIn in BuiltInPackIds)
		{
			if (string.Equals(id, builtIn, StringComparison.OrdinalIgnoreCase))
				return true;
		}
		return false;
	}

	/// <summary>某包是否默认在战斗中启用（核心包恒启用，预备包恒禁用）。</summary>
	public static bool IsDefaultEnabled(string? packId)
	{
		var id = Normalize(packId);
		if (string.Equals(id, PackCore, StringComparison.OrdinalIgnoreCase))
			return true;
		return !string.Equals(id, PackReserve, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// 纯 C# 启用判定——GameManager 的 <c>IsPackEnabled</c> 委托至此，便于 xUnit 直测。
	/// 核心包恒启用；其余包看是否在禁用集合中。
	/// </summary>
	public static bool IsPackEnabled(string? packId, ISet<string> disabledPackIds)
	{
		var id = Normalize(packId);
		if (string.Equals(id, PackCore, StringComparison.OrdinalIgnoreCase))
			return true;
		return !disabledPackIds.Contains(id);
	}

	/// <summary>
	/// 纯 C# 启用状态变更——返回是否真的变了。核心包不可禁用。
	/// </summary>
	public static bool SetPackEnabled(ISet<string> disabledPackIds, string? packId, bool enabled)
	{
		var id = Normalize(packId);
		if (!enabled && string.Equals(id, PackCore, StringComparison.OrdinalIgnoreCase))
			return false;
		return enabled ? disabledPackIds.Remove(id) : disabledPackIds.Add(id);
	}

	/// <summary>
	/// 纯 C# 卡池过滤——按禁用列表过滤，结果为空时保底返回核心包卡牌。
	/// GameManager.GetEnabledCards 委托至此，战斗/奖励/构筑随机池共用同一套口径。
	/// </summary>
	public static List<T> FilterEnabledCards<T>(
		IEnumerable<T> cards,
		Func<T, string?> packIdSelector,
		ISet<string> disabledPackIds)
	{
		var all = cards.ToList();
		var enabled = all.Where(c => IsPackEnabled(packIdSelector(c), disabledPackIds)).ToList();
		if (enabled.Count > 0)
			return enabled;

		return all.Where(c => string.Equals(
			Normalize(packIdSelector(c)),
			PackCore,
			StringComparison.OrdinalIgnoreCase)).ToList();
	}

	/// <summary>本地化 key（先查 key，缺省值用 <see cref="GetDefaultDisplayName"/>）。</summary>
	public static string GetDisplayNameKey(string? packId) => $"ui.pack.{Normalize(packId)}";

	/// <summary>未配翻译时的兜底显示名。</summary>
	public static string GetDefaultDisplayName(string? packId) => Normalize(packId) switch
	{
		PackCore => "核心",
		PackUnmannedVictory => "无人智胜",
		PackFutureHunt => "未来追杀",
		PackSuperBodyProject => "超体计划",
		PackCutoffCurrent => "断流",
		PackReserve => "预备",
		PackVanguard => "先锋",
		var other => other,
	};

	/// <summary>
	/// 枚举全部卡包 ID：内置包按稳定顺序在前，卡牌里出现的自定义 DIY 包追加在后（序数排序）。
	/// </summary>
	public static IReadOnlyList<string> GetAllPackIds(IEnumerable<string?> cardPackIds)
	{
		var ids = new List<string>(BuiltInPackIds);
		var custom = new List<string>();
		foreach (var raw in cardPackIds)
		{
			var id = Normalize(raw);
			if (IsBuiltInPack(id))
				continue;
			if (!custom.Contains(id, StringComparer.OrdinalIgnoreCase))
				custom.Add(id);
		}
		custom.Sort(StringComparer.Ordinal);
		ids.AddRange(custom);
		return ids;
	}
}
