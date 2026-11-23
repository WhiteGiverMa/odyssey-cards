using System;
using System.Collections.Generic;
using System.Linq;
using OdysseyCards.Card;
using OdysseyCards.Character;
using OdysseyCards.Core;

namespace OdysseyCards.Combat;

/// <summary>
/// 0.2.3「早期战争」纯 C# 规则助手。
/// 把各张卡牌的判定与一次性效果从 Godot 节点层剥离，供 <see cref="CombatManager"/>、
/// <see cref="CardEffectDispatcher"/> 与 xUnit 共用同一套规则（不调用任何 Godot API）。
/// </summary>
public static class EarlyWarfareRules
{
	// ===== 机会~ =====

	/// <summary>「机会~」触发条件：玩家方伤害、真实造成伤害、且目标敌方指挥点槽数 &gt; 10。</summary>
	public static bool ShouldFetchChansu(bool sourceIsPlayerSide, DamageEventInfo info, int targetMaxMana)
		=> sourceIsPlayerSide
			&& (info.HpLost > 0 || info.ArmorAbsorbed > 0)
			&& targetMaxMana > 10;

	/// <summary>从弃牌堆或牌库把指定卡牌移入手中（优先弃牌堆）。返回是否找到。</summary>
	public static bool TryFetchCardToHand(CommanderCore core, string cardId)
	{
		ArgumentNullException.ThrowIfNull(core);
		if (string.IsNullOrEmpty(cardId))
			return false;

		var fromDiscard = core.DiscardPile.FirstOrDefault(card => card.Id == cardId);
		if (fromDiscard != null)
		{
			core.DiscardPile.Remove(fromDiscard);
			core.AddToHand(fromDiscard);
			return true;
		}

		var fromDrawPile = core.DrawPile.FirstOrDefault(card => card.Id == cardId);
		if (fromDrawPile == null)
			return false;
		core.DrawPile.Remove(fromDrawPile);
		core.AddToHand(fromDrawPile);
		return true;
	}

	// ===== 烟遁 / 拉烟哥 =====

	/// <summary>烟遁目标：施法方支援阵线中不具嘲讽、不具守护的友方随从。</summary>
	public static bool IsSmokeDodgeTarget(Minion minion, bool casterIsPlayerSide)
		=> minion.BoardRow == Board.SupportRow(casterIsPlayerSide) && !minion.HasTaunt && !minion.HasGuard;

	/// <summary>
	/// 给友方支援线中无嘲讽/守护的随从施加烟幕。
	/// tickOn 必须是施法方的对手回合结束，n 层才能覆盖接下来的 n 个敌方回合。
	/// </summary>
	public static int ApplySmokeDodge(IEnumerable<Minion> friends, bool casterIsPlayerSide, int stacks, TickTiming opponentTurnEnd)
	{
		ArgumentNullException.ThrowIfNull(friends);
		int applied = 0;
		foreach (var minion in friends.Where(minion => IsSmokeDodgeTarget(minion, casterIsPlayerSide)))
		{
			minion.AddStatusEffect(new StatusEffect(StatusEffect.SmokescreenId, Math.Max(1, stacks),
				opponentTurnEnd, StatusEffectPolarity.NonNegative));
			applied++;
		}
		return applied;
	}

	/// <summary>
	/// 拉烟哥：首次行动（移动或攻击）时给所属英雄 1 层跨敌方回合的烟幕；整场驻留只触发一次。
	/// </summary>
	public static bool TryGrantFirstActionSmoke(Minion actor, Hero? owner)
	{
		if (owner == null || !actor.HasSmokeGrantOnFirstAction || actor._firstActionDone)
			return false;
		actor._firstActionDone = true;
		owner.AddStatusEffect(new StatusEffect(StatusEffect.SmokescreenId, 1,
			owner.IsPlayerSide ? TickTiming.EnemyTurnEnd : TickTiming.PlayerTurnEnd, StatusEffectPolarity.NonNegative));
		return true;
	}

	// ===== 诱饵战术 =====

