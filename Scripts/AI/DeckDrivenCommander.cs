using System;
using System.Collections.Generic;
using System.Linq;
using OdysseyCards.AI.Intents;
using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Combat;
using OdysseyCards.Core;
using OdysseyCards.Roguelike;
using GD = OdysseyCards.Core.ModelLog;

namespace OdysseyCards.AI;

/// <summary>
/// 精英/Boss 主题卡组指挥官。
/// 身体、牌堆、手牌与法力全部驱动在 <see cref="EnemyUnit.Body"/> 的 CommanderCore 上，
/// 回合内用实时状态评分贪心序列执行动作，不做对象图深拷贝。
/// </summary>
public sealed class DeckDrivenCommander : EnemyEncounter, IDeckDrivenCommander
{
	private const int EliteHealth = 38;
	private const int BossHealth = 60;
	private readonly string[] _openingDomains;
	private bool _initialized;
	private bool _heroPowerUsedThisTurn;

	public string HeroId { get; }
	public string DisplayNameKey => $"enemy.commander.{HeroId}.name";
	public string TitleKey => $"enemy.commander.{HeroId}.title";
	public string PassiveKey => $"enemy.commander.{HeroId}.passive";
	public IDecisionProvider DecisionProvider { get; } = new AiDecisionProvider();

	public DeckDrivenCommander(string heroId, int maxHealth, params string[] openingDomains)
		: base(ResolveName(heroId), maxHealth, new[] { new EnemyIntent(IntentType.Buff, 0, "") })
	{
		HeroId = heroId;
		_openingDomains = openingDomains;
		MoveStates = new[] { new MoveState("COMMANDER", null, new UnknownIntent()) };
	}

	public static DeckDrivenCommander CreateElite(string heroId) => new(heroId, EliteHealth, OpeningDomainFor(heroId));
	public static DeckDrivenCommander CreateBoss(string heroId) => new(heroId, BossHealth, OpeningDomainFor(heroId));
	public static IReadOnlyList<string> CommanderHeroIds { get; } = new[] { "ayame", "rie", "sokou" };

	private static string OpeningDomainFor(string heroId) => heroId switch
	{
		"ayame" => "idol_twilight",
		"rie" => "infinite_fire",
		"sokou" => "jiehuafa",
		_ => "zhijian",
	};

	private static string ResolveName(string heroId)
	{
		string fallback = HeroProfile.Get(heroId).DisplayName;
		return Localization.Localization.T($"enemy.commander.{heroId}.name", $"宿敌·{fallback}");
	}

	public override Weapon CreateWeapon() => HeroProfile.Get(HeroId).CreateWeapon();

	/// <summary>指挥官不显示单步意图，显示实时战术摘要供 UI 兼容读取。</summary>
	public override EnemyIntent GetCurrentIntent(CombatManager combat, Hero self)
		=> new(IntentType.Buff, 0, BuildPlanSummary(combat, self));

	public override MoveState GetCurrentMove(CombatManager combat, Hero self) => MoveStates![0];
	public override void ExecuteIntent(CombatManager combat, Hero self) { }
	public override void AdvanceMove() { }
	public override void AdvanceIntent() { }

	public void InitializeDeck(Hero body, IReadOnlyList<CardData> pool, ThemeProfile profile, int seed)
	{
		if (_initialized || body == null || profile == null || pool == null || pool.Count == 0)
			return;
		_initialized = true;

		var generated = ThemedDeckGenerator.Generate(profile, pool, new Random(seed));
		var deck = new Deck { Name = $"{Name}·{profile.ThemeName}" };
		foreach (string id in generated.CardIds)
		{
			var data = pool.FirstOrDefault(card => card.Id == id);
			if (data != null)
				deck.AddCardInCombat(data);
		}

		body.Core.Deck = deck;
		body.Core.SetupDrawPile();
		body.Core.MaxHandSize = 10;
		body.Core.SetMana(0, 0);
		body.HeroPower = HeroProfile.Get(HeroId).CreateHeroPower();
		foreach (string domain in _openingDomains)
			body.AddDomain(domain, null);
		GD.Print($"[DeckDrivenCommander] {Name} 卡组 {deck.CardCount} 张，HP {body.MaxHealth}，开场领域 {string.Join(",", _openingDomains)}");
	}

