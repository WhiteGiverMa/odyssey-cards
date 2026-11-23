using System;
using System.Collections.Generic;
using System.Linq;
using OdysseyCards.Core;

namespace OdysseyCards.Roguelike;

/// <summary>
/// 主题卡组生成器——纯 C#，可单测，不依赖 Godot API。
///
/// 生成流程：
///   1. 从卡池过滤合法候选（排除状态牌、衍生牌等不可构筑的卡）
///   2. 加入核心池招牌卡（几乎必带，保证角色识别度）：核心卡同样只能来自合法候选池，
///      「显式点名」不是衍生牌进入卡组的后门
///   3. 对剩余卡按主题权重加权随机抽取，施加约束：
///      - 法力曲线（低费/中费/高费/超高费分布）
///      - 类型分布（随从/法术/领域比例）
///      - 单卡重复上限、领域数量上限
///   4. 不足目标张数时从剩余候选按权重兜底补满
///
/// 权重计算：对每张候选卡 card，
///   weight = Σ(标签权重 × 卡的标签命中) + 人工 override
/// 其中「标签命中」= card.HasMechanicTag(tag) ? 1 : 0
/// 关键词偏好通过 <see cref="DeckProfile.KeywordWeights"/> 以同样方式叠加。
/// 未在 <see cref="DeckProfile.TagWeights"/> 中列出的标签贡献 0。
/// weight 可为负（不擅长的卡），生成器在加权随机时 clamp 到最小 1（避免完全排除但大幅降权）。
///
/// 敌方牌手厚度：普通 10~20、精英 21~39、Boss 40。当目标厚度 &gt; 20 时，
/// 单卡重复上限会按 <c>ceil(target / 12)</c> 自动放宽，保证 61 张卡池能稳定产出 40 张 Boss 卡组。
/// </summary>
public static class ThemedDeckGenerator
{
	/// <summary>玩家构筑上限——超过它即进入「敌方厚卡组」区间。</summary>
	public const int ConstructedDeckLimit = 20;

	/// <summary>
	/// 法力曲线目标分布（低/中/高/超高费比例，归一化后按此比例约束）。
	/// 低费=0-3，中费=4-7，高费=8-12，超高费=13-30。
	/// 这是「软约束」——优先满足张数，曲线作为偏好；开局主题卡组默认不鼓励超高费。
	/// </summary>
	private static readonly (int MinCost, int MaxCost, double Ratio)[] ManaCurveTargets =
	{
		(0, 3, 0.45),  // 低费 ~45%
		(4, 7, 0.35),  // 中费 ~35%
		(8, 12, 0.20),  // 高费 ~20%
		(13, 30, 0.00),  // 超高费：需要额外构筑/法力突破，不作为开局曲线目标
	};

	/// <summary>
	/// 卡组中随从与法术的目标比例（不含领域）。软约束。
	/// 炉石类游戏随从占比通常 50%-65%。
	/// </summary>
	private const double MinionRatio = 0.55;

	/// <summary>
	/// 生成结果。
	/// </summary>
	public readonly struct GenerationResult
	{
		/// <summary>生成的卡牌 ID 列表（已按 CoreCardIds 优先 + 加权随机排序）。</summary>
		public IReadOnlyList<string> CardIds { get; init; }

		/// <summary>使用的卡组画像（便于调试/展示）。</summary>
		public DeckProfile Profile { get; init; }

		/// <summary>生成统计（各标签命中数、曲线分布等），便于预览对比。</summary>
		public GenerationStats Stats { get; init; }

		public static GenerationResult Empty(DeckProfile profile) => new()
		{
			CardIds = Array.Empty<string>(),
			Profile = profile,
			Stats = GenerationStats.Empty,
		};
	}

