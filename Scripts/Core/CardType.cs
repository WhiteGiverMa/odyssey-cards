namespace OdysseyCards.Core;

/// <summary>
/// 卡牌类型。
/// </summary>
public enum CardType
{
    /// <summary>
    /// 随从牌：可召唤到场上的单位。
    /// </summary>
    Minion,

    /// <summary>
    /// 法术牌：使用时立即生效。
    /// </summary>
    Spell
}

/// <summary>
/// 卡牌稀有度。
/// </summary>
public enum CardRarity
{
    /// <summary>
    /// 普通：常见卡牌，基础效果。
    /// </summary>
    Common,

    /// <summary>
    /// 稀有：不太常见，中等效果。
    /// </summary>
    Uncommon,

    /// <summary>
    /// 罕见：不常见，强力效果。
    /// </summary>
    Rare,

    /// <summary>
    /// 传说：非常稀有，独特强力效果。
    /// </summary>
    Legendary
}
