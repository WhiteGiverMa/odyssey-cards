using Godot;

namespace OdysseyCards.Core;

/// <summary>
/// 稀有度颜色方案 — 提供两套可切换的调色板。
/// 方案 0 = 经典（金/银/铜/铁，对应大师/极佳/良好/一般）。
/// 方案 1 = 新版（紫/蓝/绿/灰白，用户指定新方案）。
///
/// 稀有度枚举值 0-6 对应：
/// Derivative=0, Master=1, Excellent=2, Good=3, Common=4, Special=5, StatusToken=6
/// </summary>
public static class RarityColorScheme
{
	/// <summary>方案数量。</summary>
	public const int SchemeCount = 2;

	/// <summary>经典方案 — 金/银/铜/铁/橙/灰。</summary>
	private static readonly Color[] ClassicColors = new Color[7]
	{
		new(0.55f, 0.55f, 0.58f, 1), // 0 衍生 — 铁灰
		new(1.00f, 0.84f, 0.00f, 1), // 1 大师 — 金
		new(0.75f, 0.78f, 0.82f, 1), // 2 极佳 — 银
		new(0.80f, 0.50f, 0.20f, 1), // 3 良好 — 铜
		new(0.48f, 0.48f, 0.50f, 1), // 4 一般 — 铁
		new(1.00f, 0.55f, 0.00f, 1), // 5 专属 — 橙
		new(0.55f, 0.55f, 0.58f, 1), // 6 状态牌 — 铁灰
	};

	/// <summary>新版方案 — 纯白/紫/蓝/绿/灰白/橙/灰。</summary>
	private static readonly Color[] NewColors = new Color[7]
	{
		new(1.00f, 1.00f, 1.00f, 1), // 0 衍生 — 纯白
		new(0.71f, 0.30f, 1.00f, 1), // 1 大师 — 紫 #B44CFF
		new(0.30f, 0.62f, 1.00f, 1), // 2 极佳 — 蓝 #4D9EFF
		new(0.30f, 0.85f, 0.39f, 1), // 3 良好 — 绿 #4DD964
		new(0.78f, 0.78f, 0.78f, 1), // 4 一般 — 灰白 #C8C8C8
		new(1.00f, 0.55f, 0.00f, 1), // 5 专属 — 橙
		new(0.50f, 0.50f, 0.50f, 1), // 6 状态牌 — 灰
	};

	/// <summary>
	/// 获取指定方案中某个稀有度的显示颜色。
	/// </summary>
	/// <param name="schemeIndex">0=经典, 1=新版</param>
	/// <param name="rarity">稀有度 0-6</param>
	public static Color GetColor(int schemeIndex, CardRarity rarity)
	{
		int r = (int)rarity;
		if (r < 0 || r > 6)
			r = 4; // 兜底：一般

		var palette = schemeIndex == 0 ? ClassicColors : NewColors;
		return palette[r];
	}

	/// <summary>
	/// 获取指定方案中某个稀有度的显示数字（即 (int)rarity）。
	/// </summary>
	public static int GetNumber(CardRarity rarity)
	{
		return (int)rarity;
	}
}
