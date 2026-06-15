using Godot;
using OdysseyCards.Combat;

namespace OdysseyCards.Card.HeroPowers;

/// <summary>
/// 绮梦英雄技能：花费4，抽1张牌，回复2点英雄生命。
/// 英雄治疗不突破生命上限。
/// </summary>
public class AyameStarlightSupplyHeroPower : IHeroPower
{
	public string Name => Localization.Localization.T("hero_power.ayame_starlight_supply.name", "星光补给");

	public int Cost => 4;

	public string Description => Localization.Localization.T("hero_power.ayame_starlight_supply.desc", "抽1张牌，回复2点生命值。");

	public bool CanUse(Hero hero)
	{
		return hero != null && !hero.IsDead && hero.CurrentMana >= Cost;
	}

	public void Execute(Hero hero, object combatManager)
	{
		if (combatManager is not CombatManager)
		{
			GD.PrintErr("[AyameHeroPower] Execute: combatManager 不是 CombatManager 类型");
			return;
		}

		if (!CanUse(hero))
			return;

		hero.SpendMana(Cost);
		var drawn = hero.DrawCards(1);
		hero.Heal(2);
		GD.Print($"[AyameHeroPower] 星光补给发动 — 抽牌 {drawn.Count} 张，回复2点生命");
	}
}
