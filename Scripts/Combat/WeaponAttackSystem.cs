using System.Collections.Generic;
using Godot;
using OdysseyCards.AI;
using OdysseyCards.Card;
using OdysseyCards.Core;

namespace OdysseyCards.Combat;

/// <summary>
/// 武器攻击系统——管理玩家英雄的武器攻击与主动技能。
/// 从 CombatManager 拆出为独立类，负责武器攻击英雄、武器攻击随从、武器主动技能使用。
/// </summary>
internal sealed class WeaponAttackSystem
{
	private readonly Board _board;
	private readonly Hero _playerHero;
	private readonly IReadOnlyList<EnemyUnit> _enemyUnits;
	private readonly GameState _state;
	private readonly AttackTracker _attackTracker;
	private readonly DeathHandler _deathHandler;
	private System.Action _onCombatStateChanged;
	private readonly System.Func<bool> _isDiscovering;
	private readonly System.Action<Minion> _triggerBaitTactics;
	private readonly System.Action<bool> _onGameOver;
	private readonly CombatManager _combatManager;

	public WeaponAttackSystem(
		Board board,
		Hero playerHero,
		IReadOnlyList<EnemyUnit> enemyUnits,
		GameState state,
		AttackTracker attackTracker,
		DeathHandler deathHandler,
		System.Action onCombatStateChanged,
		System.Func<bool> isDiscovering,
		System.Action<Minion> triggerBaitTactics,
		System.Action<bool> onGameOver,
		CombatManager combatManager)
	{
		_board = board;
		_playerHero = playerHero;
		_enemyUnits = enemyUnits;
		_state = state;
		_attackTracker = attackTracker;
		_deathHandler = deathHandler;
		_onCombatStateChanged = onCombatStateChanged;
		_isDiscovering = isDiscovering;
		_triggerBaitTactics = triggerBaitTactics;
		_onGameOver = onGameOver;
		_combatManager = combatManager;
	}

	/// <summary>
	/// 玩家英雄使用武器攻击敌方英雄。
	/// 对敌方英雄造成武器攻击力伤害，同时受到敌方武器反击伤害。
	/// </summary>
	/// <returns>攻击成功返回 true</returns>
	public bool HeroWeaponAttackHero(Hero target)
	{
		if (_isDiscovering())
		{
			GD.PrintErr("[CombatManager] HeroWeaponAttackHero 失败 — 正在发现选牌阶段");
			return false;
		}

		if (!_state.IsPlayerTurn)
		{
			GD.PrintErr("[CombatManager] HeroWeaponAttackHero 失败 — 不是玩家回合");
			return false;
		}

		if (!_playerHero.CanWeaponAttack())
		{
			GD.PrintErr("[CombatManager] HeroWeaponAttackHero 失败 — 武器不可用");
			return false;
		}

		if (!_playerHero.CanSpendMana(_playerHero.Weapon!.AttackCost))
		{
			GD.PrintErr($"[CombatManager] HeroWeaponAttackHero 失败 — 法力不足（需 {_playerHero.Weapon.AttackCost}，现有 {_playerHero.CurrentMana}）");
			return false;
		}

		// 消耗法力
		_playerHero.SpendMana(_playerHero.Weapon.AttackCost);

		// 计算武器伤害
		int weaponDamage = _playerHero.Weapon.GetModifiedDamage(_playerHero.Weapon.Attack, _playerHero);

		GD.Print($"[CombatManager] ⚔ 玩家英雄使用 {_playerHero.Weapon.Name} 攻击敌方英雄，造成 {weaponDamage} 点伤害");

		// 对敌方英雄造成伤害（敌方英雄的武器反击由 Hero.TakeDamage → CounterAttack 自动处理）
		bool targetWasSuppressingCounter = target.SuppressWeaponCounter;
		target.SuppressWeaponCounter = true;
		_combatManager.RequestDamageVfx(_playerHero, target, DamageKind.Attack, CombatDamageVfxKind.Attack);
		target.TakeDamage(weaponDamage, _playerHero);
		target.SuppressWeaponCounter = targetWasSuppressingCounter;

		// 触发武器被动命中效果（如熔毁：目标防御-1）
		_playerHero.Weapon?.PassiveSkill?.OnWeaponHit(target, _playerHero);
		if (_playerHero.Weapon?.PassiveSkill is IWeaponAttackPassive attackPassive)
			attackPassive.OnWeaponAttackResolved(_playerHero, _combatManager);

		// 记录武器攻击
		_playerHero.RecordWeaponAttack();

		GD.Print($"[CombatManager]   敌方英雄剩余生命值：{target.CurrentHealth}（护甲：{target.CurrentArmor}）");

		// 敌方英雄武器反击：显式结算，避免完全依赖隐式链路。
		if (!target.IsDead && target.Weapon != null && target.Weapon.CanCounter)
		{
			int counterDamage = target.Weapon.GetModifiedDamage(target.Weapon.Attack, target);
			GD.Print($"[CombatManager]   ⚔ 敌方英雄武器反击，造成 {counterDamage} 点伤害");
			bool wasSuppressing = _playerHero.SuppressWeaponCounter;
			_playerHero.SuppressWeaponCounter = true;
			_playerHero.TakeDamage(counterDamage, target, DamageKind.Attack);
			_playerHero.SuppressWeaponCounter = wasSuppressing;
		}

		// 检查我方英雄是否被敌方武器反击致死
		if (_playerHero.IsDead)
		{
			GD.Print("[CombatManager]   ☠ 玩家英雄在武器攻击时被敌方武器反击击杀！");
			GameManager.Instance?.RunState?.FailRun();
			_state.SetDefeat();
			_onGameOver?.Invoke(false);
			return true;
		}

		return true;
	}

