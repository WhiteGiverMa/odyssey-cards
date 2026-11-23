using System;
using System.Collections.Generic;
using System.IO;
using OdysseyCards.Core;
using OdysseyCards.Localization;
using Xunit;

namespace OdysseyCards.Tests.Unit;

/// <summary>
/// 效果图标表与双语文案的纯 C# 门禁：
/// 锁住「无限火力 / 执锐 / 拿刀防卫」等实战领域缺失图标、以及 YAML 混入 Tab 的回归。
/// </summary>
public class EffectIconTableTests
{
	private static readonly string[] DomainIds =
	{
		"unlimited_potential",
		"flying_away",
		"idol_twilight",
		"explosive_reactive_armor",
		"preemptive_strike",
		"zhijian",
		"infinite_fire",
		"focus",
		"scheme",
		"inescapable_flaw",
		"cosmic_coldness",
	};

	private static readonly string[] StatusIds =
	{
		"attack_zero",
		"meltdown",
		"weapon_disabled",
		"animosity",
		"vulnerable",
		"weak",
		"fragile",
		"total_observation",
		"attack_ban",
		"damage_over_time",
		"incapacitated",
		"shiyoru_raidenkou",
		"sutaraito_spirit",
		"smokescreen",
		"fatal_rift",
		"nadaofangyu_atk_buff_1",
		"nadaofangyu_atk_buff_2",
	};

	[Fact]
	public void AllDomains_HaveIconLabelsAndBilingualTranslations()
	{
		var translations = LoadTranslations();
		foreach (string id in DomainIds)
		{
			EffectIconData? data = EffectIconTable.GetDomain(id);
			Assert.True(data.HasValue, $"领域 {id} 缺少 EffectIconData 映射。");
			AssertIconData(id, data!.Value, translations);
		}
	}

	[Fact]
	public void AllStatuses_HaveIconLabelsAndBilingualTranslations()
	{
		var translations = LoadTranslations();
		foreach (string id in StatusIds)
		{
			EffectIconData? data = EffectIconTable.GetStatusEffect(id);
			Assert.True(data.HasValue, $"状态 {id} 缺少 EffectIconData 映射。");
			AssertIconData(id, data!.Value, translations);
		}
	}

	[Fact]
	public void NadaofangyuSerialIds_NormalizeToSharedEntry()
	{
		EffectIconData baseData = EffectIconTable.GetStatusEffect("nadaofangyu_atk_buff")!.Value;
		EffectIconData serial = EffectIconTable.GetStatusEffect("nadaofangyu_atk_buff_17")!.Value;
		Assert.Equal(baseData.Icon, serial.Icon);
		Assert.Equal(baseData.PlainLabel, serial.PlainLabel);
		Assert.Equal(baseData.NameKey, serial.NameKey);
		Assert.Null(EffectIconTable.GetStatusEffect("nadaofangyu_atk_buffish"));
		Assert.Null(EffectIconTable.GetStatusEffect(string.Empty));
		Assert.Null(EffectIconTable.GetStatusEffect(null!));
	}

	[Fact]
	public void ExplosiveReactiveArmor_HasIconMetadata()
	{
		// 0.2.3 新领域：图标表先落地，翻译键由本地化资源门禁在合流后覆盖。
		EffectIconData? data = EffectIconTable.GetDomain("explosive_reactive_armor");
		Assert.True(data.HasValue, "领域 explosive_reactive_armor 缺少 EffectIconData 映射。");
		Assert.False(string.IsNullOrWhiteSpace(data!.Value.Icon));
		Assert.False(string.IsNullOrWhiteSpace(data.Value.PlainLabel));
		Assert.False(string.IsNullOrWhiteSpace(data.Value.PlainLabelEn));
		Assert.False(string.IsNullOrWhiteSpace(data.Value.NameKey));
		Assert.False(string.IsNullOrWhiteSpace(data.Value.DescKey));
	}

	[Theory]
	[InlineData("zh.yaml")]
	[InlineData("en.yaml")]
	public void LocalizationFile_UsesSpacesOnlyAndParses(string fileName)
	{
		string path = Path.Combine(FindProjectRoot(), "Resources", "Localization", fileName);
		string yaml = File.ReadAllText(path);

		Assert.False(yaml.Contains('\t'), $"{fileName} 混入了 Tab，YAML 缩进必须只用 2 空格递增。");

		Dictionary<string, string> flattened = YamlParser.Flatten(YamlParser.Parse(yaml));
		Assert.True(flattened.Count >= 600, $"{fileName} 只解析出 {flattened.Count} 条翻译，可能发生缩进退化。");
		Assert.True(flattened.ContainsKey("domain.zhijian"));
		Assert.True(flattened.ContainsKey("domain.infinite_fire"));
		Assert.True(flattened.ContainsKey("status.smokescreen.name"));
		Assert.True(flattened.ContainsKey("status.fatal_rift.name"));
	}

	private static void AssertIconData(string id, EffectIconData data,
		(Dictionary<string, string> Zh, Dictionary<string, string> En) translations)
	{
		Assert.False(string.IsNullOrWhiteSpace(data.Icon), $"{id} 缺少 Emoji 图标。");
		Assert.False(string.IsNullOrWhiteSpace(data.PlainLabel), $"{id} 缺少中文简字。");
		Assert.False(string.IsNullOrWhiteSpace(data.PlainLabelEn), $"{id} 缺少英文简字。");
		Assert.False(string.IsNullOrWhiteSpace(data.NameKey), $"{id} 缺少 NameKey。");
		Assert.False(string.IsNullOrWhiteSpace(data.DescKey), $"{id} 缺少 DescKey。");
		AssertTranslation(id, data.NameKey, translations);
		AssertTranslation(id, data.DescKey, translations);
	}

	private static void AssertTranslation(string id, string key,
		(Dictionary<string, string> Zh, Dictionary<string, string> En) translations)
	{
		Assert.True(translations.Zh.TryGetValue(key, out string? zh) && !string.IsNullOrWhiteSpace(zh),
			$"{id} 缺少中文翻译键 {key}。");
		Assert.True(translations.En.TryGetValue(key, out string? en) && !string.IsNullOrWhiteSpace(en),
			$"{id} 缺少英文翻译键 {key}。");
	}

	private static (Dictionary<string, string> Zh, Dictionary<string, string> En) LoadTranslations()
	{
		string root = Path.Combine(FindProjectRoot(), "Resources", "Localization");
		return (
			YamlParser.Flatten(YamlParser.Parse(File.ReadAllText(Path.Combine(root, "zh.yaml")))),
			YamlParser.Flatten(YamlParser.Parse(File.ReadAllText(Path.Combine(root, "en.yaml")))));
	}

	private static string FindProjectRoot()
	{
		DirectoryInfo current = new(AppContext.BaseDirectory);
		while (true)
		{
			string localizationDir = Path.Combine(current.FullName, "Resources", "Localization");
			if (Directory.Exists(localizationDir))
			{
				return current.FullName;
			}

			if (current.Parent == null)
			{
				break;
			}

			current = current.Parent;
		}

		throw new DirectoryNotFoundException("无法定位项目根目录 Resources/Localization。");
	}
}
