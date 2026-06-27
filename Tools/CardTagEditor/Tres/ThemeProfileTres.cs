namespace OdysseyCards.Tools.CardTagEditor.Tres;

/// <summary>
/// ThemeProfile .tres 文件的类型化访问器。
/// </summary>
public class ThemeProfileTres
{
	private readonly TresDocument _doc;

	public ThemeProfileTres(TresDocument doc)
	{
		_doc = doc;
	}

	/// <summary>英雄 ID。</summary>
	public string HeroId => _doc.GetField("HeroId")?.AsString() ?? "";

	/// <summary>主题名。</summary>
	public string ThemeName => _doc.GetField("ThemeName")?.AsString() ?? "";

	/// <summary>标签权重字典（多行块）。</summary>
	public Dictionary<int, int> TagWeights
	{
		get => _doc.GetMultiField("TagWeights")?.AsIntDict() ?? new();
		set => _doc.GetMultiField("TagWeights")?.SetIntDict(value);
	}

	/// <summary>关键词权重字典（多行块，可能不存在）。</summary>
	public Dictionary<int, int> KeywordWeights
	{
		get => _doc.GetMultiField("KeywordWeights")?.AsIntDict() ?? new();
		set
		{
			var mf = _doc.GetMultiField("KeywordWeights");
			mf?.SetIntDict(value);
		}
	}

	/// <summary>是否有 KeywordWeights 行。</summary>
	public bool HasKeywordWeights => _doc.GetMultiField("KeywordWeights") != null;

	/// <summary>核心卡牌 ID 数组。</summary>
	public string[] CoreCardIds
	{
		get => _doc.GetField("CoreCardIds")?.AsStringArray() ?? Array.Empty<string>();
		set => _doc.GetField("CoreCardIds")?.SetStringArray(value);
	}

	/// <summary>卡牌权重覆盖字典（多行块）。</summary>
	public Dictionary<string, int> CardWeightOverrides
	{
		get
		{
			// 兼容单行空字典 "CardWeightOverrides = {}"
			var sf = _doc.GetField("CardWeightOverrides");
			if (sf != null)
			{
				var val = sf.ExtractValue();
				if (val == "{}") return new();
				// 单行非空字典不常见，回退
			}

			var mf = _doc.GetMultiField("CardWeightOverrides");
			if (mf == null) return new();

			// Parse string-key dict
			var result = new Dictionary<string, int>();
			foreach (var line in mf.OriginalLines)
			{
				var trimmed = line.Trim();
if (trimmed.StartsWith("CardWeightOverrides", StringComparison.Ordinal)
				|| trimmed == "}" || trimmed == "{}")
				continue;
			// "key": value, or "key": value
			var colonIdx = trimmed.IndexOf(':');
			if (colonIdx < 0) continue;
			var k = trimmed[..colonIdx].Trim().Trim('"');
			var v = trimmed[(colonIdx + 1)..].Trim().TrimEnd(',');
			result[k] = int.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
			}
			return result;
		}
	}
}
