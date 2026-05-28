using OdysseyCards.Core;

namespace OdysseyCards.Card;

/// <summary>
/// 领域运行时数据。
/// 表示英雄身上一个展开的持久领域效果，支持同类叠加层数。
/// </summary>
public class ActiveDomain
{
    /// <summary>
    /// 领域唯一标识（同名叠加）。
    /// </summary>
    public string DomainId { get; }

    /// <summary>
    /// 领域效果数据（包含触发后的 EffectType 和每层数值）。
    /// </summary>
    public CardEffectData EffectData { get; }

    /// <summary>
    /// 当前叠加层数。
    /// </summary>
    public int StackCount { get; set; }

    /// <summary>
    /// 创建领域运行时实例。
    /// </summary>
    /// <param name="domainId">领域标识</param>
    /// <param name="effectData">效果数据（每层数值）</param>
    public ActiveDomain(string domainId, CardEffectData effectData)
    {
        DomainId = domainId;
        EffectData = effectData;
        StackCount = 1;
    }
}
