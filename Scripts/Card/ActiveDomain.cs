using OdysseyCards.Core;

namespace OdysseyCards.Card;

/// <summary>
/// 领域运行时数据。
/// 表示英雄身上一个展开的持久领域效果，支持同类叠加层数。
/// 实现 <see cref="IPermanentEffect"/>——不随时间衰减，仅通过 Counter 消耗或主动移除。
/// 参考 STS2 的 Power（PowerStackType.Counter + 无 AfterSideTurnEnd 衰减）。
/// </summary>
public class ActiveDomain : IPermanentEffect
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
	/// 最近一次触发所在的战斗回合。
	/// 用于“每个敌方回合首次被攻击”这类领域，避免同一回合多段攻击消耗多层。
	/// </summary>
	public int LastTriggeredTurn { get; set; } = -1;

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
