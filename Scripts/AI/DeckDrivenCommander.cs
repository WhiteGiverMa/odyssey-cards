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
/// 通用同构牌手（卡组驱动）。
///
/// 普通敌人、精英与 Boss 在机制上完全同构：都持有真实卡组、抽牌堆、手牌与法力，
/// 回合内用实时状态评分贪心序列抽牌、出随从、打法术、推进前线、攻击。
/// 主要区分维度只有 <see cref="EnemyEncounter.MaxHealth"/> 与 <see cref="TargetDeckSize"/>：
///   普通 10~20 张 / 精英 21~39 张 / Boss 40 张。
///
/// 身体、牌堆、手牌与法力全部驱动在 <see cref="EnemyUnit.Body"/> 的 CommanderCore 上，
/// 不做对象图深拷贝。
/// </summary>
public sealed class DeckDrivenCommander : EnemyEncounter, IDeckDrivenCommander
{
	private const int EliteHealth = 38;
	private const int BossHealth = 60;
	private readonly string[] _openingDomains;
	private readonly Func<Weapon>? _weaponFactory;
	private readonly Func<IHeroPower>? _heroPowerFactory;
	private readonly IReadOnlyList<string>? _coreCardOverrides;
	private readonly IReadOnlyDictionary<CardMechanicTag, int>? _tagWeightOverrides;
	private readonly IReadOnlyDictionary<Keyword, int>? _keywordWeightOverrides;
	private readonly int _startingDefense;
	private bool _initialized;
	private bool _heroPowerUsedThisTurn;

	/// <summary>牌手 ID（同时是本地化 key 与调试标识）。</summary>
	public string HeroId { get; }

	/// <summary>显示名本地化 key。</summary>
	public string DisplayNameKey { get; }

	/// <summary>头衔本地化 key。</summary>
	public string TitleKey { get; }

	/// <summary>被动本地化 key。</summary>
	public string PassiveKey { get; }

	/// <summary>头衔中文回退。</summary>
	public string TitleFallback { get; }

	/// <summary>目标卡组厚度（普通 10~20 / 精英 21~39 / Boss 40）。</summary>
	public int TargetDeckSize { get; }

	/// <summary>该牌手自带的卡组画像；null 表示由 CombatManager 回退加载主题资源。</summary>
	public DeckProfile? DeckProfile { get; }

	/// <summary>单次受到生命伤害的上限（固璋(3) 等被动）；null 表示无上限。</summary>
	public int? DamageCap { get; }

	/// <summary>「不破」被动——每回合仅受一次生命伤害。</summary>
	public bool Unbreakable { get; }

	/// <summary>定位标签（normal / elite / boss），仅用于日志与 QA。</summary>
	public string Role { get; }

	public IDecisionProvider DecisionProvider { get; } = new AiDecisionProvider();

	/// <summary>显示名——优先本地化，缺失时回退到构造时的中文名。</summary>
	public override string DisplayName => Localization.Localization.T(DisplayNameKey, Name);

	public DeckDrivenCommander(DeckDrivenCommanderSpec spec)
		: base(
			(spec ?? throw new ArgumentNullException(nameof(spec))).DisplayName,
			spec.MaxHealth,
			new[] { new EnemyIntent(IntentType.Buff, 0, "") })
	{
		HeroId = spec.Id;
		DisplayNameKey = string.IsNullOrEmpty(spec.DisplayNameKey) ? $"enemy.deckhand.{spec.Id}.name" : spec.DisplayNameKey;
		TitleKey = string.IsNullOrEmpty(spec.TitleKey) ? $"enemy.deckhand.{spec.Id}.title" : spec.TitleKey;
		PassiveKey = string.IsNullOrEmpty(spec.PassiveKey) ? $"enemy.deckhand.{spec.Id}.passive" : spec.PassiveKey;
		TitleFallback = string.IsNullOrEmpty(spec.TitleFallback) ? "主题卡组牌手" : spec.TitleFallback;
		TargetDeckSize = Math.Max(1, spec.TargetDeckSize);
		DeckProfile = spec.Profile;
		DamageCap = spec.DamageCap;
		Unbreakable = spec.Unbreakable;
		Role = spec.Role;
		_startingDefense = spec.StartingDefense;
		_openingDomains = spec.OpeningDomains ?? Array.Empty<string>();
		_weaponFactory = spec.WeaponFactory;
		_heroPowerFactory = spec.HeroPowerFactory;
		_coreCardOverrides = spec.CoreCardOverrides;
		_tagWeightOverrides = spec.TagWeightOverrides;
		_keywordWeightOverrides = spec.KeywordWeightOverrides;
		MoveStates = new[] { new MoveState("DECKHAND", null, new UnknownIntent()) };
	}

