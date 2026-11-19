using System;
using System.Collections.Generic;

namespace OdysseyCards.Core;

/// <summary>
/// 纯 C# 的卡组画像——<see cref="ThemeProfile"/>（Godot Resource）的运行时快照。
///
/// 为什么需要它：
///   - <see cref="ThemeProfile"/> 继承 <c>Godot.Resource</c> 且使用 <c>Godot.Collections</c>，
///     在无 Godot 引擎实例的 xUnit 环境下无法构造。
///   - 敌方牌手需要「在内存里临时改写画像」（例如把 20 张的玩家主题卡组扩成精英 28 张 / Boss 40 张），
///     `with` 表达式让覆盖不污染共享资源。
///
/// 该类型必须保持纯 C#：禁止 Godot API、禁止 <c>Godot.Collections</c>。
/// </summary>
public sealed record DeckProfile
{
	private static readonly IReadOnlyDictionary<CardMechanicTag, int> EmptyTagWeights =
		new Dictionary<CardMechanicTag, int>();

	private static readonly IReadOnlyDictionary<Keyword, int> EmptyKeywordWeights =
		new Dictionary<Keyword, int>();

	private static readonly IReadOnlyDictionary<string, int> EmptyCardOverrides =
		new Dictionary<string, int>();

	/// <summary>关联的英雄 / 牌手 ID。</summary>
	public string HeroId { get; init; } = "";

	/// <summary>主题名（展示用）。</summary>
	public string ThemeName { get; init; } = "";

	/// <summary>机制标签偏好权重（正=偏好，负=不擅长，0=中性）。</summary>
	public IReadOnlyDictionary<CardMechanicTag, int> TagWeights { get; init; } = EmptyTagWeights;

	/// <summary>关键词偏好权重。</summary>
	public IReadOnlyDictionary<Keyword, int> KeywordWeights { get; init; } = EmptyKeywordWeights;

	/// <summary>招牌核心卡 ID（几乎必带）。</summary>
	public IReadOnlyList<string> CoreCardIds { get; init; } = Array.Empty<string>();

	/// <summary>单卡人工权重 override。</summary>
	public IReadOnlyDictionary<string, int> CardWeightOverrides { get; init; } = EmptyCardOverrides;

	/// <summary>目标卡组厚度。</summary>
	public int TargetDeckSize { get; init; } = 20;

	/// <summary>单卡重复上限（生成器在厚度 &gt; 20 时会自动放宽）。</summary>
	public int MaxDuplicatesPerCard { get; init; } = 2;

	/// <summary>领域牌上限。</summary>
	public int MaxDomainCards { get; init; } = 3;

	/// <summary>覆盖目标厚度（返回新实例，不修改原画像）。</summary>
	public DeckProfile WithTargetDeckSize(int targetDeckSize)
		=> this with { TargetDeckSize = Math.Max(1, targetDeckSize) };

	/// <summary>覆盖核心卡（返回新实例，不修改原画像）。</summary>
	public DeckProfile WithCoreCards(IReadOnlyList<string> coreCardIds)
		=> this with { CoreCardIds = coreCardIds ?? Array.Empty<string>() };

	/// <summary>覆盖单卡重复上限。</summary>
	public DeckProfile WithMaxDuplicates(int maxDuplicatesPerCard)
		=> this with { MaxDuplicatesPerCard = Math.Max(1, maxDuplicatesPerCard) };

	/// <summary>覆盖领域上限。</summary>
	public DeckProfile WithMaxDomainCards(int maxDomainCards)
		=> this with { MaxDomainCards = Math.Max(0, maxDomainCards) };

	/// <summary>覆盖机制标签权重。</summary>
	public DeckProfile WithTagWeights(IReadOnlyDictionary<CardMechanicTag, int> tagWeights)
		=> this with { TagWeights = tagWeights ?? EmptyTagWeights };

	/// <summary>覆盖关键词权重。</summary>
	public DeckProfile WithKeywordWeights(IReadOnlyDictionary<Keyword, int> keywordWeights)
		=> this with { KeywordWeights = keywordWeights ?? EmptyKeywordWeights };
}
