using System;
using Godot;
using OdysseyCards.AI.Intents;
using OdysseyCards.Card;
using OdysseyCards.Combat;
using OdysseyCards.Core;

namespace OdysseyCards.AI;

// ====================================================================
// 意图类型与数据结构
// ====================================================================

/// <summary>
/// 敌人意图结构体。
/// 描述敌人本回合将要执行的一个意图，包含类型、数值和显示文本。
/// </summary>
public struct EnemyIntent
{
	/// <summary>意图类型。</summary>
	public IntentType Type;

	/// <summary>意图数值（伤害量、护甲量、召唤数量等）。</summary>
	public int Value;

	/// <summary>意图描述文本，供 UI 展示。</summary>
	public string Description;

	// ===== 动态意图计算（延迟查询，每次调用重算） =====

	/// <summary>
	/// 攻击目标选择器——每次调用根据当前战场状态动态决定目标。
	/// 注入时机：<see cref="EnemyEncounter.GetCurrentIntent"/> 为 Attack 意图自动注入。
	/// 若为 null 则退化为无目标攻击（非 Attack 意图）。
	/// </summary>
	public Func<CombatManager, IDamageTarget>? TargetSelector;

	/// <summary>
	/// 伤害计算函数——每次调用重新走 DamageResolver 管线，反映当前力量/易伤等修饰。
	/// 注入时机：<see cref="EnemyEncounter.GetCurrentIntent"/> 为 Attack 意图自动注入。
	/// 若为 null 则返回静态 <see cref="Value"/>。
	/// </summary>
	public Func<CombatManager, int>? DamageCalc;

	// ===== 召唤意图的额外信息（供 UI 提前预览召唤物属性） =====

	/// <summary>召唤物名称（仅 Summon 意图时有效）。</summary>
	public string SummonMinionName;

	/// <summary>召唤物攻击力。</summary>
	public int SummonMinionAttack;

	/// <summary>召唤物生命值。</summary>
	public int SummonMinionHealth;

	/// <summary>召唤物是否具有闪击（入场即可攻击）。</summary>
	public bool SummonMinionHasCharge;

	/// <summary>
	/// 创建敌人意图实例。
	/// </summary>
	/// <param name="type">意图类型</param>
	/// <param name="value">意图数值</param>
	/// <param name="description">意图描述文本</param>
	/// <param name="summonName">召唤物名称（仅 Summon 意图）</param>
	/// <param name="summonAttack">召唤物攻击力</param>
	/// <param name="summonHealth">召唤物生命值</param>
	/// <param name="summonHasCharge">召唤物是否有闪击</param>
	public EnemyIntent(IntentType type, int value, string description,
		string summonName = "", int summonAttack = 0, int summonHealth = 0, bool summonHasCharge = false)
	{
		Type = type;
		Value = value;
		Description = description;
		SummonMinionName = summonName;
		SummonMinionAttack = summonAttack;
		SummonMinionHealth = summonHealth;
		SummonMinionHasCharge = summonHasCharge;
	}

	// ===== 动态查询方法 =====

	/// <summary>
	/// 获取当前攻击目标（仅 Attack 意图有效）。
	/// 每次调用根据战场实时状态重新计算——若有嘲讽则指向嘲讽随从，反之指向英雄。
	/// </summary>
	/// <param name="combat">战斗管理器</param>
	/// <returns>攻击目标，若 TargetSelector 未注入则返回 null</returns>
	public readonly IDamageTarget? GetTarget(CombatManager combat)
	{
		return TargetSelector?.Invoke(combat);
	}

	/// <summary>
	/// 获取当前有效伤害值（经过所有伤害修饰后的预览值）。
	/// 每次调用重新走 DamageResolver 管线，用于 UI 实时预览和实际执行。
	/// </summary>
	/// <param name="combat">战斗管理器</param>
	/// <returns>有效伤害值</returns>
	public readonly int GetEffectiveDamage(CombatManager combat)
	{
		return DamageCalc?.Invoke(combat) ?? Value;
	}