	/// <summary>
	/// 生成统计，用于预览对比不同角色的生成差异。
	/// </summary>
	public readonly struct GenerationStats
	{
		/// <summary>总张数。</summary>
		public int TotalCards { get; init; }

		/// <summary>核心池招牌卡命中数。</summary>
		public int CoreCardsIncluded { get; init; }

		/// <summary>随从数。</summary>
		public int MinionCount { get; init; }

		/// <summary>法术数。</summary>
		public int SpellCount { get; init; }

		/// <summary>领域数。</summary>
		public int DomainCount { get; init; }

		/// <summary>法力曲线分布：[低费数, 中费数, 高费数, 超高费数]。</summary>
		public int[] ManaCurve { get; init; }

		/// <summary>各机制标签的命中数（Key=标签名，Value=张数）。</summary>
		public IReadOnlyDictionary<string, int> TagCounts { get; init; }

		public static GenerationStats Empty => new()
		{
			TotalCards = 0,
			CoreCardsIncluded = 0,
			MinionCount = 0,
			SpellCount = 0,
			DomainCount = 0,
			ManaCurve = new int[ManaCurveTargets.Length],
			TagCounts = new Dictionary<string, int>(),
		};
	}

	/// <summary>
	/// 从 Godot 资源画像生成主题卡组（运行时入口）。
	/// </summary>
	public static GenerationResult Generate(ThemeProfile profile, IReadOnlyList<CardData> cardPool, Random rng)
		=> profile == null
			? GenerationResult.Empty(new DeckProfile())
			: Generate(profile.ToDeckProfile(), cardPool, rng);

	/// <summary>
	/// 生成主题卡组（纯 C# 入口）。
	/// </summary>
	/// <param name="profile">卡组画像。</param>
	/// <param name="cardPool">候选卡池（生成器内部会过滤状态牌与不可构筑稀有度）。</param>
	/// <param name="rng">随机数生成器（传入可复现的 RNG 即可确定性生成）。</param>
	/// <returns>生成结果；profile 为 null 或卡池不足返回 Empty。</returns>
	public static GenerationResult Generate(DeckProfile profile, IReadOnlyList<IDeckCard> cardPool, Random rng)
	{
		if (profile == null || cardPool == null || cardPool.Count == 0)
			return GenerationResult.Empty(profile ?? new DeckProfile());

		int target = Math.Max(1, profile.TargetDeckSize);
		var coreIds = profile.CoreCardIds.ToList();
		int maxDup = ResolveMaxDuplicates(profile, target);
		int maxDomain = Math.Max(0, profile.MaxDomainCards);

		// 1. 过滤候选：排除状态牌、衍生牌、角色专属牌（这些不可构筑）
		var legalPool = cardPool
			.Where(c => c.Type != CardType.Status)
			.Where(c => c.Rarity != CardRarity.Derivative && c.Rarity != CardRarity.Special && c.Rarity != CardRarity.StatusToken)
			.ToList();
		if (legalPool.Count == 0)
			return GenerationResult.Empty(profile);

		// 2. 核心池：CoreCardIds 只能从 legalPool 选取——衍生牌 / 状态牌 / 专属牌
		//    即使被「显式点名」也绝不进入玩家主题卡组或敌方卡组。
		var result = new List<IDeckCard>();
		var dupCount = new Dictionary<string, int>();
		int domainCount = 0;

		foreach (var coreId in coreIds)
		{
			if (result.Count >= target)
				break;
			var card = legalPool.FirstOrDefault(c => c.Id == coreId);
			if (card == null)
				continue;
			if (dupCount.GetValueOrDefault(card.Id) >= maxDup)
				continue;
			if (card.Type == CardType.Domain && domainCount >= maxDomain)
				continue;
			result.Add(card);
			dupCount[card.Id] = dupCount.GetValueOrDefault(card.Id) + 1;
			if (card.Type == CardType.Domain)
				domainCount++;
		}

		// 3. 加权随机填充剩余位置
		var remaining = target - result.Count;
		if (remaining > 0)
		{
			var weighted = legalPool
				.Where(c => dupCount.GetValueOrDefault(c.Id) < maxDup)
				.Where(c => c.Type != CardType.Domain || domainCount < maxDomain)
				.Select(c => (card: c, weight: ComputeWeight(profile, c)))
				.ToList();

			// 加权随机抽取（不重复同一实例，但允许同一 Id 达到 maxDup）
			// 使用「按权重轮盘 + 逐张抽取」简化实现
			while (result.Count < target && weighted.Count > 0)
			{
				// 过滤已达上限的卡
				weighted = weighted
					.Where(w => dupCount.GetValueOrDefault(w.card.Id) < maxDup)
					.Where(w => w.card.Type != CardType.Domain || domainCount < maxDomain)
					.ToList();
				if (weighted.Count == 0)
					break;

				// 软约束：法力曲线偏好（降低不匹配曲线的卡权重，但不硬排除）
				ApplyManaCurveSoftBias(weighted, result.Count, target, result);

				// 软约束：随从比例偏好
				ApplyMinionRatioSoftBias(weighted, result);

				var totalWeight = weighted.Sum(w => w.weight);
				if (totalWeight <= 0)
				{
					// 所有权重都 ≤0（极端情况），退化为均匀随机
					var pick = weighted[rng.Next(weighted.Count)];
					result.Add(pick.card);
				}
				else
				{
					double roll = rng.NextDouble() * totalWeight;
					double acc = 0;
					int pickedIdx = weighted.Count - 1;  // 兜底
					for (int i = 0; i < weighted.Count; i++)
					{
						acc += weighted[i].weight;
						if (roll < acc)
						{
							pickedIdx = i;
							break;
						}
					}
					var picked = weighted[pickedIdx];
					result.Add(picked.card);
				}

				var added = result[^1];
				dupCount[added.Id] = dupCount.GetValueOrDefault(added.Id) + 1;
				if (added.Type == CardType.Domain)
					domainCount++;
			}
		}

		// 4. 不足时从剩余候选兜底（忽略权重，保证张数）
		if (result.Count < target)
		{
			foreach (var c in legalPool)
			{
				if (result.Count >= target)
					break;
				if (dupCount.GetValueOrDefault(c.Id) >= maxDup)
					continue;
				if (c.Type == CardType.Domain && domainCount >= maxDomain)
					continue;
				result.Add(c);
				dupCount[c.Id] = dupCount.GetValueOrDefault(c.Id) + 1;
				if (c.Type == CardType.Domain)
					domainCount++;
			}
		}

		return BuildResult(profile, result, coreIds);
	}

