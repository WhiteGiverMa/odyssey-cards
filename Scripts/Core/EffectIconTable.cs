using OdysseyCards.Localization;
using System.Collections.Generic;

namespace OdysseyCards.Core;

/// <summary>
/// 效果图标元数据。
/// </summary>
public readonly struct EffectIconData
{
	public string Icon { get; init; }
	public string NameKey { get; init; }
	public string DescKey { get; init; }
	public bool IsBuff { get; init; }
}

/// <summary>
/// 效果图标映射表。将效果 ID 映射到 Emoji、名称和描述。
/// 名称和描述通过 Localization.T() 获取，支持多语言。
/// </summary>
public static class EffectIconTable
{
	private static readonly Dictionary<string, EffectIconData> _statusEffects = new()
	{
		["attack_zero"] = new() { Icon = "🔒", NameKey = "effect.attack_zero", DescKey = "effect.attack_zero_desc", IsBuff = false },
		["meltdown"] = new() { Icon = "🔥", NameKey = "effect.meltdown", DescKey = "effect.meltdown_desc", IsBuff = false },
		["weapon_disabled"] = new() { Icon = "⛓", NameKey = "effect.weapon_disabled", DescKey = "effect.weapon_disabled_desc", IsBuff = false },
		["animosity"] = new() { Icon = "💢", NameKey = "effect.animosity", DescKey = "effect.animosity_desc", IsBuff = false },
		["vulnerable"] = new() { Icon = "💔", NameKey = "effect.vulnerable", DescKey = "effect.vulnerable_desc", IsBuff = false },
		["weak"] = new() { Icon = "🔽", NameKey = "effect.weak", DescKey = "effect.weak_desc", IsBuff = false },
		["fragile"] = new() { Icon = "🛡", NameKey = "effect.fragile", DescKey = "effect.fragile_desc", IsBuff = false },
		["total_observation"] = new() { Icon = "👁", NameKey = "effect.total_observation", DescKey = "effect.total_observation_desc", IsBuff = false },
		["attack_ban"] = new() { Icon = "🚫", NameKey = "effect.attack_ban", DescKey = "effect.attack_ban_desc", IsBuff = false },
		["damage_over_time"] = new() { Icon = "🩸", NameKey = "effect.damage_over_time", DescKey = "effect.damage_over_time_desc", IsBuff = false },
	};

	private static readonly Dictionary<string, EffectIconData> _domains = new()
	{
		["unlimited_potential"] = new() { Icon = "♾", NameKey = "domain.unlimited_potential", DescKey = "domain.unlimited_potential_desc", IsBuff = true },
		["flying_away"] = new() { Icon = "🕊", NameKey = "domain.flying_away", DescKey = "domain.flying_away_desc", IsBuff = true },
		["idol_twilight"] = new() { Icon = "🌅", NameKey = "domain.idol_twilight", DescKey = "domain.idol_twilight_desc", IsBuff = true },
	};

	private static readonly Dictionary<string, EffectIconData> _keywordSources = new()
	{
		["bait_tactics"] = new() { Icon = "🎯", NameKey = "keyword_source.bait_tactics", DescKey = "keyword_source.bait_tactics_desc", IsBuff = true },
	};

	private static readonly Dictionary<string, EffectIconData> _modifiers = new()
	{
		["animosity"] = new() { Icon = "💢", NameKey = "effect.animosity", DescKey = "effect.animosity_desc", IsBuff = false },
	};

	/// <summary>
	/// 获取状态效果 ID 对应的图标数据。
	/// </summary>
	public static EffectIconData? GetStatusEffect(string id)
	{
		return _statusEffects.TryGetValue(id, out var data) ? data : null;
	}

	/// <summary>
	/// 获取领域 ID 对应的图标数据。
	/// </summary>
	public static EffectIconData? GetDomain(string domainId)
	{
		return _domains.TryGetValue(domainId, out var data) ? data : null;
	}

	/// <summary>
	/// 获取关键词来源 ID 对应的图标数据。
	/// </summary>
	public static EffectIconData? GetKeywordSource(string sourceId)
	{
		return _keywordSources.TryGetValue(sourceId, out var data) ? data : null;
	}

	/// <summary>
	/// 获取运行时修饰 ID 对应的图标数据（如敌意伤害翻倍）。
	/// </summary>
	public static EffectIconData? GetModifier(string modifierId)
	{
		return _modifiers.TryGetValue(modifierId, out var data) ? data : null;
	}

	/// <summary>
	/// 将 EffectIconData 转换为 DisplayableEffect。
	/// </summary>
	public static DisplayableEffect ToDisplayable(
		EffectIconData data,
		EffectCategory category,
		int stacks = 0,
		string? sourceId = null)
	{
		return new DisplayableEffect
		{
			Icon = data.Icon,
			Name = Localization.Localization.T(data.NameKey, ""),
			Stacks = stacks,
			Description = Localization.Localization.T(data.DescKey, ""),
			IsBuff = data.IsBuff,
			Category = category,
			SourceId = sourceId,
		};
	}
}