	public void ExecuteTurn(CombatManager combat, EnemyUnit unit)
	{
		var body = unit.Body;
		_heroPowerUsedThisTurn = false;
		int guard = 0;
		while (guard++ < 80 && !combat.State.IsGameOver && !body.IsDead)
		{
			string before = Snapshot(combat, body);
			var best = Enumerate(combat, body).OrderByDescending(candidate => candidate.Score).FirstOrDefault();
			if (best == null || best.Score <= 0.01)
				break;
			GD.Print($"[DeckDrivenCommander] {Name} 执行 {best.Label}（评分 {best.Score:F1}，法力 {body.CurrentMana}/{body.MaxMana}）");
			best.Execute();
			combat.CheckDeaths();
			if (Snapshot(combat, body) == before)
				break;
		}
	}

	private static string Snapshot(CombatManager combat, Hero body)
		=> $"{body.CurrentMana}|{body.Hand.Count}|{body.DeckState.DrawPile.Count}|{body.DeckState.DiscardPile.Count}|"
			+ $"{combat.Board.GetAllMinions().Count()}|{combat.Board.FrontlineOwner}|"
			+ $"{combat.PlayerHero.CurrentHealth}|{combat.PlayerHero.CurrentArmor}";

	private List<Candidate> Enumerate(CombatManager combat, Hero body)
	{
		var result = new List<Candidate>();
		var actor = combat.CreateActorContext(body, body);
		var playerHq = combat.PlayerHero;
		int lethalThreshold = playerHq.CurrentHealth + playerHq.CurrentArmor;
		var friends = combat.Board.GetAllMinions().Where(minion => !minion.IsDead && minion.IsPlayerSide == body.IsPlayerSide).ToList();

		// 斩杀与随从攻击
		foreach (var minion in friends)
		{
			if (minion.Attack <= 0)
				continue;
			foreach (var target in combat.GetCommanderAttackTargets(body, minion))
			{
				double score = ScoreAttack(minion, target, lethalThreshold);
				if (score <= 0)
					continue;
				var attacker = minion;
				var victim = target;
				result.Add(new Candidate($"随从攻击 {attacker.CardName}→{Describe(victim)}", score,
					() => combat.CommanderAttack(body, attacker, victim)));
			}
		}

		// 推进前线
		foreach (var minion in friends)
		{
			if (!combat.CanCommanderMove(body, minion))
				continue;
			double score = ScoreMove(combat.Board, body, minion);
			if (score <= 0)
				continue;
			var mover = minion;
			result.Add(new Candidate($"推进 {mover.CardName}", score, () => combat.CommanderMove(body, mover)));
		}

		// 武器平A
		if (body.Weapon is { Attack: > 0 } weapon && body.CanWeaponAttack()
			&& body.CurrentMana >= weapon.AttackCost)
		{
			foreach (var target in combat.GetCommanderWeaponTargets(body))
			{
				double score = ScoreWeaponAttack(body, target, lethalThreshold);
				if (score <= 0)
					continue;
				var victim = target;
				result.Add(new Candidate($"武器攻击→{Describe(victim)}", score,
					() => combat.CommanderWeaponAttack(body, victim)));
			}
		}

		// 武器主动技
		if (body.Weapon?.ActiveSkill is { } active && active.CanUse(body))
		{
			if (!active.RequiresTarget)
			{
				result.Add(new Candidate($"武器主动「{active.Name}」", 8 + active.Cost * 0.2,
					() => combat.CommanderWeaponActive(body, null)));
			}
			else
			{
				foreach (var target in combat.GetOpponentTargets(body.IsPlayerSide))
				{
					double score = 6 + (target is Minion m ? m.Attack * 0.6 + (m.UnitClass is UnitClass.Support or UnitClass.Skirmisher ? 3 : 0) : 1);
					var victim = target;
					result.Add(new Candidate($"武器主动「{active.Name}」→{Describe(victim)}", score,
						() => combat.CommanderWeaponActive(body, victim)));
				}
			}
		}

		// 英雄技能
		if (!_heroPowerUsedThisTurn && body.HeroPower is { } power && power.CanUse(body))
		{
			double score = ScoreHeroPower(body, power);
			if (score > 0)
				result.Add(new Candidate($"英雄技能「{power.Name}」", score,
					() =>
					{
						if (combat.CommanderUseHeroPower(body))
							_heroPowerUsedThisTurn = true;
					}));
		}

		// 出牌
		foreach (var card in body.Hand.ToList())
		{
			if (card.HasUnplayable || !combat.CanCommanderPlay(actor, card))
				continue;

			if (card.Type == CardType.Minion)
			{
				int slot = combat.Board.GetEmptySlotIndex(body.IsPlayerSide);
				if (slot < 0)
					continue;
				double score = 10 + card.Data.Attack * 1.4 + card.Data.Health * 0.9
					+ (card.Data.HasKeyword(Keyword.Charge) ? 4 : 0)
					- card.Cost * 1.1;
				if (body.CurrentMana == card.Cost)
					score += 1.5;
				var played = card;
				result.Add(new Candidate($"部署 {card.GetLocalizedName()}（{card.Cost}费）", score,
					() => combat.PlayMinion(actor, played, slot)));
			}
			else if (card.Type == CardType.Domain)
			{
				string domainId = string.IsNullOrEmpty(card.Data.DomainId) ? card.Data.Id : card.Data.DomainId;
				double score = 16 - card.Cost * 1.2;
				if (body.HasDomain(domainId))
					score -= 9;
				var played = card;
				result.Add(new Candidate($"展开领域 {card.GetLocalizedName()}", score,
					() => combat.PlayDomain(actor, played)));
			}
			else if (card.Type is CardType.Spell or CardType.Status)
			{
				var (target, targetScore) = ChooseSpellTarget(combat, body, actor, card);
				if (card.Data.RequiresTarget && target == null)
					continue;
				double score = ScoreSpell(combat, body, card, target, targetScore, lethalThreshold);
				if (score <= 0)
					continue;
				var played = card;
				var chosen = target;
				result.Add(new Candidate($"施放 {card.GetLocalizedName()}（{card.Cost}费）", score,
					() => combat.PlaySpell(actor, played, chosen)));
			}
		}

		return result;
	}

