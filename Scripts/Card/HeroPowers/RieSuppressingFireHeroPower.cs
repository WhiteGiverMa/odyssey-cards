using Godot;
using OdysseyCards.Combat;
using OdysseyCards.Core;

namespace OdysseyCards.Card.HeroPowers;

/// <summary>
/// 理恵英雄技能：抽取牌堆顶连续的直伤法术，每抽一张对敌方英雄造成1点效果伤害。
/// </summary>
public class RieSuppressingFireHeroPower : IHeroPower, IChargeCooldownSkill
{
	public string Name => Localization.Localization.T("hero_power.rie_suppressing_fire.name", "火力筛选");
	public int Cost => 6;
	public string Description => Localization.Localization.T("hero_power.rie_suppressing_fire.desc", "从牌堆顶抽取直伤法术，直到手牌满或遇到非直伤牌。每抽1张，对敌方英雄造成1点伤害。");
	public int Charges { get; private set; } = 1;
	public int MaxCharges => 1;
	public int Cooldown => 3;
	public int CurrentCooldown { get; private set; }

	public bool CanUse(Hero hero)
	{
		return hero != null && !hero.IsDead && Charges > 0 && hero.CurrentMana >= Cost;
	}

	public void Execute(Hero hero, object combatManager)
	{
		if (combatManager is not CombatManager combat)
		{
			GD.PrintErr("[RieHeroPower] Execute: combatManager 不是 CombatManager 类型");
			return;
		}

		if (!CanUse(hero))
			return;

		hero.SpendMana(Cost);
		Charges--;
		if (Charges < MaxCharges && CurrentCooldown <= 0)
			CurrentCooldown = Cooldown;

		int drawnCount = 0;
		while (hero.DeckState.Hand.Count < hero.DeckState.MaxHandSize && hero.DeckState.DrawPile.Count > 0)
		{
			var drawn = hero.DrawCards(1);
			if (drawn.Count == 0)
				break;

			var drawnCard = drawn[0];
			drawnCount++;
			var enemy = combat.GetDefaultEnemyTargetUnit()?.Body;
			if (enemy != null)
			{
				combat.RequestDamageVfx(hero, enemy, DamageKind.Effect, CombatDamageVfxKind.Spell);
				enemy.TakeDamage(1, hero, DamageKind.Effect);
			}

			if (hero.DeckState.Hand.Count >= hero.DeckState.MaxHandSize)
				break;

			if (!IsExplicitDirectDamageSpell(drawnCard.Data))
				break;
		}

		combat.CheckDeaths();
		GD.Print($"[RieHeroPower] 火力筛选抽取 {drawnCount} 张直伤法术，剩余层数 {Charges}/{MaxCharges}");
	}

	public void TickChargeCooldown()
	{
		if (Charges >= MaxCharges)
		{
			CurrentCooldown = 0;
			return;
		}

		if (CurrentCooldown > 0)
			CurrentCooldown--;

		if (CurrentCooldown <= 0)
		{
			Charges++;
			CurrentCooldown = Charges < MaxCharges ? Cooldown : 0;
			GD.Print($"[RieHeroPower] 回复1层，当前 {Charges}/{MaxCharges}");
		}
	}

	private static bool IsExplicitDirectDamageSpell(CardData data)
	{
		if (data == null || data.Type != CardType.Spell)
			return false;

		foreach (var effect in data.Effects)
		{
			if (effect.Value <= 0)
				continue;

			switch (effect.EffectType)
			{
				case CardEffectType.Damage:
				case CardEffectType.DealDamageToTarget:
				case CardEffectType.DealDamageToAllEnemies:
				case CardEffectType.DealDamageToEnemyHero:
				case CardEffectType.DealDamageToFriendlyHero:
					return true;
			}
		}

		return false;
	}
}
