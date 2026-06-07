using System.Collections.Generic;
using OdysseyCards.Core;

namespace OdysseyCards.Card;

/// <summary>
/// 运行时卡牌基类。
/// 随从（Minion）和法术（Spell）的公共抽象，
/// 不继承 Godot Node，纯 C# 类。
/// </summary>
public class Card
{
	/// <summary>
	/// 关联的卡牌数据资源。
	/// </summary>
	public CardData Data { get; }

	/// <summary>
	/// 卡牌唯一标识。
	/// </summary>
	public string Id => Data.Id;

	/// <summary>
	/// 卡牌名称（原始数据，不作为 UI 渲染源）。
	/// 渲染时使用 <see cref="GetLocalizedName"/> 以支持多语言切换。
	/// </summary>
	public string CardName => Data.CardName;

	/// <summary>
	/// 法力值消耗（部署费用）。
	/// </summary>
	public int Cost => Data.Cost;

	/// <summary>
	/// 行动花费——随从攻击时额外消耗的法力值。
	/// </summary>
	public int ActionCost => Data.ActionCost;

	/// <summary>
	/// 卡牌类型（随从或法术）。
	/// </summary>
	public CardType Type => Data.Type;

	/// <summary>
	/// 卡牌标签（多标签 [Flags] 系统）。
	/// </summary>
	public CardTag Tags => Data.Tags;

	/// <summary>
	/// 轮战：法术打出后/随从被击败后返回抽牌堆底部，不进入弃牌堆。
	/// </summary>
	public bool HasRecycle => Data.HasKeyword(Keyword.Recycle);

	/// <summary>
	/// 「偶像的黄昏」授予的被攻击后成长层数。
	/// 作为运行时牌面修饰保存在 Card 实例上，可随手牌/抽牌堆/弃牌堆流转。
	/// </summary>
	public int IdolTwilightOnAttackedStacks { get; private set; }

	/// <summary>
	/// 创建卡牌运行时实例。
	/// </summary>
	/// <param name="data">卡牌数据资源</param>
	public Card(CardData data)
	{
		Data = data;
	}

	/// <summary>
	/// 从另一张运行时卡牌复制牌面修饰。
	/// </summary>
	public void CopyRuntimeModifiersFrom(Card other)
	{
		IdolTwilightOnAttackedStacks = other.IdolTwilightOnAttackedStacks;
	}

	/// <summary>
	/// 授予「被攻击后获得 +1/+1」触发层数。
	/// </summary>
	public void GrantIdolTwilightOnAttacked(int stacks = 1)
	{
		if (Data.Type != CardType.Minion) return;
		IdolTwilightOnAttackedStacks += stacks;
	}

	/// <summary>
	/// 获取本地化的卡牌名称。
	/// 优先读取 YAML 翻译，缺失时回退到 CardData.CardName 原始字段。
	/// </summary>
	public string GetLocalizedName()
	{
		return Data?.GetLocalizedName() ?? CardName;
	}

	/// <summary>
	/// 获取本地化的卡牌描述。
	/// 优先读取 YAML 翻译，缺失时回退到 CardData.Description 原始字段。
	/// </summary>
	/// <param name="parameters">占位符替换参数（可选）</param>
	public string GetLocalizedDescription(Dictionary<string, object> parameters = null)
	{
		return Data?.GetLocalizedDescription(parameters) ?? Data?.Description ?? string.Empty;
	}

	/// <summary>
	/// 检查是否有足够法力值打出此牌。
	/// </summary>
	/// <param name="availableMana">当前可用法力值</param>
	/// <returns>费用足够时返回 true</returns>
	public virtual bool CanPlay(int availableMana)
	{
		return availableMana >= Cost;
	}

}
