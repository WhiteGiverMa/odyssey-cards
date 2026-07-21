using Godot;
using System;
using System.Collections.Generic;

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

	private static readonly Color MinionBase = new("#3d2f1a");
	private static readonly Color MinionAccent = new("#ffd98e");
	private static readonly Color SpellBase = new("#16283d");
	private static readonly Color SpellAccent = new("#7fd8ff");
	private static readonly Color DomainBase = new("#2a1a3d");
	private static readonly Color DomainAccent = new("#c9a0ff");

	// ===== 稀有度光晕 =====

	private static readonly Color GlowMaster = new(1.0f, 0.84f, 0.0f, 0.9f);   // 大师 金
	private static readonly Color GlowExcellent = new(0.75f, 0.78f, 0.85f, 0.75f); // 极佳 银
	private static readonly Color GlowGood = new(0.8f, 0.5f, 0.2f, 0.6f);      // 良好 铜
	private static readonly Color GlowSpecial = new(1.0f, 0.55f, 0.0f, 0.8f);  // 专属 橙

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
			baseColor = DomainBase;
			accentColor = DomainAccent;
		}
		else if (type == CardType.Minion)
		{
			baseColor = MinionBase;
			accentColor = MinionAccent;
		}
		else
		{
			baseColor = SpellBase;
			accentColor = SpellAccent;
		}

		var style = heroTheme switch
		{
			"ayame" => ArtworkSymbolStyle.Rune,
			"rie" => ArtworkSymbolStyle.Mecha,
			"sokou" => ArtworkSymbolStyle.Mecha,
			_ => ArtworkSymbolStyle.Abstract,
		};

		Color glow = rarity switch
		{
			CardRarity.Master => GlowMaster,
			CardRarity.Excellent => GlowExcellent,
			CardRarity.Good => GlowGood,
			CardRarity.Special => GlowSpecial,
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
			return null;

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
				continue;

			var profile = GD.Load<ThemeProfile>(path);
			if (profile == null || string.IsNullOrEmpty(profile.HeroId))
				continue;

			foreach (string cardId in profile.CoreCardIds)
			{
				map.TryAdd(cardId, profile.HeroId);
			}
		}

		GD.Print($"[CardArtworkGenerator] 主题映射已构建 — {map.Count} 张主题核心卡");
		return map;
	}
}
