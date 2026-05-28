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
    /// 卡牌名称。
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
    /// 创建卡牌运行时实例。
    /// </summary>
    /// <param name="data">卡牌数据资源</param>
    public Card(CardData data)
    {
        Data = data;
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

    /// <summary>
    /// 获取卡牌信息摘要。
    /// </summary>
    /// <returns>格式如「火球术 | 4费」</returns>
    public virtual string GetCardInfo()
    {
        return $"{CardName} | {Cost}费";
    }
}
