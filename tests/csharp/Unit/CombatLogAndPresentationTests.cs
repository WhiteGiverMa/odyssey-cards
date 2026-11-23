using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Godot;
using OdysseyCards.AI;
using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Combat;
using OdysseyCards.Core;
using OdysseyCards.UI;
using Xunit;
using RuntimeCard = OdysseyCards.Card.Card;

namespace OdysseyCards.Tests.Unit;

/// <summary>
/// 战斗日志 / 亮牌表现 / 指挥官回合循环 / Emoji 对照模式的纯 C# 门禁。
/// 不实例化 Godot Node 或 Resource：CombatManager 驱动的整场战斗由游戏内 QA 覆盖，
/// 这里锁定可离线验证的服务契约、快照隔离与本地化数据卫生。
/// </summary>
public class CombatLogAndPresentationTests
{
	// ===== 测试工具 =====

	private static string Root()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null && !File.Exists(Path.Combine(directory.FullName, "project.godot")))
		{
			directory = directory.Parent;
		}

		return directory?.FullName ?? throw new DirectoryNotFoundException("找不到项目根目录");
	}

	private static Hero CreateHero(bool isPlayerSide, int mana = 10)
	{
		var core = new CommanderCore();
		core.InitializeHealth(30, 30);
		core.SetMana(mana, mana);
		core.MaxHandSize = 10;
		return new Hero(core, isPlayerSide);
	}

	private static CountermeasureService CreateCountermeasureService() => new(_ => null);

	// ===== CombatLogService：条目与作用域 =====

	[Fact]
	public void RecordTurnStart_AppendsStandaloneEntry()
	{
		var log = new CombatLogService();

		log.RecordTurnStart(3, true, "我方英雄");

		CombatLogEntry entry = Assert.Single(log.Entries);
		Assert.Equal(CombatLogEntryKind.TurnStart, entry.Kind);
		Assert.Equal(3, entry.TurnNumber);
		Assert.True(entry.IsPlayerSide);
		Assert.Equal("我方英雄", entry.ActorName);
		Assert.Empty(entry.Tags);
		Assert.False(log.HasPendingAction);
	}

	[Fact]
	public void ActionScope_AggregatesDamageHealArmorIntelSummonAndKill()
	{
		var log = new CombatLogService();
		CombatLogEntry pending = log.BeginAction(2, true, CombatLogEntryKind.PlayOrder, "曼巴导弹",
			new RuntimeCard("spell_mamba", CardType.Spell, cost: 3), "敌方英雄");

		log.RecordDamage(4);
		log.RecordDamage(3);          // 同类数值聚合为 7
		log.RecordDamage(2, isCounterDamage: true);   // 反击伤害独立通道
		log.RecordHeal(5);
		log.RecordArmor(2);
		log.RecordIntel(1);
		log.RecordSummon();
		log.RecordSummon();
		log.RecordUnitKilled("敌方随从");
		Assert.True(log.HasPendingAction);
		Assert.Empty(log.Entries);

		CombatLogEntry? committed = log.CommitAction();
		Assert.Same(pending, committed);
		Assert.False(log.HasPendingAction);

		CombatLogEntry entry = Assert.Single(log.Entries);
		Assert.Equal(7, entry.Tags.Single(tag => tag.Kind == CombatLogTagKind.Damage).Value);
		Assert.Equal(2, entry.Tags.Single(tag => tag.Kind == CombatLogTagKind.CounterDamage).Value);
		Assert.Equal(5, entry.Tags.Single(tag => tag.Kind == CombatLogTagKind.Heal).Value);
		Assert.Equal(2, entry.Tags.Single(tag => tag.Kind == CombatLogTagKind.Armor).Value);
		Assert.Equal(1, entry.Tags.Single(tag => tag.Kind == CombatLogTagKind.Intel).Value);
		Assert.Equal(2, entry.Tags.Single(tag => tag.Kind == CombatLogTagKind.Summon).Value);
		Assert.Equal("敌方随从", entry.Tags.Single(tag => tag.Kind == CombatLogTagKind.Kill).Text);
	}

	[Fact]
	public void ActionScope_DropsNonPositiveNumericTags()
	{
		var log = new CombatLogService();
		_ = log.BeginAction(1, true, CombatLogEntryKind.EffectTriggered, "测试");

		log.RecordDamage(0);
		log.RecordHeal(-3);
		log.RecordArmor(0);
		log.RecordIntel(-1);
		log.RecordDraw(0);
		log.RecordDiscard(0);
		log.RecordSummon(0);
		_ = log.CommitAction();

		Assert.Empty(Assert.Single(log.Entries).Tags);
	}

	[Fact]
	public void RecordDiscard_WithCard_KeepsPerCardSnapshotsAndSkipsNumericMerge()
	{
		var log = new CombatLogService();
		_ = log.BeginAction(2, true, CombatLogEntryKind.PlayOrder, "刀盾危机",
			new RuntimeCard("spell_blade_crisis", CardType.Spell, cost: 2));
		var first = new RuntimeCard("card_first", CardType.Minion, cost: 3);
		var second = new RuntimeCard("card_second", CardType.Minion, cost: 4);

		log.RecordDiscard(first, isPlayerCard: true);
		log.RecordDiscard(second, isPlayerCard: true);
		log.RecordDiscard(1); // 无卡纯计数仍走聚合通道
		_ = log.CommitAction();

		CombatLogEntry entry = Assert.Single(log.Entries);
		CombatLogTag[] discardTags = entry.Tags
			.Where(tag => tag.Kind == CombatLogTagKind.Discard)
			.ToArray();
		// 两张带卡面的弃牌各自独立，绝不合并；纯计数保留自己的数字徽章。
		Assert.Equal(3, discardTags.Length);
		Assert.Contains(discardTags, tag => tag.Card?.Id == "card_first" && tag.IsPlayerCard == true);
		Assert.Contains(discardTags, tag => tag.Card?.Id == "card_second" && tag.IsPlayerCard == true);
		Assert.Contains(discardTags, tag => tag.Card == null && tag.Value == 1);

		// 历史快照隔离：原卡后续变化不影响战报卡面。
		first.CostModifier = 5;
		CombatLogTag firstTag = discardTags.Single(tag => tag.Card?.Id == "card_first");
		Assert.NotSame(first, firstTag.Card);
		Assert.Equal(0, firstTag.Card!.CostModifier);
	}

	[Fact]
	public void RecordCountermeasureResult_RecordsOnlyActuallyDiscardedCards()
	{
		var log = new CombatLogService();
		_ = log.BeginAction(2, false, CombatLogEntryKind.CountermeasureTriggered, "北风之神");
		var discarded = new RuntimeCard("enemy_card_a", CardType.Spell, cost: 1);

		log.RecordCountermeasureResult(false, new[] { discarded }, discardedCardsArePlayerSide: false);
		_ = log.CommitAction();

		CombatLogEntry entry = Assert.Single(log.Entries);
		Assert.Contains(entry.Tags, tag => tag.Kind == CombatLogTagKind.Countermeasure);
		CombatLogTag discardTag = Assert.Single(entry.Tags, tag => tag.Kind == CombatLogTagKind.Discard);
		Assert.Equal("enemy_card_a", discardTag.Card!.Id);
		Assert.False(discardTag.IsPlayerCard);

		// 真实触发但敌方无牌可弃：只有反制徽章，绝不出现假的数字弃牌。
		var emptyLog = new CombatLogService();
		_ = emptyLog.BeginAction(2, false, CombatLogEntryKind.CountermeasureTriggered, "北风之神");
		emptyLog.RecordCountermeasureResult(false, Array.Empty<RuntimeCard>(), discardedCardsArePlayerSide: false);
		_ = emptyLog.CommitAction();

		Assert.DoesNotContain(
			Assert.Single(emptyLog.Entries).Tags, tag => tag.Kind == CombatLogTagKind.Discard);
	}

	[Fact]
	public void NestedScopes_CommitInnerFirst_AndOuterKeepsItsOwnTags()
	{
		var log = new CombatLogService();
		CombatLogEntry outer = log.BeginAction(4, true, CombatLogEntryKind.PlayOrder, "外层法术",
			new RuntimeCard("spell_outer", CardType.Spell, cost: 2));
		log.RecordDamage(2);

		CombatLogEntry inner = log.BeginAction(4, true, CombatLogEntryKind.EffectTriggered, "亡语·机械蜈蚣");
		log.RecordDamage(5);
		Assert.Same(inner, log.CommitAction());

		_ = Assert.Single(log.Entries);
		Assert.Equal("亡语·机械蜈蚣", log.Entries[0].ActorName);
		Assert.True(log.HasPendingAction);

		Assert.Same(outer, log.CommitAction());
		Assert.Equal(2, log.Entries.Count);
		Assert.Equal("外层法术", log.Entries[1].ActorName);
		Assert.Equal(2, log.Entries[1].Tags.Single(tag => tag.Kind == CombatLogTagKind.Damage).Value);
	}

	[Fact]
	public void CommitAction_WithScopeReference_HandlesReorderedAndAlreadyCommittedScopes()
	{
		var log = new CombatLogService();
		CombatLogEntry outer = log.BeginAction(1, true, CombatLogEntryKind.PlayOrder, "外层");
		CombatLogEntry inner = log.BeginAction(1, true, CombatLogEntryKind.EffectTriggered, "内层");
		log.RecordDamage(1);

		// 异常路径可能先提交外层：按引用提交，绝不让内层顶替。
		Assert.Same(outer, log.CommitAction(outer));
		Assert.Equal("外层", Assert.Single(log.Entries).ActorName);
		Assert.True(log.HasPendingAction);

		Assert.Same(inner, log.CommitAction(inner));
		Assert.Equal(2, log.Entries.Count);

		// 重复提交同一作用域为空操作，不会误弹出其他作用域。
		Assert.Null(log.CommitAction(inner));
		Assert.Equal(2, log.Entries.Count);
	}

	[Fact]
	public void DefaultCapacity_RollsAtTwoHundredEntries()
	{
		var log = new CombatLogService();
		Assert.Equal(200, log.Capacity);

		for (int turn = 1; turn <= 205; turn++)
		{
			log.RecordTurnStart(turn, turn % 2 == 1, $"T{turn}");
		}

		Assert.Equal(CombatLogService.DefaultCapacity, log.Entries.Count);
		Assert.Equal(6, log.Entries[0].TurnNumber);
		Assert.Equal(205, log.Entries[^1].TurnNumber);
	}

	[Fact]
	public void Clear_DropsEntriesPendingScopesAndRaisesEvent()
	{
		var log = new CombatLogService();
		log.RecordTurnStart(1, true, "我方英雄");
		_ = log.BeginAction(1, true, CombatLogEntryKind.PlayOrder, "待提交");
		int cleared = 0;
		log.OnCleared += () => cleared++;

		log.Clear();

		Assert.Equal(1, cleared);
		Assert.Empty(log.Entries);
		Assert.False(log.HasPendingAction);
		Assert.Null(log.CommitAction());
	}

	[Fact]
	public void Snapshots_DoNotFollowLaterMinionHealthOrRuntimeModifiers()
	{
		var minion = new Minion("minion_snapshot", isPlayerSide: true, attack: 2, health: 6);
		RuntimeCard beforeDamage = minion.ToRuntimeCard();
		Assert.Equal(6, beforeDamage.RuntimeHealthSnapshot);
		Assert.Equal(2, beforeDamage.RuntimeAttackSnapshot);

		minion.TakeDamage(4, minion);

		Assert.Equal(2, minion.CurrentHealth);
		Assert.Equal(6, beforeDamage.RuntimeHealthSnapshot);

		var entry = new CombatLogEntry(2, true, CombatLogEntryKind.Attack, "攻击者",
			new RuntimeCard("minion_attacker", CardType.Minion, cost: 1), "目标随从",
			minion.ToRuntimeCard());
		Assert.Equal(2, entry.TargetCard!.RuntimeHealthSnapshot);

		var card = new RuntimeCard("spell_energy_coin", CardType.Spell) { BonusEnergyValue = 1 };
		RuntimeCard cardSnapshot = card.CreateSnapshot();
		card.BonusEnergyValue = 3;
		Assert.Equal(1, cardSnapshot.BonusEnergyValue);
		Assert.NotSame(card, cardSnapshot);
	}

	// ===== 反制牌保密性 =====

	[Fact]
	public void CountermeasureToggle_IsSilent_AndInterceptReportsCardsOnlyOnRealTrigger()
	{
		var log = new CombatLogService();
		int logEvents = 0;
		log.OnEntryAdded += _ => logEvents++;

		Hero player = CreateHero(true);
		Hero enemy = CreateHero(false);
		var borei = new RuntimeCard("counter_borei", CardType.Countermeasure, cost: 2);
		player.Core.AddToHand(borei);
		CountermeasureService service = CreateCountermeasureService();

		Assert.True(service.Arm(player, borei));
		Assert.True(service.Toggle(player, borei));
		Assert.True(service.Toggle(player, borei));
		Assert.Equal(1, service.DisarmAll(player));
		// 激活/取消/失效全部是待命状态，不属于战报结算。
		Assert.Equal(0, logEvents);
		Assert.Empty(log.Entries);

		// 未触发：俄亥俄级遇到 3 费法术不拦截，也不报告触发卡。
		Hero ohioPlayer = CreateHero(true);
		var ohio = new RuntimeCard("counter_ohio_ssbn", CardType.Countermeasure, cost: 4);
		ohioPlayer.Core.AddToHand(ohio);
		Assert.True(service.Arm(ohioPlayer, ohio));
		var cheap = new RuntimeCard("enemy_small_spell", CardType.Spell, cost: 3);
		InterceptionResult cheapResult = service.Intercept(enemy, ohioPlayer, cheap);
		Assert.False(cheapResult.Canceled);
		Assert.Empty(cheapResult.TriggeredCards!);
		Assert.True(ohio.IsCountermeasureArmed);
		Assert.Equal(0, logEvents);

		// 真正触发：返回被消耗的反制牌，调用方才据此写日志并播亮牌。
		var big = new RuntimeCard("enemy_big_spell", CardType.Spell, cost: 5);
		InterceptionResult bigResult = service.Intercept(enemy, ohioPlayer, big);
		Assert.True(bigResult.Canceled);
		Assert.Equal(1, bigResult.OhioTriggers);
		RuntimeCard triggered = Assert.Single(bigResult.TriggeredCards!);
		Assert.Equal("counter_ohio_ssbn", triggered.Id);
		Assert.Equal(0, logEvents);

		_ = log.BeginAction(1, true, CombatLogEntryKind.CountermeasureTriggered, "俄亥俄级", triggered,
			targetName: big.Id, targetCard: big);
		log.RecordCountermeasure();
		log.RecordCanceled();
		_ = log.CommitAction();
		CombatLogEntry logged = Assert.Single(log.Entries);
		Assert.Equal(CombatLogEntryKind.CountermeasureTriggered, logged.Kind);
		Assert.NotNull(logged.TargetCard);
		Assert.Equal("enemy_big_spell", logged.TargetCard!.Id);
		Assert.Contains(logged.Tags, tag => tag.Kind == CombatLogTagKind.Canceled);
	}

	// ===== CardPlayPresentationEvent =====

	[Fact]
	public void PresentationEvent_SnapshotsCardAndTargetCard()
	{
		Hero caster = CreateHero(false);
		var counter = new RuntimeCard("counter_borei", CardType.Countermeasure, cost: 2);
		var spell = new RuntimeCard("enemy_big_spell", CardType.Spell, cost: 5);

		var presented = new CardPlayPresentationEvent(caster, counter, null, true, true, spell);

		Assert.False(presented.IsPlayerSide);
		Assert.True(presented.IsCountermeasureTrigger);
		Assert.True(presented.IsCanceled);
		Assert.Equal("counter_borei", presented.Card.Id);
		Assert.NotSame(counter, presented.Card);
		Assert.Equal("enemy_big_spell", presented.TargetCard!.Id);
		Assert.NotSame(spell, presented.TargetCard);
		Assert.Null(presented.Target);
	}

	// ===== 指挥官回合循环：同步整回合与 UI 单步等价 =====

	[Fact]
	public void CommanderTurnLoop_RunsUntilStepFails_ThenAlwaysArmsCountermeasures()
	{
		var body = new Hero(new CommanderCore(), false);
		int probes = 0;
		int armed = 0;

		int actions = CommanderTurnLoop.Run(body, () => ++probes <= 4, () => armed++);

		Assert.Equal(4, actions);
		Assert.Equal(5, probes);
		Assert.Equal(1, armed);
	}

	[Fact]
	public void CommanderTurnLoop_CapsActionsAtGuardLimit()
	{
		var body = new Hero(new CommanderCore(), false);
		int armed = 0;

		int actions = CommanderTurnLoop.Run(body, () => true, () => armed++);

		Assert.Equal(CommanderTurnLoop.MaxActionsPerTurn, actions);
		Assert.Equal(1, armed);
	}

	[Fact]
	public void CommanderTurnLoop_RejectsNullArguments()
	{
		var body = new Hero(new CommanderCore(), false);
		_ = Assert.Throws<ArgumentNullException>(() => CommanderTurnLoop.Run(null!, () => false, () => { }));
		_ = Assert.Throws<ArgumentNullException>(() => CommanderTurnLoop.Run(body, null!, () => { }));
		_ = Assert.Throws<ArgumentNullException>(() => CommanderTurnLoop.Run(body, () => false, null!));
	}

	[Fact]
	public void SyncFullTurn_AndUiSingleStep_ReachIdenticalState()
	{
		// CombatManager 是 Godot Node，离线单测无法实例化；这里用完全相同的资源状态驱动两条路径：
		// A = DeckDrivenCommander.ExecuteTurn 的「一次性跑完整回合」，
		// B = EndPlayerTurnAsync 的「BeginTurn + 逐次单步 + 收尾反制」。
		// 断言法力、手牌、牌堆、前线归属、双方生命护甲与动作数完全一致。
		var sync = FakeTurnState.Create();
		sync.BeginTurn();
		_ = CommanderTurnLoop.Run(sync.Body, sync.TryExecuteSingleBestAction, sync.ArmRemainingCountermeasures);

		var stepped = FakeTurnState.Create();
		stepped.BeginTurn();
		int guard = 0;
		while (guard++ < CommanderTurnLoop.MaxActionsPerTurn && stepped.TryExecuteSingleBestAction())
		{
		}
		stepped.ArmRemainingCountermeasures();

		Assert.Equal(sync.Mana, stepped.Mana);
		Assert.Equal(sync.Hand, stepped.Hand);
		Assert.Equal(sync.DrawPile, stepped.DrawPile);
		Assert.Equal(sync.Frontline, stepped.Frontline);
		Assert.Equal(sync.PlayerHealth, stepped.PlayerHealth);
		Assert.Equal(sync.PlayerArmor, stepped.PlayerArmor);
		Assert.Equal(sync.EnemyHealth, stepped.EnemyHealth);
		Assert.Equal(sync.EnemyArmor, stepped.EnemyArmor);
		Assert.Equal(sync.Actions, stepped.Actions);
		Assert.Equal(sync.ArmedCountermeasures, stepped.ArmedCountermeasures);
		Assert.Equal(3, sync.Actions);
		Assert.Equal(1, sync.ArmedCountermeasures);
	}

	[Fact]
	public void SyncAndUiTurnPaths_ShareCommanderTurnLoopContract()
	{
		string syncPath = File.ReadAllText(Path.Combine(Root(), "Scripts", "AI", "DeckDrivenCommander.cs"));
		string uiPath = File.ReadAllText(Path.Combine(Root(), "Scripts", "Combat", "CombatManager.Turns.cs"));

		Assert.Contains("CommanderTurnLoop.Run(", syncPath, StringComparison.Ordinal);
		Assert.Contains("CommanderTurnLoop.MaxActionsPerTurn", uiPath, StringComparison.Ordinal);
		Assert.Contains("commander.BeginTurn(unit.Body)", uiPath, StringComparison.Ordinal);
		Assert.Contains("ArmRemainingCountermeasures(this, unit.Body)", uiPath, StringComparison.Ordinal);
	}

	/// <summary>
	/// 纯 C# 的回合状态替身：只携带等价性断言需要的字段，
	/// 动作序列与真实指挥官相同（3 费一张牌 + 推进前线 + 收尾反制）。
	/// </summary>
	private sealed class FakeTurnState
	{
		private readonly List<string> _hand = new();

		public Hero Body { get; private set; } = null!;
		public int Mana { get; private set; }
		public int Hand => _hand.Count;
		public int DrawPile { get; private set; }
		public string Frontline { get; private set; } = "None";
		public int PlayerHealth { get; private set; } = 30;
		public int PlayerArmor { get; private set; } = 2;
		public int EnemyHealth { get; private set; } = 30;
		public int EnemyArmor { get; private set; } = 1;
		public int Actions { get; private set; }
		public int ArmedCountermeasures { get; private set; }

		public static FakeTurnState Create()
		{
			var state = new FakeTurnState
			{
				Body = new Hero(new CommanderCore(), false),
				Mana = 9,
				DrawPile = 4,
			};
			for (int i = 0; i < 4; i++)
			{
				state._hand.Add($"card_{i}");
			}
			state._hand.Add("borei_counter");

			return state;
		}

		public void BeginTurn()
		{
			ArmedCountermeasures = 0;
		}

		public bool TryExecuteSingleBestAction()
		{
			int index = _hand.FindIndex(card => !card.EndsWith("_counter", StringComparison.Ordinal));
			if (Mana < 3 || index < 0)
			{
				return false;
			}

			Mana -= 3;
			_hand.RemoveAt(index);
			Frontline = "Enemy";
			PlayerHealth -= 1;
			PlayerArmor += 1;
			Actions++;
			return true;
		}

		public void ArmRemainingCountermeasures()
		{
			foreach (string card in _hand.ToList())
			{
				if (card.EndsWith("_counter", StringComparison.Ordinal))
				{
					_ = _hand.Remove(card);
					ArmedCountermeasures++;
				}
			}
		}
	}

	// ===== UiSymbolMode / EffectIconTable / 本地化卫生 =====

	[Fact]
	public void UiSymbolMode_ToggleSwitchesBetweenPlainAndEmoji()
	{
		bool original = UiSymbolMode.Enabled;
		try
		{
			UiSymbolMode.Enabled = false;
			Assert.Equal("甲", UiSymbolMode.Select("甲", "🛡"));
			Assert.Equal("战报", UiSymbolMode.Prefix("战报", "⚔ "));
			Assert.Equal("[OK]", UiSymbolMode.Select("[OK]", "✓"));

			UiSymbolMode.Enabled = true;
			Assert.Equal("🛡", UiSymbolMode.Select("甲", "🛡"));
			Assert.Equal("⚔ 战报", UiSymbolMode.Prefix("战报", "⚔ "));
			Assert.Equal("✓", UiSymbolMode.Select("[OK]", "✓"));
		}
		finally
		{
			UiSymbolMode.Enabled = original;
		}
	}

	[Fact]
	public void EffectIconTable_AllEntriesExposeEmojiAndPlainFallbacks()
	{
		string[] statusIds =
		{
			"attack_zero", "meltdown", "weapon_disabled", "animosity", "vulnerable", "weak", "fragile",
			"total_observation", "attack_ban", "damage_over_time", "incapacitated", "shiyoru_raidenkou",
			"sutaraito_spirit", "smokescreen", "fatal_rift",
		};
		string[] domainIds =
		{
			"unlimited_potential", "flying_away", "idol_twilight", "jiehuafa", "focus", "scheme",
			"inescapable_flaw", "preemptive_strike",
		};

		foreach (string id in statusIds)
		{
			AssertIconData(EffectIconTable.GetStatusEffect(id), id);
		}

		foreach (string id in domainIds)
		{
			AssertIconData(EffectIconTable.GetDomain(id), id);
		}

		AssertIconData(EffectIconTable.GetKeywordSource("bait_tactics"), "bait_tactics");
		AssertIconData(EffectIconTable.GetModifier("animosity"), "animosity");
		Assert.Null(EffectIconTable.GetStatusEffect("__missing__"));

		static void AssertIconData(EffectIconData? maybeData, string id)
		{
			Assert.True(maybeData.HasValue, $"{id} 不在效果图标对照表中");
			EffectIconData data = maybeData.GetValueOrDefault();
			Assert.False(string.IsNullOrWhiteSpace(data.Icon), $"{id} 缺少 Emoji 对照图标");
			Assert.False(string.IsNullOrWhiteSpace(data.PlainLabel), $"{id} 缺少中文简字");
			Assert.False(string.IsNullOrWhiteSpace(data.PlainLabelEn), $"{id} 缺少英文简字");
			Assert.False(string.IsNullOrWhiteSpace(data.NameKey), $"{id} 缺少名称翻译键");
			Assert.False(string.IsNullOrWhiteSpace(data.DescKey), $"{id} 缺少描述翻译键");
			Assert.NotEqual(0u, data.AccentColorRgba);

			string plain = EffectIconTable.GetPlainLabel(data);
			Assert.True(plain == data.PlainLabel || plain == data.PlainLabelEn);
			// ToDisplayable 会读取 Localization.T → Godot DirAccess，交给游戏内 QA 验证。
		}
	}

	[Theory]
	[InlineData("zh.yaml")]
	[InlineData("en.yaml")]
	public void LocalizationYaml_UsesStrictTwoSpaceIndentWithoutTabs(string file)
	{
		string text = File.ReadAllText(Path.Combine(Root(), "Resources", "Localization", file));

		Assert.False(text.Contains('\t', StringComparison.Ordinal), $"{file} 含 Tab 缩进");
		foreach (string line in text.Split('\n'))
		{
			if (line.Length == 0 || line.TrimStart().Length == line.Length)
			{
				continue;
			}

			int spaces = line.Length - line.TrimStart(' ').Length;
			Assert.True(spaces % 2 == 0 && line[spaces] != ' ',
				$"{file} 存在非 2 空格递增缩进：{line}");
		}
	}

	[Theory]
	[InlineData("zh.yaml")]
	[InlineData("en.yaml")]
	public void LocalizationYaml_ContainsNoBareEmojiExceptHearts(string file)
	{
		string text = File.ReadAllText(Path.Combine(Root(), "Resources", "Localization", file));
		var violations = new List<string>();
		Rune previous = default;
		foreach (Rune rune in text.EnumerateRunes())
		{
			bool heart = rune.Value == HeartRune;
			bool heartVariation = rune.Value == VariationSelector16 && previous.Value == HeartRune;
			if (!heart && !heartVariation && IsEmojiRune(rune))
			{
				violations.Add($"U+{rune.Value:X4}");
			}

			previous = rune;
		}

		Assert.True(violations.Count == 0,
			$"{file} 含未受控 Emoji：{string.Join(", ", violations.Distinct())}");
	}

	[Theory]
	[InlineData("zh.yaml")]
	[InlineData("en.yaml")]
	public void LocalizationYaml_HasCardShowcaseBadgeKeys(string file)
	{
		string text = File.ReadAllText(Path.Combine(Root(), "Resources", "Localization", file));
		foreach (string key in new[]
		{
			"log_player_order", "log_enemy_order", "log_domain_deploy", "log_counter_trigger",
			"log_counter_canceled", "log_tag_discard_card",
		})
		{
			Assert.Contains($"{key}:", text, StringComparison.Ordinal);
		}
	}

	[Theory]
	[InlineData("zh.yaml", "界面字体大小")]
	[InlineData("en.yaml", "UI Font Size")]
	public void LocalizationYaml_HasUiFontScaleSetting(string file, string expectedText)
	{
		string text = File.ReadAllText(Path.Combine(Root(), "Resources", "Localization", file));

		Assert.Contains($"font_scale: \"{expectedText}\"", text, StringComparison.Ordinal);
		Assert.Contains("deploy_speed:", text, StringComparison.Ordinal);
	}

	// ===== 战斗节奏与界面字体缩放 =====

	[Fact]
	public void UiScaler_DefaultsProvideSlowerCombatAndLargerFonts()
	{
		Assert.Equal(0.75f, UIScaler.DefaultDeployAnimationSpeed);
		Assert.Equal(0.8f, UIScaler.MinFontScale);
		Assert.Equal(1.20f, UIScaler.DefaultFontScale);
		Assert.Equal(1.8f, UIScaler.MaxFontScale);

		string source = File.ReadAllText(Path.Combine(Root(), "Scripts", "UI", "UIScaler.cs"));
		// 键名升级后，旧 settings.cfg 里存过 1.0 的玩家也会立即获得新的 0.75 慢速默认。
		Assert.Contains("\"combat_animation_speed\"", source, StringComparison.Ordinal);
		Assert.Contains("\"font_scale\"", source, StringComparison.Ordinal);
		Assert.DoesNotContain("\"deploy_animation_speed\"", source, StringComparison.Ordinal);
	}

	[Fact]
	public void UiScaler_ScaleFontSize_ClampsRangeAndRoundsToReadableMinimum()
	{
		Assert.Equal(19, UIScaler.ScaleFontSize(16, 1.20f));
		Assert.Equal(29, UIScaler.ScaleFontSize(16, 1.8f));
		Assert.Equal(13, UIScaler.ScaleFontSize(16, 0.8f));
		// 超出可调范围时夹紧到 0.8~1.8，而不是无限放大/缩小。
		Assert.Equal(29, UIScaler.ScaleFontSize(16, 5f));
		Assert.Equal(13, UIScaler.ScaleFontSize(16, 0.1f));
		Assert.Equal(8, UIScaler.ScaleFontSize(1, 1.2f));
		// 未运行 Godot 时 Instance 为空，静态辅助回退到 DefaultFontScale=1.20。
		Assert.Equal(UIScaler.ScaleFontSize(16, UIScaler.DefaultFontScale), UIScaler.ApplyFontScale(16));
	}

	[Fact]
	public void PresentationVfx_BaseDurationsKeepShowcaseAndDeployReadable()
	{
		Assert.Equal(0.22f, ReadSourceFloat("Scripts/UI/CardShowcaseVfx.cs", "EnterBaseDuration"));
		Assert.Equal(0.65f, ReadSourceFloat("Scripts/UI/CardShowcaseVfx.cs", "PlayerHoldBaseDuration"));
		Assert.Equal(0.95f, ReadSourceFloat("Scripts/UI/CardShowcaseVfx.cs", "EnemyHoldBaseDuration"));
		Assert.Equal(0.34f, ReadSourceFloat("Scripts/UI/CardShowcaseVfx.cs", "ExitBaseDuration"));

		Assert.Equal(0.26f, ReadSourceFloat("Scripts/UI/MinionDeployVfx.cs", "ApproachBaseDuration"));
		Assert.Equal(0.42f, ReadSourceFloat("Scripts/UI/MinionDeployVfx.cs", "PlayerHoldBaseDuration"));
		Assert.Equal(0.65f, ReadSourceFloat("Scripts/UI/MinionDeployVfx.cs", "EnemyHoldBaseDuration"));
		Assert.Equal(0.20f, ReadSourceFloat("Scripts/UI/MinionDeployVfx.cs", "DiveBaseDuration"));
		Assert.Equal(0.24f, ReadSourceFloat("Scripts/UI/MinionDeployVfx.cs", "LandBaseDuration"));

		Assert.Equal(0.34f, ReadSourceFloat("Scripts/UI/MinionMoveVfx.cs", "MoveBaseDuration"));
		Assert.Equal(0.12f, ReadSourceFloat("Scripts/UI/MinionMoveVfx.cs", "FadeBaseDuration"));

		Assert.Equal(0.30f, ReadSourceFloat("Scripts/UI/AttackProjectileVfx.cs", "AttackDuration"));
		Assert.Equal(0.44f, ReadSourceFloat("Scripts/UI/AttackProjectileVfx.cs", "SpellDuration"));
		Assert.Equal(0.24f, ReadSourceFloat("Scripts/UI/AttackProjectileVfx.cs", "CombatDuration"));

		// 弹道读取战斗动画速度，与其他演出共用同一倍率。
		string projectile = File.ReadAllText(Path.Combine(Root(), "Scripts", "UI", "AttackProjectileVfx.cs"));
		Assert.Contains("DeployAnimationSpeed", projectile, StringComparison.Ordinal);
	}

	[Fact]
	public void EnemyStepDelays_CoverFullShowcaseAndDeployWindows()
	{
		string uiPath = Path.Combine(Root(), "Scripts", "UI", "CombatUI.cs");
		float playCard = ReadStepDelay(uiPath, "PlayCard");
		float deploy = ReadStepDelay(uiPath, "Deploy");
		float turnPrepare = ReadStepDelay(uiPath, "TurnPrepare");
		float turnEnd = ReadStepDelay(uiPath, "TurnEnd");

		// CardShowcaseVfx 敌方完整三段：进入 + 敌方停留 + 收尾。
		float showCaseWindow = ReadSourceFloat("Scripts/UI/CardShowcaseVfx.cs", "EnterBaseDuration")
			+ ReadSourceFloat("Scripts/UI/CardShowcaseVfx.cs", "EnemyHoldBaseDuration")
			+ ReadSourceFloat("Scripts/UI/CardShowcaseVfx.cs", "ExitBaseDuration");
		// MinionDeployVfx 敌方悬停：接近 + 敌方停留 + 俯冲。
		float deployWindow = ReadSourceFloat("Scripts/UI/MinionDeployVfx.cs", "ApproachBaseDuration")
			+ ReadSourceFloat("Scripts/UI/MinionDeployVfx.cs", "EnemyHoldBaseDuration")
			+ ReadSourceFloat("Scripts/UI/MinionDeployVfx.cs", "DiveBaseDuration");

		// 战报步骤 1.45s 与亮牌 1.51s 只差收尾淡出的 0.06s——卡面阅读段（进入+停留 1.17s）完整覆盖，
		// 尾段淡出允许与下一步重叠；差值超过 0.1s 才说明步进被压回一闪而过。
		Assert.True(showCaseWindow - playCard <= 0.10f,
			$"PlayCard 步进 {playCard:F2}s 比亮牌 {showCaseWindow:F2}s 短太多");
		Assert.True(playCard >= ReadSourceFloat("Scripts/UI/CardShowcaseVfx.cs", "EnterBaseDuration")
			+ ReadSourceFloat("Scripts/UI/CardShowcaseVfx.cs", "EnemyHoldBaseDuration"),
			"PlayCard 步进必须完整覆盖亮牌的进入与停留阅读段");
		Assert.True(deploy >= deployWindow,
			$"Deploy 步进 {deploy:F2}s 容不下部署悬停 {deployWindow:F2}s");
		Assert.True(turnPrepare < playCard);
		Assert.True(turnEnd < deploy);
	}

	[Fact]
	public void ShowcaseCenter_DoesNotOverlapExpandedCombatLogDrawer()
	{
		// 战报抽屉 320px + 34px 把手 = 354px；亮牌展示位必须完全落在抽屉右缘之外。
		const float drawerRightEdge = 320f + 34f;
		var viewport = new Vector2(1600f, 900f);
		var cardSize = new Vector2(CardUI.DESIGN_WIDTH, CardUI.DESIGN_HEIGHT) * 0.98f;

		Vector2 center = CardShowcaseVfx.ResolveShowcaseCenter(viewport, cardSize);

		Assert.True(center.X - cardSize.X * 0.5f >= drawerRightEdge,
			$"亮牌展示位左缘 {center.X - cardSize.X * 0.5f:F1} 与战报抽屉右缘 {drawerRightEdge} 重叠");
	}

	private static float ReadSourceFloat(string relativePath, string constantName)
	{
		string text = File.ReadAllText(Path.Combine(Root(), relativePath));
		Match match = Regex.Match(text, $@"\b{Regex.Escape(constantName)}\s*=\s*(?<value>[0-9.]+)f");
		Assert.True(match.Success, $"{relativePath} 缺少常量 {constantName}");
		return float.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
	}

	private static float ReadStepDelay(string uiPath, string stepKind)
	{
		string text = File.ReadAllText(uiPath);
		Match match = Regex.Match(text, $@"EnemyStepKind\.{stepKind}\s*=>\s*(?<value>[0-9.]+)f");
		Assert.True(match.Success, $"CombatUI 缺少 {stepKind} 步进时长");
		return float.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
	}

	[Fact]
	public void SourceScripts_UseEmojiOnlyThroughUiSymbolModeOrReviewedMetadataTables()
	{
		// 受控 Emoji 来源：对照表元数据、DisplayableEffect.Icon 元数据、日志徽章前缀、♥ 文案。
		var approved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"Scripts/Core/EffectIconTable.cs",
			"Scripts/Card/Hero.cs",
			"Scripts/Card/Minion.cs",
			"Scripts/UI/CombatLogDrawer.cs",
		};
		var violations = new List<string>();
		string scriptsRoot = Path.Combine(Root(), "Scripts");
		foreach (string path in Directory.EnumerateFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
		{
			string relative = Path.GetRelativePath(Root(), path).Replace('\\', '/');
			if (approved.Contains(relative))
			{
				continue;
			}

			string[] lines = File.ReadAllLines(path);
			for (int i = 0; i < lines.Length; i++)
			{
				string code = StripComments(lines[i]);
				if (!ContainsNonHeartEmoji(code))
				{
					continue;
				}

				string window = string.Join("\n", lines.Skip(Math.Max(0, i - 5)).Take(11));
				if (window.Contains("UiSymbolMode", StringComparison.Ordinal))
				{
					continue;
				}

				violations.Add($"{relative}:{i + 1}");
			}
		}

		Assert.True(violations.Count == 0,
			$"以下位置出现未经 UiSymbolMode 控制的裸 Emoji：{string.Join(", ", violations)}");
	}

	// ===== Emoji 判定工具 =====

	private const int HeartRune = 0x2665;
	private const int VariationSelector16 = 0xFE0F;

	private static bool IsEmojiRune(Rune rune) => rune.Value switch
	{
		>= 0x1F000 and <= 0x1FAFF => true,
		>= 0x2600 and <= 0x27BF => true,
		>= 0x2B00 and <= 0x2BFF => true,
		_ => false,
	};

	private static bool ContainsNonHeartEmoji(string text)
	{
		Rune previous = default;
		foreach (Rune rune in text.EnumerateRunes())
		{
			bool heartVariation = rune.Value == VariationSelector16 && previous.Value == HeartRune;
			if (rune.Value != HeartRune && !heartVariation && IsEmojiRune(rune))
			{
				return true;
			}

			previous = rune;
		}
		return false;
	}

	private static string StripComments(string line)
	{
		string trimmed = line.TrimStart();
		if (trimmed.StartsWith("//", StringComparison.Ordinal)
			|| trimmed.StartsWith('*')
			|| trimmed.StartsWith("/*", StringComparison.Ordinal))
		{
			return string.Empty;
		}

		int comment = line.IndexOf(" //", StringComparison.Ordinal);
		return comment >= 0 ? line[..comment] : line;
	}
}
