using System;
using System.Collections.Generic;
using System.Linq;
using OdysseyCards.AI;
using OdysseyCards.Card;
using OdysseyCards.Core;

namespace OdysseyCards.Combat;

/// <summary>英雄武器普攻与主动技能入口；所有校验与目标规则都以持械英雄自身阵营为基准。</summary>
internal sealed class WeaponAttackSystem(
	Board board, Hero playerHero, IReadOnlyList<EnemyUnit> enemyUnits, GameState state,
	DeathHandler deathHandler, Action onCombatStateChanged, Func<bool> isDiscovering,
	Action<Minion> triggerBaitTactics, CombatManager combatManager)
{
	public bool HeroWeaponAttackHero(Hero target) => Attack(playerHero, target);
	public bool HeroWeaponAttackMinion(Minion target) => Attack(playerHero, target);

	public bool Attack(Hero wielder, IDamageTarget target)
	{
		var weapon = wielder.Weapon;
		if (state.IsGameOver || isDiscovering() || weapon == null || !wielder.CanWeaponAttack()
			|| !wielder.CanSpendMana(weapon.AttackCost) || !IsOwnerTurn(wielder))
			return false;
		if (!IsLegalTarget(wielder, target))
			return false;

		bool friendly = target is Minion m && m.IsPlayerSide == wielder.IsPlayerSide;
		wielder.SpendMana(weapon.AttackCost);
		combatManager.BeginLogAction(CombatLogEntryKind.WeaponAction, wielder, null, target, wielder, weapon.Name);
		try
		{
			int damage = weapon.GetModifiedDamage(weapon.Attack, wielder);
			if (friendly)
				((Minion)target).Heal(((IFriendlyMinionWeaponAttackPassive)weapon.PassiveSkill!).GetFriendlyMinionHealAmount(damage));
			else
			{
				if (target is Minion minion)
					triggerBaitTactics(minion);
				bool hit = AttackCombatResolver.Resolve(wielder, target, damage,
					() => combatManager.RequestDamageVfx(wielder, target, DamageKind.Attack, CombatDamageVfxKind.Attack));
				if (hit)
					weapon.PassiveSkill?.OnWeaponHit(target, wielder);
			}
			wielder.RecordWeaponAttack();
			if (!wielder.IsDead && weapon.PassiveSkill is IWeaponAttackPassive passive)
				passive.OnWeaponAttackResolved(wielder, combatManager);
			deathHandler.CheckDeaths();
		}
		finally
		{
			combatManager.CommitLogAction();
		}
		onCombatStateChanged();
		return true;
	}

	public bool UseWeaponActiveSkill() => UseWeaponActiveSkill(playerHero, combatManager.ActiveSkillTarget);

	public bool UseWeaponActiveSkill(Hero wielder, IDamageTarget? target)
	{
		if (state.IsGameOver || isDiscovering() || !IsOwnerTurn(wielder))
			return false;
		var active = wielder.Weapon?.ActiveSkill;
		if (active == null || !active.CanUse(wielder))
			return false;
		combatManager.ActiveSkillTarget = target;
		combatManager.BeginLogAction(CombatLogEntryKind.WeaponAction, wielder, null, target, wielder, active.Name);
		try
		{
			active.Execute(wielder, combatManager);
			if (combatManager.ActiveSkillTarget != null)
				wielder.Weapon?.PassiveSkill?.OnWeaponHit(combatManager.ActiveSkillTarget, wielder);
			combatManager.ActiveSkillTarget = null;
			deathHandler.CheckDeaths();
		}
		finally
		{
			combatManager.ActiveSkillTarget = null;
			combatManager.CommitLogAction();
		}
		onCombatStateChanged();
		return true;
	}

	private bool IsOwnerTurn(Hero wielder)
		=> wielder.IsPlayerSide ? state.IsPlayerTurn : state.IsEnemyTurn;

	private bool IsLegalTarget(Hero wielder, IDamageTarget target)
	{
		bool friendly = target is Minion minion && minion.IsPlayerSide == wielder.IsPlayerSide;
		if (friendly)
		{
			var weapon = wielder.Weapon;
			return weapon != null && weapon.Range == WeaponRange.FriendlySupport
				&& weapon.PassiveSkill is IFriendlyMinionWeaponAttackPassive
				&& target is Minion { IsDead: false };
		}
		return AttackTargetRules.CanAttackTarget(board, wielder, target);
	}

	internal List<IDamageTarget> GetLegalTargets(Hero wielder)
	{
		var candidates = new List<IDamageTarget>();
		if (wielder.IsPlayerSide)
			candidates.AddRange(enemyUnits.Where(unit => !unit.Body.IsDead).Select(unit => (IDamageTarget)unit.Body));
		else if (!playerHero.IsDead)
			candidates.Add(playerHero);
		candidates.AddRange(wielder.IsPlayerSide ? board.GetEnemyMinions() : board.GetPlayerMinions());
		candidates.AddRange(wielder.IsPlayerSide ? board.GetPlayerMinions() : board.GetEnemyMinions());
		return candidates.Where(target => IsLegalTarget(wielder, target)).ToList();
	}
}
