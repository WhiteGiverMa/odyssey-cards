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
		// 溢出规则：先把敌方手牌全部设为已公开，确保情报点数走溢出分支。
		foreach (var unit in combat.EnemyUnits)
		{
			foreach (var hidden in unit.Body.Hand)
				hidden.IsRevealedByIntel = true;
		}
		int armor = combat.PlayerHero.CurrentArmor;
		int beforeHealth = combat.PlayerHero.CurrentHealth;
		Check(combat.PlayMinion(scout, 0), "武侦小组部署");
		Check(combat.PlayerHero.CurrentArmor == armor && combat.PlayerHero.CurrentHealth == beforeHealth,
			"情报溢出不再回血或转格挡");
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
		Check(!combat.MinionAttackHero(runner, hq) && !combat.PlayerHero.HasSmokeScreen && !runner._firstActionDone,
			"非法首攻不能提前发烟幕或消耗首次行动");
		Check(combat.MinionAttack(runner, smokeTarget) && combat.PlayerHero.HasSmokeScreen && runner._firstActionDone,
			"合法首次行动在支付后发烟幕");
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

		// 0.2.3 目标型部署：第232先锋营（伤害战吼）与第55轨道机动兵团（推进战吼）。
		// 卡牌资源由 .tres 侧并行落地；缺失时跳过，避免未合流前门禁误报。
		if (ResourceLoader.Exists("res://Resources/Cards/Minion_232ndVanguardBattalion.tres")
			&& ResourceLoader.Exists("res://Resources/Cards/Minion_55thOrbitalCorps.tres"))
		{
			var targetDummy = new Minion(GD.Load<CardData>("res://Resources/Cards/Minion_Slime.tres"), false);
			targetDummy.GainStats(0, 10);
			combat.Board.PlaceMinion(targetDummy, 1);
			var battalion = Card("Minion_232ndVanguardBattalion");
			int dummyHealth = targetDummy.CurrentHealth;
			Check(combat.HasAnyValidBattlecryTarget(battalion), "第232先锋营应有合法战吼目标");
			Check(combat.PlayMinion(battalion, 0, targetDummy), "第232先锋营目标型部署");
			Check(targetDummy.CurrentHealth == dummyHealth - 1, "第232先锋营战吼应对敌方目标造成1点伤害");

			var supportMover = new Minion(GD.Load<CardData>("res://Resources/Cards/Minion_Slime.tres"), true);
			combat.Board.PlaceMinion(supportMover, 1);
			var corps = Card("Minion_55thOrbitalCorps");
			Check(combat.PlayMinion(corps, combat.Board.GetEmptySlotIndex(isPlayerSide: true), supportMover),
				"第55轨道机动兵团目标型部署");
			Check(supportMover.BoardRow == BoardRow.Frontline, "第55兵团战吼应把支援线随从推至前线");

			// 无任何合法目标时，随从仍可裸拍上场，战吼自动跳过。
			ClearBoard();
			var lone = Card("Minion_55thOrbitalCorps");
			Check(!combat.HasAnyValidBattlecryTarget(lone), "空场时第55兵团应无合法战吼目标");
			Check(combat.PlayMinion(lone, 0), "无合法目标时仍应裸拍上场");
			ClearBoard();
		}

		Check(combat.PlaySpell(Card("Spell_Discover"), combat.PlayerHero) && combat.IsDiscovering,
			"发现牌应正常进入选牌");
		combat.CancelDiscover();
		Check(!combat.IsDiscovering, "发现取消后应恢复玩家回合");
		var hunter = new Minion(GD.Load<CardData>("res://Resources/Cards/Minion_LianshuScout.tres"), true);
		combat.Board.PlaceMinion(hunter, 0);
		foreach (var unit in combat.EnemyUnits)
		{
			foreach (var hidden in unit.Body.Hand)
				hidden.IsRevealedByIntel = true;
		}
		armor = combat.PlayerHero.CurrentArmor;
		beforeHealth = combat.PlayerHero.CurrentHealth;
		hunter.ApplyDevDamage(1000);
		combat.CheckDeaths();
		Check(combat.PlayerHero.CurrentArmor == armor && combat.PlayerHero.CurrentHealth == beforeHealth,
			"侦察犬亡语情报溢出同样不产生额外收益");

		string result = $"M1_RUNTIME_QA passed={checks}";
		GD.Print(result);
		return result;
	}
}