	/// <summary>
	/// 获取带目标信息的动态意图描述文本（已本地化）。
	/// 根据意图类型和结构化数据动态生成本地化描述。
	/// </summary>
	/// <param name="combat">战斗管理器</param>
	/// <returns>意图 UI 显示文本</returns>
	public readonly string GetDisplayDescription(CombatManager combat)
	{
		return Type switch
		{
			IntentType.Attack => BuildAttackDescription(combat),
			IntentType.Defend => Localization.Localization.T("intent.defend_format", "获得 {amount} 点护甲")
				.Replace("{amount}", Value.ToString()),
			IntentType.Summon => BuildSummonDescription(),
			IntentType.Buff => Localization.Localization.T("intent.buff_format", "{desc}").Replace("{desc}", Description),
			_ => Description
		};
	}

	private readonly string BuildAttackDescription(CombatManager combat)
	{
		var target = TargetSelector?.Invoke(combat);
		int damage = DamageCalc?.Invoke(combat) ?? Value;
		string targetName = target switch
		{
			Hero => Localization.Localization.T("intent.target_hero", "英雄"),
			Minion m => m.GetLocalizedName(),
			_ => Localization.Localization.T("intent.target_unknown", "目标")
		};
		return Localization.Localization.T("intent.attack_format", "对{target}造成 {damage} 点伤害")
			.Replace("{target}", targetName)
			.Replace("{damage}", damage.ToString());
	}

	private readonly string BuildSummonDescription()
	{
		string format = SummonMinionHasCharge
				? Localization.Localization.T("intent.summon_charge_format", "召唤 {name} ({atk}/{hp} 闪击)")
			: Localization.Localization.T("intent.summon_format", "召唤 {name} ({atk}/{hp})");
		return format
			.Replace("{name}", SummonMinionName)
			.Replace("{atk}", SummonMinionAttack.ToString())
			.Replace("{hp}", SummonMinionHealth.ToString());
	}
}

// ====================================================================
// 敌人遭遇抽象基类
// ====================================================================

/// <summary>
/// 敌人遭遇抽象基类。
/// 定义敌人的基础属性（名称、生命值）、循环意图模式和执行接口。
/// 纯 C# 类，不继承 Godot Node——英雄由 CombatManager 管理。
/// 参考《杀戮尖塔》的 Monster/Intent 架构设计。
/// </summary>
public abstract class EnemyEncounter
{
	// ===== 基础属性 =====

	/// <summary>
	/// 敌人名称。
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// 最大生命值。
	/// </summary>
	public int MaxHealth { get; }

	/// <summary>
	/// 敌人的攻击力。影响意图造成的伤害——意图伤害 = 意图基础值 + 攻击力。
	/// 攻击力也可被降低（如离子脉冲），攻击力降低会减少意图伤害。
	/// 最小为 0（攻击力为负会减少意图伤害，但不会让意图变为治疗）。
	/// </summary>
	public int Attack { get; set; }

	// ===== 意图系统 =====

	/// <summary>
	/// 循环意图序列。按顺序逐回合执行，到末尾后回到开头。
	/// </summary>
	protected EnemyIntent[] IntentPattern { get; init; }

	/// <summary>
	/// 当前意图在意图序列中的索引。
	/// </summary>
	public int CurrentPatternIndex { get; private set; }

	/// <summary>
	/// 当前 Attack 意图已解析的的目标缓存。
	/// 在 <see cref="GetCurrentIntent"/> 首次解析时锁定，
	/// <see cref="AdvanceIntent"/> 推进意图时清空。
	/// 保证意图显示和执行阶段攻击同一目标。
	/// </summary>
	protected IDamageTarget? _cachedAttackTarget;

	// ===== 新意图系统（MoveState） =====

	/// <summary>
	/// 可选的 MoveState 序列（新意图系统）。
	/// 若设置则优先使用 MoveState 驱动意图；否则回退到旧 IntentPattern。
	/// </summary>
	protected MoveState[]? MoveStates { get; init; }

