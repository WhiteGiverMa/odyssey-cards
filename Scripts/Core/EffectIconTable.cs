using System;
using System.Collections.Generic;

namespace OdysseyCards.Core;

/// <summary>效果图标配色与双模文本元数据。</summary>
public readonly struct EffectIconData
{
	public string Icon { get; init; }
	public string PlainLabel { get; init; }
	public string PlainLabelEn { get; init; }
	public uint AccentColorRgba { get; init; }
	public string NameKey { get; init; }
	public string DescKey { get; init; }
	public bool IsBuff { get; init; }
}

/// <summary>
/// 效果图标映射表。Emoji 只供对照模式使用，默认模式显示纯色底板与简字。
/// </summary>
public static class EffectIconTable
{
	public const uint AttackColor = 0xFF6B7AFF;
	public const uint DefenseColor = 0x7FD8FFFF;
	public const uint DomainColor = 0xFFD98EFF;
	public const uint DebuffColor = 0x9B62C7FF;
	public const uint BuffColor = 0x69D99AFF;

	private static readonly Dictionary<string, EffectIconData> _statusEffects = new()
	{
		["attack_zero"] = new() { Icon = "🔒", PlainLabel = "锁", PlainLabelEn = "LCK", AccentColorRgba = DebuffColor, NameKey = "effect.attack_zero", DescKey = "effect.attack_zero_desc", IsBuff = false },
		["meltdown"] = new() { Icon = "🔥", PlainLabel = "熔", PlainLabelEn = "MLT", AccentColorRgba = AttackColor, NameKey = "effect.meltdown", DescKey = "effect.meltdown_desc", IsBuff = false },
		["weapon_disabled"] = new() { Icon = "⛓", PlainLabel = "禁", PlainLabelEn = "DIS", AccentColorRgba = DebuffColor, NameKey = "effect.weapon_disabled", DescKey = "effect.weapon_disabled_desc", IsBuff = false },
		["animosity"] = new() { Icon = "💢", PlainLabel = "怒", PlainLabelEn = "ANI", AccentColorRgba = AttackColor, NameKey = "effect.animosity", DescKey = "effect.animosity_desc", IsBuff = false },
		["vulnerable"] = new() { Icon = "💔", PlainLabel = "易", PlainLabelEn = "VUL", AccentColorRgba = AttackColor, NameKey = "effect.vulnerable", DescKey = "effect.vulnerable_desc", IsBuff = false },
		["weak"] = new() { Icon = "🔽", PlainLabel = "弱", PlainLabelEn = "WEK", AccentColorRgba = DebuffColor, NameKey = "effect.weak", DescKey = "effect.weak_desc", IsBuff = false },
		["fragile"] = new() { Icon = "🛡", PlainLabel = "脆", PlainLabelEn = "FRG", AccentColorRgba = DebuffColor, NameKey = "effect.fragile", DescKey = "effect.fragile_desc", IsBuff = false },
		["total_observation"] = new() { Icon = "👁", PlainLabel = "察", PlainLabelEn = "OBS", AccentColorRgba = DefenseColor, NameKey = "effect.total_observation", DescKey = "effect.total_observation_desc", IsBuff = false },
		["attack_ban"] = new() { Icon = "🚫", PlainLabel = "封", PlainLabelEn = "BAN", AccentColorRgba = DebuffColor, NameKey = "effect.attack_ban", DescKey = "effect.attack_ban_desc", IsBuff = false },
		["damage_over_time"] = new() { Icon = "🩸", PlainLabel = "蚀", PlainLabelEn = "DOT", AccentColorRgba = AttackColor, NameKey = "effect.damage_over_time", DescKey = "effect.damage_over_time_desc", IsBuff = false },
		["incapacitated"] = new() { Icon = "⛔", PlainLabel = "瘫", PlainLabelEn = "INC", AccentColorRgba = DebuffColor, NameKey = "effect.incapacitated", DescKey = "effect.incapacitated_desc", IsBuff = false },
		// 限时 mount 效果（原属领域表面，归类到限时 Power，name/desc 保留 domain.* 翻译键）
		["shiyoru_raidenkou"] = new() { Icon = "⚡", PlainLabel = "雷", PlainLabelEn = "RAI", AccentColorRgba = DomainColor, NameKey = "domain.shiyoru_raidenkou", DescKey = "domain.shiyoru_raidenkou_desc", IsBuff = true },
		["sutaraito_spirit"] = new() { Icon = "⭐", PlainLabel = "星", PlainLabelEn = "STR", AccentColorRgba = DomainColor, NameKey = "domain.sutaraito_spirit", DescKey = "domain.sutaraito_spirit_desc", IsBuff = true },
		["smokescreen"] = new() { Icon = "🌫", PlainLabel = "烟", PlainLabelEn = "SMK", AccentColorRgba = DefenseColor, NameKey = "status.smokescreen.name", DescKey = "status.smokescreen.desc", IsBuff = true },
		["fatal_rift"] = new() { Icon = "💀", PlainLabel = "裂", PlainLabelEn = "RFT", AccentColorRgba = AttackColor, NameKey = "status.fatal_rift.name", DescKey = "status.fatal_rift.desc", IsBuff = false },
	};

