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
    Spell,

    /// <summary>
    /// 领域牌：展开一个持久领域效果，挂在英雄身上，整场战斗生效。
    /// 同类领域可以叠加层数。
    /// </summary>
    Domain
}

/// <summary>
/// 卡牌稀有度。
/// 0=衍生（Token，无法自然获得），1=金卡（大师级），2=银卡（极佳），3=铜卡（良好），4=铁卡（普通）。
/// 稀有度当前仅用于标注，对游戏机制无影响。
/// </summary>
public enum CardRarity
{
    /// <summary>衍生卡（Token），无法通过奖励/商店自然获得。</summary>
    Derivative = 0,

    /// <summary>大师级，金卡。</summary>
    Master = 1,

    /// <summary>极佳，银卡。</summary>
    Excellent = 2,

    /// <summary>良好，铜卡。</summary>
    Good = 3,

    /// <summary>普通，铁卡。</summary>
    Common = 4
}