	/// <summary>
	/// 当前 MoveState 在序列中的索引。
	/// </summary>
	public int CurrentMoveIndex { get; protected set; }

	/// <summary>是否使用了新的 MoveState 意图系统。</summary>
	public bool HasMoveStates => MoveStates != null;

	// ===== 构造函数 =====

	/// <summary>
	/// 创建敌人遭遇实例。
	/// </summary>
	/// <param name="name">敌人名称</param>
	/// <param name="maxHealth">最大生命值</param>
	/// <param name="intentPattern">循环意图序列</param>
	protected EnemyEncounter(string name, int maxHealth, EnemyIntent[] intentPattern)
	{
		Name = name;
		MaxHealth = maxHealth;
		Attack = 0; // 默认无额外攻击力，子类可覆盖
		IntentPattern = intentPattern;
		CurrentPatternIndex = 0;
	}

	// ===== 意图操作 =====

	/// <summary>
	/// 获取当前回合的意图，并根据当前战场状态注入动态选择器。
	/// 对于 Attack 意图，自动注入 <see cref="ResolveAttackTarget"/> 和
	/// 基于 <see cref="DamageResolver.ResolvePreviewDamage"/> 的伤害计算函数。
	/// 调用者每次查询都会获得反映最新战场状态的意图。
	/// </summary>
	/// <param name="combat">战斗管理器，提供战场和目标信息</param>
	/// <returns>包含动态选择器的意图结构体</returns>
	public virtual EnemyIntent GetCurrentIntent(CombatManager combat, Hero self)
	{
		// 新系统：从虚拟方法 GetCurrentMove 获取意图（支持子类动态意图）
		if (MoveStates != null)
		{
			var move = GetCurrentMove(combat, self);
			var abstractIntent = move.Intents.Count > 0
				? move.Intents[0]
				: new OdysseyCards.AI.Intents.UnknownIntent();
			return ConvertToLegacyIntent(abstractIntent, combat, self);
		}

		// 旧系统：使用 IntentPattern
		var intent = IntentPattern[CurrentPatternIndex];
		if (intent.Type == IntentType.Attack)
		{
			// 首次解析时锁定目标 — 保证意图显示和执行阶段攻击同一随从。
			_cachedAttackTarget ??= ResolveAttackTarget(combat);
			var cachedTarget = _cachedAttackTarget;
			intent.TargetSelector = _ => cachedTarget;
			intent.DamageCalc = (c) =>
			{
				int baseWithAttack = intent.Value + Attack;
				return DamageResolver.ResolvePreviewDamage(baseWithAttack, self, cachedTarget);
			};
		}
		return intent;
	}

	/// <summary>
	/// 将新意图系统的 AbstractIntent 转换为旧的 EnemyIntent 格式（向后兼容）。
	/// </summary>
	/// <param name="abstractIntent">新意图</param>
	/// <param name="combat">战斗管理器</param>
	/// <param name="self">所属英雄身体</param>
	/// <returns>兼容的 EnemyIntent</returns>
	private EnemyIntent ConvertToLegacyIntent(AbstractIntent abstractIntent, CombatManager combat, Hero self)
	{
		if (abstractIntent is AttackIntent attackIntent)
		{
			int damage = attackIntent.GetSingleDamage(combat);
			_cachedAttackTarget ??= ResolveAttackTarget(combat);
			var cachedTarget = _cachedAttackTarget;
			var intent = new EnemyIntent(IntentType.Attack, damage, attackIntent.GetIntentDescription(combat));
			intent.TargetSelector = _ => cachedTarget;
			intent.DamageCalc = (c) =>
			{
				int baseWithAttack = damage + Attack;
				return DamageResolver.ResolvePreviewDamage(baseWithAttack, self, cachedTarget);
			};
			return intent;
		}

		var legacyType = MapToLegacyType(abstractIntent.Type);
		return new EnemyIntent(legacyType, 0, abstractIntent.GetIntentDescription(combat));
	}

