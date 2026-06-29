#nullable disable
using System.Collections.Generic;
using System.Linq;
using Godot;
using OdysseyCards.Card;
using OdysseyCards.Core;

namespace OdysseyCards.Combat;

/// <summary>
/// 运行时 QA 场景集合。
/// 保留 MCP/ChatScreen 可驱动的集成验证价值，但不再污染 CombatManager 的生产职责。
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
		var enemyHero = cm.GetDefaultEnemyTargetUnit()?.Body;
		if (enemyHero == null)
			return "诱饵战术QA失败：没有存活的敌方英雄";
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
		var engineData = GD.Load<CardData>("res://Resources/Cards/Spell_Engine.tres");
		var retrieveData = GD.Load<CardData>("res://Resources/Cards/Spell_Retrieve.tres");
		var responseData = GD.Load<CardData>("res://Resources/Cards/Spell_Response.tres");
		var adrenalineData = GD.Load<CardData>("res://Resources/Cards/Spell_Adrenaline.tres");
		var heavyStrikeData = GD.Load<CardData>("res://Resources/Cards/Spell_HeavyStrike.tres");
		var shockData = GD.Load<CardData>("res://Resources/Cards/Spell_Shock.tres");
		var tankData = GD.Load<CardData>("res://Resources/Cards/Minion_40MainBattleTank.tres");
		var centurionData = GD.Load<CardData>("res://Resources/Cards/Minion_Centurion.tres");

		if (nanoData == null || idolData == null || moonData == null || scoutData == null || slimeData == null
			|| alertData == null || strikeData == null || assaultData == null || regimentData == null
			|| engineData == null || retrieveData == null || responseData == null || adrenalineData == null
			|| heavyStrikeData == null || shockData == null || tankData == null || centurionData == null)
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
		// 方案B：领域挂在英雄上，不一次性给卡牌打标记。
		// 手牌/牌堆中的卡牌不再有 IdolTwilightOnAttackedStacks 字段；
		// 棋盘随从通过 OnDomainDeployed 获得 HasIdolTwilightBuff 显示标记。
		bool idolDomainRegistered = cm.PlayerHero.HasDomain("idol_twilight");
		bool idolBoardMarked = boardMinion.HasIdolTwilightBuff;
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

		bool recentBatchPassed = RunRecentCardsQa(cm, scoutData, slimeData, alertData, engineData, retrieveData,
			responseData, adrenalineData, heavyStrikeData, shockData, tankData, centurionData, out var recentBatchSummary);

		bool passed = nanoPlayed && nanoReplaced && idolPlayed && idolDomainRegistered && idolBoardMarked && idolTriggered && moonPlayed && moonMovedCards && recentBatchPassed;
		string result = passed
			? $"新增卡牌QA通过：纳米散尸术替换亡语并抽牌；偶像的黄昏作为持续领域挂在英雄上，被攻击后+1/+1；捞月从弃牌堆2选加入手牌；{recentBatchSummary}"
			: $"新增卡牌QA失败：nanoPlayed={nanoPlayed}, nanoReplaced={nanoReplaced}, idolPlayed={idolPlayed}, idolDomainRegistered={idolDomainRegistered}, idolBoardMarked={idolBoardMarked}, idolTriggered={idolTriggered}, moonPlayed={moonPlayed}, moonOptions={moonOptions.Count}, moonMovedCards={moonMovedCards}, recentBatch={recentBatchPassed} ({recentBatchSummary})";
		GD.Print($"[CombatRuntimeQa] {result}");
		return result;
	}

	private static bool RunRecentCardsQa(CombatManager cm, CardData scoutData, CardData slimeData, CardData alertData,
		CardData engineData, CardData retrieveData, CardData responseData, CardData adrenalineData,
		CardData heavyStrikeData, CardData shockData, CardData tankData, CardData centurionData, out string summary)
	{
		int previousMaxHandSize = cm.PlayerHero.DeckState.MaxHandSize;
		cm.PlayerHero.DeckState.MaxHandSize = 50;
		cm.PlayerHero.GainMana(100);

		bool registryContainsRecentCards = new[]
		{
			engineData, retrieveData, responseData, adrenalineData, heavyStrikeData, shockData, tankData, centurionData
		}.All(data => GameManager.Instance.GetAllCards().Any(card => card.Id == data.Id));

		int handBeforeDrawSpells = cm.PlayerHero.Hand.Count;
		AddDrawPileCards(cm, alertData, 8);
		var engineCard = new Card.Card(engineData);
		cm.AddCardToHand(engineCard);
		bool enginePlayed = cm.PlaySpell(engineCard, cm.PlayerHero);
		bool engineDrewTwo = cm.PlayerHero.Hand.Count == handBeforeDrawSpells + 2;
		bool engineRecycled = cm.PlayerHero.DeckState.DrawPile.Contains(engineCard);

		int handBeforeRetrieve = cm.PlayerHero.Hand.Count;
		AddDrawPileCards(cm, alertData, 3);
		var retrieveCard = new Card.Card(retrieveData);
		cm.AddCardToHand(retrieveCard);
		bool retrievePlayed = cm.PlaySpell(retrieveCard, cm.PlayerHero);
		bool retrieveDrewThree = cm.PlayerHero.Hand.Count == handBeforeRetrieve + 3;

		var supportMinionA = new Minion(scoutData, isPlayerSide: true);
		var supportMinionB = new Minion(scoutData, isPlayerSide: true);
		cm.Board.PlaceMinion(supportMinionA, cm.Board.GetEmptySlotIndex(isPlayerSide: true));
		cm.Board.PlaceMinion(supportMinionB, cm.Board.GetEmptySlotIndex(isPlayerSide: true));
		int responseDrawCount = cm.Board.GetPlayerMinions().Count + 1;
		int handBeforeResponse = cm.PlayerHero.Hand.Count;
		AddDrawPileCards(cm, alertData, 3);
		var responseCard = new Card.Card(responseData);
		cm.AddCardToHand(responseCard);
		bool responsePlayed = cm.PlaySpell(responseCard, cm.PlayerHero);
		bool responseDrewByMinions = cm.PlayerHero.Hand.Count == handBeforeResponse + responseDrawCount;

		int maxManaBeforeAdrenaline = cm.PlayerHero.MaxMana;
		int manaBeforeAdrenaline = cm.PlayerHero.CurrentMana;
		int handBeforeAdrenaline = cm.PlayerHero.Hand.Count;
		AddDrawPileCards(cm, alertData, 2);
		var adrenalineCard = new Card.Card(adrenalineData);
		cm.AddCardToHand(adrenalineCard);
		bool adrenalinePlayed = cm.PlaySpell(adrenalineCard, cm.PlayerHero);
		bool adrenalineDrewTwo = cm.PlayerHero.Hand.Count == handBeforeAdrenaline + 2;
		bool adrenalineGainedSlotAndMana = cm.PlayerHero.MaxMana == maxManaBeforeAdrenaline + 1
			&& cm.PlayerHero.CurrentMana == manaBeforeAdrenaline + 1;

		var enemyShockTarget = new Minion(slimeData, isPlayerSide: false);
		var shockCard = new Card.Card(shockData);
		cm.AddCardToHand(shockCard);
		AddDrawPileCards(cm, alertData, 1);
		bool shockPlayed = cm.PlaySpell(shockCard, enemyShockTarget);
		bool shockAppliedIncapacitated = enemyShockTarget.StatusEffects.TryGetValue(StatusEffect.IncapacitatedId, out var shockEffect)
			&& shockEffect.Stacks == 2
			&& shockEffect.TickOn == TickTiming.EnemyTurnEnd;
		bool shockRecycled = cm.PlayerHero.DeckState.DrawPile.Contains(shockCard);
		enemyShockTarget.TickStatusEffects(TickTiming.PlayerTurnEnd);
		bool shockDoesNotDecayOnPlayerEnd = enemyShockTarget.StatusEffects[StatusEffect.IncapacitatedId].Stacks == 2;
		enemyShockTarget.TickStatusEffects(TickTiming.EnemyTurnEnd);
		bool shockDecaysOnOwnerEnd = enemyShockTarget.StatusEffects[StatusEffect.IncapacitatedId].Stacks == 1;

		var heavyTarget = new Minion(slimeData, isPlayerSide: false);
		var heavyStrikeCard = new Card.Card(heavyStrikeData);
		cm.AddCardToHand(heavyStrikeCard);
		bool heavyStrikePlayed = cm.PlaySpell(heavyStrikeCard, heavyTarget);
		bool heavyStrikeAppliedStatuses = heavyTarget.CurrentHealth == heavyTarget.MaxHealth - 6
			&& heavyTarget.StatusEffects.TryGetValue("vulnerable", out var vulnerableEffect)
			&& vulnerableEffect.Stacks == 3
			&& heavyTarget.StatusEffects.TryGetValue(StatusEffect.IncapacitatedId, out var heavyIncapacitated)
			&& heavyIncapacitated.Stacks == 3;

		var incapacitatedDefender = new Minion(slimeData, isPlayerSide: false);
		incapacitatedDefender.AddStatusEffect(new StatusEffect(StatusEffect.IncapacitatedId, 1, TickTiming.EnemyTurnEnd));
		var healthyAttacker = new Minion(scoutData, isPlayerSide: true);
		int attackerHealthBefore = healthyAttacker.CurrentHealth;
		cm.ResolveMinionCombat(healthyAttacker, incapacitatedDefender);
		bool incapacitatedCannotCounter = healthyAttacker.CurrentHealth == attackerHealthBefore;
		bool incapacitatedCannotAttack = !cm.ExecuteEnemyMinionSmartAttack(incapacitatedDefender);
		bool heroIncapacitatedStopsWeapon = ApplyHeroIncapacitatedWeaponQa(cm);

		int handBeforeTank = cm.PlayerHero.Hand.Count;
		AddDrawPileCards(cm, alertData, 2);
		var tankCard = new Card.Card(tankData);
		cm.AddCardToHand(tankCard);
		bool tankPlayed = cm.PlayMinion(tankCard, cm.Board.GetEmptySlotIndex(isPlayerSide: true));
		bool tankBattlecryDrewTwo = cm.PlayerHero.Hand.Count == handBeforeTank + 2;

		var centurionCard = new Card.Card(centurionData);
		cm.AddCardToHand(centurionCard);
		int heroArmorBeforeCenturion = cm.PlayerHero.CurrentArmor;
		int supportArmorBeforeCenturion = supportMinionA.CurrentArmor;
		bool armorEventFired = false;
		cm.PlayerHero.OnArmorGained += amount => armorEventFired = amount == 6 || armorEventFired;
		bool centurionPlayed = cm.PlayMinion(centurionCard, cm.Board.GetEmptySlotIndex(isPlayerSide: true));
		bool centurionBattlecryArmor = supportMinionA.CurrentArmor == supportArmorBeforeCenturion + 3
			&& cm.PlayerHero.CurrentArmor == heroArmorBeforeCenturion + 6
			&& armorEventFired;

		cm.PlayerHero.DeckState.MaxHandSize = previousMaxHandSize;

		bool passed = registryContainsRecentCards && enginePlayed && engineDrewTwo && engineRecycled
			&& retrievePlayed && retrieveDrewThree && responsePlayed && responseDrewByMinions
			&& adrenalinePlayed && adrenalineDrewTwo && adrenalineGainedSlotAndMana
			&& shockPlayed && shockAppliedIncapacitated && shockRecycled && shockDoesNotDecayOnPlayerEnd && shockDecaysOnOwnerEnd
			&& heavyStrikePlayed && heavyStrikeAppliedStatuses && incapacitatedCannotCounter && incapacitatedCannotAttack && heroIncapacitatedStopsWeapon
			&& tankPlayed && tankBattlecryDrewTwo && centurionPlayed && centurionBattlecryArmor;

		summary = passed
			? "引擎/检索/响应/肾上腺素/沉重打击/震慑/40主战坦克/百机长QA通过，失能攻击限制和格挡事件通过"
			: $"recent registry={registryContainsRecentCards}, engine={enginePlayed}/{engineDrewTwo}/{engineRecycled}, retrieve={retrievePlayed}/{retrieveDrewThree}, response={responsePlayed}/{responseDrewByMinions}, adrenaline={adrenalinePlayed}/{adrenalineDrewTwo}/{adrenalineGainedSlotAndMana}, shock={shockPlayed}/{shockAppliedIncapacitated}/{shockRecycled}/{shockDoesNotDecayOnPlayerEnd}/{shockDecaysOnOwnerEnd}, heavy={heavyStrikePlayed}/{heavyStrikeAppliedStatuses}, incapacitated={incapacitatedCannotCounter}/{incapacitatedCannotAttack}/{heroIncapacitatedStopsWeapon}, tank={tankPlayed}/{tankBattlecryDrewTwo}, centurion={centurionPlayed}/{centurionBattlecryArmor}";
		return passed;
	}

	private static void AddDrawPileCards(CombatManager cm, CardData cardData, int count)
	{
		for (int i = 0; i < count; i++)
			cm.PlayerHero.AddToDrawPileBottom(new Card.Card(cardData));
	}

	private static bool ApplyHeroIncapacitatedWeaponQa(CombatManager cm)
	{
		var enemyHero = cm.GetDefaultEnemyTargetUnit()?.Body;
		if (enemyHero == null)
			return false;

		enemyHero.AddStatusEffect(new StatusEffect(StatusEffect.IncapacitatedId, 1, TickTiming.EnemyTurnEnd));
		return enemyHero.IsIncapacitated && !enemyHero.CanWeaponAttack();
	}
}
