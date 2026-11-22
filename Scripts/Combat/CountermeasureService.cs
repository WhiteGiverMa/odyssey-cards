using System;
using System.Collections.Generic;
using System.Linq;
using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Core;
using GD = OdysseyCards.Core.ModelLog;

namespace OdysseyCards.Combat;

/// <summary>
/// 反制牌（CardType.Countermeasure = 4）的纯 C# 规则服务，对齐 KARDS 反制指令：
/// <list type="bullet">
/// <item>友方回合花费法力在手中激活（Toggle/Arm），卡牌不离开手牌；</item>
/// <item>同一友方回合再次点击/拖出可取消激活（Toggle/Disarm），精确返还支付的法力；</item>
/// <item>敌方回合内敌方施放法术时由 <see cref="Intercept"/> 按手牌从左到右依次判定，
/// 「北风之神」使敌方随机弃 1 张，「俄亥俄级」取消 ≥4 费法术并塞回「能量币+1」；</item>
/// <item>防守方新回合开始时 <see cref="DisarmAll"/>，未触发的已激活反制牌失效，需重新付费。</item>
/// </list>
/// </summary>
public sealed class CountermeasureService
{
	private const string BoreiId = "counter_borei";
	private const string OhioId = "counter_ohio_ssbn";
	private const string CoinId = "spell_energy_coin";
	private const int OhioCostThreshold = 4;

	private readonly Func<string, CardData?> _loadCard;
	private readonly Action? _notify;

	public CountermeasureService(Func<string, CardData?> loadCard, Action? notify = null)
	{
		_loadCard = loadCard ?? (_ => null);
		_notify = notify;
	}

	/// <summary>是否可以在手中激活/取消激活这张反制牌（不负责回合判定）。</summary>
	public bool CanToggle(Hero owner, Card.Card? card, int? paidCost = null)
	{
		if (owner == null || card == null || card.Type != CardType.Countermeasure)
			return false;
		if (!owner.Hand.Contains(card))
			return false;

		int cost = paidCost ?? card.GetEffectiveCost();
		return card.IsCountermeasureArmed || owner.CanSpendMana(cost);
	}

	/// <summary>友方回合内在手中激活/取消激活。返回是否真的发生了状态变化。</summary>
	public bool Toggle(Hero owner, Card.Card? card, int? paidCost = null)
	{
		if (!CanToggle(owner, card, paidCost))
			return false;

		bool changed = card!.IsCountermeasureArmed
			? Disarm(owner, card)
			: Arm(owner, card, paidCost);
		if (changed)
			_notify?.Invoke();
		return changed;
	}

	/// <summary>花费法力激活，卡牌留在手牌。</summary>
	public bool Arm(Hero owner, Card.Card? card, int? paidCost = null)
	{
		if (owner == null || card == null || card.Type != CardType.Countermeasure
			|| card.IsCountermeasureArmed || !owner.Hand.Contains(card))
			return false;

		int cost = paidCost ?? card.GetEffectiveCost();
		if (!owner.CanSpendMana(cost))
			return false;

		owner.SpendMana(cost);
		card.IsCountermeasureArmed = true;
		card.ArmedManaCost = cost;
		GD.Print($"[CountermeasureService] {owner.CardNameOf(card)} 已激活（支付 {cost} 费，留在手中）");
		return true;
	}

	/// <summary>取消激活并返还实际支付的费用，卡牌留在手牌。</summary>
	public bool Disarm(Hero owner, Card.Card? card)
	{
		if (owner == null || card == null || !card.IsCountermeasureArmed)
			return false;

		int refund = card.ArmedManaCost;
		card.IsCountermeasureArmed = false;
		card.ArmedManaCost = 0;
		owner.RestoreMana(refund);
		GD.Print($"[CountermeasureService] {owner.CardNameOf(card)} 已取消激活（返还 {refund} 费）");
		return true;
	}

