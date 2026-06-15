using Godot;
using OdysseyCards.Combat;
using OdysseyCards.Core;
using OdysseyCards.Character;
using static OdysseyCards.Infrastructure.Commands.CombatUIHelper;
using Minion = OdysseyCards.Card.Minion;
using Hero = OdysseyCards.Card.Hero;

namespace OdysseyCards.Infrastructure.Commands;

// ===== /qa_tombstone /qa_bait_tactics /qa_new_cards =====

public class QaTombstoneCommand : DevConsoleCommand
{
	public override string Name => "qa_tombstone";
	public override string Signature => "/qa_tombstone";
	public override string Description => "验证墓碑伤害结算。";

	public override CommandResult Execute(string[] args)
	{
		var tombstoneData = GD.Load<CardData>("res://Resources/Cards/Minion_Tombstone.tres");
		if (tombstoneData == null)
			return CommandResult.Fail("QA失败：无法加载墓碑资源");

		var tombstone = new Minion(tombstoneData, isPlayerSide: true);
		var defendedTargetData = new CardData { Id = "qa_defended_target", CardName = "QA防御目标", Attack = 1, Health = 20, Defense = 1 };
		var defendedTarget = new Minion(defendedTargetData, isPlayerSide: false);

		int battlecryDamage = DamageResolver.ResolveDamage(1, tombstone, defendedTarget, DamageKind.Effect);
		int attackDamage = DamageResolver.ResolveDamage(tombstone.Attack, tombstone, defendedTarget, DamageKind.Attack);

		var friendlyHeroCore = new CommanderCore();
		friendlyHeroCore.InitializeHealth(30);
		var friendlyHero = new Hero(friendlyHeroCore, isPlayerSide: true) { Weapon = new OdysseyCards.Card.MagicWand() };
		friendlyHero.ModifyDefense(1);
		friendlyHero.TakeDamage(1, tombstone, DamageKind.Effect);
		bool effectDidNotCounter = tombstone.CurrentHealth == tombstone.MaxHealth;
		bool effectDamageResolved = friendlyHero.CurrentHealth == 27;

		var counterHeroCore = new CommanderCore();
		counterHeroCore.InitializeHealth(30);
		var counterHero = new Hero(counterHeroCore, isPlayerSide: false) { Weapon = new OdysseyCards.Card.RollingLog() };
		counterHero.TakeDamage(tombstone.Attack, tombstone, DamageKind.Attack);
		bool counterDamageUsedDefense = tombstone.CurrentHealth == tombstone.MaxHealth;

		bool passed = battlecryDamage == 3 && attackDamage == 8 && effectDamageResolved && effectDidNotCounter && counterDamageUsedDefense;

		return passed
			? CommandResult.Ok($"墓碑QA通过：战吼效果={battlecryDamage}，攻击={attackDamage}，Effect不反击={effectDidNotCounter}，反击吃防={counterDamageUsedDefense}")
			: CommandResult.Fail($"墓碑QA失败：战吼效果={battlecryDamage}（期望3），攻击={attackDamage}（期望8），Effect后墓碑血={tombstone.CurrentHealth}（期望{tombstone.MaxHealth}），友方英雄血={friendlyHero.CurrentHealth}（期望27）");
	}
}

public class QaBaitTacticsCommand : DevConsoleCommand
{
	public override string Name => "qa_bait_tactics";
	public override string Signature => "/qa_bait_tactics";
	public override string Description => "验证诱饵战术双阵营触发。";

	public override CommandResult Execute(string[] args)
	{
		var cm = CombatManager.Instance;
		if (cm == null)
			return CommandResult.Fail("未在战斗中");

		string result = CombatRuntimeQa.RunBaitTacticsQa(cm);
		RefreshCombatUI(cm);
		return result.Contains("QA通过") ? CommandResult.Ok(result) : CommandResult.Fail(result);
	}
}

public class QaNewCardsCommand : DevConsoleCommand
{
	public override string Name => "qa_new_cards";
	public override string Signature => "/qa_new_cards";
	public override string Description => "验证近期新卡的核心规则行为。";

	public override CommandResult Execute(string[] args)
	{
		var cm = CombatManager.Instance;
		if (cm == null)
			return CommandResult.Fail("未在战斗中");

		string result = CombatRuntimeQa.RunNewCardsQa(cm);
		RefreshCombatUI(cm);
		return result.Contains("QA通过") ? CommandResult.Ok(result) : CommandResult.Fail(result);
	}
}