	/// <summary>创建精英宿敌少女（38 HP / 28 张主题卡组）。</summary>
	public static DeckDrivenCommander CreateElite(string heroId) => new(new DeckDrivenCommanderSpec
	{
		Id = heroId,
		DisplayName = $"宿敌·{HeroProfile.Get(heroId).DisplayName}",
		DisplayNameKey = $"enemy.commander.{heroId}.name",
		TitleKey = $"enemy.commander.{heroId}.title",
		TitleFallback = "宿敌指挥官",
		PassiveKey = $"enemy.commander.{heroId}.passive",
		MaxHealth = EliteHealth,
		TargetDeckSize = 28,
		OpeningDomains = new[] { OpeningDomainFor(heroId) },
		WeaponFactory = () => HeroProfile.Get(heroId).CreateWeapon(),
		HeroPowerFactory = () => HeroProfile.Get(heroId).CreateHeroPower(),
		Role = "elite",
	});

	/// <summary>创建 Boss 宿敌少女（60 HP / 40 张主题卡组）。</summary>
	public static DeckDrivenCommander CreateBoss(string heroId) => new(new DeckDrivenCommanderSpec
	{
		Id = heroId,
		DisplayName = $"宿敌·{HeroProfile.Get(heroId).DisplayName}",
		DisplayNameKey = $"enemy.commander.{heroId}.name",
		TitleKey = $"enemy.commander.{heroId}.title",
		TitleFallback = "宿敌指挥官",
		PassiveKey = $"enemy.commander.{heroId}.passive",
		MaxHealth = BossHealth,
		TargetDeckSize = 40,
		OpeningDomains = new[] { OpeningDomainFor(heroId) },
		WeaponFactory = () => HeroProfile.Get(heroId).CreateWeapon(),
		HeroPowerFactory = () => HeroProfile.Get(heroId).CreateHeroPower(),
		Role = "boss",
	});

	public static IReadOnlyList<string> CommanderHeroIds { get; } = new[] { "ayame", "rie", "sokou" };

	private static string OpeningDomainFor(string heroId) => heroId switch
	{
		"ayame" => "idol_twilight",
		// 0.2.3：无限火力移入预备包，理惠改开「先发制人」（直伤核心领域）。
		"rie" => "preemptive_strike",
		// 0.2.3：接化发移除，溯光改开「飞远」（同属受击给「受身」体系）。
		"sokou" => "flying_away",
		_ => "zhijian",
	};

	/// <summary>开局防御修正（劫蛋者等负防御牌手）。</summary>
	public override int StartingDefense => _startingDefense;

	public override Weapon CreateWeapon() => _weaponFactory?.Invoke() ?? new RollingLog();

	/// <summary>牌手不显示单步意图，显示实时战术摘要供 UI 兼容读取。</summary>
	public override EnemyIntent GetCurrentIntent(CombatManager combat, Hero self)
		=> new(IntentType.Buff, 0, BuildPlanSummary(combat, self));

	public override MoveState GetCurrentMove(CombatManager combat, Hero self) => MoveStates![0];
	public override void ExecuteIntent(CombatManager combat, Hero self) { }
	public override void AdvanceMove() { }
	public override void AdvanceIntent() { }

	public void InitializeDeck(Hero body, IReadOnlyList<CardData> pool, DeckProfile profile, int seed)
	{
		if (_initialized || body == null || pool == null || pool.Count == 0)
			return;

		// 优先级：牌手自带画像 > 外部（CombatManager 加载的主题资源）> 通用兜底画像。
		var source = DeckProfile ?? profile ?? new DeckProfile
		{
			HeroId = HeroId,
			ThemeName = "通用牌组",
			TargetDeckSize = TargetDeckSize,
		};

		_initialized = true;
		var effective = ResolveEffectiveProfile(source);
		var generated = ThemedDeckGenerator.Generate(effective, pool, new Random(seed));
		var deck = new Deck { Name = $"{Name}·{effective.ThemeName}" };
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
		body.HeroPower = _heroPowerFactory?.Invoke();
		foreach (string domain in _openingDomains)
			body.AddDomain(domain, null);
		GD.Print($"[DeckDrivenCommander] {Name}（{Role}）卡组 {deck.CardCount} 张，HP {body.MaxHealth}，"
			+ $"开场领域 {(_openingDomains.Length == 0 ? "无" : string.Join(",", _openingDomains))}");
	}

