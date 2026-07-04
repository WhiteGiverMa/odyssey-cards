namespace OdysseyCards.Relic;
using System;
using System.Linq;
using OdysseyCards.Card;
using OdysseyCards.Combat;
using OdysseyCards.Core;

/// <summary>
/// 超体插件盒——战斗开始时随机一张「超体」机制 tag 卡牌放入手牌，花费变为 0。
/// </summary>
public sealed class SuperBodyBoxRelic : AbstractRelic
{
	public override string Id => "super_body_box";
	public override string Name => "超体插件盒";
	public override string Description => "战斗开始时，将随机一张「超体」牌放入你的手牌，使其花费为0。";

	public override void OnBattleStart(CombatManager combat)
	{
		var allCards = GameManager.Instance.GetAllCards();
		var superBodyCards = allCards
			.Where(cd => cd.HasMechanicTag(CardMechanicTag.SuperBody))
			.ToList();

		if (superBodyCards.Count == 0)
			return;

		var pickedData = superBodyCards[Random.Shared.Next(superBodyCards.Count)];
		var newCard = new Card(pickedData);
		newCard.CostModifier = -pickedData.Cost;
		combat.PlayerHero.DeckState.AddToHand(newCard);
	}
}
