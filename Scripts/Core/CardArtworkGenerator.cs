using System;
using System.Collections.Generic;
using Godot;

namespace OdysseyCards.Core;

/// <summary>
/// 卡面符号风格——按卡牌所属英雄主题分派。
/// </summary>
public enum ArtworkSymbolStyle
{
	/// <summary>魔法符文（双线描边 + 环绕小星），红裤衩/绮梦系。</summary>
	Rune,

	/// <summary>机械线稿（直角折线 + 准星装饰），理惠/溯光系。</summary>
	Mecha,

	/// <summary>通用几何抽象（单线几何），无主题归属卡牌。</summary>
	Abstract,
}

/// <summary>
/// 程序化卡图规格——纯数据描述一张卡的卡面图案。
/// 由 <see cref="CardArtworkGenerator.ResolveSpec"/> 纯函数派生，
/// 渲染层（CardArtworkView）只负责按规格绘制，不做决策。
/// </summary>
public readonly struct CardArtworkSpec
{
	/// <summary>稳定哈希种子（FNV-1a），驱动星点分布等随机细节。</summary>
	public readonly int Seed;

	/// <summary>几何符号索引（0..SymbolCount-1）。</summary>
	public readonly int SymbolIndex;

	/// <summary>符号风格。</summary>
	public readonly ArtworkSymbolStyle Style;

	/// <summary>类型基调深色（卡面底部）。</summary>
	public readonly Color BaseColor;

	/// <summary>类型基调亮色（卡面顶部）。</summary>
	public readonly Color AccentColor;

	/// <summary>稀有度光晕色；Alpha 为 0 表示无光晕。</summary>
	public readonly Color GlowColor;

	/// <summary>星点数量。</summary>
	public readonly int StarCount;

	/// <summary>符号旋转角（弧度，小角度抖动）。</summary>
	public readonly float SymbolRotation;

	/// <summary>副层几何索引（0..5），决定星盘、装甲与棱镜的细分拓扑。</summary>
	public readonly int SecondaryGeometryIndex;

	/// <summary>轨道精密刻度数量（24..64，8 的倍数）。</summary>
	public readonly int OrbitTickCount;

	/// <summary>中心能量核辉光强度（0.65..1.0）。</summary>
	public readonly float CoreGlowStrength;

	/// <summary>稀有度切角纹饰样式（0..3）。</summary>
	public readonly int CornerOrnamentIndex;

	/// <summary>双焦点星云的偏移系数（0..1）。</summary>
	public readonly float NebulaOffset;

	public CardArtworkSpec(int seed, int symbolIndex, ArtworkSymbolStyle style,
		Color baseColor, Color accentColor, Color glowColor, int starCount, float symbolRotation)
	{
		Seed = seed;
		SymbolIndex = symbolIndex;
		Style = style;
		BaseColor = baseColor;
		AccentColor = accentColor;
		GlowColor = glowColor;
		StarCount = starCount;
		SymbolRotation = symbolRotation;

		// 独立混合种子，不推进 ResolveSpec 原有随机序列，保持历史公开契约。
		uint detail = unchecked((uint)seed * 747796405u + 2891336453u);
		detail = (detail ^ (detail >> 16)) * 2246822519u;
		detail ^= detail >> 13;
		SecondaryGeometryIndex = (int)(detail % 6);
		OrbitTickCount = 24 + (int)((detail >> 4) % 6) * 8;
		CoreGlowStrength = 0.65f + ((detail >> 9) & 255) / 255f * 0.35f;
		CornerOrnamentIndex = (int)((detail >> 17) & 3);
		NebulaOffset = ((detail >> 20) & 1023) / 1023f;
	}
}

/// <summary>
/// 程序化卡图生成器——0 美术资产下为每张卡派生独一无二且可复现的卡面规格。
/// 同一张卡（同 Id）跨会话生成结果一致；规格派生为纯函数，可单元测试。
/// </summary>
public static class CardArtworkGenerator
{
	/// <summary>几何符号库大小。</summary>
	public const int SymbolCount = 12;

	// ===== 星途色板 · 类型基调 =====

	private static readonly Color _minionBase = new("#3d2f1a");
	private static readonly Color _minionAccent = new("#ffd98e");
	private static readonly Color _spellBase = new("#16283d");
	private static readonly Color _spellAccent = new("#7fd8ff");
	private static readonly Color _domainBase = new("#2a1a3d");
	private static readonly Color _domainAccent = new("#c9a0ff");

	// ===== 稀有度光晕 =====

	private static readonly Color _glowMaster = new(1.0f, 0.84f, 0.0f, 0.9f);   // 大师 金
	private static readonly Color _glowExcellent = new(0.75f, 0.78f, 0.85f, 0.75f); // 极佳 银
	private static readonly Color _glowGood = new(0.8f, 0.5f, 0.2f, 0.6f);      // 良好 铜
	private static readonly Color _glowSpecial = new(1.0f, 0.55f, 0.0f, 0.8f);  // 专属 橙

