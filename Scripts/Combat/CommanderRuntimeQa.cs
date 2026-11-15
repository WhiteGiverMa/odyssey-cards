#if DEBUG
using System;
using System.Linq;
using Godot;
using OdysseyCards.AI;
using OdysseyCards.Card;
using OdysseyCards.Core;

namespace OdysseyCards.Combat;

/// <summary>
/// M3 指挥官运行时门禁：真实资源 + 真实 CombatManager，多回合驱动 DeckDrivenCommander。
/// 仅 DEBUG 构建使用，会改变当前战局；由 tests/godot/CommanderQa.tscn 启动。
/// </summary>
internal static class CommanderRuntimeQa
{
	public static string Run(CombatManager combat, string expectedHeroId = "rie")
	{
		int checks = 0;
		void Check(bool condition, string message)
		{
			if (!condition)
				throw new InvalidOperationException($"M3_QA_FAILED: {message}");
			checks++;
		}
		Card.Card Load(string file) => new(GD.Load<CardData>($"res://Resources/Cards/{file}.tres"));

		// 1. 指挥官身体、卡组、武器、技能与开场领域。
		Check(combat.EnemyUnits.Count == 1, "指挥官战应只有一个敌方 actor");
		var unit = combat.EnemyUnits[0];
		Check(unit.Controller is IDeckDrivenCommander, "敌方控制器应为 IDeckDrivenCommander");
		var commander = (IDeckDrivenCommander)unit.Controller;
		Check(commander.HeroId == expectedHeroId, $"FightOverride 应生成 {expectedHeroId} 宿敌");
		Check(unit.Body.MaxHealth == 38, "精英指挥官生命补正应为 38");
		Check(unit.Body.Hand.Count == 5, "指挥官起手应为 5 张");
		Check(unit.Body.MaxMana == 3, "指挥官首回合前应与玩家同起点（3/3）");
		Check(unit.Body.HeroPower != null, "指挥官应配备英雄技能");
		bool correctWeapon = expectedHeroId switch
		{
			"ayame" => unit.Body.Weapon is MagicWand,
			"sokou" => unit.Body.Weapon is RayPistol,
			_ => unit.Body.Weapon is SvdsM338,
		};
		Check(correctWeapon, $"{expectedHeroId} 指挥官应配备专属武器");
		string openingDomain = expectedHeroId switch
		{
			"ayame" => "idol_twilight",
			"sokou" => "jiehuafa",
			_ => "infinite_fire",
		};
		Check(unit.Body.HasDomain(openingDomain), $"{expectedHeroId} 指挥官开场应展开 {openingDomain}");
		Check(unit.Body.Hand.All(card => !card.IsRevealedByIntel), "指挥官起手应全部为暗牌");
		int deckTotal = unit.Body.Hand.Count + unit.Body.DeckState.DrawPile.Count + unit.Body.DeckState.DiscardPile.Count;
		Check(deckTotal == 20, $"主题卡组应为 20 张（实际 {deckTotal}）");

		// 2. 宿敌池与 /fight 注册。
		var rivals = EnemyRegistry.GetRivalCommanderIds(expectedHeroId);
		Check(!rivals.Contains(expectedHeroId, StringComparer.OrdinalIgnoreCase), "宿敌池应排除当前指挥官自身");
		Check(rivals.Count == 2, "宿敌池应剩余两位少女指挥官");
		foreach (string id in new[] { "commander_ayame", "commander_rie", "commander_sokou" })
		{
			var created = EnemyRegistry.Create(id);
			Check(created.Count == 1 && created[0] is DeckDrivenCommander, $"EnemyRegistry 应注册 {id}");
		}

		// 3. 情报：玩家真实打出「检索」，从左往右翻开指挥官手牌。
		var retrieve = Load("Spell_Retrieve");
		combat.AddCardToHand(retrieve);
		combat.PlayerHero.SetMana(30, 30);
		Check(combat.PlaySpell(retrieve, combat.PlayerHero), "玩家应能施放检索");
		Check(unit.Body.Hand.Count >= 2, "指挥官手牌应足够验证情报");
		Check(unit.Body.Hand[0].IsRevealedByIntel && unit.Body.Hand[1].IsRevealedByIntel,
			"情报应从左往右翻开前两张暗牌");

		// 4. 自然多回合：法力增长、抽牌、抢线、铺场、攻击与武器/英雄技能。
		combat.PlayerHero.Core.InitializeHealth(1000, 1000);
		int startMaxMana = unit.Body.MaxMana;
		int maxBoard = combat.Board.GetEnemyMinions().Count;
		bool everSpentMana = false;
		bool everFrontline = combat.Board.FrontlineOwner == FrontlineOwner.Enemy;
		bool everDamagedPlayer = combat.PlayerHero.CurrentHealth < 1000;
		for (int turn = 0; turn < 6 && !combat.State.IsGameOver; turn++)
		{
			combat.EndPlayerTurn();
			maxBoard = Math.Max(maxBoard, combat.Board.GetEnemyMinions().Count);
			everSpentMana |= unit.Body.CurrentMana < unit.Body.MaxMana;
			everFrontline |= combat.Board.FrontlineOwner == FrontlineOwner.Enemy;
			everDamagedPlayer |= combat.PlayerHero.CurrentHealth < 1000;
		}
		Check(unit.Body.MaxMana >= startMaxMana + 6, $"指挥官法力应自然增长（{startMaxMana}→{unit.Body.MaxMana}）");
		Check(maxBoard > 0, "指挥官应至少部署或召唤一个随从");
		Check(everSpentMana, "指挥官应实际消耗法力");
		Check(everFrontline, "指挥官应抢占中央前线");
		Check(everDamagedPlayer, "指挥官应使用随从/武器/法术对玩家造成伤害");
		Check(unit.Body.Hand.Count <= unit.Body.DeckState.MaxHandSize, "指挥官手牌不应超过上限");

		// 5. 交互牌全卡池秒选：发现/刀盾危机/捞月/吸血狗，AI 必须同步完成且不死锁。
		unit.Body.DiscardHand();
		unit.Body.Core.SetHealth(38, 38);
		RefillCommanderPile(unit.Body, GameManager.Instance.GetAllCards()[0]);
		foreach (string file in new[] { "Spell_Discover", "Spell_BladeCrisis", "Spell_MoonFishing", "Spell_BloodDogsHandFill", "Spell_Retrieve" })
			unit.Body.Core.AddToHand(Load(file));
		unit.Body.SetMana(30, 30);
		int handBefore = unit.Body.Hand.Count;
		int boardBefore = combat.Board.GetEnemyMinions().Count;
		combat.EndPlayerTurn();
		Check(combat.State.Phase == CombatPhase.PlayerTurn, "交互牌 AI 回合应正常结束");
		Check(unit.Body.Hand.Count != handBefore || combat.Board.GetEnemyMinions().Count != boardBefore
			|| unit.Body.DeckState.DiscardPile.Count > 0, "AI 应至少执行一个交互动作");

		// 6. 全卡池通吃：逐张塞给指挥官，AI 决策 + 效果分发不得抛异常或卡死。
		combat.PlayerHero.Core.InitializeHealth(100000, 100000);
		var allCards = GameManager.Instance.GetAllCards();
		Check(allCards.Count == 61, $"全卡池应为 61 张（实际 {allCards.Count}）");
		foreach (var data in allCards)
		{
			try
			{
				unit.Body.DiscardHand();
				unit.Body.Core.SetHealth(38, 38);
				RefillCommanderPile(unit.Body, allCards[0]);
				unit.Body.Core.AddToHand(new Card.Card(data));
				unit.Body.SetMana(30, 30);
				combat.EndPlayerTurn();
				if (combat.State.IsGameOver)
					throw new InvalidOperationException("战局意外结束");
			}
			catch (Exception exception)
			{
				throw new InvalidOperationException($"卡牌 {data.Id} 结算失败：{exception.Message}", exception);
			}
		}
		Check(combat.State.Phase == CombatPhase.PlayerTurn, "全卡池逐张结算后应回到玩家回合");

		static void RefillCommanderPile(Hero body, CardData filler)
		{
			if (body.DeckState.DrawPile.Count >= 40)
				return;
			foreach (var card in body.DeckState.DiscardPile.ToArray())
				body.DeckState.DrawPile.Add(card);
			body.DeckState.DiscardPile.Clear();
			while (body.DeckState.DrawPile.Count < 40)
				body.Core.AddToDrawPileBottom(new Card.Card(filler));
			body.Core.ResetFatigue();
		}

		GD.Print($"[CommanderRuntimeQa] 通过 {checks} 项断言：指挥官 {commander.HeroId}，"
			+ $"生命 {unit.Body.CurrentHealth}/{unit.Body.MaxHealth}，法力 {unit.Body.CurrentMana}/{unit.Body.MaxMana}，"
			+ $"手牌 {unit.Body.Hand.Count}，牌堆 {unit.Body.DeckState.DrawPile.Count}，弃牌 {unit.Body.DeckState.DiscardPile.Count}，"
			+ $"敌方随从 {combat.Board.GetEnemyMinions().Count}，前线 {combat.Board.FrontlineOwner}");
		return $"M3_COMMANDER_QA_OK checks={checks}";
	}
}

#endif
