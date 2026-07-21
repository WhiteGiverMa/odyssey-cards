using Godot;

namespace OdysseyCards.Core;

/// <summary>
/// 角色主题画像资源。每个角色定义「偏好哪些机制标签」+「招牌核心卡」，
/// 供 <see cref="OdysseyCards.Roguelike.ThemedDeckGenerator"/> 生成主题随机卡组。
///
/// 设计原则：
///   - 权重值是「相对偏好」，可正可负。0 = 中性，正值 = 偏好，负值 = 不擅长。
///     生成器对每张候选卡计算 Σ(标签权重 × 卡的标签) + override，作为抽卡概率权重。
///   - 权重值随时可改——这是数值平衡切入口。改一个数字就能调整某角色出现某类卡的概率。
///   - <see cref="CoreCardIds"/> 是主题味道的下限保险：即使加权随机抽偏了，
///     核心池的招牌卡仍保证角色识别度。
///   - 主题是「宽松」的：权重不强制排他，不同角色画像可以有交集（如绮梦和溯光都偏好 Draw）。
///
/// 资源位置：Resources/Themes/ThemeProfile_&lt;HeroId&gt;.tres
/// </summary>
public partial class ThemeProfile : Resource
{
	/// <summary>
	/// 关联的英雄 ID（ayame/rie/sokou）。
	/// </summary>
	[Export] public string HeroId { get; set; } = "";

	/// <summary>
	/// 主题名（展示用，如「绮梦·守护续航」）。
	/// </summary>
	[Export] public string ThemeName { get; set; } = "";

	/// <summary>
	/// 标签偏好权重表。
	/// Key = <see cref="CardMechanicTag"/> 的单个标签（不要用组合标签），
	/// Value = 权重（正=偏好，负=不擅长，0=中性）。
	/// 未列出的标签视为权重 0。
	///
	/// Godot 编辑器中用整数 Key 表示枚举位值（如 DirectDamage=1, Heal=4）。
	/// </summary>
	[Export] public Godot.Collections.Dictionary<int, int> TagWeights { get; set; } = new();

	/// <summary>
	/// 关键词偏好权重表。
	/// Key = <see cref="Keyword"/> 的枚举值（如 Recycle=8、Deathrattle=4），Value = 权重。
	/// 与 <see cref="TagWeights"/> 正交：机制标签描述「做什么」，关键词描述「以什么规则触发」。
	/// </summary>
	[Export] public Godot.Collections.Dictionary<int, int> KeywordWeights { get; set; } = new();

	/// <summary>
	/// 招牌核心卡 ID 列表（5-8 张）。
	/// 生成卡组时几乎必带，保证角色识别度。
	/// </summary>
	[Export] public Godot.Collections.Array<string> CoreCardIds { get; set; } = new();

	/// <summary>
	/// 单张卡的人工权重 override。
	/// Key = 卡牌 ID，Value = 额外权重（叠加到机械计算结果上）。
	/// 用于「机械算不出来但人懂」的特殊关联（剧情/设定/特定 combo）。
	/// </summary>
	[Export] public Godot.Collections.Dictionary<string, int> CardWeightOverrides { get; set; } = new();

	/// <summary>
	/// 生成卡组的目标张数（默认 20，与构筑上限一致）。
	/// </summary>
	[Export] public int TargetDeckSize { get; set; } = 20;

	/// <summary>
	/// 单张卡在卡组中的最大重复数（默认 2，避免同一卡堆叠）。
	/// </summary>
	[Export] public int MaxDuplicatesPerCard { get; set; } = 2;

	/// <summary>
	/// 领域牌在卡组中的最大数量（默认 3，太多领域会卡手）。
	/// </summary>
	[Export] public int MaxDomainCards { get; set; } = 3;

	/// <summary>
	/// 获取某标签的权重（未列出返回 0）。
	/// </summary>
	public int GetTagWeight(CardMechanicTag tag)
	{
		// TagWeights 的 Key 是位值，可能存的是组合标签；这里只查精确匹配的单标签
		if (TagWeights.TryGetValue((int)tag, out var w))
			return w;
		return 0;
	}

	/// <summary>
	/// 获取某关键词的权重（未列出返回 0）。
	/// </summary>
	public int GetKeywordWeight(Keyword keyword)
	{
		if (KeywordWeights.TryGetValue((int)keyword, out var w))
			return w;
		return 0;
	}

	/// <summary>
	/// 获取某张卡的人工 override 权重（未列出返回 0）。
	/// </summary>
	public int GetCardOverride(string cardId)
	{
		if (CardWeightOverrides.TryGetValue(cardId, out var w))
			return w;
		return 0;
	}
}
