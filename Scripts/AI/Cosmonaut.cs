using System;
using Godot;
using OdysseyCards.AI.Intents;
using OdysseyCards.Card;
using OdysseyCards.Combat;
using OdysseyCards.Core;
using OdysseyCards.Character;

#pragma warning disable CS0618

namespace OdysseyCards.AI;

/// <summary>
/// 宇宙员 — 第二位面 Boss。
/// HP 80，武器：磁轨手枪（攻击力1，被动：目标1回合无法攻击）。
///
/// 意图循环：
///   A(强化: 获得宇宙冷漠 + 加入解释) → B(无边黑暗 + 宇宙冷漠) →
///   [C/D 随机×3回合，不可3连] → B → [C/D×3] → ...
///   热力值≥200%时下一回合固定 E(复制解释)，然后继续循环。
///
/// C: 对玩家英雄造成10点伤害
/// D: 对所有玩家目标造成3点伤害2次
/// E: 复制玩家手中的「解释」
/// </summary>
public class Cosmonaut : EnemyEncounter
{
	private const string ExplainCardId = "spell_explain";

	// 状态机变量
	private int _phase;           // 0=A, 1=B, 2-4=CD loop
	private int _cdRoundIndex;    // CD 循环中的位置 (0,1,2)
	private bool _ePending;       // 热力值≥200%，下次固定E
	private int _savedPhase;      // E 触发前保存的 phase
	private int _savedCdIndex;    // E 触发前保存的 CD index
	private int _lastCCount;      // C 连续次数(防止3连)
	private int _lastDCount;      // D 连续次数(防止3连)
	private bool _currentActionIsC; // 当前CD动作为C

	public Cosmonaut()
		: base("宇宙员", 80, new EnemyIntent[]
		{
			new(IntentType.Attack, 0, "") // 占位
		})
	{
		Attack = 1;
		MoveStates = new MoveState[]
		{
			new("cosmo_placeholder", null, new UnknownIntent()),
		};
		_phase = 0;
		_cdRoundIndex = 0;
	}

	public override MoveState GetCurrentMove(CombatManager combat, Hero self)
	{
		// 热力值≥200% → 标记 E，只有非首回合才触发
		if (combat.Heat.CurrentHeat >= 1.2f && !_ePending && _phase != 0)
		{
			_ePending = true;
			_savedPhase = _phase;
			_savedCdIndex = _cdRoundIndex;
		}

		// E 优先
		if (_ePending)
			return CreateMoveE(combat);

		switch (_phase)
		{
			case 0:
				return CreateMoveA(combat);
			case 1:
				return CreateMoveB(combat, self);
			default: // 2-4: CD loop
				return CreateMoveCD(combat, self);
		}
	}

	public override void AdvanceMove()
	{
		// E 回合之后：恢复状态
		if (_ePending)
		{
			_ePending = false;
			_phase = _savedPhase;
			_cdRoundIndex = _savedCdIndex;
			AdvanceIntent();
			return;
		}

		switch (_phase)
		{
			case 0: // A → B
				_phase = 1;
				break;
			case 1: // B → CD loop
				_phase = 2;
				_cdRoundIndex = 0;
				_lastCCount = 0;
				_lastDCount = 0;
				break;
			default: // CD loop: advance or wrap back to B
				_cdRoundIndex++;
				if (_cdRoundIndex >= 3)
				{
					_cdRoundIndex = 0;
					_phase = 1; // 回到 B
				}
				break;
		}

		AdvanceIntent();
		_cachedAttackTarget = null;
	}