	private static readonly Dictionary<string, EffectIconData> _domains = new()
	{
		["unlimited_potential"] = new() { Icon = "♾", PlainLabel = "潜", PlainLabelEn = "INF", AccentColorRgba = DomainColor, NameKey = "domain.unlimited_potential", DescKey = "domain.unlimited_potential_desc", IsBuff = true },
		["flying_away"] = new() { Icon = "🕊", PlainLabel = "飞", PlainLabelEn = "FLY", AccentColorRgba = DomainColor, NameKey = "domain.flying_away", DescKey = "domain.flying_away_desc", IsBuff = true },
		["idol_twilight"] = new() { Icon = "🌅", PlainLabel = "昏", PlainLabelEn = "IDL", AccentColorRgba = DomainColor, NameKey = "domain.idol_twilight", DescKey = "domain.idol_twilight_desc", IsBuff = true },
		["jiehuafa"] = new() { Icon = "🛡", PlainLabel = "接", PlainLabelEn = "JHF", AccentColorRgba = DefenseColor, NameKey = "domain.jiehuafa", DescKey = "domain.jiehuafa_desc", IsBuff = true },
		["focus"] = new() { Icon = "🎯", PlainLabel = "聚", PlainLabelEn = "FCS", AccentColorRgba = DomainColor, NameKey = "domain.focus", DescKey = "domain.focus_desc", IsBuff = true },
		["scheme"] = new() { Icon = "🪺", PlainLabel = "谋", PlainLabelEn = "SCH", AccentColorRgba = DomainColor, NameKey = "domain.scheme", DescKey = "domain.scheme_desc", IsBuff = true },
		["inescapable_flaw"] = new() { Icon = "🥚", PlainLabel = "瑕", PlainLabelEn = "FLW", AccentColorRgba = DebuffColor, NameKey = "domain.inescapable_flaw", DescKey = "domain.inescapable_flaw_desc", IsBuff = false },
		["preemptive_strike"] = new() { Icon = "⚔", PlainLabel = "先", PlainLabelEn = "PRE", AccentColorRgba = AttackColor, NameKey = "domain.preemptive_strike", DescKey = "domain.preemptive_strike_desc", IsBuff = true },
	};

	private static readonly Dictionary<string, EffectIconData> _keywordSources = new()
	{
		["bait_tactics"] = new() { Icon = "🎯", PlainLabel = "诱", PlainLabelEn = "BAT", AccentColorRgba = BuffColor, NameKey = "keyword_source.bait_tactics", DescKey = "keyword_source.bait_tactics_desc", IsBuff = true },
	};

	private static readonly Dictionary<string, EffectIconData> _modifiers = new()
	{
		["animosity"] = new() { Icon = "💢", PlainLabel = "怒", PlainLabelEn = "ANI", AccentColorRgba = AttackColor, NameKey = "effect.animosity", DescKey = "effect.animosity_desc", IsBuff = false },
	};

	public static EffectIconData? GetStatusEffect(string id)
	{
		return _statusEffects.TryGetValue(id, out var data) ? data : null;
	}

	public static EffectIconData? GetDomain(string domainId)
	{
		return _domains.TryGetValue(domainId, out var data) ? data : null;
	}

	public static EffectIconData? GetKeywordSource(string sourceId)
	{
		return _keywordSources.TryGetValue(sourceId, out var data) ? data : null;
	}

	public static EffectIconData? GetModifier(string modifierId)
	{
		return _modifiers.TryGetValue(modifierId, out var data) ? data : null;
	}

	/// <summary>按当前语言解析默认模式简字。</summary>
	public static string GetPlainLabel(string chinese, string english)
	{
		return string.Equals(Localization.Localization.CurrentLanguage, "en", StringComparison.OrdinalIgnoreCase)
			? english : chinese;
	}

	/// <summary>按当前语言解析默认模式简字。</summary>
	public static string GetPlainLabel(EffectIconData data) => GetPlainLabel(data.PlainLabel, data.PlainLabelEn);

	/// <summary>将效果元数据转换为 UI 无关的双模显示快照。</summary>
	public static DisplayableEffect ToDisplayable(
		EffectIconData data,
		EffectCategory category,
		int stacks = 0,
		string sourceId = null)
	{
		return new DisplayableEffect
		{
			Icon = data.Icon,
			PlainLabel = GetPlainLabel(data),
			AccentColorRgba = data.AccentColorRgba,
			Name = Localization.Localization.T(data.NameKey, ""),
			Stacks = stacks,
			Description = Localization.Localization.T(data.DescKey, ""),
			IsBuff = data.IsBuff,
			Category = category,
			SourceId = sourceId,
		};
	}
}