	/// <summary>
	/// 将新意图类型枚举映射到旧意图类型枚举。
	/// </summary>
	/// <param name="newType">新意图类型</param>
	/// <returns>对应的旧意图类型</returns>
	private static IntentType MapToLegacyType(OdysseyCards.AI.Intents.IntentType newType)
	{
		return newType switch
		{
			OdysseyCards.AI.Intents.IntentType.Attack
				or OdysseyCards.AI.Intents.IntentType.MultiAttack
				or OdysseyCards.AI.Intents.IntentType.DeathBlow => IntentType.Attack,
			OdysseyCards.AI.Intents.IntentType.Defend => IntentType.Defend,
			OdysseyCards.AI.Intents.IntentType.Summon => IntentType.Summon,
			OdysseyCards.AI.Intents.IntentType.Buff
				or OdysseyCards.AI.Intents.IntentType.StatusCard
				or OdysseyCards.AI.Intents.IntentType.SpellCast => IntentType.Buff,
			_ => IntentType.Attack
		};
	}

	/// <summary>
	/// 默认攻击目标选择器。
	/// 根据战场实时状态：若玩家方有嘲讽随从则随机选择一个，否则攻击玩家英雄。
	/// 子类可重写以实现特殊的目标选择逻辑（如"总是攻击最左侧随从"）。
	/// </summary>
	/// <param name="combat">战斗管理器</param>
	/// <returns>攻击目标</returns>
	protected virtual IDamageTarget ResolveAttackTarget(CombatManager combat)
	{
		var taunts = combat.Board.GetTaunts(ofEnemy: false);
		if (taunts.Count > 0)
			return taunts[Random.Shared.Next(taunts.Count)];
		return combat.PlayerHero;
	}

	/// <summary>
	/// 将意图索引推进到序列的下一个位置。
	/// 到达序列末尾时循环回到开头。
	/// </summary>
	public virtual void AdvanceIntent()
	{
		_cachedAttackTarget = null;
		CurrentPatternIndex = (CurrentPatternIndex + 1) % IntentPattern.Length;
	}

	/// <summary>
	/// 清空当前攻击目标缓存。
	/// 战场随从、嘲讽或其他会影响合法目标的状态变化后调用，
	/// 让下一次意图刷新重新锁定显示/执行共用的目标。
	/// </summary>
	public void ResetCachedAttackTarget()
	{
		_cachedAttackTarget = null;
	}

	// ===== MoveState 操作（新意图系统） =====

	/// <summary>
	/// 获取当前 MoveState。
	/// 若 <see cref="MoveStates"/> 已设置则从中获取；否则将旧 IntentPattern 自动包装为单意图 MoveState。
	/// </summary>
	/// <param name="combat">战斗管理器</param>
	/// <param name="self">所属英雄身体</param>
	/// <returns>当前 MoveState</returns>
	public virtual MoveState GetCurrentMove(CombatManager combat, Hero self)
	{
		if (MoveStates != null)
			return MoveStates[CurrentMoveIndex];

		// 向后兼容：将旧的 IntentPattern 包装为 MoveState
		var oldIntent = GetCurrentIntent(combat, self);
		// 旧意图包装：无 OnPerform（由 ExecuteIntent 处理），使用占位意图
		var placeholderIntent = new OdysseyCards.AI.Intents.UnknownIntent();
		return new MoveState($"legacy_pattern_{CurrentPatternIndex}", null, placeholderIntent);
	}

	/// <summary>
	/// 推进到下一 MoveState。同时调用 <see cref="AdvanceIntent"/> 保证向后兼容。
	/// </summary>
	public virtual void AdvanceMove()
	{
		AdvanceIntent();
		if (MoveStates != null)
			CurrentMoveIndex = (CurrentMoveIndex + 1) % MoveStates.Length;
	}

	/// <summary>
	/// 执行当前 MoveState 的 OnPerform 回调（若已设置）。
	/// 迁移后所有敌人统一通过此方法执行——Boss 与随从无区别。
	/// </summary>
	/// <param name="combat">战斗管理器</param>
	/// <param name="self">所属英雄身体（随从时为 null）</param>
	public void ExecuteMove(CombatManager combat, Hero? self)
	{
		GetCurrentMove(combat, self!)?.OnPerform?.Invoke(combat, self);
	}

