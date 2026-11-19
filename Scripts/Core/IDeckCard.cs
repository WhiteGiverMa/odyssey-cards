namespace OdysseyCards.Core;

/// <summary>
/// 卡组生成器可消费的最小卡牌视图。
///
/// 存在的理由：<see cref="CardData"/> 继承自 <c>Godot.Resource</c>，在纯 C# 单测环境（xUnit）
/// 中无法安全实例化。主题卡组生成只关心「Id / 类型 / 稀有度 / 费用 / 标签 / 关键词」，
/// 因此抽成接口，让测试可用纯 C# stub 驱动 <see cref="OdysseyCards.Roguelike.ThemedDeckGenerator"/>。
/// </summary>
public interface IDeckCard
{
	/// <summary>卡牌唯一 ID（snake_case）。</summary>
	string Id { get; }

	/// <summary>卡牌类型（随从/法术/领域/状态）。</summary>
	CardType Type { get; }

	/// <summary>稀有度——生成器用它过滤衍生/特殊牌。</summary>
	CardRarity Rarity { get; }

	/// <summary>法力消耗。</summary>
	int Cost { get; }

	/// <summary>
	/// 所属卡包（系列）ID。内置值见 <see cref="CardPackCatalog"/>；DIY 卡可为任意字符串。
	/// 卡池过滤（启用/禁用卡包）依赖此字段。
	/// </summary>
	string PackId { get; }

	/// <summary>是否带有指定机制标签。</summary>
	bool HasMechanicTag(CardMechanicTag tag);

	/// <summary>是否带有指定战斗关键词。</summary>
	bool HasKeyword(Keyword keyword);
}