	/// <summary>主题映射缓存：cardId → heroId（懒加载 ThemeProfile）。</summary>
	private static Dictionary<string, string>? _cardThemeMap;

	/// <summary>
	/// 纯函数：由卡牌身份派生卡面规格。同输入恒同输出。
	/// </summary>
	/// <param name="cardId">卡牌 ID（snake_case）。</param>
	/// <param name="type">卡牌类型（随从/法术）。</param>
	/// <param name="rarity">稀有度。</param>
	/// <param name="tags">机制标签（用于领域判定）。</param>
	/// <param name="domainId">领域标识（非空即领域牌）。</param>
	/// <param name="heroTheme">所属英雄 ID（ayame/rie/sokou），null 或空走通用风格。</param>
	public static CardArtworkSpec ResolveSpec(string cardId, CardType type, CardRarity rarity,
		CardMechanicTag tags, string domainId, string? heroTheme)
	{
		int seed = StableHash(cardId ?? "");
		var rng = new Random(seed);

		bool isDomain = !string.IsNullOrEmpty(domainId) || tags.HasFlag(CardMechanicTag.Domain);
		Color baseColor, accentColor;
		if (isDomain)
		{
			baseColor = _domainBase;
			accentColor = _domainAccent;
		}
		else if (type == CardType.Minion)
		{
			baseColor = _minionBase;
			accentColor = _minionAccent;
		}
		else
		{
			baseColor = _spellBase;
			accentColor = _spellAccent;
		}

		ArtworkSymbolStyle style = heroTheme switch
		{
			"ayame" => ArtworkSymbolStyle.Rune,
			"rie" => ArtworkSymbolStyle.Mecha,
			"sokou" => ArtworkSymbolStyle.Mecha,
			_ => ArtworkSymbolStyle.Abstract,
		};

		Color glow = rarity switch
		{
			CardRarity.Master => _glowMaster,
			CardRarity.Excellent => _glowExcellent,
			CardRarity.Good => _glowGood,
			CardRarity.Special => _glowSpecial,
			CardRarity.Derivative or CardRarity.Common or CardRarity.StatusToken => new Color(0, 0, 0, 0),
			_ => new Color(0, 0, 0, 0),
		};

		int symbolIndex = rng.Next(SymbolCount);
		int starCount = 18 + rng.Next(15); // 18..32
		float rotation = (float)(rng.NextDouble() - 0.5) * 0.35f; // ±10° 抖动

		return new CardArtworkSpec(seed, symbolIndex, style, baseColor, accentColor, glow, starCount, rotation);
	}

	/// <summary>
	/// 解析卡牌所属英雄主题（ayame/rie/sokou），无归属返回 null。
	/// 通过各 ThemeProfile 的 CoreCardIds 反向映射，懒加载并缓存。
	/// </summary>
	public static string? ResolveHeroTheme(string cardId)
	{
		if (string.IsNullOrEmpty(cardId))
		{
			return null;
		}

		_cardThemeMap ??= BuildThemeMap();
		return _cardThemeMap.TryGetValue(cardId, out string? heroId) ? heroId : null;
	}

	/// <summary>测试挂钩：清空主题映射缓存。</summary>
	public static void ResetThemeCache() => _cardThemeMap = null;

	/// <summary>
	/// 稳定字符串哈希（FNV-1a，32 位）。
	/// 不能用 string.GetHashCode()——.NET 运行时跨会话随机化，会破坏卡面可复现性。
	/// </summary>
	public static int StableHash(string s)
	{
		const uint fnvPrime = 16777619;
		uint hash = 2166136261;
		foreach (char c in s)
		{
			hash ^= c;
			hash *= fnvPrime;
		}
		return (int)(hash & 0x7FFFFFFF);
	}

	private static Dictionary<string, string> BuildThemeMap()
	{
		var map = new Dictionary<string, string>();
		string[] themePaths =
		{
			"res://Resources/Themes/ThemeProfile_Ayame.tres",
			"res://Resources/Themes/ThemeProfile_Rie.tres",
			"res://Resources/Themes/ThemeProfile_Sokou.tres",
		};

		foreach (string path in themePaths)
		{
			if (!ResourceLoader.Exists(path))
			{
				continue;
			}

			ThemeProfile profile = GD.Load<ThemeProfile>(path);
			if (profile == null || string.IsNullOrEmpty(profile.HeroId))
			{
				continue;
			}

			foreach (string cardId in profile.CoreCardIds)
			{
				_ = map.TryAdd(cardId, profile.HeroId);
			}
		}

		GD.Print($"[CardArtworkGenerator] 主题映射已构建 — {map.Count} 张主题核心卡");
		return map;
	}
}