	/// <summary>
	/// 解析单卡重复上限。目标厚度超过玩家构筑上限（20）时自动放宽，
	/// 保证 61 张卡池能稳定生成 40 张 Boss 卡组。
	/// </summary>
	public static int ResolveMaxDuplicates(DeckProfile profile, int targetDeckSize)
	{
		int configured = Math.Max(1, profile?.MaxDuplicatesPerCard ?? 1);
		if (targetDeckSize <= ConstructedDeckLimit)
			return configured;

		int scaled = (int)Math.Ceiling(targetDeckSize / 12.0);
		return Math.Max(configured, scaled);
	}

	/// <summary>
	/// 计算单张卡对某卡组画像的主题权重。
	/// weight = Σ(标签权重 × 卡的标签命中) + 人工 override
	/// 最小 clamp 到 1（避免负权重完全排除，但大幅降权）。
	/// </summary>
	private static int ComputeWeight(DeckProfile profile, IDeckCard card)
	{
		int weight = 0;
		foreach (var (tag, tagWeight) in profile.TagWeights)
		{
			if (tagWeight == 0)
				continue;
			if (card.HasMechanicTag(tag))
				weight += tagWeight;
		}
		foreach (var (keyword, keywordWeight) in profile.KeywordWeights)
		{
			if (keywordWeight == 0)
				continue;
			if (card.HasKeyword(keyword))
				weight += keywordWeight;
		}
		weight += profile.CardWeightOverrides.TryGetValue(card.Id, out int overrideWeight)
			? overrideWeight
			: 0;
		return Math.Max(1, weight);
	}

