namespace OdysseyCards.Tools.CardTagEditor.Tres;

/// <summary>
/// .tres 文件的完整行级模型。
/// 每一行都是一个 TresLine 节点——要么是原样透传的 VerbatimLine，要么是可读写字段。
/// 非此工具关注的字段/行作为 VerbatimLine 保留以保证 round-trip 保真。
/// </summary>
public class TresDocument
{
	public List<TresLine> Lines { get; } = new();

	/// <summary>按 Key 查找拥有字段（单行类型，跳过已删除的）。</summary>
	public OwnedField? GetField(string key) =>
		Lines.OfType<OwnedField>().FirstOrDefault(f => f.Key == key && !f._deleted);

	/// <summary>按 Key 查找拥有多行字段（字典块类型）。</summary>
	public OwnedMultiLineField? GetMultiField(string key) =>
		Lines.OfType<OwnedMultiLineField>().FirstOrDefault(f => f.Key == key);

	/// <summary>将所有行写入 TextWriter。</summary>
	public void WriteTo(TextWriter writer)
	{
		var allLines = Lines.SelectMany(l => l.GetLines()).ToList();
		for (int i = 0; i < allLines.Count; i++)
		{
			writer.Write(allLines[i]);
			if (i < allLines.Count - 1)
				writer.Write('\n');
		}
	}

	/// <summary>回写为完整文本字符串。</summary>
	public override string ToString()
	{
		return string.Join("\n", Lines.SelectMany(l => l.GetLines()));
	}

	/// <summary>克隆文档（所有行浅拷贝，覆盖值深拷贝）。</summary>
	public TresDocument Clone()
	{
		var doc = new TresDocument();
		foreach (var line in Lines)
			doc.Lines.Add(line.Clone());
		return doc;
	}
}

/// <summary>.tres 行节点基类。</summary>
public abstract class TresLine
{
	/// <summary>获取此节点表示的所有输出行（不含换行符）。</summary>
	public abstract IEnumerable<string> GetLines();

	/// <summary>写入此行到 TextWriter。</summary>
	public void WriteTo(TextWriter writer)
	{
		// 默认：GetLines 然后 writer.WriteLine
		foreach (var line in GetLines())
			writer.WriteLine(line);
	}

	/// <summary>浅拷贝（覆盖值按需在子类处理）。</summary>
	public abstract TresLine Clone();
}

/// <summary>原样透传行——不解析、不修改。</summary>
public class VerbatimLine : TresLine
{
	/// <summary>原始行文本（不含换行符）。</summary>
	public string Text { get; init; } = "";

	public override IEnumerable<string> GetLines() { yield return Text; }

	/// <summary>向后兼容写入。</summary>
	public override TresLine Clone() => new VerbatimLine { Text = Text };

	public override string ToString() => Text;
}

/// <summary>
/// 单行拥有字段——Key = Value 形式，可以被解析和修改。
/// 如 MechanicTags = 1、Keywords = Array[int]([8])]、CoreCardIds = PackedStringArray(...) 等。
/// </summary>
public class OwnedField : TresLine
{
	/// <summary>字段名（等号左侧）。</summary>
	public string Key { get; init; } = "";

	/// <summary>原始行文本（完整的 "Key = Value" 行）。</summary>
	public string OriginalText { get; init; } = "";

	/// <summary>若非 null，回写时使用此文本替代 OriginalText。</summary>
	public string? OverrideText { get; set; }

	public bool IsModified => OverrideText != null;

	/// <summary>解析为整数（使用 InvariantCulture）。</summary>
	public int AsInt() => int.Parse(ExtractValue(), System.Globalization.CultureInfo.InvariantCulture);

	/// <summary>解析为字符串（去掉引号）。</summary>
	public string AsString() => TrimQuotes(ExtractValue());

	/// <summary>解析为 int 数组（Keywords）。</summary>
	public int[] AsIntArray()
	{
		var v = ExtractValue();
		return ParseIntArray(v);
	}

	/// <summary>解析为 string 数组（CoreCardIds PackedStringArray）。</summary>
	public string[] AsStringArray()
	{
		var v = ExtractValue();
		return ParseStringArray(v);
	}

	/// <summary>提取 "Key = " 之后的值部分（优先使用覆盖文本）。</summary>
	public string ExtractValue()
	{
		var source = OverrideText ?? OriginalText;
		var eqIdx = source.IndexOf('=');
		if (eqIdx < 0) return "";
		return source[(eqIdx + 1)..].Trim();
	}

	/// <summary>更新值为整数。</summary>
	public void SetInt(int value)
	{
		OverrideText = $"{Key} = {value}";
	}

	/// <summary>更新值为 int 数组（统一输出 Array[int]([...])] 格式）。</summary>
	public void SetIntArray(int[] values)
	{
		var inner = string.Join(", ", values);
		OverrideText = $"{Key} = Array[int]([{inner}])";
	}

	/// <summary>更新值为 string 数组（PackedStringArray）。</summary>
	public void SetStringArray(string[] values)
	{
		var inner = string.Join(", ", values.Select(s => $"\"{s}\""));
		OverrideText = $"{Key} = PackedStringArray({inner})";
	}

	/// <summary>删除此字段（标记为不输出）。</summary>
	public void Delete()
	{
		OverrideText = null; // handled by writer: skip this line
		_deleted = true;
	}

	internal bool _deleted;