	public override void ExecuteIntent(CombatManager combat, Hero self)
	{
		var move = GetCurrentMove(combat, self);
		GD.Print($"[宇宙员] 执行 MoveState：{move.Id} (phase={_phase}, cdRound={_cdRoundIndex})");

		// C 动作：单目标攻击 → 触发磁轨手枪被动
		if (move.Id == "cosmo_c")
		{
			var target = ResolveAttackTarget(combat);
			int rawDmg = 10 + Attack;
			if (target is Minion minionTarget)
			{
				combat.TriggerBaitTacticsOnAttacked(minionTarget);
				bool ambush = minionTarget.HasAmbush && !minionTarget.AmbushUsedThisTurn;
				if (ambush) minionTarget.AmbushUsedThisTurn = true;

				self.SuppressWeaponCounter = true;
				self.TakeDamage(minionTarget.Attack, minionTarget);
				self.SuppressWeaponCounter = false;
				if (ambush && self.IsDead) return;

				minionTarget.TakeDamage(rawDmg, self);
			}
			else
			{
				target?.TakeDamage(rawDmg, self);
			}

			// 磁轨手枪被动
			self.Weapon?.PassiveSkill?.OnWeaponHit(target, self);
			GD.Print($"[宇宙员] 磁轨锁定：{target}");
		}
		else
		{
			move.OnPerform?.Invoke(combat, self);
		}
	}

	// ===== MoveState 创建 =====

	private MoveState CreateMoveA(CombatManager combat)
	{
		return new MoveState(
			"cosmo_a",
			(cm, _) =>
			{
				// 获得1层宇宙冷漠
				ApplyCosmicColdness(cm, 1);
				// 向玩家手牌加入「解释」
				AddExplainToHand(cm);
			},
			new BuffIntent(),
			new StatusIntent(1)
		);
	}

	private MoveState CreateMoveB(CombatManager combat, Hero self)
	{
		return new MoveState(
			"cosmo_b",
			(cm, _) =>
			{
				CastBoundlessDarkness(cm, self);
				ApplyCosmicColdness(cm, 1);
				GD.Print("[宇宙员] B: 无边黑暗 + 宇宙冷漠");
			},
			new SpellCastIntent("无边黑暗", "使所有敌人获得3层易伤和3层脆弱"),
			new BuffIntent()
		);
	}

	private MoveState CreateMoveCD(CombatManager combat, Hero self)
	{
		// 随机选C或D，防止3连
		bool chooseC;
		if (_lastCCount >= 2) chooseC = false;
		else if (_lastDCount >= 2) chooseC = true;
		else chooseC = Random.Shared.Next(2) == 0;

		_currentActionIsC = chooseC;

		if (chooseC)
		{
			_lastCCount++;
			_lastDCount = 0;
			return new MoveState(
				"cosmo_c",
				null, // ExecuteIntent handles C
				new SingleAttackIntent(c =>
					DamageResolver.ResolvePreviewDamage(10 + Attack, self, ResolveAttackTarget(c)))
			);
		}
		else
		{
			_lastDCount++;
			_lastCCount = 0;
			return new MoveState(
				"cosmo_d",
				(cm, hero) =>
				{
					var playerHero = cm.PlayerHero;
					for (int hit = 0; hit < 2; hit++)
					{
						if (playerHero.IsDead) break;
						playerHero.TakeDamage(3, self, DamageKind.Attack);
					}
					foreach (var minion in cm.Board.GetPlayerMinions())
					{
						for (int hit = 0; hit < 2; hit++)
						{
							if (minion.IsDead) break;
							minion.TakeDamage(3, self, DamageKind.Attack);
						}
					}
					GD.Print("[宇宙员] D: 对所有目标造成3伤害×2");
				},
				new MultiAttackIntent(c => DamageResolver.ResolvePreviewDamage(3, self, null), 2)
			);
		}
	}

	private MoveState CreateMoveE(CombatManager combat)
	{
		return new MoveState(
			"cosmo_e",
			(cm, _) =>
			{
				// 复制玩家手中的「解释」
				DuplicateExplainInHand(cm);
			},
			new StatusIntent(1)
		);
	}

	// ===== 辅助方法 =====

