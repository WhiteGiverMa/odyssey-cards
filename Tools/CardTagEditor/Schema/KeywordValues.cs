namespace OdysseyCards.Tools.CardTagEditor.Schema;

/// <summary>
/// Keyword 的镜像常量（与游戏枚举对齐）。
/// </summary>
public static class KeywordValues
{
	public const int None = 0;
	public const int Charge = 1;
	public const int Taunt = 2;
	public const int Battlecry = 3;
	public const int Deathrattle = 4;
	public const int Windfury = 5;
	public const int Ambush = 6;
	public const int Impact = 7;
	public const int Recycle = 8;
	public const int Unplayable = 9;
	public const int Ethereal = 10;
	public const int Qiqiao = 11;

	/// <summary>所有有效值（不含 None）。</summary>
	public static readonly int[] AllValues = new[]
	{
		Charge, Taunt, Battlecry, Deathrattle, Windfury, Ambush, Impact,
		Recycle, Unplayable, Ethereal, Qiqiao
	};

	/// <summary>值→英文名 映射。</summary>
	public static readonly Dictionary<int, string> ValueToName = new()
	{
		[Charge] = "Charge",
		[Taunt] = "Taunt",
		[Battlecry] = "Battlecry",
		[Deathrattle] = "Deathrattle",
		[Windfury] = "Windfury",
		[Ambush] = "Ambush",
		[Impact] = "Impact",
		[Recycle] = "Recycle",
		[Unplayable] = "Unplayable",
		[Ethereal] = "Ethereal",
		[Qiqiao] = "Qiqiao",
	};

	/// <summary>英文名→值 映射。</summary>
	public static readonly Dictionary<string, int> NameToValue = ValueToName
		.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

	/// <summary>解析 int 数组为关键词名列表。</summary>
	public static List<string> ParseValues(IEnumerable<int> values)
	{
		var result = new List<string>();
		foreach (var v in values)
		{
			if (ValueToName.TryGetValue(v, out var name))
				result.Add(name);
			else
				result.Add($"Unknown({v})");
		}
		return result;
	}

	/// <summary>从关键词名列表编码为 int 数组。</summary>
	public static int[] EncodeValues(IEnumerable<string> names)
	{
		return names.Select(n => NameToValue.TryGetValue(n, out var v) ? v : -1)
			.Where(v => v >= 0)
			.ToArray();
	}

	/// <summary>检查值是否有效。</summary>
	public static bool IsValid(int value) => ValueToName.ContainsKey(value);
}
