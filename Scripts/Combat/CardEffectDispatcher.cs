#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OdysseyCards.AI;
using AiIntentType = OdysseyCards.AI.Intents.IntentType;
using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Core;

namespace OdysseyCards.Combat;

/// <summary>
/// 卡牌效果分发器。
/// 从 CombatManager 中拆出 EffectType→Handler 注册表与具体效果处理逻辑，
/// 让 CombatManager 只保留战斗流转与玩家操作入口。
/// </summary>
internal sealed class CardEffectDispatcher
{
	private readonly CommanderCore _playerCore;
	private readonly Hero _playerHero;
	private readonly IReadOnlyList<EnemyUnit> _enemyUnits;
	private readonly Board _board;
	private readonly GameState _state;
	private readonly Action _notifyCombatStateChanged;
		private readonly Action<CardEffectData> _handleDiscoverEffect;
		private readonly Action<List<Card.Card>, int> _beginDiscardDiscoverSelection;
		private readonly Action<List<Card.Card>, int, int, bool> _beginHandDiscardSelection;
		private readonly Action<Card.Card> _beginCopyHandFillSelection;
		/// <summary>抉择 UI 触发回调——参数：titleKey, titleFallback, optionLabels, optionDescriptions, onChosen(index)。</summary>
		private readonly Action<string, string, IReadOnlyList<string>, IReadOnlyList<string>, Action<int>> _beginChooseOne;
		private readonly Action<object?, IDamageTarget, DamageKind, CombatDamageVfxKind> _requestDamageVfx;
	private readonly Dictionary<CardEffectType, Action<CardEffectData, object?, IDamageSource?, object?>> _handlers;

	// 先发制人领域：每回合首张直伤牌是否已消费
	private bool _preemptiveStrikeConsumedThisTurn;

	public CardEffectDispatcher(
		CommanderCore playerCore,
		Hero playerHero,
		IReadOnlyList<EnemyUnit> enemyUnits,
		Board board,
		GameState state,
		Action notifyCombatStateChanged,
		Action<CardEffectData> handleDiscoverEffect,
		Action<List<Card.Card>, int> beginDiscardDiscoverSelection,
		Action<List<Card.Card>, int, int, bool> beginHandDiscardSelection,
		Action<Card.Card> beginCopyHandFillSelection,
		Action<string, string, IReadOnlyList<string>, IReadOnlyList<string>, Action<int>> beginChooseOne,
		Action<object?, IDamageTarget, DamageKind, CombatDamageVfxKind> requestDamageVfx)
	{
		_playerCore = playerCore;
		_playerHero = playerHero;
		_enemyUnits = enemyUnits;
		_board = board;
		_state = state;
		_notifyCombatStateChanged = notifyCombatStateChanged;
		_handleDiscoverEffect = handleDiscoverEffect;
		_beginDiscardDiscoverSelection = beginDiscardDiscoverSelection;
		_beginHandDiscardSelection = beginHandDiscardSelection;
		_beginCopyHandFillSelection = beginCopyHandFillSelection;
		_beginChooseOne = beginChooseOne;
		_requestDamageVfx = requestDamageVfx;

		_handlers = new Dictionary<CardEffectType, Action<CardEffectData, object?, IDamageSource?, object?>>()
		{
			[CardEffectType.Damage] = HandleDamage,
			[CardEffectType.DealDamageToTarget] = HandleDamage,
			[CardEffectType.DealDamageToEnemyHero] = HandleDealDamageToEnemyHero,
			[CardEffectType.DealDamageToFriendlyHero] = HandleDealDamageToFriendlyHero,
			[CardEffectType.DealDamageToAllEnemies] = HandleDealDamageToAllEnemies,
			[CardEffectType.DealDamageToAllEnemiesAndHero] = HandleDealDamageToAllEnemiesAndHero,
			[CardEffectType.DrawCards] = HandleDrawCards,
			[CardEffectType.Heal] = HandleHeal,
			[CardEffectType.RestoreHealth] = HandleHeal,
			[CardEffectType.GainArmor] = HandleGainArmor,
				[CardEffectType.GainMaxHealth] = HandleGainMaxHealth,
				[CardEffectType.SummonMinion] = HandleSummonMinion,
				[CardEffectType.BuffMinion] = HandleBuffMinion,
				[CardEffectType.GainEnergy] = HandleGainEnergy,
				[CardEffectType.GainManaSlot] = HandleGainManaSlot,
				[CardEffectType.RemoveNaturalManaCap] = HandleRemoveNaturalManaCap,
				[CardEffectType.Discover] = HandleDiscoverEffectDispatch,
				[CardEffectType.MountHeroEffect] = HandleMountHeroEffect,
			[CardEffectType.ReplaceDeathrattleWithDraw] = HandleReplaceDeathrattleWithDraw,
			[CardEffectType.ChooseFromDiscard] = HandleChooseFromDiscard,
			[CardEffectType.DiscardRandom] = HandleDiscardRandom,
			[CardEffectType.DiscardChoose] = HandleDiscardChoose,
				[CardEffectType.ShuffleTribeCards] = HandleShuffleTribeCards,
				[CardEffectType.ApplyStatusToTarget] = HandleApplyStatusToTarget,
				[CardEffectType.DrawByFriendlyMinions] = HandleDrawByFriendlyMinions,
				[CardEffectType.GainManaSlotAndMana] = HandleGainManaSlotAndMana,
				[CardEffectType.GainArmorToFriendlyBoard] = HandleGainArmorToFriendlyBoard,
				[CardEffectType.Custom] = HandleCustomEffect,
			};
	}

	public void ExecuteEffect(CardEffectData effect, object? target, IDamageSource? source = null, object? visualSource = null)
	{
		if (_handlers.TryGetValue(effect.EffectType, out var handler))
		{
			handler(effect, target, source, visualSource ?? source);
			return;
		}

		GD.Print($"[CardEffectDispatcher] 未处理的效果类型：{effect.EffectType}（{effect.GetDescription()}）");
	}