	/// <summary>
	/// 玩家英雄使用武器攻击敌方随从。
	/// 对敌方随从造成武器攻击力伤害，同时受到随从攻击力反击（互砍）。
	/// 武器反击在此流程中被抑制，避免无限循环。
	/// </summary>
	/// <param name="target">目标敌方随从</param>
	/// <returns>攻击成功返回 true</returns>
	public bool HeroWeaponAttackMinion(Minion target)
	{
		if (_isDiscovering())
		{
			GD.PrintErr("[CombatManager] HeroWeaponAttackMinion 失败 — 正在发现选牌阶段");
			return false;
		}

		if (!_state.IsPlayerTurn)
		{
			GD.PrintErr("[CombatManager] HeroWeaponAttackMinion 失败 — 不是玩家回合");
			return false;
		}

		if (!_playerHero.CanWeaponAttack())
		{
			GD.PrintErr("[CombatManager] HeroWeaponAttackMinion 失败 — 武器不可用");
			return false;
		}

		if (!_playerHero.CanSpendMana(_playerHero.Weapon!.AttackCost))
		{
			GD.PrintErr($"[CombatManager] HeroWeaponAttackMinion 失败 — 法力不足（需 {_playerHero.Weapon.AttackCost}，现有 {_playerHero.CurrentMana}）");
			return false;
		}

		if (target == null || target.IsDead)
		{
			GD.PrintErr("[CombatManager] HeroWeaponAttackMinion 失败 — 目标无效");
			return false;
		}

		bool isFriendlyTarget = target.IsPlayerSide;
		if (isFriendlyTarget && _playerHero.Weapon?.PassiveSkill is not IFriendlyMinionWeaponAttackPassive)
		{
			GD.PrintErr("[CombatManager] HeroWeaponAttackMinion 失败 — 不能攻击己方随从");
			return false;
		}

		// 嘲讽检测：武器攻击也受嘲讽限制
		var enemyTaunts = _board.GetTaunts(ofEnemy: true);
		if (!isFriendlyTarget && enemyTaunts.Count > 0 && !enemyTaunts.Contains(target))
		{
			GD.PrintErr($"[CombatManager] HeroWeaponAttackMinion 失败 — 敌方有 {enemyTaunts.Count} 个嘲讽随从阻挡");
			return false;
		}

		// 消耗法力
		_playerHero.SpendMana(_playerHero.Weapon.AttackCost);

		// 计算武器伤害
		int weaponDamage = _playerHero.Weapon.GetModifiedDamage(_playerHero.Weapon.Attack, _playerHero);
		if (isFriendlyTarget && _playerHero.Weapon.PassiveSkill is IFriendlyMinionWeaponAttackPassive friendlyAttackPassive)
		{
			int healAmount = friendlyAttackPassive.GetFriendlyMinionHealAmount(weaponDamage);
			target.Heal(healAmount);
			_playerHero.RecordWeaponAttack();

			if (_playerHero.Weapon.PassiveSkill is IWeaponAttackPassive resolvedPassive)
				resolvedPassive.OnWeaponAttackResolved(_playerHero, _combatManager);

			GD.Print($"[CombatManager] ✨ 玩家英雄使用 {_playerHero.Weapon.Name} 为 {target.CardName} 贴膜，获得 {healAmount} 点生命");
			return true;
		}

		GD.Print($"[CombatManager] ⚔ 玩家英雄使用 {_playerHero.Weapon.Name} 攻击 {target.CardName}，造成 {weaponDamage} 点伤害");

		_triggerBaitTactics(target);

		// 伏击检查：目标有伏击且本回合未消耗时，目标先手攻击英雄
		bool targetAmbush = target.HasAmbush && !target.AmbushUsedThisTurn;
		if (targetAmbush)
		{
			target.AmbushUsedThisTurn = true;
			GD.Print($"[CombatManager]   ⚡ {target.CardName} 伏击先手，对英雄造成 {target.Attack} 伤害");
			_playerHero.SuppressWeaponCounter = true;
			_playerHero.TakeDamage(target.Attack, target);
			_playerHero.SuppressWeaponCounter = false;

			// 伏击击杀英雄 → 攻击被取消
			if (_playerHero.IsDead)
			{
				GD.Print($"[CombatManager]   ☠ 玩家英雄被 {target.CardName} 伏击击杀，攻击被取消");
				return false;
			}
		}

		// 英雄武器攻击随从（第一次伤害：英雄→随从）
		_combatManager.RequestDamageVfx(_playerHero, target, DamageKind.Attack, CombatDamageVfxKind.Attack);
		target.TakeDamage(weaponDamage, _playerHero);

		// 触发武器被动命中效果（如熔毁：目标防御-1）
		_playerHero.Weapon?.PassiveSkill?.OnWeaponHit(target, _playerHero);
		if (_playerHero.Weapon?.PassiveSkill is IWeaponAttackPassive attackPassive)
			attackPassive.OnWeaponAttackResolved(_playerHero, _combatManager);

		// 随从反击英雄（第二次伤害：随从→英雄）。
		// 如果伏击已触发则跳过——伏击先手已经完成了随从的反击。
		// 抑制武器反击，避免英雄武器对随从的反击再次触发。
		if (!target.IsDead && !targetAmbush)
		{
			_playerHero.SuppressWeaponCounter = true;
			_playerHero.TakeDamage(target.Attack, target);
			_playerHero.SuppressWeaponCounter = false;
		}

		target.TriggerIdolTwilightOnAttacked();

		// 记录武器攻击
		_playerHero.RecordWeaponAttack();

		GD.Print($"[CombatManager]   交锋后 — 英雄剩余 {_playerHero.CurrentHealth}HP，" +
				  $"{target.CardName}：{target.CurrentHealth}血");

		// 检查随从死亡
		if (target.IsDead)
		{
			GD.Print($"[CombatManager]   ☠ {target.CardName} 被击杀");
			_board.RemoveMinion(target);
		}

		// 全局死亡检查
		_deathHandler.CheckDeaths();
		// 胜负判定由 Hero.OnDeath 事件驱动，不再手动调用
		return true;
	}

