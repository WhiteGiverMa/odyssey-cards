#nullable enable
using OdysseyCards.Combat;
using OdysseyCards.UI;
using System.Linq;
using static OdysseyCards.Infrastructure.Commands.CombatUIHelper;

namespace OdysseyCards.Infrastructure.Commands;

// ===== /damage 系列 =====

public class DamageCommand : DevConsoleCommand
{
	public override string Name => "damage";
	public override string[] Aliases => ["dmg"];
	public override string Signature => "/damage [-c] N";
	public override string Description => "对敌方英雄造成 N 点伤害。加 -c 进入点击模式。";
	public override CompletionCandidate[]? GetArgCandidates(string _) => null;
	public override CommandResult Execute(string[] args)
	{
		var cm = CombatManager.Instance;
		if (cm == null)
			return CommandResult.Fail("未在战斗中");
		int n = args.Length > 0 && int.TryParse(args[0], out var v) ? v : 1;
		var enemyHero = cm.EnemyUnits[0].Body;
		enemyHero.TakeDamage(n, null);
		cm.CheckVictoryOrDefeat();
		RefreshCombatUI(cm);
		return CommandResult.Ok($"对敌方英雄造成 {n} 点伤害（剩余 {enemyHero.CurrentHealth}）");
	}
}

public class DamageEnemyCommand : DevConsoleCommand
{
	public override string Name => "damage_enemy";
	public override string[] Aliases => ["denemy"];
	public override string Signature => "/damage_enemy N";
	public override string Description => "对敌方英雄造成 N 点伤害（显式）。";
	public override CommandResult Execute(string[] args)
	{
		var cm = CombatManager.Instance;
		if (cm == null)
			return CommandResult.Fail("未在战斗中");
		int n = args.Length > 0 && int.TryParse(args[0], out var v) ? v : 1;
		var enemyHero = cm.EnemyUnits[0].Body;
		enemyHero.TakeDamage(n, null);
		cm.CheckVictoryOrDefeat();
		RefreshCombatUI(cm);
		return CommandResult.Ok($"对敌方英雄造成 {n} 点伤害（剩余 {enemyHero.CurrentHealth}）");
	}
}

public class DamageSelfCommand : DevConsoleCommand
{
	public override string Name => "damage_self";
	public override string[] Aliases => ["dself"];
	public override string Signature => "/damage_self N";
	public override string Description => "对己方英雄造成 N 点伤害。";
	public override CommandResult Execute(string[] args)
	{
		var cm = CombatManager.Instance;
		if (cm == null)
			return CommandResult.Fail("未在战斗中");
		int n = args.Length > 0 && int.TryParse(args[0], out var v) ? v : 1;
		cm.PlayerHero.TakeDamage(n, null);
		cm.CheckVictoryOrDefeat();
		RefreshCombatUI(cm);
		return CommandResult.Ok($"对己方英雄造成 {n} 点伤害（剩余 {cm.PlayerHero.CurrentHealth}）");
	}
}

public class DamageESlotCommand : DevConsoleCommand
{
	public override string Name => "damage_eslot";
	public override string[] Aliases => ["des"];
	public override string Signature => "/damage_eslot X N";
	public override string Description => "对敌方槽位 X(0-4) 随从造成 N 点伤害。";
	public override CommandResult Execute(string[] args)
	{
		var cm = CombatManager.Instance;
		if (cm == null)
			return CommandResult.Fail("未在战斗中");
		if (args.Length < 2)
			return CommandResult.Fail("用法: /damage_eslot <槽位0-4> <伤害值>");
		if (!int.TryParse(args[0], out var slot) || slot < 0 || slot > 4)
			return CommandResult.Fail("槽位需为 0-4");
		int dmg = int.Parse(args[1]);
		var m = cm.Board.GetMinionAt(slot, isPlayerSide: false);
		if (m == null || m.IsDead)
			return CommandResult.Fail($"敌方槽位 {slot} 无有效随从");
		m.TakeDamage(dmg, null);
		string msg = $"对敌方槽位{slot} {m.CardName} 造成 {dmg} 点伤害（剩余 {m.CurrentHealth}）";
		if (m.IsDead)
			cm.Board.RemoveMinion(m);
		cm.CheckDeaths();
		cm.CheckVictoryOrDefeat();
		RefreshCombatUI(cm);
		return CommandResult.Ok(msg);
	}
}

public class DamagePSlotCommand : DevConsoleCommand
{
	public override string Name => "damage_pslot";
	public override string[] Aliases => ["dps"];
	public override string Signature => "/damage_pslot X N";
	public override string Description => "对己方槽位 X(0-4) 随从造成 N 点伤害。";
	public override CommandResult Execute(string[] args)
	{
		var cm = CombatManager.Instance;
		if (cm == null)
			return CommandResult.Fail("未在战斗中");
		if (args.Length < 2)
			return CommandResult.Fail("用法: /damage_pslot <槽位0-4> <伤害值>");
		if (!int.TryParse(args[0], out var slot) || slot < 0 || slot > 4)
			return CommandResult.Fail("槽位需为 0-4");
		int dmg = int.Parse(args[1]);
		var m = cm.Board.GetMinionAt(slot, isPlayerSide: true);
		if (m == null || m.IsDead)
			return CommandResult.Fail($"己方槽位 {slot} 无有效随从");
		m.TakeDamage(dmg, null);
		string msg = $"对己方槽位{slot} {m.CardName} 造成 {dmg} 点伤害（剩余 {m.CurrentHealth}）";
		if (m.IsDead)
			cm.Board.RemoveMinion(m);
		cm.CheckDeaths();
		cm.CheckVictoryOrDefeat();
		RefreshCombatUI(cm);
		return CommandResult.Ok(msg);
	}
}

public class DamageAllCommand : DevConsoleCommand
{
	public override string Name => "damage_all";
	public override string[] Aliases => ["dall"];
	public override string Signature => "/damage_all N";
	public override string Description => "对所有敌方随从造成 N 点伤害。";
	public override CommandResult Execute(string[] args)
	{
		var cm = CombatManager.Instance;
		if (cm == null)
			return CommandResult.Fail("未在战斗中");
		int n = args.Length > 0 && int.TryParse(args[0], out var v) ? v : 1;
		var enemies = cm.Board.GetEnemyMinions().Where(m => !m.IsDead).ToList();
		foreach (var e in enemies)
		{ e.TakeDamage(n, null); if (e.IsDead) cm.Board.RemoveMinion(e); }
		cm.CheckDeaths();
		cm.CheckVictoryOrDefeat();
		RefreshCombatUI(cm);
		return CommandResult.Ok($"对所有敌方随从造成 {n} 点伤害（命中 {enemies.Count} 个目标）");
	}
}

/// <summary>共享的 CombatUI 刷新辅助方法。</summary>
internal static class CombatUIHelper
{
	public static void RefreshCombatUI(CombatManager cm)
	{
		var ui = cm.GetNodeOrNull<CombatUI>("CanvasLayer/CombatUI");
		ui?.RefreshAll();
	}
}