	/// <summary>
	/// 应用宇宙冷漠领域——使「解释」费用+1。
	/// 叠加多层时费用叠加。
	/// </summary>
	private static void ApplyCosmicColdness(CombatManager combat, int stacks)
	{
		var enemyHero = combat.EnemyUnits[0].Body;
		var effectData = new CardEffectData
		{
			EffectType = CardEffectType.Custom,
			CustomEffectName = "CosmicColdness",
			Value = stacks,
		};

		enemyHero.AddDomain("cosmic_coldness", effectData);
		GD.Print($"[宇宙员] 获得宇宙冷漠 ×{stacks}（解释费用+{stacks}）");

		// 更新所有「解释」卡牌的费用
		UpdateExplainCostModifiers(combat, enemyHero);
	}

	/// <summary>
	/// 向玩家手牌加入一张「解释」。
	/// </summary>
	private static void AddExplainToHand(CombatManager combat)
	{
		var explainData = GD.Load<CardData>($"res://Resources/Cards/{ExplainCardId}.tres");
		if (explainData == null)
		{
			GD.PrintErr("[宇宙员] 未找到解释卡牌资源");
			return;
		}

		var card = new OdysseyCards.Card.Card(explainData);
		combat.PlayerHero.DeckState.AddToHand(card);
		GD.Print("[宇宙员] 将「解释」加入玩家手牌");
	}

	/// <summary>
	/// 释放无边黑暗——所有敌人获得3层易伤+3层脆弱。
	/// </summary>
	private static void CastBoundlessDarkness(CombatManager combat, Hero self)
	{
		GD.Print("[宇宙员] 释放「无边黑暗」！");

		var playerHero = combat.PlayerHero;
		playerHero.AddStatusEffect(new StatusEffect("vulnerable", 3, TickTiming.PlayerTurnEnd));
		playerHero.AddStatusEffect(new StatusEffect("fragile", 3, TickTiming.PlayerTurnEnd));

		foreach (var minion in combat.Board.GetPlayerMinions())
		{
			minion.AddStatusEffect(new StatusEffect("vulnerable", 3, TickTiming.PlayerTurnEnd));
			minion.AddStatusEffect(new StatusEffect("fragile", 3, TickTiming.PlayerTurnEnd));
		}
	}

	/// <summary>
	/// 复制玩家手中的「解释」。
	/// </summary>
	private static void DuplicateExplainInHand(CombatManager combat)
	{
		var explainCards = new System.Collections.Generic.List<OdysseyCards.Card.Card>();
		foreach (var card in combat.PlayerHero.Hand)
		{
			if (card.Id == ExplainCardId)
				explainCards.Add(card);
		}

		if (explainCards.Count == 0)
		{
			GD.Print("[宇宙员] 玩家手中没有「解释」，无法复制");
			return;
		}

		// 复制第一张
		var original = explainCards[0];
		var copy = new OdysseyCards.Card.Card(original.Data);
		copy.CopyRuntimeModifiersFrom(original);
		combat.PlayerHero.DeckState.AddToHand(copy);
		GD.Print($"[宇宙员] 复制了一张「解释」加入玩家手牌");
	}

	/// <summary>
	/// 更新所有「解释」卡牌的费用修改器。
	/// 每次宇宙冷漠层数变化时调用。
	/// </summary>
	private static void UpdateExplainCostModifiers(CombatManager combat, Hero enemyHero)
	{
		int totalStacks = 0;
		if (enemyHero.ActiveDomains.TryGetValue("cosmic_coldness", out var domain))
			totalStacks = domain.StackCount;

		// 扫描所有区域
		UpdateCardsInList(combat.PlayerHero.Hand, totalStacks);
		UpdateCardsInList(combat.PlayerHero.DeckState.DrawPile, totalStacks);
		UpdateCardsInList(combat.PlayerHero.DeckState.DiscardPile, totalStacks);
	}

	private static void UpdateCardsInList(
		System.Collections.Generic.IReadOnlyList<OdysseyCards.Card.Card> cards, int costModifier)
	{
		foreach (var card in cards)
		{
			if (card.Id == ExplainCardId)
				card.CostModifier = costModifier;
		}
	}
}

#pragma warning restore CS0618
