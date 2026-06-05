#nullable disable
using System.Collections.Generic;
using System.Linq;
using Godot;
using OdysseyCards.Card;
using OdysseyCards.Core;

namespace OdysseyCards.Combat;

/// <summary>
/// 运行时 QA 场景集合。
/// 保留 MCP/DevConsole 可驱动的集成验证价值，但不再污染 CombatManager 的生产职责。
/// </summary>
internal static class CombatRuntimeQa
{
    public static string RunBaitTacticsQa(CombatManager cm)
    {
        var baitData = GD.Load<CardData>("res://Resources/Cards/Spell_BaitTactics.tres");
        var playerMinionData = GD.Load<CardData>("res://Resources/Cards/Minion_18thRegiment.tres");
        var enemyMinionData = GD.Load<CardData>("res://Resources/Cards/Minion_Slime.tres");

        if (baitData == null || playerMinionData == null || enemyMinionData == null)
            return "诱饵战术QA失败：无法加载所需卡牌资源";

        cm.PlayerHero.GainMana(20);
        var enemyHero = cm.EnemyUnits[0].Body;
        int initialDefense = enemyHero.Defense;

        var friendlyTarget = new Minion(playerMinionData, isPlayerSide: true);
        var enemyAttacker = new Minion(enemyMinionData, isPlayerSide: false);
        var friendlySpell = new Card.Card(baitData);
        cm.AddCardToHand(friendlySpell);
        bool friendlySpellPlayed = cm.PlaySpell(friendlySpell, friendlyTarget);
        bool friendlyBuffApplied = friendlyTarget.HasAmbush && friendlyTarget.HasImpact && friendlyTarget.HasBaitTacticsOnAttacked;
        cm.ResolveMinionCombat(enemyAttacker, friendlyTarget);
        bool friendlyTriggerWorked = enemyHero.Defense == initialDefense - 1;

        var enemyTarget = new Minion(enemyMinionData, isPlayerSide: false);
        var playerAttacker = new Minion(playerMinionData, isPlayerSide: true);
        var enemySpell = new Card.Card(baitData);
        cm.AddCardToHand(enemySpell);
        bool enemySpellPlayed = cm.PlaySpell(enemySpell, enemyTarget);
        bool enemyBuffApplied = enemyTarget.HasAmbush && enemyTarget.HasImpact && enemyTarget.HasBaitTacticsOnAttacked;
        cm.ResolveMinionCombat(playerAttacker, enemyTarget);
        bool enemyTriggerWorked = enemyHero.Defense == initialDefense - 2;

        bool passed = friendlySpellPlayed
            && friendlyBuffApplied
            && friendlyTriggerWorked
            && enemySpellPlayed
            && enemyBuffApplied
            && enemyTriggerWorked;

        string result = passed
            ? $"诱饵战术QA通过：友方目标触发、敌方目标触发，玩家敌方的英雄防御 {initialDefense}->{enemyHero.Defense}"
            : $"诱饵战术QA失败：friendlySpell={friendlySpellPlayed}, friendlyBuff={friendlyBuffApplied}, friendlyTrigger={friendlyTriggerWorked}, enemySpell={enemySpellPlayed}, enemyBuff={enemyBuffApplied}, enemyTrigger={enemyTriggerWorked}, defense={enemyHero.Defense}";
        GD.Print($"[CombatRuntimeQa] {result}");
        return result;
    }

