using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OdysseyCards.Core;
using OdysseyCards.Localization;
using Xunit;

namespace OdysseyCards.Tests.Unit;

/// <summary>不实例化引擎 Resource 的全卡池迁移门禁；实际资源加载另走 Godot QA。</summary>
public class FrontlineResourceTests
{
	private static string Root()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null && !File.Exists(Path.Combine(directory.FullName, "project.godot")))
			directory = directory.Parent;
		return directory?.FullName ?? throw new DirectoryNotFoundException("找不到项目根目录");
	}

	[Fact]
	public void AllCards_MigrateExplicitTopology_AndPreserveValidEffectReferences()
	{
		var files = Directory.GetFiles(Path.Combine(Root(), "Resources", "Cards"), "*.tres");
		Assert.Equal(81, files.Length);
		Assert.Equal(31, files.Count(path => Path.GetFileName(path).StartsWith("Minion_", StringComparison.Ordinal)));
		foreach (string path in files)
		{
			string text = File.ReadAllText(path);
			Assert.DoesNotMatch(@"(?m)^Speed\s*=", text);
			if (Path.GetFileName(path).StartsWith("Minion_", StringComparison.Ordinal))
			{
				Assert.Matches(@"(?m)^UnitClass = [0-3]$", text);
				Assert.Matches(@"(?m)^ActionCost = \d+$", text);
			}
			else
				Assert.Matches(@"(?m)^FrontlineOnlySpell = (true|false)$", text);
			var definitions = Regex.Matches(text, "\\[sub_resource[^\\]]*id=\"([^\"]+)\"\\]")
				.Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
			foreach (Match reference in Regex.Matches(text, "SubResource\\(\"([^\"]+)\"\\)"))
				Assert.Contains(reference.Groups[1].Value, definitions);
		}
	}

	[Theory]
	[InlineData("Minion_DetectiveSquad.tres", 1)]
	[InlineData("Minion_LianshuScout.tres", 1)]
	[InlineData("Spell_Expose.tres", 1)]
	public void IntelCards_UseAuthoritativeTagAndEffect(string file, int points)
	{
		string text = File.ReadAllText(Path.Combine(Root(), "Resources", "Cards", file));
		int tags = int.Parse(Regex.Match(text, @"(?m)^MechanicTags = (\d+)$").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
		Assert.True(((CardMechanicTag)tags).HasFlag(CardMechanicTag.Intel));
		Assert.Matches($"EffectType = 35\\r?\\nValue = {points}", text);
	}

	[Theory]
	[InlineData("zh.yaml")]
	[InlineData("en.yaml")]
	public void Localization_AllCardsAndNewRules_HaveKeysAndTwoSpaceIndent(string file)
	{
		string text = File.ReadAllText(Path.Combine(Root(), "Resources", "Localization", file));
		Assert.DoesNotContain('\t', text);
		foreach (string line in text.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)))
			Assert.Equal(0, (line.Length - line.TrimStart(' ').Length) % 2);
		Dictionary<string, string> keys = YamlParser.Flatten(YamlParser.Parse(text));
		foreach (string path in Directory.GetFiles(Path.Combine(Root(), "Resources", "Cards"), "*.tres"))
		{
			string id = Regex.Match(File.ReadAllText(path), "(?m)^Id = \"([^\"]+)\"").Groups[1].Value;
			Assert.True(keys.ContainsKey($"cards.{id}.name"), $"{file}: {id}.name 缺失");
			Assert.True(keys.ContainsKey($"cards.{id}.description"), $"{file}: {id}.description 缺失");
		}
		foreach (string key in new[] { "unit_class.vanguard", "unit_class.assault", "unit_class.support", "unit_class.skirmisher", "keyword.guard", "keyword.intel", "keyword.intel_overflow" })
			Assert.True(keys.ContainsKey(key), $"{file}: {key} 缺失");
		Assert.True(keys.TryGetValue("ui.settings.sfx_volume", out string? volumeLabel) && !string.IsNullOrWhiteSpace(volumeLabel),
			$"{file}: 音效音量标签缺失");
	}
}
