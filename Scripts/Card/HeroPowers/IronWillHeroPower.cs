using Godot;
using OdysseyCards.Combat;

namespace OdysseyCards.Card.HeroPowers;

/// <summary>
/// 铁腕英雄技能 — 消耗 2 点法力，获得 4 点护甲。
/// 每回合限用一次。玩家初始英雄技能。
/// </summary>
public class IronWillHeroPower : IHeroPower
{
	public string Name => Localization.Localization.T("hero_power.iron_will.name", "铁腕");

	public int Cost => 2;

	public string Description => Localization.Localization.T("hero_power.iron_will.desc", "获得4点护甲");

	/// <summary>
	/// 检查是否可以使用：英雄存活、法力足够。
	/// </summary>
	public bool CanUse(Hero hero)
	{
		if (hero == null)
			return false;
		if (hero.IsDead)
			return false;
		return hero.CurrentMana >= Cost;
	}

	/// <summary>
	/// 执行英雄技能：获得 4 点护甲，消耗 2 点法力。
	/// </summary>
	/// <param name="hero">使用技能的英雄</param>
	/// <param name="combatManager">战斗管理器（object 类型，使用时 cast 为 CombatManager）</param>
	public void Execute(Hero hero, object combatManager)
	{
		if (hero == null)
		{
			GD.PrintErr("[IronWillHeroPower] Execute: hero 为 null");
			return;
		}

		if (combatManager is not CombatManager cm)
		{
			GD.PrintErr("[IronWillHeroPower] Execute: combatManager 不是 CombatManager 类型");
			return;
		}

		hero.GainArmor(4);
		hero.SpendMana(Cost);
		GD.Print($"[IronWillHeroPower] 铁腕发动 — 消耗 {Cost} 费，获得 4 点护甲（当前护甲：{hero.CurrentArmor}）");
	}
}