	private (object? Target, double Score) ChooseSpellTarget(CombatManager combat, Hero body,
		EffectActorContext actor, Card.Card card)
	{
		object? bestTarget = null;
		double bestScore = double.MinValue;
		var candidates = combat.GetOpponentTargets(body.IsPlayerSide)
			.Concat(combat.GetFriendlyTargets(body.IsPlayerSide));
		foreach (var target in candidates)
		{
			if (target is not IDamageTarget)
				continue;
			if (!combat.ValidateTarget(actor, card, target))
				continue;
			double score = ScoreTargetForCard(combat, body, card, (IDamageTarget)target);
			if (score > bestScore)
			{
				bestScore = score;
				bestTarget = target;
			}
		}
		return (bestTarget, bestScore);
	}

	private static double ScoreTargetForCard(CombatManager combat, Hero body, Card.Card card, IDamageTarget target)
	{
		double score = 0;
		bool friendly = target is Hero hero ? hero.IsPlayerSide == body.IsPlayerSide
			: ((Minion)target).IsPlayerSide == body.IsPlayerSide;
		foreach (var effect in card.Data.Effects)
		{
			// 只有真正读取 target 的效果才参与目标评分；抽牌/回蓝/召唤/挂载等自身效果不偏置目标。
			bool targetDamage = effect.EffectType is CardEffectType.Damage or CardEffectType.DealDamageToTarget;
			bool targetStatus = effect.EffectType == CardEffectType.ApplyStatusToTarget;
			bool targetHeal = effect.EffectType is CardEffectType.Heal or CardEffectType.RestoreHealth;
			bool targetBuff = effect.EffectType == CardEffectType.BuffMinion;

			if (targetDamage || targetStatus)
			{
				if (friendly)
				{
					score -= 8;
					continue;
				}
				score += effect.Value * 1.4;
				if (target is Minion enemyMinion)
				{
					int dealt = DamageResolver.ResolvePreviewDamage(effect.Value, body, enemyMinion);
					score += enemyMinion.Attack * 0.8;
					if (dealt >= enemyMinion.CurrentHealth + enemyMinion.CurrentArmor)
						score += 9 + enemyMinion.Attack * 1.5;
					if (enemyMinion.UnitClass is UnitClass.Support or UnitClass.Skirmisher)
						score += 3;
				}
				else if (target is Hero enemyHero)
				{
					if (effect.Value >= enemyHero.CurrentHealth + enemyHero.CurrentArmor)
						score += 120;
					score += 2;
				}
			}
			else if (targetHeal || targetBuff)
			{
				if (!friendly)
				{
					score -= 6;
					continue;
				}
				if (target is Minion friendlyMinion)
					score += 2 + friendlyMinion.Attack * 0.3;
				else if (target is Hero friendlyHero)
					score += friendlyHero.MaxHealth - friendlyHero.CurrentHealth > 0 ? 4 : 0;
			}
			else if (effect.EffectType == CardEffectType.Custom)
			{
				score += ScoreCustomTarget(effect, target, friendly);
			}
		}
		return score;
	}