	/// <summary>
	/// 把外来画像（主题资源）按牌手规格改写成最终卡组配置。纯 C#，可单测。
	/// </summary>
	internal DeckProfile ResolveEffectiveProfile(DeckProfile source)
	{
		var effective = source.WithTargetDeckSize(TargetDeckSize);
		if (_coreCardOverrides != null)
			effective = effective.WithCoreCards(_coreCardOverrides);
		if (_tagWeightOverrides != null)
			effective = effective.WithTagWeights(_tagWeightOverrides);
		if (_keywordWeightOverrides != null)
			effective = effective.WithKeywordWeights(_keywordWeightOverrides);
		return effective;
	}

	public EnemyStepKind LastActionKind { get; private set; } = EnemyStepKind.TurnPrepare;

	/// <summary>重置每个指挥官回合的一次性资源标记。</summary>
	public void BeginTurn(Hero body)
	{
		_heroPowerUsedThisTurn = false;
		LastActionKind = EnemyStepKind.TurnPrepare;
	}

	/// <summary>执行当前评分最高的一个动作；与原同步贪心循环共用完全相同的候选与结算。</summary>
	public bool TryExecuteSingleBestAction(CombatManager combat, EnemyUnit unit)
	{
		var body = unit.Body;
		if (combat.State.IsGameOver || body.IsDead)
			return false;
		string before = Snapshot(combat, body);
		var best = Enumerate(combat, body).OrderByDescending(candidate => candidate.Score).FirstOrDefault();
		if (best == null || best.Score <= 0.01)
			return false;
		GD.Print($"[DeckDrivenCommander] {Name} 执行 {best.Label}（评分 {best.Score:F1}，法力 {body.CurrentMana}/{body.MaxMana}）");
		LastActionKind = best.StepKind;
		best.Execute();
		combat.CheckDeaths();
		return Snapshot(combat, body) != before;
	}

	/// <summary>回合末静默激活富余反制牌，不记日志、不亮牌。</summary>
	public void ArmRemainingCountermeasures(CombatManager combat, Hero body)
	{
		foreach (var card in body.Hand.ToList())
		{
			if (card.Type == CardType.Countermeasure && combat.CanArmCountermeasure(body, card))
				combat.ArmCountermeasure(body, card);
		}
	}

