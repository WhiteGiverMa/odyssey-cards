namespace OdysseyCards.Tools.CardTagEditor.Schema;

/// <summary>
/// CardMechanicTag 的镜像常量（与游戏枚举 post-merge 对齐）。
/// 本工具零 Godot 依赖，不使用游戏程序集。
/// </summary>
public static class CardMechanicTagValues
{
	public const int None = 0;
	public const int DirectDamage = 1;
	public const int DamageOverTime = 2;
	public const int Heal = 4;
	public const int Armor = 8;
	public const int Draw = 16;
	public const int Discover = 32;
	public const int Summon = 64;
	public const int Buff = 128;
	public const int Silence = 256;
	public const int Discard = 512;
	public const int Domain = 1024;
	public const int WeaponSynergy = 2048;
	public const int ManaRamp = 4096;
	public const int StatusApply = 8192;
	public const int Shuffle = 16384;
	public const int Token = 32768;
	public const int Mechanics = 65536;
	public const int SuperBody = 131072;

	/// <summary>所有有效位值，按声明顺序。</summary>
	public static readonly int[] AllBits = new[]
	{
		DirectDamage, DamageOverTime, Heal, Armor, Draw, Discover, Summon, Buff,
		Silence, Discard, Domain, WeaponSynergy, ManaRamp, StatusApply, Shuffle, Token, Mechanics, SuperBody
	};

	/// <summary>位值→英文名 映射。</summary>
	public static readonly Dictionary<int, string> BitToName = new()
	{
		[DirectDamage] = "DirectDamage",
		[DamageOverTime] = "DamageOverTime",
		[Heal] = "Heal",
		[Armor] = "Armor",
		[Draw] = "Draw",
		[Discover] = "Discover",
		[Summon] = "Summon",
		[Buff] = "Buff",
		[Silence] = "Silence",
		[Discard] = "Discard",
		[Domain] = "Domain",
		[WeaponSynergy] = "WeaponSynergy",
		[ManaRamp] = "ManaRamp",
		[StatusApply] = "StatusApply",
		[Shuffle] = "Shuffle",
		[Token] = "Token",
		[Mechanics] = "Mechanics",
		[SuperBody] = "SuperBody",
	};

	/// <summary>英文名→位值 映射。</summary>
	public static readonly Dictionary<string, int> NameToBit = BitToName
		.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

	/// <summary>解析位掩码为位名列表。</summary>
	public static List<string> ParseBits(int mask)
	{
		var result = new List<string>();
		foreach (var bit in AllBits)
		{
			if ((mask & bit) != 0 && BitToName.TryGetValue(bit, out var name))
				result.Add(name);
		}
		return result;
	}

	/// <summary>从位名列表编码为位掩码。</summary>
	public static int EncodeBits(IEnumerable<string> names)
	{
		int mask = 0;
		foreach (var name in names)
		{
			if (NameToBit.TryGetValue(name, out var bit))
				mask |= bit;
		}
		return mask;
	}

	/// <summary>检查位掩码是否只包含已知位。</summary>
	public static bool IsValidMask(int mask) => (mask & ~AllValidBits) == 0;

	/// <summary>所有已知位的 OR 组合。</summary>
	public static readonly int AllValidBits = AllBits.Aggregate(0, (a, b) => a | b);
}
