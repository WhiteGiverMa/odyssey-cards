using Godot;
using OdysseyCards.Combat;
using OdysseyCards.Core;

namespace OdysseyCards.Relic;

/// <summary>
/// 实习工牌：负面藏品。
/// 战斗开始时将一张「疲劳」洗入抽牌堆。
/// </summary>
public sealed class InternBadgeRelic : AbstractRelic
{
	public override string Id => "intern_badge";
	public override string Name => "实习工牌";
	public override string Description => "战斗开始时将一张「疲劳」洗入抽牌堆。";

	public override bool IsBeneficial => false;

	public override void OnBattleStart(CombatManager combat)
	{
		// 从注册表获取疲劳卡牌数据
		var fatigueData = GameManager.Instance.GetCardById("status_tiredness");
		if (fatigueData == null)
		{
			GD.PrintErr("[InternBadge] 未找到疲劳卡牌数据（id: status_tiredness）");
			return;
		}

		var fatigueCard = new OdysseyCards.Card.Card(fatigueData);

		// 洗入抽牌堆（即加入后洗牌）
		combat.PlayerHero.DeckState.AddToDrawPileBottom(fatigueCard);
		combat.PlayerHero.ShuffleDrawPile();

		GD.Print("[InternBadge] 将「疲劳」洗入抽牌堆");
	}
}
