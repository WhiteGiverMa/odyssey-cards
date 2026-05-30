using System;

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
/// 0=衍生（Token，无法自然获得），1=金卡（大师级），2=银卡（极佳），3=铜卡（良好），
/// 4=铁卡（普通），5=角色专属特殊卡（不在常规奖励中出现）。
///
/// 0~4 的稀有度值 = 单次卡牌奖励中该卡作为选项出现时的同名最大张数。
/// 例如 Common(4) 的卡牌在奖励中出现时最多 4 张，Master(1) 最多 1 张。
/// Derivative(0) 和 Special(5) 不会出现在奖励中。
/// </summary>
public enum CardRarity
{
    /// <summary>衍生卡（Token），无法通过奖励/商店自然获得。</summary>
    Derivative = 0,

    /// <summary>大师级，金卡。奖励中最多 1 张同名。</summary>
    Master = 1,

    /// <summary>极佳，银卡。奖励中最多 2 张同名。</summary>
    Excellent = 2,

    /// <summary>良好，铜卡。奖励中最多 3 张同名。</summary>
    Good = 3,

    /// <summary>普通，铁卡。奖励中最多 4 张同名。</summary>
    Common = 4,

    /// <summary>角色专属特殊卡，不在常规稀有度奖励中出现。</summary>
    Special = 5
}

/// <summary>
/// 稀有度工具方法。
/// </summary>
public static class CardRarityExtensions
{
    /// <summary>
    /// 获取该稀有度在奖励中出现的同名最大张数。
    /// Derivative(0) 和 Special(5) 返回 0（不出现）。
    /// </summary>
    public static int GetMaxRewardCopies(this CardRarity rarity)
    {
        return rarity switch
        {
            CardRarity.Common => 4,
            CardRarity.Good => 3,
            CardRarity.Excellent => 2,
            CardRarity.Master => 1,
            CardRarity.Derivative => 0,
            CardRarity.Special => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(rarity), rarity, "未知稀有度")
        };
    }

    /// <summary>
    /// 该稀有度的卡牌能否在奖励中出现。
    /// </summary>
    public static bool CanAppearInReward(this CardRarity rarity)
    {
        return rarity is CardRarity.Common or CardRarity.Good
            or CardRarity.Excellent or CardRarity.Master;
    }
}
