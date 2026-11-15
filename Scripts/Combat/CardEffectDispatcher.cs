#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using OdysseyCards.AI;
using OdysseyCards.Card;
using OdysseyCards.Core;
using GD = OdysseyCards.Core.ModelLog;

namespace OdysseyCards.Combat;

/// <summary>效果只接受明确的施法者上下文；不推测玩家、不保存全局的当前操作者。</summary>
internal sealed class CardEffectDispatcher(
	Hero playerHero, IReadOnlyList<EnemyUnit> enemyUnits, Board board, Action notify,
	Action<object?, IDamageTarget, DamageKind, CombatDamageVfxKind> requestVfx,
	IntelService intel, Func<string, CardData?> loadCard, Func<IReadOnlyList<CardData>> cardPool)
{
	private int _nadaoBuffSerial;

	public void ExecuteEffect(CardEffectData effect, object? target, EffectActorContext actor, Action? completed = null)
	{
		ArgumentNullException.ThrowIfNull(actor);
		var hero = actor.CasterHero;
		var core = actor.OwnerCore;
		int value = effect.Value;
		switch (effect.EffectType)
		{
			case CardEffectType.Damage:
			case CardEffectType.DealDamageToTarget:
				Damage(target as IDamageTarget, (int)(value * actor.SpellDamageMultiplier), actor);
				break;
			case CardEffectType.DealDamageToFriendlyHero:
				Damage(hero, value, actor);
				break;
			case CardEffectType.DealDamageToEnemyHero:
				Damage(target is Hero h && h.IsPlayerSide != actor.IsPlayerSide ? h : EnemyHeroes(actor).FirstOrDefault(), value, actor);
				break;
			case CardEffectType.DealDamageToAllEnemies:
				foreach (var minion in Enemies(actor))
					Damage(minion, value, actor);
				break;
			case CardEffectType.DealDamageToAllEnemiesAndHero:
				foreach (var enemy in EnemyTargets(actor))
					Damage(enemy, value, actor);
				break;
			case CardEffectType.DrawCards:
				hero.DrawCards(value);
				break;
			case CardEffectType.Intel:
				IReadOnlyList<Card.Card>? hand = actor.IsPlayerSide
					? enemyUnits.FirstOrDefault(unit => !unit.Body.IsDead && unit.Controller is IDeckDrivenCommander)?.Body.Hand
					: playerHero.Hand;
				intel.Resolve(hero, value, hand);
				break;
			case CardEffectType.Heal:
			case CardEffectType.RestoreHealth:
				if (actor.PlayedCard?.Data?.RequiresTarget == true && target is Minion healMinion)
					healMinion.Heal(value);
				else if (actor.PlayedCard?.Data?.RequiresTarget == true && target is Hero healHero)
					healHero.Heal(value);
				else
					hero.Heal(value);
				break;
			case CardEffectType.GainArmor:
				hero.GainArmor(value);
				break;
			case CardEffectType.GainMaxHealth:
				core.InitializeHealth(core.MaxHealth + value, core.CurrentHealth + value);
				break;
			case CardEffectType.GainEnergy:
				hero.GainMana(value);
				break;
			case CardEffectType.GainManaSlot:
				core.GainManaSlots(value);
				break;
			case CardEffectType.GainManaSlotAndMana:
				core.GainManaSlots(value, fillNewSlots: true);
				break;
			case CardEffectType.SummonMinion:
				Summon(effect.TargetType, Math.Max(1, value), actor);
				break;
			case CardEffectType.BuffMinion:
				if (target is Minion buff)
					buff.GainStats(value, effect.SecondaryValue);
				break;
			case CardEffectType.ReplaceDeathrattleWithDraw:
				if (target is Minion deathrattle)
					deathrattle.ReplaceDeathrattleEffects(new[] { new CardEffectData { EffectType = CardEffectType.DrawCards, Value = Math.Max(1, value) } });
				break;
			case CardEffectType.ApplyStatusToTarget:
				ApplyStatus(target, string.IsNullOrWhiteSpace(effect.TargetType) ? StatusEffect.IncapacitatedId : effect.TargetType, Math.Max(1, value));
				break;
			case CardEffectType.DrawByFriendlyMinions:
				hero.DrawCards(Friends(actor).Count + Math.Max(1, value));
				break;
			case CardEffectType.GainArmorToFriendlyBoard:
				foreach (var friend in Friends(actor))
					friend.GainArmor(value);
				hero.GainArmor(value * Math.Max(1, effect.SecondaryValue));
				break;
			case CardEffectType.DiscardRandom:
				foreach (var card in core.Hand.Where(card => card != actor.PlayedCard).OrderBy(_ => Random.Shared.Next()).Take(Math.Max(0, value)).ToArray())
					hero.DiscardCard(card);
				break;
			case CardEffectType.DiscardChoose:
			case CardEffectType.DiscardChooseUpTo:
				ChooseDiscard(actor, value, effect.EffectType == CardEffectType.DiscardChooseUpTo, false, completed);
				return;
			case CardEffectType.Discover:
				var options = cardPool().Where(data => data.Id != "spell_discover")
					.OrderBy(_ => Random.Shared.Next()).Take(Math.Max(1, value)).Select(data => new Card.Card(data)).ToArray();
				Choose(actor, CardDecisionKind.Discover, options, 1, 1, cards =>
				{
					foreach (var card in cards)
						core.AddToHand(card);
				}, completed, canCancel: true);
				return;
			case CardEffectType.ChooseFromDiscard:
				var discardOptions = core.DiscardPile.OrderBy(_ => Random.Shared.Next()).Take(value > 0 ? value : 5).ToArray();
				int picks = Math.Min(effect.SecondaryValue > 0 ? effect.SecondaryValue : 2, core.MaxHandSize - core.Hand.Count);
				Choose(actor, CardDecisionKind.RecoverDiscard, discardOptions, Math.Max(0, picks), Math.Max(0, picks), cards =>
				{
					foreach (var card in cards)
						hero.DeckState.MoveFromDiscardToHand(card);
				}, completed, canCancel: true);
				return;
			case CardEffectType.ShuffleTribeCards:
				if (Enum.TryParse<CardMechanicTag>(effect.TargetType, out var tag) && tag != CardMechanicTag.None)
				{
					var pool = cardPool().Where(data => data.Type == CardType.Minion && data.HasMechanicTag(tag)).ToArray();
					for (int i = 0; i < value && pool.Length > 0; i++)
						core.AddToDrawPileBottom(new Card.Card(pool[Random.Shared.Next(pool.Length)]));
					core.ShuffleDrawPile();
				}
				break;
			case CardEffectType.MountHeroEffect:
				HandleMountHeroEffect(effect, actor);
				break;
			case CardEffectType.Custom:
				if (HandleCustom(effect, target, actor, completed))
					return;
				break;
			case CardEffectType.GrantIdolTwilight:
			case CardEffectType.RemoveNaturalManaCap:
				break;
			default:
				GD.Print($"[CardEffectDispatcher] 未实现效果 {effect.EffectType}");
				break;
		}
		notify();
		completed?.Invoke();
	}

	private bool HandleCustom(CardEffectData effect, object? target, EffectActorContext actor, Action? completed)
	{
		var hero = actor.CasterHero;
		int value = effect.Value;
		switch (effect.CustomEffectName)
		{
			case "CopyToAdjacentSlot":
			case "Jiehuafa":
				break;
			case "AddPlanToHand":
				var plan = loadCard("res://Resources/Cards/Spell_Plan.tres");
				if (plan != null)
					actor.OwnerCore.AddToHand(new Card.Card(plan));
				break;
			case "FlyingAway":
				hero.GainArmor(value);
				break;
			case "StripArmor":
				if (target is Hero armoredHero)
					armoredHero.RemoveArmor();
				else if (target is Minion armoredMinion)
					armoredMinion.RemoveArmor();
				break;
			case "BaitTactics":
				if (target is Minion bait)
					bait.GrantBaitTactics();
				break;
			case "Animosity":
				if (target is Minion animosity)
				{
					animosity.HasTaunt = true;
					animosity._damageModifiers.Add(new RelativeAnimosityModifier(actor.IsPlayerSide));
					animosity.AddDeathrattleEffect(new CardEffectData { EffectType = CardEffectType.DrawCards, Value = 1 });
				}
				break;
			case "BladeCrisis":
				ChooseDiscard(actor, value > 0 ? value : 5, true, true, completed);
				return true;
			case "BloodDogsHandFill":
				Choose(actor, CardDecisionKind.CopyHandFill, hero.Hand.Where(card => card != actor.PlayedCard).ToArray(), 1, 1, cards =>
				{
					if (cards.Count == 0)
						return;
					while (hero.Hand.Count < actor.OwnerCore.MaxHandSize)
						actor.OwnerCore.AddToHand(new Card.Card(cards[0].Data));
				}, completed);
				return true;
			case "BoundlessDarkness":
				foreach (var enemy in EnemyTargets(actor))
				{
					ApplyStatus(enemy, "vulnerable", Math.Max(1, value));
					ApplyStatus(enemy, "fragile", Math.Max(1, value));
				}
				break;
			case "MambaMissile":
				for (int i = 0; i < (value > 0 ? value : 5); i++)
				{
					var targets = EnemyTargets(actor);
					if (targets.Count == 0)
						break;
					int roll = Random.Shared.Next(targets.Sum(Threat));
					var chosen = targets[^1];
					foreach (var candidate in targets)
					{
						roll -= Threat(candidate);
						if (roll < 0)
						{ chosen = candidate; break; }
					}
					Damage(chosen, effect.SecondaryValue > 0 ? effect.SecondaryValue : 3, actor);
				}
				break;
			case "ElbowStrike":
				Damage(target as IDamageTarget, hero.CurrentArmor * Math.Max(1, value), actor);
				break;
			case "ExplainEffect":
				if (target is Hero explained && explained.IsPlayerSide != actor.IsPlayerSide)
					ApplyStatus(explained, "total_observation", Math.Max(1, value));
				break;
			case "SmokeRestore":
				if (hero.HasSmokeScreen)
					hero.Heal(value > 0 ? value : 4);
				foreach (var friend in Friends(actor).Where(minion => minion.HasSmokeScreen))
					friend.Heal(value > 0 ? value : 4);
				break;
			case "SmokeDodge":
				ApplyStatus(hero, StatusEffect.SmokescreenId, Math.Max(1, value), actor.OwnTurnEnd, StatusEffectPolarity.NonNegative);
				foreach (var friend in Friends(actor).Where(minion => !minion.HasTaunt && !minion.HasGuard))
					ApplyStatus(friend, StatusEffect.SmokescreenId, Math.Max(1, value), actor.OwnTurnEnd, StatusEffectPolarity.NonNegative);
				hero.DrawCards(effect.SecondaryValue > 0 ? effect.SecondaryValue : 1);
				break;
			case "FragBullet":
				Damage(target as IDamageTarget, value > 0 ? value : 5, actor);
				ApplyFatalRift(target, actor, effect.SecondaryValue > 0 ? effect.SecondaryValue : 18);
				break;
			case "NaDaoFangYu":
				var modifier = new HeroAttackBonus(hero, value > 0 ? value : 3);
				hero._damageModifiers.Add(modifier);
				hero.DrawCards(1);
				HandleMountHeroEffect(new CardEffectData { CustomEffectName = "NaDaoExpiry" }, actor, modifier);
				actor.DecisionProvider.ChooseOption(new OptionDecisionRequest("ui.choose_one.nadaofangyu", "抉择：",
					new[] { Localization.Localization.T("ui.choose_one.recycle_option", "获得轮战"), Localization.Localization.T("ui.choose_one.draw_option", "多抽1张牌") },
					new[] { Localization.Localization.T("ui.choose_one.recycle_desc", "在本次获得轮战效果"), Localization.Localization.T("ui.choose_one.draw_desc", "额外多抽1张牌") },
					new double[] { 2, hero.DeckState.DrawPile.Count > 0 ? 3 : 0 }), index =>
					{
						if (index == 0 && actor.PlayedCard != null)
							actor.PlayedCard.GrantedRecycle = true;
						else if (index == 1)
							hero.DrawCards(1);
						notify();
						completed?.Invoke();
					});
				return true;
		}
		return false;
	}

	private void ChooseDiscard(EffectActorContext actor, int amount, bool optional, bool blade, Action? completed)
	{
		var options = actor.CasterHero.Hand.Where(card => card != actor.PlayedCard).ToArray();
		int max = Math.Min(Math.Max(0, amount), options.Length);
		Choose(actor, blade ? CardDecisionKind.BladeCrisis : CardDecisionKind.Discard,
			options, optional ? 0 : max, max, cards =>
			{
				foreach (var card in cards)
					actor.CasterHero.DiscardCard(card);
				if (blade)
				{
					Summon("res://Resources/Cards/Minion_WhatTheDogDoing.tres", cards.Count, actor);
					actor.CasterHero.DrawCards(cards.Count);
				}
			}, completed, canCancel: optional);
	}

	private void Choose(EffectActorContext actor, CardDecisionKind kind, IReadOnlyList<Card.Card> options,
		int min, int max, Action<IReadOnlyList<Card.Card>> resolve, Action? completed, bool canCancel = false)
	{
		max = Math.Clamp(max, 0, options.Count);
		min = Math.Clamp(min, 0, max);
		if (max == 0)
		{
			completed?.Invoke();
			return;
		}
		bool resolved = false;
		actor.DecisionProvider.ChooseCards(new CardDecisionRequest(actor.CasterHero, kind, options, min, max, canCancel), cards =>
		{
			if (resolved)
				return;
			resolved = true;
			resolve(cards.Distinct().Where(options.Contains).Take(max).ToArray());
			notify();
			completed?.Invoke();
		});
	}

	private void Summon(string id, int count, EffectActorContext actor)
	{
		var data = loadCard(id);
		if (data is not { IsMinion: true })
			return;
		for (int i = 0; i < count; i++)
		{
			int slot = board.GetEmptySlotIndex(actor.IsPlayerSide);
			if (slot < 0)
				break;
			board.PlaceMinion(new Minion(data, actor.IsPlayerSide) { OwnerHero = actor.CasterHero }, slot);
		}
	}

	/// <summary>限时挂载效果的唯一 OnTick 注入点；闭包捕获的是固定的施法英雄。</summary>
	private void HandleMountHeroEffect(CardEffectData effect, EffectActorContext actor, IDamageModifier? modifier = null)
	{
		var hero = actor.CasterHero;
		string id = effect.CustomEffectName switch
		{
			"ShiyoruRaidenkou" => "shiyoru_raidenkou",
			"SutaraitoSpiritNextTurn" => "sutaraito_spirit",
			"NaDaoExpiry" => $"nadaofangyu_atk_buff_{++_nadaoBuffSerial}",
			_ => "",
		};
		if (id.Length == 0)
			return;
		int stacks = id == "shiyoru_raidenkou" ? (effect.SecondaryValue > 0 ? effect.SecondaryValue : 4) : 1;
		if (hero.StatusEffects.TryGetValue(id, out var existing))
		{
			existing.Stacks += stacks;
			if (modifier != null)
				hero._damageModifiers.Remove(modifier);
			return;
		}
		TickTiming timing = id == "sutaraito_spirit" ? actor.OwnTurnStart : id == "shiyoru_raidenkou" ? actor.OwnTurnEnd : actor.OpponentTurnEnd;
		hero.AddStatusEffect(new StatusEffect(id, stacks, timing, StatusEffectPolarity.NonNegative)
		{
			OnTick = status =>
			{
				if (hero.IsDead)
					return;
				if (id == "shiyoru_raidenkou")
				{
					var targets = EnemyTargets(actor);
					if (targets.Count > 0)
						Damage(targets[Random.Shared.Next(targets.Count)], effect.Value > 0 ? effect.Value : 5, actor);
				}
				else if (id == "sutaraito_spirit")
				{
					hero.DrawCards((effect.SecondaryValue > 0 ? effect.SecondaryValue : 2) * status.Stacks);
					hero.GainMana((effect.Value > 0 ? effect.Value : 2) * status.Stacks);
					status.Stacks = 1;
				}
				else if (modifier != null)
					hero._damageModifiers.Remove(modifier);
				notify();
			},
		});
	}

	private void ApplyFatalRift(object? entity, EffectActorContext actor, int threshold)
	{
		if (entity is not IDamageTarget target || Health(target) <= 0 || Health(target) * 100L >= MaxHealth(target) * (long)threshold)
			return;
		ApplyStatus(entity, "fatal_rift", 1);
		bool boss = entity is Hero hero && enemyUnits.Any(unit => unit.Body == hero && unit.Brain.IsBoss);
		void OnDamage(DamageEventInfo info, IDamageSource? source)
		{
			bool active = entity is Hero h ? h.HasStatusEffect("fatal_rift") : ((Minion)entity).HasStatusEffect("fatal_rift");
			if (!boss && active && source?.IsPlayerSide == actor.IsPlayerSide && Health(target) > 0
				&& Health(target) * 100L < MaxHealth(target) * (long)threshold)
				Damage(target, 9999, actor);
		}
		if (entity is Hero h)
			h.OnDamageTaken += OnDamage;
		else if (entity is Minion minion)
			minion.OnDamageTaken += OnDamage;
	}

	private void Damage(IDamageTarget? target, int amount, EffectActorContext actor)
	{
		if (target == null)
			return;
		requestVfx(actor.PlayedCard ?? (object)actor.ActualDamageSource, target, DamageKind.Effect, CombatDamageVfxKind.Spell);
		target.TakeDamage(amount, actor.ActualDamageSource, DamageKind.Effect);
	}

	private static void ApplyStatus(object? target, string id, int stacks, TickTiming? timing = null,
		StatusEffectPolarity polarity = StatusEffectPolarity.Negative)
	{
		bool player = target is Hero h ? h.IsPlayerSide : target is Minion { IsPlayerSide: true };
		var status = new StatusEffect(id, stacks, timing ?? (player ? TickTiming.PlayerTurnEnd : TickTiming.EnemyTurnEnd), polarity);
		if (target is Hero hero)
			hero.AddStatusEffect(status);
		else if (target is Minion minion)
			minion.AddStatusEffect(status);
	}

	private List<Minion> Friends(EffectActorContext actor) => actor.IsPlayerSide ? board.GetPlayerMinions() : board.GetEnemyMinions();
	private List<Minion> Enemies(EffectActorContext actor) => actor.IsPlayerSide ? board.GetEnemyMinions() : board.GetPlayerMinions();
	private IEnumerable<Hero> EnemyHeroes(EffectActorContext actor) => actor.IsPlayerSide
		? enemyUnits.Where(unit => !unit.Body.IsDead).Select(unit => unit.Body) : new[] { playerHero }.Where(hero => !hero.IsDead);
	private List<IDamageTarget> EnemyTargets(EffectActorContext actor) => EnemyHeroes(actor).Cast<IDamageTarget>().Concat(Enemies(actor)).ToList();
	private static int Threat(IDamageTarget target) => Math.Max(1, target is Minion m ? m.Attack : ((Hero)target).BaseAttack);
	private static int Health(IDamageTarget target) => target is Hero h ? h.CurrentHealth : ((Minion)target).CurrentHealth;
	private static int MaxHealth(IDamageTarget target) => target is Hero h ? h.MaxHealth : ((Minion)target).MaxHealth;

	private sealed class HeroAttackBonus(Hero owner, int bonus) : IDamageModifier
	{
		public DamagePhase Phase => DamagePhase.ADDITIVE;
		public int ModifyDamageDealt(int damage, DamageContext context) => ReferenceEquals(context.Source, owner) && context.Kind == DamageKind.Attack ? damage + bonus : damage;
		public int ModifyDamageTaken(int damage, DamageContext context) => damage;
	}

	private sealed class RelativeAnimosityModifier(bool side) : IDamageModifier
	{
		public DamagePhase Phase => DamagePhase.MULTIPLICATIVE;
		public int ModifyDamageDealt(int damage, DamageContext context) => damage;
		public int ModifyDamageTaken(int damage, DamageContext context) => context.Source?.IsPlayerSide == side ? damage * 2 : damage;
	}
}
