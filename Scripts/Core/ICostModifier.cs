namespace OdysseyCards.Core;

/// <summary>
/// 卡牌费用修改器接口。
/// 用于 StatusEffect、ActiveDomain 等系统运行时修改卡牌费用。
/// 与 Relic 的 ModifyCost 不同——此接口用于"宇宙冷漠"等
/// 需要扫描多个卡牌区域（手牌/抽牌堆/弃牌堆）的大范围费用修改。
/// </summary>
public interface ICostModifier
{
	/// <summary>
	/// 修改指定卡牌的有效费用。
	/// </summary>
	/// <param name="card">目标卡牌</param>
	/// <param name="currentCost">当前累计费用</param>
	/// <returns>修改后的费用</returns>
	int ModifyCost(Card.Card card, int currentCost);
}