	// ===== 意图执行辅助方法 =====

	/// <summary>
	/// 执行攻击意图——对目标造成伤害，若目标是随从则触发反击。
	/// 集中处理攻击流程，避免各敌人类别重复实现，同时确保反击逻辑一致性。
	/// </summary>
	/// <param name="combat">战斗管理器</param>
	protected void ExecuteAttackIntent(CombatManager combat, Hero self)
	{
		var intent = GetCurrentIntent(combat, self);
		var target = intent.GetTarget(combat);
		int rawDmg = intent.Value + Attack;

		if (target is Minion minionTarget)
		{
			combat.TriggerBaitTacticsOnAttacked(minionTarget);

			// 伏击检查：随从有伏击且本回合未消耗时，先手伤害有击杀取消效果
			bool ambush = minionTarget.HasAmbush && !minionTarget.AmbushUsedThisTurn;
			if (ambush)
				minionTarget.AmbushUsedThisTurn = true;

			// 先造成反击伤害（正常反击 或 伏击先手）
			self.SuppressWeaponCounter = true;
			self.TakeDamage(minionTarget.Attack, minionTarget);
			self.SuppressWeaponCounter = false;
			string label = ambush ? "伏击先手" : "反击";
			GD.Print($"[{Name}] {minionTarget.CardName} {label}，对敌人造成 {minionTarget.Attack} 伤害");

			// 伏击击杀攻击者 → 攻击被取消
			if (ambush && self.IsDead)
			{
				GD.Print($"[{Name}] ☠ 被 {minionTarget.CardName} 伏击击杀，攻击被取消");
				return;
			}

			// 敌方英雄对随从造成伤害（source=self 使热力值在 DamageResolver 中生效一次）
			minionTarget.TakeDamage(rawDmg, self);
			GD.Print($"[{Name}] 攻击 {minionTarget.CardName}，造成 {rawDmg} 伤害");
		}
		else
		{
			// 敌方英雄攻击玩家英雄（或其他非随从目标）
			target?.TakeDamage(rawDmg, self);
		}
	}

	// ===== 统一执行方法 =====

	/// <summary>
	/// 执行当前意图。默认通过 ExecuteMove 委托给 MoveState.OnPerform。
	/// 子类可重写以添加额外逻辑（如张郎/珊胡的 D-move 处理）。
	/// </summary>
	public virtual void ExecuteIntent(CombatManager combat, Hero self)
	{
		ExecuteMove(combat, self);
	}
}

// ====================================================================
// 具体敌人类型
// ====================================================================

/// <summary>
/// 邪教徒 — 基础教学敌人。
/// 意图模式：攻击(6) → 攻击(6) → 防御(5) → 循环。
/// 生命值 20，攻击较高但防御薄弱，适合作为第一个遭遇战。
/// </summary>
public class Cultist : EnemyEncounter
{
	public Cultist()
		: base("邪教徒", 20, new EnemyIntent[]
		{
			new(IntentType.Attack, 0, "") // 占位，实际由 MoveStates 驱动
		})
	{
		MoveStates = new MoveState[]
		{
			new("A1", (c, s) => ExecuteAttackIntent(c, s!), new SingleAttackIntent(6)),
			new("A2", (c, s) => ExecuteAttackIntent(c, s!), new SingleAttackIntent(6)),
			new("D", (c, s) => s!.GainArmor(5), new DefendIntent()),
		};
	}
}