    public static string RunNewCardsQa(CombatManager cm)
    {
        var nanoData = GD.Load<CardData>("res://Resources/Cards/Spell_NanoCorpseArt.tres");
        var idolData = GD.Load<CardData>("res://Resources/Cards/Domain_IdolTwilight.tres");
        var moonData = GD.Load<CardData>("res://Resources/Cards/Spell_MoonFishing.tres");
        var scoutData = GD.Load<CardData>("res://Resources/Cards/Minion_LianshuScout.tres");
        var slimeData = GD.Load<CardData>("res://Resources/Cards/Minion_Slime.tres");
        var alertData = GD.Load<CardData>("res://Resources/Cards/Spell_Alert.tres");
        var strikeData = GD.Load<CardData>("res://Resources/Cards/Spell_Strike.tres");
        var assaultData = GD.Load<CardData>("res://Resources/Cards/Spell_Assault.tres");
        var regimentData = GD.Load<CardData>("res://Resources/Cards/Minion_18thRegiment.tres");

        if (nanoData == null || idolData == null || moonData == null || scoutData == null || slimeData == null
            || alertData == null || strikeData == null || assaultData == null || regimentData == null)
            return "新增卡牌QA失败：资源加载不完整";

        cm.PlayerHero.GainMana(50);
        cm.PlayerHero.AddToDrawPileBottom(new Card.Card(alertData));

        var nanoTarget = new Minion(scoutData, isPlayerSide: true);
        var nanoCard = new Card.Card(nanoData);
        cm.AddCardToHand(nanoCard);
        bool nanoPlayed = cm.PlaySpell(nanoCard, nanoTarget);
        bool nanoReplaced = nanoTarget.HasDeathrattle
            && nanoTarget.DeathrattleEffects.Count == 1
            && nanoTarget.DeathrattleEffects[0].EffectType == CardEffectType.DrawCards
            && nanoTarget.DeathrattleEffects[0].Value == 1;

        var handMinionCard = new Card.Card(regimentData);
        var drawMinionCard = new Card.Card(scoutData);
        var discardMinionCard = new Card.Card(scoutData);
        cm.AddCardToHand(handMinionCard);
        cm.PlayerHero.AddToDrawPileBottom(drawMinionCard);
        cm.PlayerHero.AddToDiscardPile(discardMinionCard);
        var boardMinion = new Minion(regimentData, isPlayerSide: true);
        cm.Board.PlaceMinion(boardMinion, cm.Board.GetEmptySlotIndex(isPlayerSide: true));

        var idolCard = new Card.Card(idolData);
        cm.AddCardToHand(idolCard);
        bool idolPlayed = cm.PlayDomain(idolCard);
        bool idolGrantedZones = handMinionCard.IdolTwilightOnAttackedStacks == 1
            && drawMinionCard.IdolTwilightOnAttackedStacks == 1
            && discardMinionCard.IdolTwilightOnAttackedStacks == 1
            && boardMinion.IdolTwilightOnAttackedStacks == 1;
        int beforeAttack = boardMinion.Attack;
        int beforeHealth = boardMinion.CurrentHealth;
        cm.ResolveMinionCombat(new Minion(slimeData, isPlayerSide: false), boardMinion);
        bool idolTriggered = boardMinion.Attack == beforeAttack + 1
            && boardMinion.CurrentHealth == beforeHealth - slimeData.Attack + 1;

        var discardA = new Card.Card(strikeData);
        var discardB = new Card.Card(assaultData);
        var discardC = new Card.Card(alertData);
        cm.PlayerHero.AddToDiscardPile(discardA);
        cm.PlayerHero.AddToDiscardPile(discardB);
        cm.PlayerHero.AddToDiscardPile(discardC);
        int discardBeforeMoon = cm.PlayerHero.DeckState.DiscardPile.Count;
        var moonCard = new Card.Card(moonData);
        cm.AddCardToHand(moonCard);
        bool moonPlayed = cm.PlaySpell(moonCard, cm.PlayerHero);
        var moonOptions = cm.DiscoverRuntimeOptions?.Take(2).ToList() ?? new List<Card.Card>();
        while (cm.PlayerHero.Hand.Count > 8)
        {
            cm.PlayerHero.RemoveFromHand(cm.PlayerHero.Hand[0]);
        }
        cm.ConfirmDiscoverCards(moonOptions);
        bool moonMovedCards = moonOptions.Count == 2
            && moonOptions.All(c => cm.PlayerHero.Hand.Contains(c))
            && cm.PlayerHero.DeckState.DiscardPile.Count == discardBeforeMoon - 2 + 1;

        bool passed = nanoPlayed && nanoReplaced && idolPlayed && idolGrantedZones && idolTriggered && moonPlayed && moonMovedCards;
        string result = passed
            ? "新增卡牌QA通过：纳米散尸术替换亡语并抽牌；偶像的黄昏授予跨区域触发且被攻击后+1/+1；捞月从弃牌堆2选加入手牌"
            : $"新增卡牌QA失败：nanoPlayed={nanoPlayed}, nanoReplaced={nanoReplaced}, idolPlayed={idolPlayed}, idolGrantedZones={idolGrantedZones}, idolTriggered={idolTriggered}, moonPlayed={moonPlayed}, moonOptions={moonOptions.Count}, moonMovedCards={moonMovedCards}";
        GD.Print($"[CombatRuntimeQa] {result}");
        return result;
    }
}
