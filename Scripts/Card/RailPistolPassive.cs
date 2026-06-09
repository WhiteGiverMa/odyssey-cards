using Godot;
using OdysseyCards.Core;

namespace OdysseyCards.Card;

/// <summary>
/// 磁轨手枪被动——被此武器攻击的目标1回合内无法攻击。
/// OnWeaponHit 中向目标施加 "attack_ban" 状态效果，持续到目标的下个回合结束。
/// </summary>
public class RailPistolPassive : IWeaponPassive
{
	public string Name => "磁轨锁定";
	public string Description => "被此武器攻击的目标1回合内无法攻击";
	public string NameKey => "weapon.passive.rail_lock.name";
	public string DescKey => "weapon.passive.rail_lock.desc";

	public int ModifyWeaponDamage(int baseDamage) => baseDamage;

	public void OnWeaponHit(IDamageTarget target, Hero wielder)
	{
		// 施加 "attack_ban"：目标1回合内无法攻击
		if (target is Hero targetHero)
		{
			targetHero.AddStatusEffect(new StatusEffect(
				id: "attack_ban",
				stacks: 1,
				tickOn: targetHero.IsPlayerSide ? TickTiming.PlayerTurnEnd : TickTiming.EnemyTurnEnd
			));
			// 立即禁用武器攻击
			if (targetHero.Weapon != null)
				targetHero.Weapon.IsDisabled = true;
			GD.Print($"[RailPistol] {targetHero} 被磁轨锁定，1回合内无法攻击");
		}
		else if (target is Minion targetMinion)
		{
			targetMinion.AddStatusEffect(new StatusEffect(
				id: "attack_ban",
				stacks: 1,
				tickOn: targetMinion.IsPlayerSide ? TickTiming.PlayerTurnEnd : TickTiming.EnemyTurnEnd
			));
			GD.Print($"[RailPistol] {targetMinion.CardName} 被磁轨锁定，1回合内无法攻击");
		}
	}
}

/// <summary>
/// 磁轨手枪——宇宙员的武器。
/// 攻击力1，被动：磁轨锁定（目标1回合内无法攻击）。
/// </summary>
public class RailPistol : Weapon
{
	public RailPistol()
		: base(
			name: "磁轨手枪",
			attack: 1,
			attackCost: 0,
			passive: new RailPistolPassive())
	{
	}

	public override string NameKey => "weapon.rail_pistol.name";
}
