using System;
using Godot;
using OdysseyCards.AI.Intents;
using OdysseyCards.Card;
using OdysseyCards.Combat;
using OdysseyCards.Core;

namespace OdysseyCards.AI;

/// <summary>
/// 劫蛋者。
/// 开场释放难逃之瑕，随后每 3 回合将 B/C/D 随机排列后依次执行。
/// </summary>
public sealed class Goutansha : EnemyEncounter
{
	private const string SmartEggPath = "res://Resources/Cards/Minion_SmartStinkyEgg.tres";

	private readonly int[] _cycleOrder = [0, 1, 2];
	private bool _openingMovePending = true;
	private int _cycleIndex;

	public Goutansha()
		: base("劫蛋者", 35, [new EnemyIntent(IntentType.DebuffStrong, 0, "")])
	{
		MoveStates = [new MoveState("goutansha_placeholder", null, new UnknownIntent())];
		PrepareNextCycleOrder();
	}

	public override int StartingDefense => -1;

	public override Weapon CreateWeapon() => new LightCrucibleClamp();

	public override MoveState GetCurrentMove(CombatManager combat, Hero self)
	{
		if (_openingMovePending)
		{
			return new MoveState(
				"goutansha_opening_flaw",
				(cm, _) => ExecuteOpeningDebuff(cm),
				new DebuffIntent(strong: true),
				new SpellCastIntent("难逃之瑕", "使玩家获得难逃之瑕、2层脆弱和1层易伤"));
		}

		return _cycleOrder[_cycleIndex] switch
		{
			0 => new MoveState(
				"goutansha_focus_summon",
				(cm, hero) => ExecuteFocusSummon(cm, hero!),
				new BuffIntent(),
				new SummonIntent()),
			1 => new MoveState(
				"goutansha_spell_barrage",
				(cm, hero) => ExecuteSpellBarrage(cm, hero!),
				new SpellDamageIntent(
					spellName: "恶臭连弹",
					spellDescription: "对玩家随机目标造成 4 点法术伤害 2 次，获得 2 层蓄谋",
					damageCalc: c => DamageResolver.ResolvePreviewDamage(4, self, c.PlayerHero, DamageKind.Effect),
					repeats: 2),
				new BuffIntent()),
			_ => new MoveState(
				"goutansha_guard_summon",
				(cm, hero) => ExecuteGuardSummon(cm, hero!),
				new DefendIntent(),
				new SummonIntent()),
		};
	}

	public override void ExecuteIntent(CombatManager combat, Hero self)
	{
		var move = GetCurrentMove(combat, self);
		GD.Print($"[劫蛋者] 执行 MoveState：{move.Id}");
		move.OnPerform?.Invoke(combat, self);
	}

	public override void AdvanceMove()
	{
		_cachedAttackTarget = null;
		if (_openingMovePending)
		{
			_openingMovePending = false;
			_cycleIndex = 0;
			return;
		}

		_cycleIndex++;
		if (_cycleIndex >= _cycleOrder.Length)
		{
			_cycleIndex = 0;
			PrepareNextCycleOrder();
		}
	}

	public override void AdvanceIntent()
	{
		AdvanceMove();
	}

	private void PrepareNextCycleOrder()
	{
		for (int i = _cycleOrder.Length - 1; i > 0; i--)
		{
			int swapIndex = Random.Shared.Next(i + 1);
			(_cycleOrder[i], _cycleOrder[swapIndex]) = (_cycleOrder[swapIndex], _cycleOrder[i]);
		}
	}

	private static void ExecuteOpeningDebuff(CombatManager combat)
	{
		combat.PlayerHero.AddDomain("inescapable_flaw", null);
		combat.PlayerHero.AddStatusEffect(new StatusEffect("fragile", 2, TickTiming.PlayerTurnEnd));
		combat.PlayerHero.AddStatusEffect(new StatusEffect("vulnerable", 1, TickTiming.PlayerTurnEnd));
		GD.Print("[劫蛋者] 使玩家获得难逃之瑕、2层脆弱和1层易伤");
	}

	private static void ExecuteFocusSummon(CombatManager combat, Hero self)
	{
		self.AddDomain("focus", null);
		self.AddDomain("focus", null);
		SummonSmartEggs(combat, self, 1);
		GD.Print("[劫蛋者] 获得 2 层聚焦，并召唤 1 个智能臭鸡蛋");
	}

	private static void ExecuteSpellBarrage(CombatManager combat, Hero self)
	{
		for (int hit = 0; hit < 2; hit++)
		{
			combat.DealSmartEnemySpellDamage(self, 4);
		}

		self.AddDomain("scheme", null);
		self.AddDomain("scheme", null);
		GD.Print("[劫蛋者] 对随机玩家目标造成法术伤害 2 次，并获得 2 层蓄谋");
	}

	private static void ExecuteGuardSummon(CombatManager combat, Hero self)
	{
		self.GainArmor(7);
		SummonSmartEggs(combat, self, 2);
		GD.Print("[劫蛋者] 获得 7 点格挡，并召唤 2 个智能臭鸡蛋");
	}

	private static void SummonSmartEggs(CombatManager combat, Hero summoner, int count)
	{
		if (!ResourceLoader.Exists(SmartEggPath))
		{
			GD.PrintErr($"[劫蛋者] 未找到智能臭鸡蛋资源：{SmartEggPath}");
			return;
		}

		var data = GD.Load<CardData>(SmartEggPath);
		if (data == null)
		{
			GD.PrintErr("[劫蛋者] 智能臭鸡蛋资源加载失败");
			return;
		}

		int schemeStacks = summoner.ActiveDomains.TryGetValue("scheme", out var scheme)
					? Math.Max(0, scheme.StackCount)
			: 0;

		for (int i = 0; i < count; i++)
		{
			int slot = combat.Board.GetEmptySlotIndex(isPlayerSide: false);
			if (slot < 0)
				break;

			var egg = new Minion(data, isPlayerSide: false)
			{
				IntentBrain = null,
			};
			egg.IntentBrain = new SmartStinkyEggBrain(egg);

			for (int stack = 0; stack < schemeStacks; stack++)
			{
				egg.AddDomain("focus", null);
			}

			combat.Board.PlaceMinion(egg, slot);
			if (schemeStacks > 0)
			{
				GD.Print($"[劫蛋者] 智能臭鸡蛋视作触发战吼：获得 {schemeStacks} 层聚焦");
			}
		}
	}
}