	public void ExecuteTurn(CombatManager combat, EnemyUnit unit)
	{
		var body = unit.Body;
		BeginTurn(body);
		CommanderTurnLoop.Run(body,
			() => TryExecuteSingleBestAction(combat, unit),
			() => ArmRemainingCountermeasures(combat, body));
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
				result.Add(new Candidate($"随从攻击 {attacker.CardName}→{Describe(victim)}", score, EnemyStepKind.Attack,
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
			result.Add(new Candidate($"推进 {mover.CardName}", score, EnemyStepKind.Move, () => combat.CommanderMove(body, mover)));
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
				result.Add(new Candidate($"武器攻击→{Describe(victim)}", score, EnemyStepKind.Weapon,
					() => combat.CommanderWeaponAttack(body, victim)));
			}
		}

		// 武器主动技
		if (body.Weapon?.ActiveSkill is { } active && active.CanUse(body))
		{
			if (!active.RequiresTarget)
			{
				result.Add(new Candidate($"武器主动「{active.Name}」", 8 + active.Cost * 0.2, EnemyStepKind.Weapon,
					() => combat.CommanderWeaponActive(body, null)));
			}
			else
			{
				foreach (var target in combat.GetOpponentTargets(body.IsPlayerSide))
				{
					double score = 6 + (target is Minion m ? m.Attack * 0.6 + (m.UnitClass is UnitClass.Support or UnitClass.Skirmisher ? 3 : 0) : 1);
					var victim = target;
					result.Add(new Candidate($"武器主动「{active.Name}」→{Describe(victim)}", score, EnemyStepKind.Weapon,
						() => combat.CommanderWeaponActive(body, victim)));
				}
			}
		}

		// 英雄技能
		if (!_heroPowerUsedThisTurn && body.HeroPower is { } power && power.CanUse(body))
		{
			double score = ScoreHeroPower(body, power);
			if (score > 0)
				result.Add(new Candidate($"英雄技能「{power.Name}」", score, EnemyStepKind.HeroPower,
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
				// 目标型部署：有合法目标时额外评估战吼收益，裸拍则在无目标时才允许。
				object? battlecryTarget = null;
				if (card.Data.RequiresTarget && combat.HasAnyValidBattlecryTarget(actor, card))
				{
					(battlecryTarget, double battlecryScore) = ChooseBattlecryTarget(combat, actor, body, card);
					score += Math.Max(0, battlecryScore * 0.5);
				}
				var played = card;
				var deployedTarget = battlecryTarget;
				result.Add(new Candidate($"部署 {card.GetLocalizedName()}（{card.Cost}费）", score, EnemyStepKind.Deploy,
					() => combat.PlayMinion(actor, played, slot, deployedTarget)));
			}
			else if (card.Type == CardType.Domain)
			{
				string domainId = string.IsNullOrEmpty(card.Data.DomainId) ? card.Data.Id : card.Data.DomainId;
				double score = 16 - card.Cost * 1.2;
				if (body.HasDomain(domainId))
					score -= 9;
				var played = card;
				result.Add(new Candidate($"展开领域 {card.GetLocalizedName()}", score, EnemyStepKind.PlayCard,
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
				result.Add(new Candidate($"施放 {card.GetLocalizedName()}（{card.Cost}费）", score, EnemyStepKind.PlayCard,
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

	/// <summary>
	/// 目标型部署的战吼目标评分：把敌方随从推上前线会替对手抢线，必须拒绝。
	/// </summary>
	private static (object? Target, double Score) ChooseBattlecryTarget(CombatManager combat, EffectActorContext actor,
		Hero body, Card.Card card)
	{
		object? bestTarget = null;
		double bestScore = double.MinValue;
		bool movesToFrontline = card.Data.BattlecryEffects.Any(effect => effect.CustomEffectName == "MoveTargetToFrontline");
		foreach (var target in combat.GetOpponentTargets(body.IsPlayerSide).Concat(combat.GetFriendlyTargets(body.IsPlayerSide)))
		{
			if (target is not IDamageTarget damageTarget || !combat.ValidateBattlecryTarget(actor, card, target))
				continue;
			double score = movesToFrontline
				? ScoreMoveToFrontlineTarget(damageTarget, body)
				: ScoreBattlecryTarget(combat, body, card, damageTarget);
			if (score > bestScore)
			{
				bestScore = score;
				bestTarget = target;
			}
		}
		return (bestTarget, bestScore == double.MinValue ? 0 : bestScore);
	}

	private static double ScoreMoveToFrontlineTarget(IDamageTarget target, Hero body)
	{
		if (target is not Minion minion || minion.IsPlayerSide != body.IsPlayerSide)
			return -5;
		return 5 + minion.Attack * 1.2 + (minion.UnitClass is UnitClass.Vanguard or UnitClass.Assault ? 3 : 0);
	}

	private static double ScoreTargetForCard(CombatManager combat, Hero body, Card.Card card, IDamageTarget target)
		=> ScoreEffectsForTarget(combat, body, card.Data.Effects, target);

	private static double ScoreBattlecryTarget(CombatManager combat, Hero body, Card.Card card, IDamageTarget target)
		=> ScoreEffectsForTarget(combat, body, card.Data.BattlecryEffects, target);

	private static double ScoreEffectsForTarget(CombatManager combat, Hero body,
		IEnumerable<CardEffectData> effects, IDamageTarget target)
	{
		double score = 0;
		bool friendly = target is Hero hero ? hero.IsPlayerSide == body.IsPlayerSide
			: ((Minion)target).IsPlayerSide == body.IsPlayerSide;
		foreach (var effect in effects)
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
			"PerfectBlock" => friendly ? 5 : -6,
			"MoveTargetToFrontline" => friendly && target is Minion ? 5 : -5,
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
				case CardEffectType.BuffFriendlyMinions:
					score += 4 + effect.Value * 2 + effect.SecondaryValue;
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
			"AddPlanToHand" or "AddIgniteToHand" => 5,
			"PerfectBlock" => friendlyTarget ? 5 : -6,
			"ExplosiveReactiveArmor" => 6,
			"BuffFriendlyMinions" => 7,
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
		// 支援与游击自带全图射程（游击在支援线还享有免受近战普攻与同线护航），不主动上前线送死并浪费本回合开火机会。
		if (minion.UnitClass is UnitClass.Support or UnitClass.Skirmisher)
			return 0;
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
		string title = Localization.Localization.T(TitleKey, TitleFallback);
		return $"{title}：手牌 {self.Hand.Count}，指挥点 {self.CurrentMana}/{self.MaxMana}，前线 {frontline}，威胁 {threats}";
	}

	private static string Describe(IDamageTarget target) => target switch
	{
		Hero hero => hero.IsPlayerSide ? "玩家HQ" : "敌方HQ",
		Minion minion => minion.GetLocalizedName(),
		_ => "目标",
	};

	private sealed record Candidate(string Label, double Score, EnemyStepKind StepKind, Action Execute);
}

/// <summary>
/// <see cref="DeckDrivenCommander"/> 的构造描述——把「牌手是谁」与「怎么打牌」解耦。
/// 所有字段都是纯 C#，可在单测中安全构造。
/// </summary>
public sealed class DeckDrivenCommanderSpec
{
	/// <summary>牌手 ID（snake_case，同时作为本地化 key 后缀）。</summary>
	public string Id { get; init; } = "";

	/// <summary>显示名中文回退（本地化缺失时使用）。</summary>
	public string DisplayName { get; init; } = "";

	/// <summary>显示名本地化 key；为空时用 <c>enemy.deckhand.{Id}.name</c>。</summary>
	public string DisplayNameKey { get; init; } = "";

	/// <summary>头衔本地化 key；为空时用 <c>enemy.deckhand.{Id}.title</c>。</summary>
	public string TitleKey { get; init; } = "";

	/// <summary>头衔中文回退。</summary>
	public string TitleFallback { get; init; } = "主题卡组牌手";

	/// <summary>被动本地化 key；为空时用 <c>enemy.deckhand.{Id}.passive</c>。</summary>
	public string PassiveKey { get; init; } = "";

	/// <summary>最大生命值。</summary>
	public int MaxHealth { get; init; } = 20;

	/// <summary>目标卡组厚度（普通 10~20 / 精英 21~39 / Boss 40）。</summary>
	public int TargetDeckSize { get; init; } = 20;

	/// <summary>自带卡组画像；null 表示由 CombatManager 回退加载主题资源。</summary>
	public DeckProfile? Profile { get; init; }

	/// <summary>对外来画像（主题资源）的核心卡覆盖；null 表示不覆盖。</summary>
	public IReadOnlyList<string>? CoreCardOverrides { get; init; }

	/// <summary>对外来画像的机制标签权重覆盖；null 表示不覆盖。</summary>
	public IReadOnlyDictionary<CardMechanicTag, int>? TagWeightOverrides { get; init; }

	/// <summary>对外来画像的关键词权重覆盖；null 表示不覆盖。</summary>
	public IReadOnlyDictionary<Keyword, int>? KeywordWeightOverrides { get; init; }

	/// <summary>开场展开的领域 ID。</summary>
	public string[] OpeningDomains { get; init; } = Array.Empty<string>();

	/// <summary>初始武器工厂；null 时回退棍木。</summary>
	public Func<Weapon>? WeaponFactory { get; init; }

	/// <summary>英雄技能工厂；null 表示该牌手没有英雄技能。</summary>
	public Func<IHeroPower>? HeroPowerFactory { get; init; }

	/// <summary>开局防御修正。</summary>
	public int StartingDefense { get; init; }

	/// <summary>单次生命伤害上限（固璋等被动）。</summary>
	public int? DamageCap { get; init; }

	/// <summary>「不破」被动——每回合仅受一次生命伤害。</summary>
	public bool Unbreakable { get; init; }

	/// <summary>定位标签（normal / elite / boss）。</summary>
	public string Role { get; init; } = "normal";
}
