using System.Text.RegularExpressions;

namespace OdysseyCards.Tools.CardTagEditor.Tres;

/// <summary>
/// .tres 文件解析器。解析文本行→TresDocument，对非拥有行原样保留。
/// </summary>
public static class TresParser
{
	/// <summary>CardData .tres 中此工具拥有的单行字段。</summary>
	private static readonly HashSet<string> CardOwnedSingleFields = new(StringComparer.OrdinalIgnoreCase)
	{
		"Id", "CardName", "Type",
		"MechanicTags", "Tags", "Keywords",
		"Cost", "Rarity", "Attack", "Health", "Defense",
		"ActionCost", "DomainId", "Description",
		"RequiresTarget", "TargetFilter", "ExcludeFilter",
		"Artwork", "BonusDamageToDefendedTargets",
	};

	/// <summary>ThemeProfile .tres 中此工具拥有的单行字段。</summary>
	private static readonly HashSet<string> ThemeOwnedSingleFields = new(StringComparer.OrdinalIgnoreCase)
	{
		"HeroId", "ThemeName", "TargetDeckSize", "MaxDuplicatesPerCard", "MaxDomainCards",
		"CoreCardIds",
	};

	/// <summary>CardData .tres 中可有的多行字典字段（需要被识别为拥有字段）。</summary>
	private static readonly HashSet<string> CardMultiLineFields = new(StringComparer.OrdinalIgnoreCase)
	{
		// CardData 没有多行字典字段（Keywords 是单行），但为了扩展性保留此集合
	};

	/// <summary>ThemeProfile .tres 中的多行字典字段。</summary>
	private static readonly HashSet<string> ThemeMultiLineFields = new(StringComparer.OrdinalIgnoreCase)
	{
		"TagWeights", "KeywordWeights", "CardWeightOverrides",
	};

	/// <summary>单行 Key=Value 匹配正则。</summary>
	private static readonly Regex KeyValueRegex = new(
		@"^(\w+)\s*=\s*(.+)$", RegexOptions.Compiled);

	/// <summary>从文件路径解析 .tres。</summary>
	public static TresDocument Parse(string filePath)
	{
		var lines = File.ReadAllLines(filePath);
		return ParseLines(lines, Path.GetFileName(filePath));
	}

	/// <summary>从文本行解析 .tres。</summary>
	public static TresDocument ParseLines(string[] rawLines, string? fileName = null)
	{
		var doc = new TresDocument();
		bool inResource = false;

		// 推断资源类型
		bool isTheme = fileName?.Contains("ThemeProfile", StringComparison.OrdinalIgnoreCase) ?? false;

		for (int i = 0; i < rawLines.Length; i++)
		{
			var line = rawLines[i];

			// 检测是否进入 [resource] 段
			if (line.TrimStart().StartsWith("[resource]", StringComparison.Ordinal))
			{
				inResource = true;
				doc.Lines.Add(new VerbatimLine { Text = line });
				continue;
			}

			// 非 [resource] 段内 → 原样透传
			if (!inResource)
			{
				doc.Lines.Add(new VerbatimLine { Text = line });
				continue;
			}

			// 在 [resource] 段内，识别所属行
			var trimmed = line.TrimStart();

			// 空行 → 原样透传
			if (string.IsNullOrWhiteSpace(line))
			{
				doc.Lines.Add(new VerbatimLine { Text = line });
				continue;
			}

			// 注释行 → 原样透传
			if (trimmed.StartsWith(';') || trimmed.StartsWith('#'))
			{
				doc.Lines.Add(new VerbatimLine { Text = line });
				continue;
			}

			// 尝试匹配 Key = Value
			var match = KeyValueRegex.Match(trimmed);
			if (!match.Success)
			{
				doc.Lines.Add(new VerbatimLine { Text = line });
				continue;
			}

			var key = match.Groups[1].Value;
			var value = match.Groups[2].Value.Trim();

			// 检查是否是多行字典块的开始 (Key = { 或 Key = {})
			if (value == "{" || value == "{}")
			{
				var multiKey = DetermineMultiKey(key, isTheme);
				if (multiKey != null)
				{
					var mlField = ParseMultiLineField(rawLines, ref i, key, value);
					doc.Lines.Add(mlField);
					continue;
				}
			}

			// 检查是否是拥有的单行字段
			bool isOwned = isTheme
				? ThemeOwnedSingleFields.Contains(key)
				: CardOwnedSingleFields.Contains(key);

			if (isOwned)
			{
				doc.Lines.Add(new OwnedField
				{
					Key = key,
					OriginalText = line, // 保留原始缩进！
				});
			}
			else
			{
				doc.Lines.Add(new VerbatimLine { Text = line });
			}
		}

		return doc;
	}

	/// <summary>确定某 key 是否应被视为多行字段。</summary>
	private static string? DetermineMultiKey(string key, bool isTheme)
	{
		if (isTheme && ThemeMultiLineFields.Contains(key))
			return key;
		if (!isTheme && CardMultiLineFields.Contains(key))
			return key;
		return null;
	}

	/// <summary>解析多行字典字段。</summary>
	private static OwnedMultiLineField ParseMultiLineField(string[] lines, ref int i, string key, string rawValue)
	{
		var mlField = new OwnedMultiLineField { Key = key };

		if (rawValue == "{}")
		{
			// 空字典在一行内
			mlField.OriginalLines.Add(lines[i]); // "Key = {}"
		}
		else
		{
			// 多行字典：Key = { 开头，直到 }
			mlField.OriginalLines.Add(lines[i]); // "Key = {"
			i++;
			while (i < lines.Length)
			{
				mlField.OriginalLines.Add(lines[i]);
				if (lines[i].TrimStart().StartsWith('}'))
					break;
				i++;
			}
		}

		return mlField;
	}

	/// <summary>整段文本解析（用于测试 fixture）。</summary>
	public static TresDocument ParseText(string text)
	{
		var lines = text.Replace("\r\n", "\n").Split('\n');
		return ParseLines(lines);
	}
}