	/// <summary>防守方新回合开始：未触发的已激活反制牌自动失效（不退费，卡留在手牌）。</summary>
	public int DisarmAll(Hero owner)
	{
		if (owner == null)
			return 0;

		int count = 0;
		foreach (var card in owner.Hand.ToList())
		{
			if (!card.IsCountermeasureArmed)
				continue;
			card.IsCountermeasureArmed = false;
			card.ArmedManaCost = 0;
			count++;
		}
		if (count > 0)
		{
			GD.Print($"[CountermeasureService] {count} 张反制牌在新回合自动失效");
			_notify?.Invoke();
		}
		return count;
	}

	/// <summary>
	/// 敌方施放法术时触发防守方手牌中的已激活反制牌。
	/// </summary>
	/// <param name="caster">施法方（被干扰的一方）</param>
	/// <param name="defender">持有已激活反制牌的一方</param>
	/// <param name="spell">施放中的法术</param>
	/// <returns>Canceled=true 表示该法术不得结算、不得进入弃牌堆。</returns>
	public InterceptionResult Intercept(Hero caster, Hero defender, Card.Card? spell)
	{
		if (caster == null || defender == null || spell == null || spell.Type != CardType.Spell)
			return default;
		if (caster.IsDead || defender.IsDead)
			return default;

		bool canceled = false;
		var triggeredCards = new List<Card.Card>();
		int boreiTriggers = 0;
		int ohioTriggers = 0;
		int spellCost = spell.GetEffectiveCost();

		// 按手牌从左到右顺序检查；先复制快照，避免 DiscardCard 修改正在遍历的列表。
		foreach (var counter in defender.Hand.ToList())
		{
			if (!counter.IsCountermeasureArmed)
				continue;

			if (counter.Id == BoreiId)
			{
				Consume(defender, counter);
				triggeredCards.Add(counter);
				CutoffCurrentEffects.DiscardRandomFromHand(caster, 1, 0, exclude: spell);
				boreiTriggers++;
				GD.Print($"[CountermeasureService] 北风之神触发：{caster.CardNameOf(null)} 随机弃 1 张牌");
				continue;
			}

			if (counter.Id == OhioId && spellCost >= OhioCostThreshold)
			{
				Consume(defender, counter);
				triggeredCards.Add(counter);
				var coin = CreateCoin();
				caster.DeckState.AddToDrawPileTop(coin);
				canceled = true;
				ohioTriggers++;
				GD.Print($"[CountermeasureService] 俄亥俄级触发：「{spell.CardName}」被反制，能量币+1 塞回卡组顶");
				break; // 法术已被阻止，后续反制牌无需再判定
			}
		}

		if (boreiTriggers > 0 || canceled)
			_notify?.Invoke();

		return new InterceptionResult(canceled, boreiTriggers, ohioTriggers, triggeredCards);
	}

	/// <summary>消耗已激活的反制牌：移入手牌之外并进弃牌堆，不退费。</summary>
	private static void Consume(Hero defender, Card.Card counter)
	{
		counter.IsCountermeasureArmed = false;
		counter.ArmedManaCost = 0;
		defender.DiscardCard(counter);
	}

	private Card.Card CreateCoin()
	{
		var data = _loadCard(CoinId) ?? _loadCard("res://Resources/Cards/Spell_EnergyCoin.tres");
		var coin = data != null
			? new Card.Card(data)
			: new Card.Card(CoinId, CardType.Spell);
		coin.BonusEnergyValue = 1;
		return coin;
	}
}

/// <summary>反制拦截结果：Canceled=true 时调用方必须放弃法术结算与弃牌。</summary>
public readonly record struct InterceptionResult(
	bool Canceled,
	int BoreiTriggers,
	int OhioTriggers,
	IReadOnlyList<Card.Card>? TriggeredCards = null);

internal static class CountermeasureServiceExtensions
{
	internal static string CardNameOf(this Hero hero, Card.Card? card)
		=> card?.CardName ?? (hero.IsPlayerSide ? "玩家英雄" : "敌方英雄");
}