	/// <summary>
	/// 执行武器主动技能。
	/// 由 CombatUI 的技能按钮触发。
	/// </summary>
	/// <returns>执行成功返回 true</returns>
	public bool UseWeaponActiveSkill()
	{
		if (_isDiscovering())
		{
			GD.PrintErr("[CombatManager] UseWeaponActiveSkill 失败 — 正在发现选牌阶段");
			return false;
		}

		if (!_state.IsPlayerTurn)
		{
			GD.PrintErr("[CombatManager] UseWeaponActiveSkill 失败 — 不是玩家回合");
			return false;
		}

		var active = _playerHero.Weapon?.ActiveSkill;
		if (active == null)
		{
			GD.PrintErr("[CombatManager] UseWeaponActiveSkill 失败 — 武器无主动技能");
			return false;
		}

		if (!active.CanUse(_playerHero))
		{
			GD.PrintErr($"[CombatManager] UseWeaponActiveSkill 失败 — 技能不可用（冷却 {active.CurrentCooldown}，法力 {_playerHero.CurrentMana}/{active.Cost}）");
			return false;
		}

		GD.Print($"[CombatManager] ★ 使用武器主动技能：{active.Name}");
		active.Execute(_playerHero, _combatManager);

		// 触发武器被动命中效果（如熔毁：目标防御-1）
		if (_combatManager.ActiveSkillTarget != null)
		{
			_playerHero.Weapon?.PassiveSkill?.OnWeaponHit(_combatManager.ActiveSkillTarget, _playerHero);
		}

		_combatManager.ActiveSkillTarget = null;

		return true;
	}
}
