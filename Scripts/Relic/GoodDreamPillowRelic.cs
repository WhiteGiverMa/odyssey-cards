using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OdysseyCards.Combat;
using OdysseyCards.Core;

namespace OdysseyCards.Relic;

/// <summary>
/// 好梦抱枕：战斗开始时将抽牌堆中的一张随机领域牌抽取。
/// 每打出一张领域牌，就将一张随机领域牌加入手牌（印牌）。
/// </summary>
public sealed class GoodDreamPillowRelic : AbstractRelic
{
    public override string Id => "good_dream_pillow";
    public override string Name => "好梦抱枕";
    public override string Description => "战斗开始时将抽牌堆中的一张随机领域牌抽取。每打出一张领域牌，就将一张随机领域牌加入手牌。";

    public override void OnBattleStart(CombatManager combat)
    {
        var drawPile = combat.PlayerHero.DeckState.DrawPile;
        var domainCards = drawPile.Where(c => c.Type == CardType.Domain).ToList();

        if (domainCards.Count == 0) return;

        var random = new Random();
        var picked = domainCards[random.Next(domainCards.Count)];

        // 从抽牌堆移除并加入手牌
        drawPile.Remove(picked);
        combat.PlayerHero.DeckState.AddToHand(picked);

        GD.Print($"[GoodDreamPillow] 战斗开始：从抽牌堆抽出领域牌「{picked.CardName}」");
    }

    public override void OnCardPlayed(CombatManager combat, OdysseyCards.Card.Card card, int actualCost)
    {
        if (card.Type != CardType.Domain) return;

        // 获取所有领域牌 ID（从注册表）
        var allCards = GameManager.Instance.GetAllCards();
        var domainCardDatas = allCards.Where(cd => cd.Type == CardType.Domain).ToList();

        if (domainCardDatas.Count == 0) return;

        var random = new Random();
        var pickedData = domainCardDatas[random.Next(domainCardDatas.Count)];
        var newCard = new OdysseyCards.Card.Card(pickedData);

        combat.PlayerHero.DeckState.AddToHand(newCard);

        GD.Print($"[GoodDreamPillow] 打出领域牌，印牌：「{newCard.CardName}」加入手牌");
    }
}
