namespace OdysseyCards.Tools.CardTagEditor.Schema;

/// <summary>
/// UnitClass 的镜像常量（与 KARDS 三线重构后的游戏枚举对齐）。
/// </summary>
public static class UnitClassValues
{
	public const int Vanguard = 0;
	public const int Assault = 1;
	public const int Support = 2;
	public const int Skirmisher = 3;

	/// <summary>所有有效兵种值，按枚举声明顺序。</summary>
	public static readonly int[] AllValues = new[]
	{
		Vanguard, Assault, Support, Skirmisher
	};

	/// <summary>值→英文枚举名映射。</summary>
	public static readonly Dictionary<int, string> ValueToName = new()
	{
		[Vanguard] = "Vanguard",
		[Assault] = "Assault",
		[Support] = "Support",
		[Skirmisher] = "Skirmisher",
	};

	/// <summary>值→中文兵种名映射。</summary>
	public static readonly Dictionary<int, string> ValueToDisplayName = new()
	{
		[Vanguard] = "先锋",
		[Assault] = "突击",
		[Support] = "支援",
		[Skirmisher] = "游击",
	};

	/// <summary>英文枚举名→值映射。</summary>
	public static readonly Dictionary<string, int> NameToValue = ValueToName
		.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

	/// <summary>获取英文枚举名，未知值保留数值以便诊断。</summary>
	public static string GetName(int value) =>
		ValueToName.TryGetValue(value, out var name) ? name : $"Unknown({value})";

	/// <summary>获取中文兵种名，未知值保留数值以便诊断。</summary>
	public static string GetDisplayName(int value) =>
		ValueToDisplayName.TryGetValue(value, out var name) ? name : $"Unknown({value})";

	/// <summary>检查兵种值是否有效。</summary>
	public static bool IsValid(int value) => ValueToName.ContainsKey(value);
}