/// <summary>
/// 史莱姆首领 — 召唤型敌人。
/// 意图模式：攻击(8) → 召唤(1) → 防御(4) → 循环。
/// 生命值 40，会定期召唤 1/1 软泥怪随从铺场。
/// </summary>
public class SlimeBoss : EnemyEncounter
{
	public SlimeBoss()
		: base("史莱姆首领", 40, new EnemyIntent[]
		{
			new(IntentType.Attack, 0, "") // 占位
		})
	{
		MoveStates = new MoveState[]
		{
			new("A", (c, s) => ExecuteAttackIntent(c, s!), new SingleAttackIntent(8)),
			new("S", (c, _) => TrySummonSlime(c), new SummonIntent()),
			new("D", (c, s) => s!.GainArmor(4), new DefendIntent()),
		};
	}

	/// <summary>
	/// 尝试在敌方战场召唤一只 1/1 软泥怪随从（闪击）。
	/// 从 .tres 资源加载，与玩家卡牌同源。
	/// 若战场已满则不执行（最佳尝试策略）。
	/// </summary>
	/// <param name="combat">战斗管理器</param>
	private static void TrySummonSlime(CombatManager combat)
	{
		if (!combat.Board.CanPlaceMinion(isPlayerSide: false))
		{
			GD.Print("[SlimeBoss] 敌方战场已满，软泥怪无法召唤");
			return;
		}

		const string path = "res://Resources/Cards/Minion_Slime.tres";
		if (!ResourceLoader.Exists(path))
		{
			GD.PrintErr($"[SlimeBoss] 未找到软泥怪卡牌资源：{path}");
			return;
		}

		var slimeData = GD.Load<CardData>(path);
		if (slimeData == null)
		{
			GD.PrintErr("[SlimeBoss] 软泥怪卡牌资源加载失败");
			return;
		}

		var slime = new Minion(slimeData, isPlayerSide: false);
		int slot = combat.Board.GetEmptySlotIndex(isPlayerSide: false);
		combat.Board.PlaceMinion(slime, slot);

		GD.Print($"[SlimeBoss] 在敌方槽位 {slot} 召唤了软泥怪（{slime.Attack}/{slime.CurrentHealth}）");
	}
}

/// <summary>
/// 狼骑兵 — 速攻型敌人。
/// 意图模式：攻击(5) → 循环（每回合攻击）。
/// 生命值仅 12，但每回合稳定输出，考验玩家的爆发击杀能力。
/// </summary>
public class WolfRider : EnemyEncounter
{
	public WolfRider()
		: base("狼骑兵", 12, new EnemyIntent[]
		{
			new(IntentType.Attack, 0, "") // 占位
		})
	{
		var attack = new MoveState("A", (c, s) => ExecuteAttackIntent(c, s!), new SingleAttackIntent(5));
		attack.FollowUpState = attack; // 自循环
		MoveStates = new[] { attack };
	}
}

/// <summary>
/// 实习机械师 — 召唤型敌人，会召唤机械静螳并为其提供护甲。
/// 意图模式：召唤(1)→增益(5)→增益(5)→增益(5)→...（若机械静螳死亡则重新召唤）。
/// 生命值 20，武器为棍木（攻击力 1）。
/// </summary>
public class ApprenticeMechanic : EnemyEncounter
{
	private const string MechLancerPath = "res://Resources/Cards/Minion_Mech_Lancer.tres";

	private readonly MoveState _moveSummon;
	private readonly MoveState _moveBuff;

	public ApprenticeMechanic()
		: base("实习机械师", 20, new EnemyIntent[]
		{
			new(IntentType.Attack, 0, "") // 占位
		})
	{
		Attack = 1;
		_moveSummon = new MoveState("SUMMON", (c, _) => TrySummonMechLancer(c), new SummonIntent());
		_moveBuff = new MoveState("BUFF", (c, _) => BuffMechLancer(c), new BuffIntent());
		_moveSummon.FollowUpState = _moveBuff; // 召完就加护甲
		_moveBuff.FollowUpState = _moveBuff;   // 自循环，直到随从死亡
		MoveStates = new[] { _moveSummon, _moveBuff };
	}

	public override MoveState GetCurrentMove(CombatManager combat, Hero self)
	{
		// 设置 CurrentMoveIndex，让基类 GetCurrentIntent 读取正确的 MoveState
		CurrentMoveIndex = HasFriendlyMechLancer(combat) ? 1 : 0;
		return MoveStates![CurrentMoveIndex];
	}

