using System;
using System.Linq;
using OdysseyCards.Combat;
using static OdysseyCards.Infrastructure.Commands.CombatUIHelper;

namespace OdysseyCards.Infrastructure.Commands;

// ===== /draw /mana /heal /armor =====

public class DrawCommand : ChatScreenCommand
{
	public override string Name => "draw";
	public override string[] Aliases => ["d"];
	public override string Signature => "/draw N";
	public override string Description => "抽 N 张牌。";
	public override CommandResult Execute(string[] args)
	{
		var cm = CombatManager.Instance;
		if (cm == null)
			return CommandResult.Fail("未在战斗中");
		int n = args.Length > 0 && int.TryParse(args[0], out var v) ? v : 1;
		cm.PlayerHero.DrawCards(n);
		return CommandResult.Ok($"抽 {n} 张牌（手牌 {cm.PlayerHero.Hand.Count}）");
	}
}

public class ManaCommand : ChatScreenCommand
{
	public override string Name => "mana";
	public override string[] Aliases => ["m"];
	public override string Signature => "/mana N";
	public override string Description => "获得 N 点法力。";
	public override CommandResult Execute(string[] args)
	{
		var cm = CombatManager.Instance;
		if (cm == null)
			return CommandResult.Fail("未在战斗中");
		int n = args.Length > 0 && int.TryParse(args[0], out var v) ? v : 1;
		cm.PlayerHero.GainMana(n);
		return CommandResult.Ok($"获得 {n} 点法力（当前 {cm.PlayerHero.CurrentMana}）");
	}
}

public class HealCommand : ChatScreenCommand
{
	public override string Name => "heal";
	public override string[] Aliases => ["h"];
	public override string Signature => "/heal N";
	public override string Description => "恢复 N 点生命值。";
	public override CommandResult Execute(string[] args)
	{
		var cm = CombatManager.Instance;
		if (cm == null)
			return CommandResult.Fail("未在战斗中");
		int n = args.Length > 0 && int.TryParse(args[0], out var v) ? v : 1;
		cm.PlayerHero.Heal(n);
		return CommandResult.Ok($"恢复 {n} 点生命值（当前 {cm.PlayerHero.CurrentHealth}）");
	}
}

public class ArmorCommand : ChatScreenCommand
{
	public override string Name => "armor";
	public override string[] Aliases => ["a"];
	public override string Signature => "/armor N";
	public override string Description => "获得 N 点护甲。";
	public override CommandResult Execute(string[] args)
	{
		var cm = CombatManager.Instance;
		if (cm == null)
			return CommandResult.Fail("未在战斗中");
		int n = args.Length > 0 && int.TryParse(args[0], out var v) ? v : 1;
		cm.PlayerHero.GainArmor(n);
		return CommandResult.Ok($"获得 {n} 点护甲（当前 {cm.PlayerHero.CurrentArmor}）");
	}
}

public class PurifyCommand : ChatScreenCommand
{
	public override string Name => "purify";
	public override string[] Aliases => ["cleanse"];
	public override string Signature => "/purify all | /purify <n>";
	public override string Description => "净化玩家英雄所有负面效果，或按效果栏从左到右第 n 个负面效果净化。";

	public override CompletionCandidate[]? GetArgCandidates(string partialArg)
	{
		var cm = CombatManager.Instance;
		if (cm == null)
		{
			return
			[
				new CompletionCandidate("all", "all", "净化玩家英雄全部负面效果"),
				new CompletionCandidate("1", "1", "净化第 1 个负面效果"),
			];
		}

		var effects = cm.PlayerHero.GetPurifiableNegativeEffects();
		var candidates = effects
			.Select((effect, index) => new CompletionCandidate(
				(index + 1).ToString(),
				(index + 1).ToString(),
				$"{effect.Name} x{Math.Max(1, effect.Stacks)}"))
			.ToList();

		candidates.Insert(0, new CompletionCandidate("all", "all", $"净化全部负面效果（当前 {effects.Count} 个）"));
		return candidates
			.Where(c => c.InsertText.StartsWith(partialArg, StringComparison.OrdinalIgnoreCase))
			.Take(8)
			.ToArray();
	}

	public override CommandResult Execute(string[] args)
	{
		var cm = CombatManager.Instance;
		if (cm == null)
			return CommandResult.Fail("未在战斗中");

		if (args.Length == 0 || string.Equals(args[0], "all", StringComparison.OrdinalIgnoreCase))
		{
			int removed = cm.PlayerHero.PurifyAllNegativeEffects();
			RefreshCombatUI(cm);
			return CommandResult.Ok($"已净化玩家英雄全部负面效果（移除 {removed} 个）");
		}

		if (!int.TryParse(args[0], out var index) || index <= 0)
			return CommandResult.Fail("用法: /purify all 或 /purify <n>");

		string? removedName = cm.PlayerHero.PurifyNegativeEffectAt(index);
		if (removedName == null)
			return CommandResult.Fail($"不存在第 {index} 个可净化负面效果");

		RefreshCombatUI(cm);
		return CommandResult.Ok($"已净化第 {index} 个负面效果：{removedName}");
	}
}