	public override IEnumerable<string> GetLines()
	{
		if (_deleted) yield break;
		yield return OverrideText ?? OriginalText;
	}

	public override TresLine Clone() => new OwnedField
	{
		Key = Key,
		OriginalText = OriginalText,
		OverrideText = OverrideText,
		_deleted = _deleted,
	};

	/// <summary>解析 "Keywords = [...]" 或 "Keywords = Array[int]([...])]" 格式。</summary>
	public static int[] ParseIntArray(string value)
	{
		// 剥离 "Array[int](" 前缀和尾部 ")"
		if (value.StartsWith("Array[int]", StringComparison.Ordinal))
		{
			var parenStart = value.IndexOf('(');
			if (parenStart >= 0)
			{
				value = value[(parenStart + 1)..];
				if (value.EndsWith(')'))
					value = value[..^1];
			}
		}

		// 找到 [...] 部分
		var bracketStart = value.IndexOf('[');
		if (bracketStart < 0) return Array.Empty<int>();

		// 找到匹配的 ]
		var depth = 0;
		var bracketEnd = -1;
		for (int i = bracketStart; i < value.Length; i++)
		{
			if (value[i] == '[') depth++;
			else if (value[i] == ']') { depth--; if (depth == 0) { bracketEnd = i; break; } }
		}

		if (bracketEnd < 0) return Array.Empty<int>();

		var inner = value[(bracketStart + 1)..bracketEnd].Trim();
		if (string.IsNullOrEmpty(inner)) return Array.Empty<int>();

		return inner.Split(',', StringSplitOptions.RemoveEmptyEntries)
			.Select(s => int.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture))
			.ToArray();
	}

	/// <summary>解析 "PackedStringArray("a", "b")" 格式。</summary>
	public static string[] ParseStringArray(string value)
	{
		var parenStart = value.IndexOf('(');
		if (parenStart < 0) return Array.Empty<string>();
		var parenEnd = value.LastIndexOf(')');
		if (parenEnd < 0) return Array.Empty<string>();

		var inner = value[(parenStart + 1)..parenEnd].Trim();
		if (string.IsNullOrEmpty(inner)) return Array.Empty<string>();

		// 匹配带引号的字符串（考虑转义，但 .tres 中不会有转义引号）
		var result = new List<string>();
		var inQuote = false;
		var start = 0;
		for (int i = 0; i < inner.Length; i++)
		{
			if (inner[i] == '"') { inQuote = !inQuote; }
			else if (inner[i] == ',' && !inQuote)
			{
				result.Add(TrimQuotes(inner[start..i].Trim()));
				start = i + 1;
			}
		}
		if (start < inner.Length)
			result.Add(TrimQuotes(inner[start..].Trim()));

		return result.ToArray();
	}

	private static string TrimQuotes(string s)
	{
		s = s.Trim();
		if (s.Length >= 2 && s[0] == '"' && s[^1] == '"')
			return s[1..^1];
		return s;
	}

	public override string ToString() => OverrideText ?? OriginalText;
}

/// <summary>
/// 多行拥有字段——以 "Key = {" 开始、以 "}" 结束的字典块。
/// 如 TagWeights、KeywordWeights、CardWeightOverrides 等。
/// </summary>
public class OwnedMultiLineField : TresLine
{
	/// <summary>字段名。</summary>
	public string Key { get; init; } = "";

	/// <summary>原始多行文本（含 "Key = {" ... "}"）。</summary>
	public List<string> OriginalLines { get; init; } = new();

	/// <summary>若非 null，回写时使用此行集替代 OriginalLines。</summary>
	public List<string>? OverrideLines { get; set; }

	public bool IsModified => OverrideLines != null;

	/// <summary>解析为 int→int 字典。</summary>
	public Dictionary<int, int> AsIntDict()
	{
		var result = new Dictionary<int, int>();
		foreach (var line in OriginalLines)
		{
			// 跳过首行 "Key = {" 和末行 "}"
			var trimmed = line.Trim();
			if (trimmed == $"{Key} = {{" || trimmed == "}" || trimmed == "{}")
				continue;

			// 匹配 "key: value," 或 "key: value"
			var colonIdx = trimmed.IndexOf(':');
			if (colonIdx < 0) continue;
			var k = int.Parse(trimmed[..colonIdx].Trim(), System.Globalization.CultureInfo.InvariantCulture);
			var v = trimmed[(colonIdx + 1)..].Trim().TrimEnd(',');
			result[k] = int.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
		}
		return result;
	}

	/// <summary>设置 int 字典（生成多行格式）。</summary>
	public void SetIntDict(Dictionary<int, int> dict)
	{
		if (dict.Count == 0)
		{
			OverrideLines = new List<string> { $"{Key} = {{}}" };
			return;
		}

		var lines = new List<string> { $"{Key} = {{" };
		foreach (var kv in dict.OrderBy(kv => kv.Key))
			lines.Add($"\t{kv.Key}: {kv.Value},");
		lines.Add("}");
		OverrideLines = lines;
	}

	public override IEnumerable<string> GetLines()
	{
		return OverrideLines ?? OriginalLines;
	}

	public override TresLine Clone() => new OwnedMultiLineField
	{
		Key = Key,
		OriginalLines = OriginalLines.ToList(),
		OverrideLines = OverrideLines?.ToList(),
	};

	public override string ToString() => string.Join("\n", OverrideLines ?? OriginalLines);
}
