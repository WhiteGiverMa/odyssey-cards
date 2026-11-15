using System;
using OdysseyCards.Card;
using OdysseyCards.Core;

namespace OdysseyCards.Combat;

/// <summary>普攻交锋统一结算。伏击替代反击，失能不可反击，支援/免反击武器不触发伏击。</summary>
internal static class AttackCombatResolver
{
	public static bool Resolve(IDamageSource attacker, IDamageTarget defender, int damage, Action? onAttack = null)
	{
		if (attacker is not IDamageTarget attackerTarget)
			return false;
		if (attacker is Minion { IsDead: true } or Minion { IsIncapacitated: true }
			or Hero { IsDead: true } or Hero { IsIncapacitated: true })
			return false;
		if (defender is Minion { IsDead: true } or Hero { IsDead: true })
			return false;
		bool ignoreCounter = attacker is Minion { UnitClass: UnitClass.Support } or Hero { Weapon.IgnoreCounterAttack: true };
		bool impact = attacker is Minion { HasImpact: true } or Hero { HasImpact: true };
		bool canCounter = defender switch
		{
			Minion m => !m.IsIncapacitated,
			Hero h => !h.IsIncapacitated && h.Weapon is { CanCounter: true },
			_ => false,
		};
		bool ambush = !ignoreCounter && canCounter && defender switch
		{
			Minion m => m.HasAmbush && !m.AmbushUsedThisTurn,
			Hero h => h.HasAmbush && !h.AmbushUsedThisTurn,
			_ => false,
		};
		int counter = defender switch
		{
			Minion m => m.Attack,
			Hero { Weapon: not null } h => h.Weapon.GetModifiedDamage(h.Weapon.Attack, h),
			_ => 0,
		};
		if (ambush)
		{
			if (defender is Minion m)
				m.AmbushUsedThisTurn = true;
			else if (defender is Hero h)
				h.AmbushUsedThisTurn = true;
			if (!impact)
				DealWithoutCounter(attackerTarget, counter, (IDamageSource)defender);
		}
		bool alive = attacker switch { Minion m => !m.IsDead, Hero h => !h.IsDead, _ => false };
		if (alive)
		{
			onAttack?.Invoke();
			DealWithoutCounter(defender, damage, attacker);
			if (canCounter && !ignoreCounter && !impact && !ambush)
				DealWithoutCounter(attackerTarget, counter, (IDamageSource)defender);
		}
		if (impact)
		{
			if (attacker is Minion m)
				m.HasImpact = false;
			else if (attacker is Hero h)
				h.HasImpact = false;
		}
		return alive;
	}

	private static void DealWithoutCounter(IDamageTarget target, int damage, IDamageSource source)
	{
		if (target is not Hero hero)
		{
			target.TakeDamage(damage, source, DamageKind.Attack);
			return;
		}
		bool previous = hero.SuppressWeaponCounter;
		hero.SuppressWeaponCounter = true;
		try
		{
			hero.TakeDamage(damage, source, DamageKind.Attack);
		}
		finally
		{
			hero.SuppressWeaponCounter = previous;
		}
	}
}