	private static double ScoreCustomTarget(CardEffectData effect, IDamageTarget target, bool friendly)
	{
		bool enemyMinion = !friendly && target is Minion;
		return effect.CustomEffectName switch
		{
			"ElbowStrike" or "FragBullet" or "StripArmor" => friendly ? -6 : (enemyMinion ? 8 : 3),
			"ExplainEffect" => friendly ? -8 : 3,
			"BaitTactics" or "Animosity" => friendly ? 5 : -5,
			_ => 0,
		};
	}

	private double ScoreSpell(CombatManager combat, Hero body, Card.Card card, object? target,
		double targetScore, int lethalThreshold)
	{
		double score = 4 - card.Cost * 0.7;
		int enemyMinions = combat.Board.GetPlayerMinions().Count;
		int hqDamage = 0;
		foreach (var effect in card.Data.Effects)
		{
			switch (effect.EffectType)
			{
				case CardEffectType.Damage:
				case CardEffectType.DealDamageToTarget:
					score += effect.Value * 1.5;
					if (target is Hero hero && hero.IsPlayerSide)
						hqDamage += effect.Value;
					break;
				case CardEffectType.DealDamageToEnemyHero:
					hqDamage += effect.Value;
					score += effect.Value * 1.5;
					break;
				case CardEffectType.DealDamageToAllEnemies:
					score += effect.Value * enemyMinions * 1.1;
					break;
				case CardEffectType.DealDamageToAllEnemiesAndHero:
					score += effect.Value * (enemyMinions + 1) * 1.2;
					hqDamage += effect.Value;
					break;
				case CardEffectType.DrawCards:
					score += effect.Value * 2.4;
					break;
				case CardEffectType.Heal:
				case CardEffectType.RestoreHealth:
					score += Math.Min(effect.Value, body.MaxHealth - body.CurrentHealth) * 1.2;
					break;
				case CardEffectType.GainArmor:
					score += effect.Value * 1.1;
					break;
				case CardEffectType.GainMaxHealth:
					score += effect.Value * 2;
					break;
				case CardEffectType.DealDamageToFriendlyHero:
					score -= effect.Value * 3;
					break;
				case CardEffectType.SummonMinion:
					score += 10 * Math.Max(1, effect.Value);
					break;
				case CardEffectType.BuffMinion:
					score += 5;
					break;
				case CardEffectType.Intel:
					score += 4 + effect.Value * 1.5;
					break;
				case CardEffectType.Discover:
					score += 6;
					break;
				case CardEffectType.ChooseFromDiscard:
					score += 4;
					break;
				case CardEffectType.MountHeroEffect:
					score += 7;
					break;
				case CardEffectType.ApplyStatusToTarget:
					score += effect.Value * 2;
					break;
				case CardEffectType.GainManaSlot:
				case CardEffectType.GainManaSlotAndMana:
					score += effect.Value * 3;
					break;
				case CardEffectType.Custom:
					score += ScoreCustomSpell(body, effect, target);
					break;
			}
		}

		if (hqDamage >= lethalThreshold)
			score += 500;
		if (target != null)
			score += Math.Max(-6, Math.Min(10, targetScore * 0.35));
		return score;
	}

	private static double ScoreCustomSpell(Hero body, CardEffectData effect, object? target)
	{
		bool friendlyTarget = target is Hero hero ? hero.IsPlayerSide == body.IsPlayerSide
			: target is Minion minion && minion.IsPlayerSide == body.IsPlayerSide;
		return effect.CustomEffectName switch
		{
			"BladeCrisis" => 6 + body.Hand.Count * 0.5,
			"BloodDogsHandFill" => 9,
			"BoundlessDarkness" => 8,
			"MambaMissile" => effect.Value * 2.2,
			"ElbowStrike" => body.CurrentArmor * 1.4,
			"FragBullet" => effect.Value * 1.8,
			"SmokeRestore" => 3,
			"SmokeDodge" => 6,
			"NaDaoFangYu" => 6,
			"Animosity" => friendlyTarget ? 5 : -5,
			"BaitTactics" => friendlyTarget ? 5 : -5,
			"ExplainEffect" => friendlyTarget ? -8 : 3,
			"StripArmor" => friendlyTarget ? -4 : 4,
			"FlyingAway" => 4,
			"AddPlanToHand" => 4,
			_ => 0,
		};
	}

