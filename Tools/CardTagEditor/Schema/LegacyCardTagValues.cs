namespace OdysseyCards.Tools.CardTagEditor.Schema;

/// <summary>
/// 旧 CardTag 枚举的镜像常量（仅 migrate 命令使用）。
/// </summary>
public static class LegacyCardTagValues
{
	public const int None = 0;
	public const int Mechanics = 1;

	/// <summary>
	/// 迁移映射：旧 CardTag.Mechanics(1) → 新 CardMechanicTag.Mechanics(65536)。
	/// </summary>
	public static int MigrateBits(int oldTags)
	{
		int result = 0;
		if ((oldTags & Mechanics) != 0)
			result |= CardMechanicTagValues.Mechanics;
		return result;
	}
}
