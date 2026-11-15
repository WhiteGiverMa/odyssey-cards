using System;
using System.Linq;
using Godot;
using OdysseyCards.Card;
using OdysseyCards.Core;

namespace OdysseyCards.Combat;

/// <summary>DEBUG 网关调用的 M1/M2 运行时集成门禁。会改变当前测试战局。</summary>
internal static class FrontlineRuntimeQa
{
	public static string Run(CombatManager combat)
	{
		int checks = 0;
		void Check(bool condition, string message)
		{
			if (!condition)
				throw new InvalidOperationException($"M1_QA_FAILED: {message}");
			checks++;
		}
		Card.Card Card(string file)
		{
			var card = new Card.Card(GD.Load<CardData>($"res://Resources/Cards/{file}.tres"));
			combat.AddCardToHand(card);
			return card;
		}
		void ClearBoard()
		{
			foreach (var unit in combat.Board.GetAllMinions().ToList())
				combat.Board.RecallMinion(unit);
			combat.PlayerHero.SetMana(30, 30);
			combat.PlayerHero.Heal(combat.PlayerHero.MaxHealth);
		}

		ClearBoard();
		combat.PlayerHero.DeckState.MaxHandSize = 30;
		combat.PlayerHero.Weapon = new MagicWand();
		for (int i = 0; i < 3; i++)
			combat.Board.PlaceMinion(new Minion(GD.Load<CardData>("res://Resources/Cards/Minion_Slime.tres"), true), i);
		Check(combat.PlayMinion(Card("Minion_WhatTheDogDoing"), 3), "满支援线刀盾战吼应安全跳过复制");
		Check(combat.Board.GetPlayerMinions().Count == 4 && combat.Board.FrontlineOwner == FrontlineOwner.None, "复制不能溢入前线");

		ClearBoard();
		Check(combat.PlayMinion(Card("Minion_18thRegiment"), 0), "先锋部署");
		var regiment = combat.Board.GetMinionAt(0, true)!;
		Check(regiment.HasGuard && !regiment.HasTaunt && !combat.CanMoveMinionToFrontline(regiment), "守护替代嘲讽且无闪击部署冷却");
		ClearBoard();

		Check(combat.PlayMinion(Card("Minion_40BMainBattleTank"), 0), "闪击突击部署");
		var tank = combat.Board.GetMinionAt(0, true)!;
		tank.GainStats(0, 20);
		var enemy = new Minion(GD.Load<CardData>("res://Resources/Cards/Minion_40AMainBattleTank.tres"), false);
		enemy.GainStats(0, 30);
		combat.Board.PlaceMinion(enemy, 0);
		var eventCounts = new int[2];
		void Placed(Minion _, BoardPosition position) => eventCounts[0]++;
		void Moved(Minion _, BoardPosition from, BoardPosition to) => eventCounts[1]++;
		combat.Board.OnMinionPlaced += Placed;
		combat.Board.OnMinionMoved += Moved;
		int mana = combat.PlayerHero.CurrentMana;
		Check(combat.MinionAttack(tank, enemy), "一步突击应推进并攻击后排嘲讽");
		combat.Board.OnMinionPlaced -= Placed;
		combat.Board.OnMinionMoved -= Moved;
		Check(combat.PlayerHero.CurrentMana == mana - 2 * tank.ActionCost, "突击走打分段扣油");
		Check(tank.BoardRow == BoardRow.Frontline && eventCounts[0] == 0 && eventCounts[1] == 1, "移动不能重触发部署事件");
		Check(!combat.MinionAttack(tank, enemy), "默认攻击配额不能重复消费");

		ClearBoard();
		var scout = Card("Minion_DetectiveSquad");
		scout.IsRevealedByIntel = true;
		int armor = combat.PlayerHero.CurrentArmor;
		Check(combat.PlayMinion(scout, 0), "武侦小组部署");
		Check(combat.PlayerHero.CurrentArmor == armor + 1, "战吼情报对意图敌人转格挡");
		Check(combat.Board.GetMinionAt(0, true)!.IsRevealedByIntel, "公开实例部署后保留情报状态");
		ClearBoard();

		var blocker = new Minion(GD.Load<CardData>("res://Resources/Cards/Minion_40AMainBattleTank.tres"), false);
		blocker.GainStats(0, 30);
		combat.Board.PlaceMinion(blocker, 0);
		combat.Board.MoveMinionToFrontline(blocker);
		var hq = combat.GetDefaultEnemyTargetUnit()!.Body;
		hq.Heal(hq.MaxHealth);
		hq.GainArmor(30);
		var elbow = Card("Spell_ElbowStrike");
		mana = combat.PlayerHero.CurrentMana;
		Check(!combat.PlaySpell(elbow, hq) && combat.PlayerHero.CurrentMana == mana, "肘击被敌占前线阻挡且不扣费");
		Check(combat.PlaySpell(Card("Spell_Strike"), hq), "普通法术应跨前线");
		Check(!combat.IsDiscovering, "普通法术不得误入选牌阶段");
		Check(combat.SelectSmartEnemyAttackTarget(hq, 5) is Hero, "敌占前线时敌方近战可直达我方HQ");
		ClearBoard();

		var front = new Minion(GD.Load<CardData>("res://Resources/Cards/Minion_18thRegiment.tres"), true);
		combat.Board.PlaceMinion(front, 0);
		combat.Board.MoveMinionToFrontline(front);
		Check(ReferenceEquals(combat.SelectSmartEnemyAttackTarget(hq, 5), front), "道中近战意图必须命中玩家前线");
		Check(combat.HeroWeaponAttackMinion(front), "魔法棒可跨线治疗我方前线");
		front.ApplyDevDamage(1000);
		combat.CheckDeaths();
		Check(combat.Board.FrontlineOwner == FrontlineOwner.None && !combat.Board.Contains(front), "死亡扫描包含前线且释放控制权");
		ClearBoard();
		var preemptiveTarget = new Minion(GD.Load<CardData>("res://Resources/Cards/Minion_40AMainBattleTank.tres"), false);
		preemptiveTarget.GainStats(0, 40);
		combat.Board.PlaceMinion(preemptiveTarget, 0);
		combat.PlayerHero.AddDomain("preemptive_strike", null);
		var strike = Card("Spell_Strike");
		int expectedDamage = (int)(3 * (1.0f + combat.PlayerHero.Hand.Count * 0.1f));
		int health = preemptiveTarget.CurrentHealth;
		Check(combat.PlaySpell(strike, preemptiveTarget) && preemptiveTarget.CurrentHealth == health - expectedDamage,
			"先发制人必须实际传入放大后的伤害");
		combat.PlayerHero.RemoveDomain("preemptive_strike");
		ClearBoard();

		var smokeRunner = Card("Minion_RayanGe");
		Check(combat.PlayMinion(smokeRunner, 0), "拉烟哥部署");
		var runner = combat.Board.GetMinionAt(0, true)!;
		var smokeTarget = new Minion(GD.Load<CardData>("res://Resources/Cards/Minion_40AMainBattleTank.tres"), false);
		smokeTarget.GainStats(0, 30);
		combat.Board.PlaceMinion(smokeTarget, 0);
		smokeTarget.AddStatusEffect(new StatusEffect(StatusEffect.SmokescreenId, 1, TickTiming.EnemyTurnEnd));
		// 嘲讽不能获得烟幕，改用 HQ 烟幕作为非法目标验证首攻事务。
		hq.AddStatusEffect(new StatusEffect(StatusEffect.SmokescreenId, 1, TickTiming.EnemyTurnEnd));
		Check(!combat.MinionAttackHero(runner, hq) && !combat.PlayerHero.HasSmokeScreen && !runner._firstAttackDone,
			"非法首攻不能提前发烟幕或消耗首攻");
		Check(combat.MinionAttack(runner, smokeTarget) && combat.PlayerHero.HasSmokeScreen && runner._firstAttackDone,
			"合法首攻在支付后发烟幕");
		hq.RemoveStatusEffect(StatusEffect.SmokescreenId);
		combat.PlayerHero.RemoveStatusEffect(StatusEffect.SmokescreenId);
		ClearBoard();

		var enemyVanguard = new Minion(GD.Load<CardData>("res://Resources/Cards/Minion_Slime.tres"), false);
		combat.Board.PlaceMinion(enemyVanguard, 0);
		var enemySupport = new Minion(GD.Load<CardData>("res://Resources/Cards/Minion_CentipedeSiege.tres"), false);
		combat.Board.PlaceMinion(enemySupport, 1);
		combat.PlayerHero.GainArmor(100);
		var vanguardHits = new int[1];
		void CountVanguardHit(Hero _, IDamageSource source, int damage)
		{
			if (ReferenceEquals(source, enemyVanguard))
				vanguardHits[0]++;
		}
		combat.PlayerHero.OnAttacked += CountVanguardHit;
		combat.EndPlayerTurn();
		combat.PlayerHero.OnAttacked -= CountVanguardHit;
		Check(enemyVanguard.BoardRow == BoardRow.Frontline && vanguardHits[0] == 0,
			"敌方先锋优先推进且推进回合不再攻击");
		Check(enemySupport.BoardRow == BoardRow.EnemySupport, "敌方支援留守后排");
		ClearBoard();

		Check(combat.PlayDomain(Card("Domain_Jiehuafa")), "接化发领域部署");
		var guardedFront = new Minion(GD.Load<CardData>("res://Resources/Cards/Minion_40AMainBattleTank.tres"), true);
		guardedFront.GainArmor(100);
		combat.Board.PlaceMinion(guardedFront, 0);
		combat.Board.MoveMinionToFrontline(guardedFront);
		combat.Board.PlaceMinion(new Minion(GD.Load<CardData>("res://Resources/Cards/Minion_Slime.tres"), false), 0);
		int ukemiBefore = combat.PlayerHero.Hand.Count(card => card.Id == "spell_ukemi");
		combat.EndPlayerTurn();
		Check(combat.PlayerHero.Hand.Count(card => card.Id == "spell_ukemi") == ukemiBefore + 1,
			"接化发应响应针对前线随从的完全格挡攻击");
		ClearBoard();
		Check(combat.PlaySpell(Card("Spell_Discover"), combat.PlayerHero) && combat.IsDiscovering,
			"发现牌应执行情报后正常进入选牌");
		combat.CancelDiscover();
		Check(!combat.IsDiscovering, "发现取消后应恢复玩家回合");
		var hunter = new Minion(GD.Load<CardData>("res://Resources/Cards/Minion_LianshuScout.tres"), true);
		combat.Board.PlaceMinion(hunter, 0);
		armor = combat.PlayerHero.CurrentArmor;
		hunter.ApplyDevDamage(1000);
		combat.CheckDeaths();
		Check(combat.PlayerHero.CurrentArmor == armor + 1, "侦察犬亡语情报在离场后仍归属于原阵营");

		string result = $"M1_RUNTIME_QA passed={checks}";
		GD.Print(result);
		return result;
	}
}