	public override void AdvanceMove()
	{
		AdvanceIntent(); // 维持 MoveStates 数组索引循环
		if (MoveStates != null)
			CurrentMoveIndex = (CurrentMoveIndex + 1) % MoveStates.Length;
	}

	/// <summary>
	/// 检查敌方战场上是否存在存活的我方机械静螳。
	/// </summary>
	/// <param name="combat">战斗管理器</param>
	/// <returns>存在机械静螳返回 true</returns>
	private static bool HasFriendlyMechLancer(CombatManager combat)
	{
		foreach (var minion in combat.Board.GetEnemyMinions())
		{
			if (minion.Id == "minion_Mech_Lancer")
				return true;
		}
		return false;
	}

	/// <summary>
	/// 尝试在敌方战场召唤机械静螳（4/3 嘲讽 伏击）。
	/// 若战场已满则跳过（最佳尝试策略）。
	/// 召唤的随从在当前敌方回合不可攻击（由 _enemyMinionsCanAttack 快照机制保证）。
	/// </summary>
	/// <param name="combat">战斗管理器</param>
	private static void TrySummonMechLancer(CombatManager combat)
	{
		if (!combat.Board.CanPlaceMinion(isPlayerSide: false))
		{
			GD.Print("[ApprenticeMechanic] 敌方战场已满，机械静螳无法召唤");
			return;
		}

		if (!ResourceLoader.Exists(MechLancerPath))
		{
			GD.PrintErr($"[ApprenticeMechanic] 未找到机械静螳卡牌资源：{MechLancerPath}");
			return;
		}

		var data = GD.Load<CardData>(MechLancerPath);
		if (data == null)
		{
			GD.PrintErr("[ApprenticeMechanic] 机械静螳卡牌资源加载失败");
			return;
		}

		var mechLancer = new Minion(data, isPlayerSide: false);
		mechLancer.HasTaunt = true; // 召唤时赋予嘲讽（基础卡牌仅有伏击）
		int slot = combat.Board.GetEmptySlotIndex(isPlayerSide: false);
		combat.Board.PlaceMinion(mechLancer, slot);

		GD.Print($"[ApprenticeMechanic] 在敌方槽位 {slot} 召唤了机械静螳（{mechLancer.Attack}/{mechLancer.CurrentHealth} 嘲讽 伏击）");
	}

	/// <summary>
	/// 为战场上所有存活的机械静螳增加 5 点护甲。
	/// </summary>
	/// <param name="combat">战斗管理器</param>
	private static void BuffMechLancer(CombatManager combat)
	{
		bool found = false;
		foreach (var minion in combat.Board.GetEnemyMinions())
		{
			if (minion.Id == "minion_Mech_Lancer" && !minion.IsDead)
			{
				minion.GainArmor(5);
				GD.Print($"[ApprenticeMechanic] 机械静螳获得 5 点护甲，当前护甲：{minion.CurrentArmor}");
				found = true;
			}
		}

		if (!found)
		{
			GD.Print("[ApprenticeMechanic] 战场上没有机械静螳可增益");
		}
	}
}

/// <summary>
/// 守护者 — 第一位面 Boss。
/// 意图模式：攻击(12) → 防御(8) → 攻击(12) → 循环。
/// 生命值 60，高伤害高耐久，考验玩家的资源管理和爆发能力。
/// </summary>
public class GuardianBoss : EnemyEncounter
{
	public GuardianBoss()
		: base("守护者", 60, new EnemyIntent[]
		{
			new(IntentType.Attack, 0, "") // 占位
		})
	{
		MoveStates = new MoveState[]
		{
			new("A1", (c, s) => ExecuteAttackIntent(c, s!), new SingleAttackIntent(12)),
			new("D", (c, s) => s!.GainArmor(8), new DefendIntent()),
			new("A2", (c, s) => ExecuteAttackIntent(c, s!), new SingleAttackIntent(12)),
		};
	}
}