	/// <summary>
	/// 诱饵战术：被攻击时让施法方阵营的敌方英雄随机弃 1 张手牌。
	/// casterSideOpponents 必须是「相对施法方的敌方英雄」，与受击随从的阵营无关。
	/// </summary>
	public static bool TryTriggerBaitTactics(Minion target, IReadOnlyList<Hero> casterSideOpponents)
	{
		if (!target.HasBaitTacticsOnAttacked)
			return false;
		var victim = casterSideOpponents.Count > 0 ? casterSideOpponents[0] : null;
		if (victim == null)
			return false;
		CutoffCurrentEffects.DiscardRandomFromHand(victim, 1, 0);
		return true;
	}

	// ===== 完美格挡 =====

	/// <summary>
	/// 完美格挡：目标获得格挡，并监听其下一次受击。
	/// 若这次攻击没有造成生命伤害（finalDamage &lt;= 0）回调一次；监听必然先注销，绝不重复给牌。
	/// 两次完美格挡会挂两个独立监听，在下一次受击时各自触发并注销。
	/// </summary>
	public static void ApplyPerfectBlock(IDamageTarget target, int armor, Action onFullyBlocked)
	{
		ArgumentNullException.ThrowIfNull(target);
		ArgumentNullException.ThrowIfNull(onFullyBlocked);

		if (target is Hero hero)
		{
			hero.GainArmor(armor);
			void OnHeroAttacked(Hero attacked, IDamageSource _, int finalDamage)
			{
				attacked.OnAttacked -= OnHeroAttacked;
				if (finalDamage <= 0)
					onFullyBlocked();
			}
			hero.OnAttacked += OnHeroAttacked;
			return;
		}

		if (target is Minion minion)
		{
			minion.GainArmor(armor);
			void OnMinionAttacked(Minion attacked, IDamageSource _, int finalDamage)
			{
				attacked.OnAttacked -= OnMinionAttacked;
				if (finalDamage <= 0)
					onFullyBlocked();
			}
			minion.OnAttacked += OnMinionAttacked;
		}
	}

	// ===== 全体友方 +N/+N =====

	/// <summary>使所有友方随从获得 +attack/+health（英雄不在其中）。</summary>
	public static void BuffFriendlyMinions(IEnumerable<Minion> friends, int attack, int health)
	{
		ArgumentNullException.ThrowIfNull(friends);
		foreach (var minion in friends)
			minion.GainStats(attack, health);
	}

	// ===== M10「布克」动态行动花费 =====

	/// <summary>该阵营是否有存活先锋单位（进行中的死亡单位不算）。</summary>
	public static bool HasLivingVanguard(IEnumerable<Minion> minions, bool isPlayerSide)
		=> minions.Any(minion => !minion.IsDead && minion.IsPlayerSide == isPlayerSide
			&& minion.UnitClass == UnitClass.Vanguard);

	/// <summary>按是否有友方先锋同步「M10“布克”」行动花费调整值（-1 / 0）。</summary>
	public static void ApplyBookerActionCost(IEnumerable<Card.Card> cards, bool hasFriendlyVanguard)
	{
		ArgumentNullException.ThrowIfNull(cards);
		foreach (var card in cards)
		{
			if (card.Id == "minion_m10_booker")
				card.ActionCostModifier = hasFriendlyVanguard ? -1 : 0;
		}
	}

	// ===== 目标型部署 =====

	/// <summary>「移动目标至前线」类部署战吼的棋盘前置条件：目标必须在支援线且前线可进驻。</summary>
	public static bool IsValidMoveToFrontlineTarget(Board board, object? target)
		=> target is Minion minion && board.CanMoveMinionToFrontline(minion);

	/// <summary>
	/// 部署流程是否需要先选战吼目标；无任何合法目标时返回 false，随从直接裸拍上场、战吼跳过。
	/// </summary>
	public static bool NeedsBattlecryTargetSelection(bool cardRequiresTarget, bool hasAnyValidTarget)
		=> cardRequiresTarget && hasAnyValidTarget;

	// ===== 亡语抽牌归属 =====

	/// <summary>
	/// 亡语抽牌的归属标记：按施法方绝对阵营固化，避免随从拥有者（可能是被施法的敌方）窃取抽牌。
	/// </summary>
	public static string OwnerDrawTargetType(bool casterIsPlayerSide)
		=> casterIsPlayerSide ? "owner_player" : "owner_enemy";
}
