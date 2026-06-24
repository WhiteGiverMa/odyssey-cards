using System;
using System.Collections.Generic;
using System.IO;
using OdysseyCards.Localization;
using Xunit;

namespace OdysseyCards.Tests.Unit;

/// <summary>
/// 单元测试 — 本地化 YAML 解析。
/// 锁住曾经导致运行时只加载 435 条翻译的缩进回归。
/// </summary>
public class YamlParserTests
{
	[Fact]
	public void Parse_WithStandardTwoSpaceIndentation_PreservesNestedLocalizationKeys()
	{
		string yaml = """
			ui:
			  menu:
			    title: "奥德赛卡牌"
			cards:
			  domain_flying_away:
			    name: "飞远"
			    description: "领域描述"
			""";

		Dictionary<string, string> flattened = YamlParser.Flatten(YamlParser.Parse(yaml));

		Assert.Equal("奥德赛卡牌", flattened["ui.menu.title"]);
		Assert.Equal("飞远", flattened["cards.domain_flying_away.name"]);
		Assert.Equal("领域描述", flattened["cards.domain_flying_away.description"]);
	}

	[Fact]
	public void Parse_WithLegacyTabChildIndentation_PreservesNestedLocalizationKeys()
	{
		string yaml = "ui:\n"
			+ "  menu:\n"
			+ "\ttitle: \"奥德赛卡牌\"\n"
			+ "cards:\n"
			+ "  domain_flying_away:\n"
			+ "\tname: \"飞远\"\n"
			+ "\tdescription: \"领域描述\"\n";

		Dictionary<string, string> flattened = YamlParser.Flatten(YamlParser.Parse(yaml));

		Assert.Equal("奥德赛卡牌", flattened["ui.menu.title"]);
		Assert.Equal("飞远", flattened["cards.domain_flying_away.name"]);
		Assert.Equal("领域描述", flattened["cards.domain_flying_away.description"]);
		Assert.False(flattened.ContainsKey("cards.name"));
	}

	[Theory]
	[InlineData("zh.yaml", 600)]
	[InlineData("en.yaml", 600)]
	public void Parse_ProjectLocalizationFile_PreservesNamespacedCardKeys(string fileName, int minimumKeyCount)
	{
		string filePath = Path.Combine(FindProjectRoot(), "Resources", "Localization", fileName);
		string yaml = File.ReadAllText(filePath);

		Dictionary<string, string> flattened = YamlParser.Flatten(YamlParser.Parse(yaml));

		Assert.True(flattened.Count >= minimumKeyCount, $"{fileName} 只解析出 {flattened.Count} 条翻译，可能发生缩进退化。");
		Assert.True(flattened.ContainsKey("ui.menu.title"));
		Assert.True(flattened.ContainsKey("cards.domain_flying_away.name"));
		Assert.True(flattened.ContainsKey("cards.domain_flying_away.description"));
		Assert.False(flattened.ContainsKey("cards.name"));
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