	private void HandleDamage(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
	{
		int finalDamage = effect.Value;

		// 先发制人领域：每回合首张直伤牌按手牌数 +10%/张
		if (!_preemptiveStrikeConsumedThisTurn && _playerHero.HasDomain("preemptive_strike")
			&& source is IDamageSource src && src.IsPlayerSide)
		{
			_preemptiveStrikeConsumedThisTurn = true;
			int handCount = _playerHero.DeckState.Hand.Count;
			float multiplier = 1.0f + handCount * 0.1f;
			finalDamage = (int)(effect.Value * multiplier);
			GD.Print($"[CardEffectDispatcher] 先发制人：手牌{handCount}张，伤害 ×{multiplier:F1} = {finalDamage}");
		}

		if (target is Minion minionTarget)
		{
			_requestDamageVfx(visualSource, minionTarget, DamageKind.Effect, CombatDamageVfxKind.Spell);
			minionTarget.TakeDamage(effect.Value, source, DamageKind.Effect);
			GD.Print($"[CardEffectDispatcher] 对 {minionTarget.CardName} 造成 {effect.Value} 点伤害");
		}
		else if (target is Hero heroTarget)
		{
			_requestDamageVfx(visualSource, heroTarget, DamageKind.Effect, CombatDamageVfxKind.Spell);
			heroTarget.TakeDamage(effect.Value, source, DamageKind.Effect);
			GD.Print($"[CardEffectDispatcher] 对英雄造成 {effect.Value} 点伤害");
		}
		else
		{
			GD.PrintErr("[CardEffectDispatcher] 目标类型不支持伤害");
		}
	}

	private void HandleDealDamageToEnemyHero(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
	{
		if (target is not Hero hero)
			return;
		_requestDamageVfx(visualSource, hero, DamageKind.Effect, CombatDamageVfxKind.Spell);
		hero.TakeDamage(effect.Value, source, DamageKind.Effect);
		GD.Print($"[CardEffectDispatcher] 对敌方英雄造成 {effect.Value} 点伤害（剩余 {hero.CurrentHealth}）");
	}

	private void HandleDealDamageToFriendlyHero(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
	{
		_requestDamageVfx(visualSource, _playerHero, DamageKind.Effect, CombatDamageVfxKind.Spell);
		_playerHero.TakeDamage(effect.Value, source, DamageKind.Effect);
		GD.Print($"[CardEffectDispatcher] 对友方英雄造成 {effect.Value} 点伤害（剩余 {_playerHero.CurrentHealth}）");
	}

	private void HandleDealDamageToAllEnemies(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
	{
		int hitCount = 0;
		foreach (var enemyMinion in GetEnemyMinionsFor(source))
		{
			_requestDamageVfx(visualSource, enemyMinion, DamageKind.Effect, CombatDamageVfxKind.Spell);
			enemyMinion.TakeDamage(effect.Value, source, DamageKind.Effect);
			hitCount++;
		}
		GD.Print($"[CardEffectDispatcher] 对所有敌方随从造成 {effect.Value} 点伤害（命中 {hitCount} 个目标）");
	}

	/// <summary>
	/// 对敌方全体（英雄+随从）造成法术伤害。
	/// 阵营相对 source 自动判断——玩家方 source 打敌方全体，敌方 source 打玩家方全体。
	/// </summary>
	private void HandleDealDamageToAllEnemiesAndHero(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
	{
		int hitCount = 0;
		foreach (var hero in GetEnemyHeroesFor(source))
		{
			_requestDamageVfx(visualSource, hero, DamageKind.Effect, CombatDamageVfxKind.Spell);
			hero.TakeDamage(effect.Value, source, DamageKind.Effect);
			hitCount++;
		}
		foreach (var enemyMinion in GetEnemyMinionsFor(source))
		{
			_requestDamageVfx(visualSource, enemyMinion, DamageKind.Effect, CombatDamageVfxKind.Spell);
			enemyMinion.TakeDamage(effect.Value, source, DamageKind.Effect);
			hitCount++;
		}
		string sideLabel = IsPlayerCaster(source) ? "敌方" : "玩家方";
		GD.Print($"[CardEffectDispatcher] 对{sideLabel}全体造成 {effect.Value} 点法术伤害（命中 {hitCount} 个目标）");
	}

	private void HandleDrawCards(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
	{
		_playerHero.DrawCards(effect.Value);
		GD.Print($"[CardEffectDispatcher] 抽 {effect.Value} 张牌");
	}

	private void HandleHeal(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
	{
		// ponytail: 修复 BUG——Heal 忽略 target，总是治疗玩家英雄。
		// 现在按 target 分流：Minion 走贴膜（Minion.Heal 抬高 MaxHealth）；Hero 走 Clamp(MaxHealth)。
		// 兼容：target 为 null（旧调用路径），回退治疗玩家英雄。
		switch (target)
		{
			case Minion minionTarget:
				minionTarget.Heal(effect.Value);
				GD.Print($"[CardEffectDispatcher] 恢复 {effect.Value} 点生命值（随从 {minionTarget.CardName}，当前 {minionTarget.CurrentHealth}/{minionTarget.MaxHealth}）");
				return;
			case Hero heroTarget:
				heroTarget.Heal(effect.Value);
				GD.Print($"[CardEffectDispatcher] 恢复 {effect.Value} 点生命值（英雄，当前 {heroTarget.CurrentHealth}/{heroTarget.MaxHealth}）");
				return;
			default:
				_playerCore.Heal(effect.Value);
				GD.Print($"[CardEffectDispatcher] 恢复 {effect.Value} 点生命值（默认玩家英雄，当前 {_playerHero.CurrentHealth}）");
				return;
		}
	}

	private void HandleGainArmor(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
	{
		_playerHero.GainArmor(effect.Value);
		GD.Print($"[CardEffectDispatcher] 获得 {effect.Value} 点护甲（当前 {_playerHero.CurrentArmor}）");
	}

	private void HandleGainMaxHealth(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
	{
		_playerCore.InitializeHealth(_playerCore.MaxHealth + effect.Value, _playerCore.CurrentHealth + effect.Value);
		GD.Print($"[CardEffectDispatcher] 最大生命值 +{effect.Value} 并恢复等量生命值（当前 {_playerHero.CurrentHealth}/{_playerHero.MaxHealth}）");
	}

	private void HandleSummonMinion(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
	{
		int emptySlot = _board.GetEmptySlotIndex(isPlayerSide: true);
		if (emptySlot >= 0)
		{
			GD.Print($"[CardEffectDispatcher] 召唤随从效果：{effect.GetDescription()}（原型：仅记录日志）");
		}
		else
		{
			GD.Print("[CardEffectDispatcher] 召唤随从失败 — 战场已满");
		}
	}

		private void HandleBuffMinion(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
		{
			if (target is Minion buffTarget)
			{
				GD.Print($"[CardEffectDispatcher] BuffMinion：{effect.GetDescription()} → {buffTarget.CardName}（原型：暂未实现属性修改）");
		}
		else
		{
			GD.Print("[CardEffectDispatcher] BuffMinion 需要有效的随从目标");
			}
		}

		private void HandleGainEnergy(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
		{
			_playerHero.GainMana(effect.Value);
			GD.Print($"[CardEffectDispatcher] 获得 {effect.Value} 点临时法力（当前 {_playerHero.CurrentMana}/{_playerHero.MaxMana}）");
		}

		private void HandleGainManaSlot(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
	{
		_state.GainManaSlot(effect.Value);
		_playerCore.SetMana(_playerCore.CurrentMana, _state.PlayerMaxMana);
		GD.Print($"[CardEffectDispatcher] 获得 {effect.Value} 个法力水晶槽（总上限 {_state.PlayerMaxMana}）");
	}

	private void HandleGainManaSlotAndMana(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
	{
		int beforeMax = _state.PlayerMaxMana;
		_state.GainManaSlot(effect.Value);
		int gainedSlots = _state.PlayerMaxMana - beforeMax;
		_playerCore.SetMana(_playerCore.CurrentMana + gainedSlots, _state.PlayerMaxMana);
		GD.Print($"[CardEffectDispatcher] 获得 {gainedSlots} 个法力槽和 {gainedSlots} 点法力（当前 {_playerHero.CurrentMana}/{_playerHero.MaxMana}）");
	}

	private static void HandleRemoveNaturalManaCap(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
	{
		GD.Print("[CardEffectDispatcher] 无限潜能领域已展开，自然增长上限提升至 30");
	}

	private void HandleDiscoverEffectDispatch(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
	{
		_handleDiscoverEffect(effect);
	}

	private static void HandleReplaceDeathrattleWithDraw(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
	{
		if (target is not Minion minionTarget)
		{
			GD.Print("[CardEffectDispatcher] 替换亡语需要有效的随从目标");
			return;
		}

		int drawCount = Math.Max(1, effect.Value);
		var drawEffect = new CardEffectData { EffectType = CardEffectType.DrawCards, Value = drawCount };
		minionTarget.ReplaceDeathrattleEffects(new[] { drawEffect });
		GD.Print($"[CardEffectDispatcher] {minionTarget.CardName} 获得亡语：抽 {drawCount} 张牌");
	}

	private void HandleChooseFromDiscard(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
	{
		int optionCount = effect.Value > 0 ? effect.Value : 5;
		int pickCount = effect.SecondaryValue > 0 ? effect.SecondaryValue : 2;
		var options = GetRandomCardsFromDiscard(optionCount);

		if (options.Count == 0)
		{
			GD.Print("[CardEffectDispatcher] 捞月：弃牌堆为空，无牌可选");
			return;
		}

		_beginDiscardDiscoverSelection(options, pickCount);
		GD.Print($"[CardEffectDispatcher] 捞月：从弃牌堆展示 {options.Count} 张，选择 {Math.Min(pickCount, options.Count)} 张");
	}

	private void HandleDiscardRandom(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
	{
		int discardCount = effect.Value;
		var hand = _playerHero.Hand.ToList();

		if (hand.Count == 0)
		{
			GD.Print("[CardEffectDispatcher] 随机弃牌：手牌为空，无法弃牌");
			return;
		}

		int actualDiscard = Math.Min(discardCount, hand.Count);
		using var rng = new RandomNumberGenerator();
		rng.Randomize();

		for (int i = 0; i < actualDiscard; i++)
		{
			int randomIndex = rng.RandiRange(0, hand.Count - 1);
			var card = hand[randomIndex];
			GD.Print($"[CardEffectDispatcher] 随机弃掉: {card.GetLocalizedName()}");
			_playerHero.DiscardCard(card);
			hand.RemoveAt(randomIndex);
		}

		GD.Print($"[CardEffectDispatcher] 随机弃牌完成：弃掉 {actualDiscard}/{discardCount} 张牌");
		_notifyCombatStateChanged();
	}

	private void HandleDiscardChoose(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
	{
		int mustDiscard = effect.Value;
		var handCopy = _playerHero.Hand.ToList();

		if (handCopy.Count == 0)
		{
			GD.Print("[CardEffectDispatcher] 主动弃牌：手牌为空，无法弃牌");
			return;
		}

		int discardCount = Math.Min(mustDiscard, handCopy.Count);
		_beginHandDiscardSelection(handCopy, discardCount, discardCount, false);
		GD.Print($"[CardEffectDispatcher] 主动弃牌：从手牌 {handCopy.Count} 张中弃掉 {discardCount}/{mustDiscard} 张");
	}

		private void HandleShuffleTribeCards(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
		{
		int insertCount = effect.Value;
		if (!Enum.TryParse<CardMechanicTag>(effect.TargetType, out var targetTag) || targetTag == CardMechanicTag.None)
		{
			GD.PrintErr($"[CardEffectDispatcher] 种族洗牌：无法识别的机制标签 '{effect.TargetType}'");
			return;
		}

		var pool = GameManager.Instance.GetAllCards()
			.Where(cardData => cardData.MechanicTags.HasFlag(targetTag) && cardData.Type == CardType.Minion)
			.ToList();

		if (pool.Count == 0)
		{
			GD.Print($"[CardEffectDispatcher] 种族洗牌：没有符合条件的 {effect.TargetType} 随从卡牌");
			return;
		}

		using var rng = new RandomNumberGenerator();
		rng.Randomize();

		for (int i = 0; i < insertCount; i++)
		{
			int randomIndex = rng.RandiRange(0, pool.Count - 1);
			var cardData = pool[randomIndex];
			var card = new Card.Card(cardData);
			_playerHero.InsertCardToDrawPile(card);
			GD.Print($"[CardEffectDispatcher] 洗入抽牌堆: {card.GetLocalizedName()}");
		}

		_playerHero.ShuffleDrawPile();
		GD.Print($"[CardEffectDispatcher] 种族洗牌完成：将 {insertCount} 张随机 {effect.TargetType} 随从洗入抽牌堆");
			_notifyCombatStateChanged();
		}

	private void HandleApplyStatusToTarget(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
	{
		string statusId = string.IsNullOrWhiteSpace(effect.TargetType)
			? StatusEffect.IncapacitatedId
			: effect.TargetType;
		int stacks = Math.Max(1, effect.Value);

		switch (target)
		{
			case Hero hero:
				hero.AddStatusEffect(new StatusEffect(statusId, stacks, GetStatusTickTimingFor(hero)));
				break;
			case Minion minion:
				minion.AddStatusEffect(new StatusEffect(statusId, stacks, GetStatusTickTimingFor(minion)));
				break;
			default:
				GD.Print($"[CardEffectDispatcher] 施加状态失败：目标无效（{statusId}）");
				return;
		}

		GD.Print($"[CardEffectDispatcher] 目标获得 {stacks} 层状态「{statusId}」");
		_notifyCombatStateChanged();
	}

	private void HandleDrawByFriendlyMinions(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
	{
		int baseDraw = effect.Value > 0 ? effect.Value : 1;
		int minionCount = GetFriendlyMinionsFor(source).Count;
		int drawCount = minionCount + baseDraw;
		_playerHero.DrawCards(drawCount);
		GD.Print($"[CardEffectDispatcher] 响应：友方随从 {minionCount} 个，抽 {drawCount} 张牌");
	}

	private void HandleGainArmorToFriendlyBoard(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
	{
		int minionArmor = Math.Max(0, effect.Value);
		int heroMultiplier = effect.SecondaryValue > 0 ? effect.SecondaryValue : 2;
		var minions = GetFriendlyMinionsFor(source);

		foreach (var minion in minions)
			minion.GainArmor(minionArmor);

		var hero = GetFriendlyHeroFor(source);
		int heroArmor = minionArmor * heroMultiplier;
		hero?.GainArmor(heroArmor);

		string sideLabel = IsPlayerCaster(source) ? "玩家方" : "敌方";
		GD.Print($"[CardEffectDispatcher] {sideLabel}全体获得格挡：随从 {minionArmor}，英雄 {heroArmor}");
		_notifyCombatStateChanged();
	}

private void HandleMountHeroEffect(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
	{
		switch (effect.CustomEffectName)
		{
			case "ShiyoruRaidenkou":
				MountShiyoruRaidenkou(effect);
				break;

			case "SutaraitoSpiritNextTurn":
				MountSutaraitoSpirit(effect);
				break;

			default:
				GD.Print($"[CardEffectDispatcher] 未处理的英雄挂载效果：{effect.CustomEffectName}");
				break;
		}
	}

	/// <summary>
	/// 四夜雷电光：玩家回合结束时对随机敌人造成法术伤害（限时挂载效果）。
	/// 改走 <see cref="StatusEffect"/> 通道（类比 STS2 RegenPower 在 AfterSideTurnEnd 中 heal+Decrement），
	/// 用 OnTick 在 Tick 衰减前触发一次伤害，Tick 自动 -1 回合数并归零移除。
	/// 决策见 AGENTS_LOCAL/pending：四夜雷电光是限时型（自动每回合衰减），不是永久 Power。
	/// </summary>
	private void MountShiyoruRaidenkou(CardEffectData effect)
	{
		const string statusId = "shiyoru_raidenkou";
		int turns = effect.SecondaryValue > 0 ? effect.SecondaryValue : 4;
		int damage = effect.Value > 0 ? effect.Value : 5;

		if (_playerHero.StatusEffects.TryGetValue(statusId, out var existing))
		{
			// 再次释放叠加回合数（取最后一个 effect 的伤害值——同 ID 假定数值一致）
			existing.Stacks += turns;
			GD.Print($"[CardEffectDispatcher] 四夜雷电光：剩余回合叠加至 {existing.Stacks}");
			return;
		}

		_playerHero.AddStatusEffect(new StatusEffect(
			statusId,
			turns,
			TickTiming.PlayerTurnEnd,
			StatusEffectPolarity.NonNegative)
		{
			OnTick = _ => TriggerShiyoruRaidenkou(damage),
		});
		GD.Print($"[CardEffectDispatcher] 四夜雷电光：挂载 {turns} 回合");
	}

	private void TriggerShiyoruRaidenkou(int damage)
	{
		if (_playerHero.IsDead)
			return;

		var candidates = new List<IDamageTarget>();
		foreach (var unit in _enemyUnits)
		{
			if (!unit.Body.IsDead)
				candidates.Add(unit.Body);
		}
		candidates.AddRange(_board.GetEnemyMinions().Where(m => !m.IsDead));
		if (candidates.Count == 0)
			return;

		using var rng = new RandomNumberGenerator();
		rng.Randomize();
		var target = candidates[rng.RandiRange(0, candidates.Count - 1)];

		if (target is Hero heroTarget)
		{
			_requestDamageVfx(_playerHero, heroTarget, DamageKind.Effect, CombatDamageVfxKind.Spell);
			heroTarget.TakeDamage(damage, _playerHero, DamageKind.Effect);
		}
		else if (target is Minion minionTarget)
		{
			_requestDamageVfx(_playerHero, minionTarget, DamageKind.Effect, CombatDamageVfxKind.Spell);
			minionTarget.TakeDamage(damage, _playerHero, DamageKind.Effect);
		}

		GD.Print($"[CardEffectDispatcher] 四夜雷电光触发：随机敌人受到 {damage} 点法术伤害");
		_notifyCombatStateChanged();
	}

	/// <summary>
	/// 星途精神：下回合开始时额外抽牌并获得法力（限时一次性挂载）。
	/// 改走 <see cref="StatusEffect"/> 通道（类比 STS2 DuplicationPower：AfterSideTurnEnd→效果+Remove），
	/// Stacks = 触发次数（可叠加），Tick 在玩家回合开始时消耗一层后自动移除。
	/// </summary>
	private void MountSutaraitoSpirit(CardEffectData effect)
	{
		const string statusId = "sutaraito_spirit";
		int drawCount = effect.SecondaryValue > 0 ? effect.SecondaryValue : 2;
		int manaGain = effect.Value > 0 ? effect.Value : 2;
		int activations = 1;

		if (_playerHero.StatusEffects.TryGetValue(statusId, out var existing))
		{
			existing.Stacks += activations;
			GD.Print($"[CardEffectDispatcher] 星途精神：下回合触发层数叠加至 {existing.Stacks}");
			return;
		}

		_playerHero.AddStatusEffect(new StatusEffect(
			statusId,
			activations,
			TickTiming.PlayerTurnStart,
			StatusEffectPolarity.NonNegative)
		{
			OnTick = e =>
			{
				int actualDraw = drawCount * e.Stacks;
				int actualMana = manaGain * e.Stacks;
				_playerHero.DrawCards(actualDraw);
				_playerHero.GainMana(actualMana);
				GD.Print($"[CardEffectDispatcher] 星途精神触发：抽 {actualDraw} 张牌，获得 {actualMana} 点法力");
				_notifyCombatStateChanged();
			},
		});
		GD.Print($"[CardEffectDispatcher] 星途精神：下回合开始触发 {activations} 次");
	}

	private void HandleCustomEffect(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
	{
		switch (effect.CustomEffectName)
		{
			case "AddPlanToHand":
				var planData = GD.Load<CardData>("res://Resources/Cards/Spell_Plan.tres");
				if (planData != null)
				{
					_playerCore.AddToHand(new Card.Card(planData));
					GD.Print("[CardEffectDispatcher] 将「计划」加入手牌");
				}
				else
				{
					GD.PrintErr("[CardEffectDispatcher] 无法加载计划卡牌资源");
				}
				break;

			case "FlyingAway":
				_playerHero.GainArmor(effect.Value);
				GD.Print($"[CardEffectDispatcher] 飞远：获得 {effect.Value} 点格挡（护甲）");
				break;

			case "StripArmor":
				if (target is Hero heroTarget)
				{
					int armorLost = heroTarget.CurrentArmor;
					heroTarget.RemoveArmor();
					GD.Print($"[CardEffectDispatcher] 移除目标所有护甲（失去 {armorLost} 点）");
				}
				else
				{
					GD.Print("[CardEffectDispatcher] StripArmor 目标无护甲（非英雄单位），无效果");
				}
				break;

			case "BaitTactics":
				if (target is Minion baitTarget)
				{
					baitTarget.GrantBaitTactics();
					GD.Print($"[CardEffectDispatcher] 诱饵战术：{baitTarget.CardName} 获得伏击、冲击与被攻击触发");
				}
				else
				{
					GD.Print("[CardEffectDispatcher] 诱饵战术需要有效的随从目标");
				}
				break;

			case "Animosity":
				if (target is Minion animosityTarget)
				{
					animosityTarget.HasTaunt = true;
					animosityTarget._damageModifiers.Add(new AnimosityDamageModifier());
					animosityTarget.AddDeathrattleEffect(new CardEffectData
					{
						EffectType = CardEffectType.DrawCards,
						Value = 1,
					});
					_notifyCombatStateChanged();
					GD.Print($"[CardEffectDispatcher] 敌意：{animosityTarget.CardName} 获得嘲讽、伤害翻倍（玩家阵营）和亡语抽牌");
				}
				else
				{
					GD.Print("[CardEffectDispatcher] 敌意需要有效的随从目标");
				}
				break;

			case "BladeCrisis":
				int maxDiscard = effect.Value > 0 ? effect.Value : 5;
				var hand = _playerHero.Hand.ToList();
				if (hand.Count == 0)
				{
					GD.Print("[CardEffectDispatcher] 刀盾危机：手牌为空");
					return;
				}

				_beginHandDiscardSelection(hand, 0, Math.Min(maxDiscard, hand.Count), true);
				GD.Print($"[CardEffectDispatcher] 刀盾危机：可选最多{Math.Min(maxDiscard, hand.Count)}张手牌弃掉");
				break;

			case "BloodDogsHandFill":
				if (visualSource is not Card.Card sourceSpell)
				{
					GD.PrintErr("[CardEffectDispatcher] 十万条吸血狗：无法识别当前法术牌");
					return;
				}

				_beginCopyHandFillSelection(sourceSpell);
				GD.Print("[CardEffectDispatcher] 十万条吸血狗：进入复制手牌选择");
				break;

			case "BoundlessDarkness":
				HandleBoundlessDarkness(effect, source);
				break;

			case "MambaMissile":
				HandleMambaMissile(effect, source, visualSource);
				break;

			case "ElbowStrike":
				HandleElbowStrike(effect, target, source, visualSource);
				break;

			case "ExplainEffect":
				HandleExplainEffect(effect, target);
				break;

			// ===== 新卡效果（2025-07-05） =====

			case "SmokeRestore":
				HandleSmokeRestore(effect);
				break;

			case "FragBullet":
				HandleFragBullet(effect, target, source, visualSource);
				break;

			case "SmokeDodge":
				HandleSmokeDodge(effect);
				break;

			case "NaDaoFangYu":
				HandleNaDaoFangYu(effect);
				break;

			default:
				GD.Print($"[CardEffectDispatcher] 未处理的Custom效果：{effect.CustomEffectName}");
				break;
		}
	}

	private static bool IsPlayerCaster(IDamageSource? source)
	{
		return source?.IsPlayerSide ?? true;
	}

	private IEnumerable<Hero> GetEnemyHeroesFor(IDamageSource? source)
	{
		if (IsPlayerCaster(source))
		{
			foreach (var unit in _enemyUnits)
			{
				if (!unit.Body.IsDead)
					yield return unit.Body;
			}

			yield break;
		}

		yield return _playerHero;
	}

	private List<Minion> GetEnemyMinionsFor(IDamageSource? source)
	{
		return IsPlayerCaster(source) ? _board.GetEnemyMinions() : _board.GetPlayerMinions();
	}

		private static TickTiming GetEnemyStatusTickTimingFor(IDamageSource? source)
		{
			return IsPlayerCaster(source) ? TickTiming.EnemyTurnEnd : TickTiming.PlayerTurnEnd;
		}

	private static TickTiming GetStatusTickTimingFor(Hero target)
	{
		return target.IsPlayerSide ? TickTiming.PlayerTurnEnd : TickTiming.EnemyTurnEnd;
	}

	private static TickTiming GetStatusTickTimingFor(Minion target)
	{
		return target.IsPlayerSide ? TickTiming.PlayerTurnEnd : TickTiming.EnemyTurnEnd;
	}

	private Hero? GetFriendlyHeroFor(IDamageSource? source)
	{
		if (IsPlayerCaster(source))
			return _playerHero;

		if (source is Hero sourceHero)
			return sourceHero;

		return _enemyUnits.FirstOrDefault(unit => !unit.Body.IsDead)?.Body;
	}

	private List<Minion> GetFriendlyMinionsFor(IDamageSource? source)
	{
		return IsPlayerCaster(source) ? _board.GetPlayerMinions() : _board.GetEnemyMinions();
	}

	private void HandleMambaMissile(CardEffectData effect, IDamageSource? source, object? visualSource)
	{
		int hitCount = effect.Value > 0 ? effect.Value : 5;
		int damage = effect.SecondaryValue > 0 ? effect.SecondaryValue : 3;
		using var rng = new RandomNumberGenerator();
		rng.Randomize();

		int actualHits = 0;
		for (int i = 0; i < hitCount; i++)
		{
			var target = SelectWeightedEnemyThreat(source, rng);
			if (target == null)
				break;

			_requestDamageVfx(visualSource, target, DamageKind.Effect, CombatDamageVfxKind.Spell);
			target.TakeDamage(damage, source, DamageKind.Effect);
			actualHits++;
		}

		GD.Print($"[CardEffectDispatcher] 曼巴导弹：发射 {actualHits}/{hitCount} 枚，每枚 {damage} 点伤害");
		_notifyCombatStateChanged();
	}

	private IDamageTarget? SelectWeightedEnemyThreat(IDamageSource? source, RandomNumberGenerator rng)
	{
		var candidates = new List<(IDamageTarget Target, int Weight)>();

		foreach (var enemyHero in GetEnemyHeroesFor(source))
			candidates.Add((enemyHero, GetHeroThreatWeight(enemyHero)));

		foreach (var minion in GetEnemyMinionsFor(source))
		{
			if (!minion.IsDead)
				candidates.Add((minion, Math.Max(1, minion.IsIncapacitated ? 1 : minion.Attack)));
		}

		if (candidates.Count == 0)
			return null;

		int totalWeight = candidates.Sum(candidate => candidate.Weight);
		int roll = rng.RandiRange(1, totalWeight);
		foreach (var (target, weight) in candidates)
		{
			roll -= weight;
			if (roll <= 0)
				return target;
		}

		return candidates[^1].Target;
	}

	private int GetHeroThreatWeight(Hero hero)
	{
		int attackThreat = Math.Max(0, ((IDamageSource)hero).BaseAttack);
		var combat = CombatManager.Instance;
		if (combat == null)
			return Math.Max(1, attackThreat);

		var unit = _enemyUnits.FirstOrDefault(enemy => enemy.Body == hero);
		if (unit == null)
			return Math.Max(1, attackThreat);

		var intent = unit.GetCurrentIntent(combat);
		int intentThreat = intent.Type switch
		{
			AiIntentType.Attack or AiIntentType.MultiAttack or AiIntentType.DeathBlow or AiIntentType.SpellCast => intent.GetEffectiveDamage(combat),
			_ => 0,
		};
		return Math.Max(1, attackThreat + intentThreat);
	}

	private void HandleElbowStrike(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
	{
		if (target is not IDamageTarget damageTarget)
		{
			GD.Print("[CardEffectDispatcher] 肘击需要有效目标");
			return;
		}

		int multiplier = effect.Value > 0 ? effect.Value : 1;
		int damage = _playerHero.CurrentArmor * multiplier;
		_requestDamageVfx(visualSource, damageTarget, DamageKind.Effect, CombatDamageVfxKind.Spell);
		damageTarget.TakeDamage(damage, source, DamageKind.Effect);
		GD.Print($"[CardEffectDispatcher] 肘击：按英雄格挡造成 {damage} 点伤害");
	}

	/// <summary>
	/// 「无边黑暗」——施放者的所有敌人获得易伤+脆弱。
	/// </summary>
	private void HandleBoundlessDarkness(CardEffectData effect, IDamageSource? source = null)
	{
		int stacks = Math.Max(1, effect.Value);
		TickTiming tickOn = GetEnemyStatusTickTimingFor(source);
		int heroCount = 0;
		int minionCount = 0;

		foreach (var hero in GetEnemyHeroesFor(source))
		{
			ApplyBoundlessDarknessStatus(hero, stacks, tickOn);
			heroCount++;
		}

		foreach (var minion in GetEnemyMinionsFor(source))
		{
			ApplyBoundlessDarknessStatus(minion, stacks, tickOn);
			minionCount++;
		}

		string sideLabel = IsPlayerCaster(source) ? "敌方" : "玩家方";
		GD.Print($"[CardEffectDispatcher] 无边黑暗：{sideLabel}全体目标获得 {stacks} 层易伤和脆弱（英雄 {heroCount}，随从 {minionCount}，合计 {heroCount + minionCount}）");
		_notifyCombatStateChanged();
	}

	private static void ApplyBoundlessDarknessStatus(IDamageTarget target, int stacks, TickTiming tickOn)
	{
		switch (target)
		{
			case Hero hero:
				hero.AddStatusEffect(new StatusEffect("vulnerable", stacks, tickOn));
				hero.AddStatusEffect(new StatusEffect("fragile", stacks, tickOn));
				break;
			case Minion minion:
				minion.AddStatusEffect(new StatusEffect("vulnerable", stacks, tickOn));
				minion.AddStatusEffect(new StatusEffect("fragile", stacks, tickOn));
				break;
		}
	}

	/// <summary>
	/// 「解释」——敌方英雄获得总观效应，此牌回到手牌并进入不可打出状态。
	/// </summary>
	private void HandleExplainEffect(CardEffectData effect, object? target)
	{
		// 找到敌方英雄
		Hero? enemyHero = null;
		// 通过 target 或从 CombatManager 获取（这里需要外部传入）
		// 由于 CardEffectDispatcher 没有直接引用敌方英雄，使用 target 参数
		if (target is Hero hero && !hero.IsPlayerSide)
			enemyHero = hero;

		if (enemyHero == null)
		{
			GD.Print("[CardEffectDispatcher] 解释需要敌方英雄作为目标");
			return;
		}

		int stacks = Math.Max(1, effect.Value);
		// 敌方英雄获得总观效应
		enemyHero.AddStatusEffect(new StatusEffect("total_observation", stacks, TickTiming.EnemyTurnEnd));
		GD.Print($"[CardEffectDispatcher] 解释：敌方英雄获得 {stacks} 层总观效应");

		// 将当前打出的卡牌返回手牌（通过 CardEffectDispatcher 无法直接访问当前卡牌）
		// 此逻辑由 CombatManager.PlaySpell 中的特殊处理完成
		GD.Print("[CardEffectDispatcher] 解释：此牌将返回手牌（由CombatManager处理）");
		_notifyCombatStateChanged();
	}

	// ===== 新卡效果（2025-07-05） =====

	/// <summary>烟雾恢复——友方带烟幕的单位 +4 生命值。</summary>
	private void HandleSmokeRestore(CardEffectData effect)
	{
		int amount = effect.Value > 0 ? effect.Value : 4;
		int healed = 0;

		// 玩家英雄+友方随从中有烟幕的
		if (_playerHero.HasSmokeScreen)
		{
			_playerHero.Heal(amount);
			healed++;
		}
		foreach (var minion in _board.GetPlayerMinions())
		{
			if (!minion.IsDead && minion.HasSmokeScreen)
			{
				minion.Heal(amount);
				healed++;
			}
		}
		GD.Print($"[CardEffectDispatcher] 烟雾恢复：{healed} 个带烟幕的友方单位恢复 {amount} 点生命值");
		_notifyCombatStateChanged();
	}

	/// <summary>碎片子弹——对目标造成 5 点伤害；若 HP<18% 挂致命裂痕（再受友方伤害后消灭，boss 免斩杀提示一次）。</summary>
	private void HandleFragBullet(CardEffectData effect, object? target, IDamageSource? source, object? visualSource)
	{
		int damage = effect.Value > 0 ? effect.Value : 5;
		int fatalThresholdPercent = effect.SecondaryValue > 0 ? effect.SecondaryValue : 18;

		// Step 1: 造成直伤
		ExecuteEffect(new CardEffectData { EffectType = CardEffectType.Damage, Value = damage }, target, source, visualSource);

		// Step 2: 检查 HP < 阈值 → 挂致命裂痕
		if (target is Hero heroTarget)
		{
			if (!heroTarget.IsDead && heroTarget.CurrentHealth > 0)
			{
				float ratio = (float)heroTarget.CurrentHealth / heroTarget.MaxHealth;
				if (ratio < fatalThresholdPercent / 100f)
				{
					ApplyFatalRift(heroTarget as IDamageTarget, heroTarget);
				}
			}
		}
		else if (target is Minion minionTarget)
		{
			if (!minionTarget.IsDead && minionTarget.CurrentHealth > 0)
			{
				float ratio = (float)minionTarget.CurrentHealth / minionTarget.MaxHealth;
				if (ratio < fatalThresholdPercent / 100f)
				{
					ApplyFatalRift(minionTarget, minionTarget);
				}
			}
		}
	}

	private bool _fatalRiftBossShownOnce;

	private void ApplyFatalRift(IDamageTarget target, object entity)
	{
		// ponytail: Boss 免疫斩杀——挂负面但不算消灭。
		// 通过 EnemyEncounter.IsBoss 标志检查。
		bool isBoss = false;
		if (entity is Hero h && !h.IsPlayerSide && _enemyUnits.Any(u => ReferenceEquals(u.Body, h) && u.Brain.IsBoss))
		{
			isBoss = true;
			if (!_fatalRiftBossShownOnce)
			{
				GD.Print("[CardEffectDispatcher] BOSS免疫斩杀——致命裂痕已挂为负面（无消灭效果）");
				_fatalRiftBossShownOnce = true;
			}
		}

		const string statusId = "fatal_rift";
		var se = new StatusEffect(statusId, 1, TickTiming.EnemyTurnEnd, StatusEffectPolarity.Negative);

		switch (entity)
		{
			case Hero heroEntity:
				heroEntity.AddStatusEffect(se);
				break;
			case Minion minionEntity:
				minionEntity.AddStatusEffect(se);
				break;
		}

		// 订阅伤害事件——每次受到友方伤害后检查消灭条件
		if (target is Minion m)
		{
			m.OnDamageTaken += (info, src) =>
			{
				if (src != null && src.IsPlayerSide && m.HasStatusEffect("fatal_rift") && !m.IsDead)
				{
					float r = (float)m.CurrentHealth / m.MaxHealth;
					if (r < 0.18f)
						TriggerFatalRiftElimination(m, m.CurrentHealth, m.MaxHealth, isBoss);
				}
			};
		}
		else if (target is Hero heroTarget && !heroTarget.IsPlayerSide)
		{
			heroTarget.OnDamageTaken += (info, src) =>
			{
				if (src != null && src.IsPlayerSide && heroTarget.HasStatusEffect("fatal_rift") && !heroTarget.IsDead)
				{
					float r = (float)heroTarget.CurrentHealth / heroTarget.MaxHealth;
					if (r < 0.18f)
						TriggerFatalRiftElimination(heroTarget, heroTarget.CurrentHealth, heroTarget.MaxHealth, isBoss);
				}
			};
		}

		if (!isBoss)
		{
			GD.Print($"[CardEffectDispatcher] 致命裂痕已挂；生命值低于阈值时受友方伤害后消灭");
		}
	}

	private void TriggerFatalRiftElimination(IDamageTarget fatalTarget, int currentHp, int maxHp,
		bool isBoss)
	{
		if (isBoss)
		{
			if (!_fatalRiftBossShownOnce)
			{
				GD.Print("[CardEffectDispatcher] BOSS免疫致命裂痕斩杀——负面已存在但消灭不生效");
				_fatalRiftBossShownOnce = true;
			}
			return;
		}

		GD.Print($"[CardEffectDispatcher] 致命裂痕触发消灭——目标生命值 {currentHp}/{maxHp} (<18%)");
		_requestDamageVfx(_playerHero, fatalTarget, DamageKind.Effect, CombatDamageVfxKind.Spell);
		fatalTarget.TakeDamage(9999, _playerHero, DamageKind.Effect);
		_notifyCombatStateChanged();
	}

	/// <summary>烟遁——所有友方单位获得 1 层烟幕，抽 1 张牌。</summary>
	private void HandleSmokeDodge(CardEffectData effect)
	{
		int stacks = effect.Value > 0 ? effect.Value : 1;
		int drawCount = effect.SecondaryValue > 0 ? effect.SecondaryValue : 1;
		int applied = 0;

		_playerHero.AddStatusEffect(new StatusEffect(
			StatusEffect.SmokescreenId, stacks, TickTiming.PlayerTurnEnd, StatusEffectPolarity.NonNegative));
		applied++;

		foreach (var minion in _board.GetPlayerMinions())
		{
			if (!minion.IsDead)
			{
				minion.AddStatusEffect(new StatusEffect(
					StatusEffect.SmokescreenId, stacks, TickTiming.PlayerTurnEnd, StatusEffectPolarity.NonNegative));
				applied++;
			}
		}

		_playerHero.DrawCards(drawCount);
		GD.Print($"[CardEffectDispatcher] 烟遁：{applied} 个友方单位获得 {stacks} 层烟幕，抽 {drawCount} 张牌");
		_notifyCombatStateChanged();
	}

	/// <summary>拿刀防卫——友方英雄 +3 攻击力直到下个敌方回合结束，抽 1 张牌；抉择：获得轮战 或 多抽 1 张。</summary>
	private void HandleNaDaoFangYu(CardEffectData effect)
	{
		int attackBonus = effect.Value > 0 ? effect.Value : 3;
		int drawCount = 1;

		// 临时攻击增益 modifier——加到英雄武器/法术伤害
		var attackMod = new NaDaoFangYuDamageModifier(attackBonus);
		_playerHero._damageModifiers.Add(attackMod);

		_playerHero.DrawCards(drawCount);
		GD.Print($"[CardEffectDispatcher] 拿刀防卫：英雄攻击+{attackBonus}，抽{drawCount}张牌——等待抉择");

		// 抉择：在本次获得轮战 或 多抽1张牌
		_beginChooseOne(
			"ui.choose_one.nadaofangyu",
			"抉择：",
			new[] { "获得轮战", "多抽1张牌" },
			new[] { "在本次获得轮战效果", "额外多抽1张牌" },
			(chosenIndex) =>
			{
				if (chosenIndex == 0)
				{
					GD.Print("[CardEffectDispatcher] 拿刀防卫：获得轮战");
					const string recycleId = "nadaofangyu_recycle";
					_playerHero.AddStatusEffect(new StatusEffect(recycleId, 1, TickTiming.EnemyTurnEnd, StatusEffectPolarity.NonNegative));
				}
				else if (chosenIndex == 1)
				{
					_playerHero.DrawCards(1);
					GD.Print("[CardEffectDispatcher] 拿刀防卫：多抽1张牌（共2张）");
				}
			});

		// 下个敌方回合结束后移除攻击增益
		const string atkBuffId = "nadaofangyu_atk_buff";
		_playerHero.AddStatusEffect(new StatusEffect(atkBuffId, 1, TickTiming.EnemyTurnEnd, StatusEffectPolarity.NonNegative)
		{
			OnTick = _ =>
			{
				_playerHero._damageModifiers.Remove(attackMod);
				GD.Print("[CardEffectDispatcher] 拿刀防卫：攻击增益已移除");
			},
		});
	}

	/// <summary>拿刀防卫专用伤害修改器——英雄造成的伤害加固定值。</summary>
	private sealed class NaDaoFangYuDamageModifier(int bonus) : IDamageModifier
	{
		public DamagePhase Phase => DamagePhase.ADDITIVE;

		public int ModifyDamageDealt(int currentDamage, DamageContext context)
		{
			if (context.Source is Hero h && h.IsPlayerSide)
				return currentDamage + bonus;
			return currentDamage;
		}

		public int ModifyDamageTaken(int currentDamage, DamageContext context) => currentDamage;
	}

	// === 工具方法 ===

	/// <summary>重置先发制人领域回合标记——由 DomainTriggerManager 每回合调用。</summary>
	public void ResetPreemptiveStrike() => _preemptiveStrikeConsumedThisTurn = false;

	private List<Card.Card> GetRandomCardsFromDiscard(int count)
	{
		var pool = _playerHero.DeckState.DiscardPile.ToList();
		if (pool.Count <= count)
			return pool;

		using var rng = new RandomNumberGenerator();
		rng.Randomize();
		for (int i = pool.Count - 1; i > 0; i--)
		{
			int j = rng.RandiRange(0, i);
			(pool[i], pool[j]) = (pool[j], pool[i]);
		}
		return pool.Take(count).ToList();
	}
}
