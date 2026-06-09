namespace OdysseyCards.Core;

/// <summary>
/// 标记接口——永久效果（不随时间衰减）。
/// 实现此接口的效果在战斗期间持续存在，不会自动衰减。
/// 例：ActiveDomain（领域效果）——只通过 Counter 消耗或主动移除。
/// 参考 STS2 中没有实现 ITemporaryPower 的 Power 类。
/// </summary>
public interface IPermanentEffect
{
}

/// <summary>
/// 标记接口——临时效果（随时间衰减）。
/// 实现此接口的效果在每个 TickTiming 触发时自动递减层数。
/// 例：StatusEffect（状态效果）——每 EnemyTurnEnd 减一层。
/// 参考 STS2 中实现 ITemporaryPower 的 Power 类。
/// </summary>
public interface ITemporaryEffect
{
}