	internal static double ScoreAttack(Minion attacker, IDamageTarget target, int lethalThreshold)
	{
		if (target is Hero hero)
		{
			if (hero.IsPlayerSide == attacker.IsPlayerSide)
				return -1;
			int hqDamage = DamageResolver.ResolvePreviewDamage(attacker.Attack, attacker, hero);
			return hqDamage >= lethalThreshold ? 9000 : hqDamage * 2.4 + 1;
		}

		var defender = (Minion)target;
		if (defender.IsPlayerSide == attacker.IsPlayerSide)
			return -1;
		int dealt = DamageResolver.ResolvePreviewDamage(attacker.Attack, attacker, defender);
		bool kills = dealt >= defender.CurrentHealth + defender.CurrentArmor;
		int counter = defender.IsIncapacitated || attacker.UnitClass == UnitClass.Support
			? 0 : DamageResolver.ResolvePreviewDamage(defender.Attack, defender, attacker);
		bool dies = counter >= attacker.CurrentHealth + attacker.CurrentArmor;
		double score = dealt * 1.4;
		if (kills)
			score += 11 + defender.Attack * 1.8 + defender.MaxHealth * 0.6;
		if (dies)
			score -= 9 + attacker.Attack * 1.8 + attacker.MaxHealth * 0.6;
		if (defender.UnitClass is UnitClass.Support or UnitClass.Skirmisher)
			score += 5;
		if (defender.HasTaunt)
			score += 2;
		return score;
	}

	internal static double ScoreWeaponAttack(Hero body, IDamageTarget target, int lethalThreshold)
	{
		if (body.Weapon == null)
			return -1;
		int damage = body.Weapon.GetModifiedDamage(body.Weapon.Attack, body);
		if (target is Hero hero)
		{
			if (hero.IsPlayerSide == body.IsPlayerSide)
				return -1;
			int dealt = DamageResolver.ResolvePreviewDamage(damage, body, hero);
			return dealt >= lethalThreshold ? 9000 : dealt * 2.2 + 1;
		}
		var defender = (Minion)target;
		if (defender.IsPlayerSide == body.IsPlayerSide)
			return -1;
		int hit = DamageResolver.ResolvePreviewDamage(damage, body, defender);
		bool kills = hit >= defender.CurrentHealth + defender.CurrentArmor;
		int counter = defender.IsIncapacitated || body.Weapon.IgnoreCounterAttack
			? 0 : DamageResolver.ResolvePreviewDamage(defender.Attack, defender, body);
		double score = hit * 1.4;
		if (kills)
			score += 10 + defender.Attack * 1.5;
		if (counter >= body.CurrentHealth + body.CurrentArmor)
			score -= 25;
		if (defender.UnitClass is UnitClass.Support or UnitClass.Skirmisher)
			score += 4;
		return score;
	}

	internal static double ScoreMove(Board board, Hero body, Minion minion)
	{
		FrontlineOwner own = body.IsPlayerSide ? FrontlineOwner.Player : FrontlineOwner.Enemy;
		if (board.FrontlineOwner == FrontlineOwner.None)
			return 14 + minion.Attack * 1.2 + (minion.UnitClass == UnitClass.Assault ? 4 : 0);
		if (board.FrontlineOwner == own)
			return 5 + minion.Attack * 0.4;
		return 0;
	}

	private static double ScoreHeroPower(Hero body, IHeroPower power)
	{
		double score = 5 - power.Cost * 0.4;
		if (body.MaxHealth - body.CurrentHealth > 0)
			score += 2;
		return score;
	}

	private string BuildPlanSummary(CombatManager combat, Hero self)
	{
		int threats = combat.Board.GetPlayerMinions().Count;
		int frontline = combat.Board.GetAllMinions().Count(minion =>
			!minion.IsDead && minion.IsPlayerSide == self.IsPlayerSide && minion.BoardRow == BoardRow.Frontline);
		string title = Localization.Localization.T(TitleKey, "主题卡组指挥官");
		return $"{title}：手牌 {self.Hand.Count}，法力 {self.CurrentMana}/{self.MaxMana}，前线 {frontline}，威胁 {threats}";
	}

	private static string Describe(IDamageTarget target) => target switch
	{
		Hero hero => hero.IsPlayerSide ? "玩家HQ" : "敌方HQ",
		Minion minion => minion.GetLocalizedName(),
		_ => "目标",
	};

	private sealed record Candidate(string Label, double Score, Action Execute);
}
