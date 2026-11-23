using System;
using System.Collections.Generic;
using System.Linq;
using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Core;
using GD = OdysseyCards.Core.ModelLog;

namespace OdysseyCards.Combat;

/// <summary>
/// 0.2.2「断流」卡包的纯 C# 效果实现——不依赖 Godot Resource 实例，
/// 由 <see cref="CardEffectDispatcher"/> 的 CustomEffectName 分发调用，同时便于 xUnit 直测。
/// </summary>
public static class CutoffCurrentEffects
{
	/// <summary>
	/// 从指定英雄剩余手牌中随机弃牌；maxCost &lt;= 0 表示无费用上限。
	/// exclude 用于排除正在结算的卡牌（反制触发时施法牌仍在手中）。
	/// </summary>
	public static int DiscardRandomFromHand(Hero? hero, int count, int maxCost, Card.Card? exclude = null)
		=> DiscardRandomFromHandDetailed(hero, count, maxCost, exclude).Count;

	/// <summary>
	/// 同 <see cref="DiscardRandomFromHand"/>，但返回实际被弃掉的卡牌实例，
	/// 供战报展示「弃掉了哪张牌」并挂载历史卡面预览。
	/// </summary>
	public static IReadOnlyList<Card.Card> DiscardRandomFromHandDetailed(
		Hero? hero, int count, int maxCost, Card.Card? exclude = null)
	{
		if (hero == null || count <= 0)
			return Array.Empty<Card.Card>();

		var discarded = new List<Card.Card>();
		for (int i = 0; i < count; i++)
		{
			var candidates = hero.Hand
				.Where(card => !ReferenceEquals(card, exclude)
					&& (maxCost <= 0 || card.GetEffectiveCost() <= maxCost))
				.ToList();
			if (candidates.Count == 0)
				break;
			Card.Card picked = candidates[Random.Shared.Next(candidates.Count)];
			hero.DiscardCard(picked);
			// 奇巧会把卡牌改为自动打出，不进入弃牌堆——这类不算弃牌，也不应写进战报。
			if (hero.DeckState.DiscardPile.Contains(picked))
				discarded.Add(picked);
		}
		return discarded;
	}

	/// <summary>
	/// 使随从返回其所有者抽牌堆顶部。用 RecallMinion（非 RemoveMinion），不触发死亡与亡语。
	/// </summary>
	public static bool ReturnMinionToOwnerDeckTop(Board board, Hero? playerHero, Hero? enemyHero, object? target)
	{
		if (target is not Minion minion || minion.IsDead)
			return false;

		var owner = minion.OwnerHero
			?? (minion.IsPlayerSide ? playerHero : enemyHero);
		if (owner == null)
			return false;

		var clean = minion.Data != null
			? new Card.Card(minion.Data)
			: new Card.Card(minion.Id, CardType.Minion);
		board.RecallMinion(minion);
		owner.DeckState.AddToDrawPileTop(clean);
		string ownerName = owner.IsPlayerSide ? "玩家英雄" : "敌方英雄";
		GD.Print($"[CutoffCurrentEffects] 「{minion.CardName}」已返回 {ownerName} 的抽牌堆顶部（不触发亡语）");
		return true;
	}

	/// <summary>将 N 张指定 ID 的卡牌加入手牌。ids 用 '|' 分隔。</summary>
	public static int AddNamedCardsToHand(CommanderCore core, string ids, int count, Func<string, Card.Card> factory)
	{
		if (core == null || factory == null || count <= 0)
			return 0;

		int added = 0;
		foreach (string id in (ids ?? string.Empty)
			.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			for (int i = 0; i < count; i++)
			{
				var card = factory(id);
				if (card == null)
					continue;
				core.AddToHand(card);
				added++;
			}
		}
		return added;
	}

	/// <summary>能量币：基础 1 点 + 动态 +n 层数。</summary>
	public static int GainEnergyCoinMana(Hero hero, int bonus)
	{
		int amount = 1 + Math.Max(0, bonus);
		hero.GainMana(amount);
		return amount;
	}

	/// <summary>按 ID 或资源路径创建运行时卡；无 Godot 资源时回退为纯 C# 卡牌（便于 xUnit 直测）。</summary>
	public static Card.Card CreateCardById(string idOrPath, CardType fallbackType, Func<string, CardData?> loadCard)
	{
		var data = loadCard(idOrPath);
		if (data != null)
			return new Card.Card(data);

		string leaf = idOrPath;
		int slash = leaf.LastIndexOf('/');
		if (slash >= 0)
			leaf = leaf[(slash + 1)..];
		int dot = leaf.LastIndexOf('.');
		if (dot >= 0)
			leaf = leaf[..dot];
		return new Card.Card(leaf, fallbackType);
	}
}
