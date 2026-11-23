using System.Collections.Generic;
using System.Linq;
using Godot;
using OdysseyCards.Combat;

namespace OdysseyCards.Card.HeroPowers;

/// <summary>
/// 溯光英雄技能：洗切抽牌堆，抽2张牌，然后强制选择弃1张牌。
/// </summary>
public class SokouRestructureHeroPower : IHeroPower, IChargeCooldownSkill
{
	public string Name => Localization.Localization.T("hero_power.sokou_restructure.name", "重整");
	public int Cost => 4;
	public string Description => Localization.Localization.T("hero_power.sokou_restructure.desc", "洗切抽牌堆，抽2张牌，然后弃1张牌。冷却1回合，最多存储2层。");
	public int Charges { get; private set; } = 1;
	public int MaxCharges => 2;
	public int Cooldown => 1;
	public int CurrentCooldown { get; private set; }

	public bool CanUse(Hero hero)
	{
		return hero != null && !hero.IsDead && Charges > 0 && hero.CurrentMana >= Cost;
	}

	public void Execute(Hero hero, object combatManager)
	{
		if (combatManager is not CombatManager combat)
		{
			GD.PrintErr("[SokouHeroPower] Execute: combatManager 不是 CombatManager 类型");
			return;
		}

		if (!CanUse(hero))
			return;

		hero.SpendMana(Cost);
		Charges--;
		if (Charges < MaxCharges && CurrentCooldown <= 0)
			CurrentCooldown = Cooldown;

		hero.ShuffleDrawPile();
		var drawn = hero.DrawCards(2);
		GD.Print($"[SokouHeroPower] 重整：洗切抽牌堆，抽取 {drawn.Count} 张");

		if (hero.DeckState.Hand.Count == 0)
		{
			GD.Print("[SokouHeroPower] 重整：手牌为空，跳过弃牌");
			return;
		}

		combat.BeginForcedHandDiscardSelection(
			hero,
			hero.DeckState.Hand.ToList(),
			1,
			CombatManager.PendingSelectionMode.RestructureDiscard,
			selected => DiscardSelected(combat, hero, selected, Name));
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
			GD.Print($"[SokouHeroPower] 回复1层，当前 {Charges}/{MaxCharges}");
		}
	}

	private static void DiscardSelected(CombatManager combat, Hero hero, IReadOnlyList<Card> selected, string powerName)
	{
		var discarded = selected.Take(1).ToArray();
		if (discarded.Length == 0)
			return;

		// 玩家选牌是异步的：英雄技能作用域可能已提交，这里补一个「重整」弃牌作用域，
		// 保证战报能看到具体被弃掉的牌与卡面；同步路径沿用外层作用域，避免条目倒序。
		CombatLogEntry? scope = combat.CombatLog.HasPendingAction
			? null
			: combat.BeginLogAction(CombatLogEntryKind.HeroPower, hero, primarySource: hero, actorName: powerName);
		try
		{
			foreach (var card in discarded)
			{
				hero.DiscardCard(card);
				GD.Print($"[SokouHeroPower] 重整弃掉「{card.CardName}」");
			}
		}
		finally
		{
			if (scope != null)
				combat.CommitLogAction(scope);
		}
	}
}