	/// <summary>
	/// 法力曲线软约束：根据当前卡组的曲线分布，降低已超额曲线段的卡权重。
	/// 不硬排除——优先满足张数。
	/// </summary>
	private static void ApplyManaCurveSoftBias(List<(IDeckCard card, int weight)> weighted, int currentCount, int target, List<IDeckCard> currentDeck)
	{
		if (currentCount == 0)
			return;

		// 计算当前卡组各曲线段张数
		var segmentCounts = new int[ManaCurveTargets.Length];
		foreach (var c in currentDeck)
		{
			for (int i = 0; i < ManaCurveTargets.Length; i++)
			{
				if (c.Cost >= ManaCurveTargets[i].MinCost && c.Cost <= ManaCurveTargets[i].MaxCost)
				{
					segmentCounts[i]++;
					break;
				}
			}
		}

		// 找到已超额的段，降低该段卡的权重
		for (int i = 0; i < weighted.Count; i++)
		{
			var cost = weighted[i].card.Cost;
			for (int s = 0; s < ManaCurveTargets.Length; s++)
			{
				if (cost >= ManaCurveTargets[s].MinCost && cost <= ManaCurveTargets[s].MaxCost)
				{
					double expectedRatio = ManaCurveTargets[s].Ratio;
					int expectedCount = (int)Math.Ceiling(target * expectedRatio);
					if (segmentCounts[s] >= expectedCount)
					{
						// 已超额：权重减半
						weighted[i] = (weighted[i].card, Math.Max(1, weighted[i].weight / 2));
					}
					break;
				}
			}
		}
	}

	/// <summary>
	/// 随从比例软约束：随从过多时降低随从权重，法术过多时降低法术权重。
	/// </summary>
	private static void ApplyMinionRatioSoftBias(List<(IDeckCard card, int weight)> weighted, List<IDeckCard> currentDeck)
	{
		if (currentDeck.Count == 0)
			return;

		// 只看随从+法术（领域不算入此约束）
		var nonDomain = currentDeck.Where(c => c.Type != CardType.Domain).ToList();
		if (nonDomain.Count == 0)
			return;

		int minionCount = nonDomain.Count(c => c.Type == CardType.Minion);
		double currentMinionRatio = (double)minionCount / nonDomain.Count;

		for (int i = 0; i < weighted.Count; i++)
		{
			if (weighted[i].card.Type == CardType.Domain)
				continue;

			if (weighted[i].card.Type == CardType.Minion && currentMinionRatio > MinionRatio + 0.15)
			{
				// 随从过多：随从权重减半
				weighted[i] = (weighted[i].card, Math.Max(1, weighted[i].weight / 2));
			}
			else if (weighted[i].card.Type == CardType.Spell && currentMinionRatio < MinionRatio - 0.15)
			{
				// 法术过多：法术权重减半
				weighted[i] = (weighted[i].card, Math.Max(1, weighted[i].weight / 2));
			}
		}
	}

	/// <summary>
	/// 构建生成结果 + 统计。
	/// </summary>
	private static GenerationResult BuildResult(DeckProfile profile, List<IDeckCard> cards, List<string> coreIds)
	{
		var cardIds = cards.Select(c => c.Id).ToList();

		int minionCount = cards.Count(c => c.Type == CardType.Minion);
		int spellCount = cards.Count(c => c.Type == CardType.Spell);
		int domainCount = cards.Count(c => c.Type == CardType.Domain);

		var manaCurve = new int[ManaCurveTargets.Length];
		foreach (var c in cards)
		{
			for (int i = 0; i < ManaCurveTargets.Length; i++)
			{
				if (c.Cost >= ManaCurveTargets[i].MinCost && c.Cost <= ManaCurveTargets[i].MaxCost)
				{
					manaCurve[i]++;
					break;
				}
			}
		}

		// 标签命中统计
		var tagCounts = new Dictionary<string, int>();
		foreach (CardMechanicTag tag in Enum.GetValues<CardMechanicTag>())
		{
			if (tag == CardMechanicTag.None)
				continue;
			int count = cards.Count(c => c.HasMechanicTag(tag));
			if (count > 0)
				tagCounts[tag.ToString()] = count;
		}
		foreach (Keyword keyword in Enum.GetValues<Keyword>())
		{
			if (keyword == Keyword.None)
				continue;
			int count = cards.Count(c => c.HasKeyword(keyword));
			if (count > 0)
				tagCounts[$"Keyword:{keyword}"] = count;
		}

		int coreIncluded = cards.Count(c => coreIds.Contains(c.Id));

		var stats = new GenerationStats
		{
			TotalCards = cards.Count,
			CoreCardsIncluded = coreIncluded,
			MinionCount = minionCount,
			SpellCount = spellCount,
			DomainCount = domainCount,
			ManaCurve = manaCurve,
			TagCounts = tagCounts,
		};

		return new GenerationResult
		{
			CardIds = cardIds,
			Profile = profile,
			Stats = stats,
		};
	}
}
