namespace OdysseyCards.Relic;
using OdysseyCards.Combat;

/// <summary>
/// 魔幻陀螺——参考 STS2 不休陀螺：没有手牌时抽 1 张牌。
/// </summary>
public sealed class MagicalTopRelic : AbstractRelic
{
	public override string Id => "magical_top";
	public override string Name => "魔幻陀螺";
	public override string Description => "没有手牌时抽1张牌。";

	public override void OnTurnStart(CombatManager combat)
	{
		if (combat.PlayerHero.DeckState.Hand.Count == 0)
			combat.PlayerHero.DrawCards(1);
	}
}
