using Godot;
using OdysseyCards.AI;
using OdysseyCards.Combat;
using OdysseyCards.Core;
using OdysseyCards.Roguelike;
using OdysseyCards.UI;
using System;
using System.Linq;

namespace OdysseyCards.Infrastructure.Commands;

internal static class CommandSceneLookup
{
	public static MapUI FindMapUI()
	{
		var root = GameManager.Instance?.GetTree()?.Root;
		return root == null ? null : FindMapUIRecursive(root);
	}

	private static MapUI FindMapUIRecursive(Node node)
	{
		if (node is MapUI mapUI)
			return mapUI;

		foreach (var child in node.GetChildren())
		{
			var found = FindMapUIRecursive(child);
			if (found != null)
				return found;
		}

		return null;
	}
}

// ===== /end /fight /refresh /intent_debug =====

public class EndCommand : DevConsoleCommand
{
    public override string Name => "end";
    public override string[] Aliases => ["endturn"];
    public override string Signature => "/end";
    public override string Description => "强制结束玩家回合。";
    public override CommandResult Execute(string[] args)
    {
        var cm = CombatManager.Instance;
        if (cm == null) return CommandResult.Fail("未在战斗中");
        cm.EndPlayerTurn();
        return CommandResult.Ok("强制结束玩家回合");
    }
}

public class FightCommand : DevConsoleCommand
{
    public override string Name => "fight";
    public override string Signature => "/fight <enemy>";
    public override string Description => "直接与指定敌人战斗（跳过地图）。";

    public override CompletionCandidate[]? GetArgCandidates(string partialArg)
    {
        return EnemyRegistry.AllIds
            .Where(id => id.StartsWith(partialArg, StringComparison.OrdinalIgnoreCase))
            .OrderBy(id => id)
            .Take(8)
            .Select(id => new CompletionCandidate(id, id, "直接开启对应战斗"))
            .ToArray();
    }

    public override CommandResult Execute(string[] args)
    {
        var cm = CombatManager.Instance;
        if (args.Length < 1)
            return CommandResult.Fail($"用法: /fight <enemy>  可用: {string.Join(", ", EnemyRegistry.AllIds)}");

        var fightId = args[0].ToLowerInvariant();
        var fightEnemies = EnemyRegistry.Create(fightId);
        if (fightEnemies.Count == 0)
            return CommandResult.Fail($"未知敌人: {fightId}，可用: {string.Join(", ", EnemyRegistry.AllIds)}");

        GameManager.Instance!.FightOverride = fightEnemies;
        // DevConsole.cs will handle the scene change since it needs Godot API
        return CommandResult.Ok($"__FIGHT__{string.Join(", ", fightEnemies.Select(e => e.Name))}");
    }
}

public class RefreshCommand : DevConsoleCommand
{
    public override string Name => "refresh";
    public override string[] Aliases => ["r"];
    public override string Signature => "/refresh";
    public override string Description => "刷新战斗 UI。";
    public override CommandResult Execute(string[] args)
    {
        var cm = CombatManager.Instance;
        if (cm == null) return CommandResult.Fail("未在战斗中");
        var combatUINode = cm.GetNodeOrNull<Control>("../CanvasLayer/CombatUI");
        if (combatUINode != null)
        {
            var refreshMethod = combatUINode.GetType().GetMethod("RefreshAll");
            refreshMethod?.Invoke(combatUINode, null);
        }
        return CommandResult.Ok("UI 已刷新");
    }
}

public class IntentDebugCommand : DevConsoleCommand
{
    public override string Name => "intent_debug";
    public override string Signature => "/intent_debug";
    public override string Description => "显示当前敌方意图目标（QA）。";
    public override CommandResult Execute(string[] args)
    {
        var cm = CombatManager.Instance;
        if (cm == null) return CommandResult.Fail("未在战斗中");

        var lines = new System.Text.StringBuilder();
        for (int i = 0; i < cm.EnemyUnits.Count; i++)
        {
            var unit = cm.EnemyUnits[i];
            var intent = unit.GetCurrentIntent(cm);
            var target = intent.GetTarget(cm);
            lines.AppendLine($"[Enemy[{i}] {unit.Brain.Name}: {intent.Type} -> {DescribeTarget(target)}, damage={intent.GetEffectiveDamage(cm)}]");
        }

        var combatUI = cm.GetNodeOrNull<CombatUI>("CanvasLayer/CombatUI");
        var arrows = combatUI?.GetIntentArrowDebugInfo();
        lines.Append(string.IsNullOrEmpty(arrows) ? "Arrows: <none>" : $"Arrows:\n{arrows}");

        return CommandResult.Ok(lines.ToString());
    }

    private static string DescribeTarget(IDamageTarget? target) => target switch
    {
        OdysseyCards.Card.Hero h => h.IsPlayerSide ? "Hero:Player" : "Hero:Enemy",
        OdysseyCards.Card.Minion m => $"Minion:{m.GetLocalizedName()}@{m.BoardSlotIndex}",
        null => "<none>",
        _ => target.GetType().Name,
    };
}

// ===== /skip（原名 /room）=====

public class SkipCommand : DevConsoleCommand
{
	public override string Name => "skip";
	public override string[] Aliases => [];
	public override string Signature => "/skip [--no-reward]";
	public override string Description => "直接结束当前房间。添加 --no-reward 跳过战斗金币奖励。";

	public override CommandResult Execute(string[] args)
	{
		bool grantReward = !args.Contains("--no-reward");
		var cm = CombatManager.Instance;
		var gm = GameManager.Instance;

		// 情况 1：在战斗中 → 强制胜利
		if (cm != null && !cm.State.IsGameOver)
		{
			cm.ForceVictory(grantReward);
			return CommandResult.Ok($"强制结束战斗房间（{(grantReward ? "含金币奖励" : "跳过奖励")}）");
		}

		// 情况 2：在地图界面 → 直接推进
		var mapUI = CommandSceneLookup.FindMapUI();
		if (mapUI != null)
		{
			mapUI.DevForceCompleteRoom();
			return CommandResult.Ok("已跳过当前房间，推进到下一层");
		}

		return CommandResult.Fail("无法找到 MapUI，且不在战斗中。请确认当前处于冒险中。");
	}
}

// ===== /room <type>（参考 STS2 RoomConsoleCmd）=====

public class RoomCommand : DevConsoleCommand
{
	public override string Name => "room";
	public override string Signature => "/room <type> [--id <eventId>]";
	public override string Description => "用指定类型房间覆盖当前层。类型: monster/elite/boss/treasure/shop/event/rest。Event 类型可用 --id 指定事件。";

	public override CompletionCandidate[]? GetArgCandidates(string partialArg)
	{
		var types = new[] { "monster", "elite", "boss", "treasure", "shop", "event", "rest" };
		return types
			.Where(t => t.StartsWith(partialArg, StringComparison.OrdinalIgnoreCase))
			.Select(t => new CompletionCandidate(t, t, GetTypeDescription(t)))
			.ToArray();
	}

	private static string GetTypeDescription(string type) => type switch
	{
		"monster" => "普通怪物战斗",
		"elite" => "精英怪物战斗",
		"boss" => "Boss战斗",
		"treasure" => "奖励房间",
		"shop" => "商店",
		"event" => "叙事事件",
		"rest" => "休息站点",
		_ => "",
	};

	public override CommandResult Execute(string[] args)
	{
		if (args.Length < 1)
			return CommandResult.Fail("用法: /room <type>  类型: monster/elite/boss/treasure/shop/event/rest");

		var gm = GameManager.Instance;
		if (gm?.RunState == null)
			return CommandResult.Fail("当前不在冒险中");

		var typeStr = args[0].ToLowerInvariant();
		Roguelike.RoomType? roomType = typeStr switch
		{
			"monster" => Roguelike.RoomType.Monster,
			"elite" => Roguelike.RoomType.Elite,
			"boss" => Roguelike.RoomType.Boss,
			"treasure" => Roguelike.RoomType.Treasure,
			"shop" => Roguelike.RoomType.Shop,
			"event" => Roguelike.RoomType.Event,
			"rest" => Roguelike.RoomType.RestSite,
			_ => null,
		};

		if (roomType == null)
			return CommandResult.Fail($"未知房间类型: {typeStr}，可用: monster/elite/boss/treasure/shop/event/rest");

		// 提取 --id 参数（Event 专用）
		string? eventId = null;
		for (int i = 1; i < args.Length - 1; i++)
		{
			if (args[i] == "--id" && i + 1 < args.Length)
				eventId = args[i + 1];
		}

		// 验证事件 ID
		if (roomType == Roguelike.RoomType.Event && eventId != null)
		{
			var found = Roguelike.EventPool.All.Any(e => e.Id.Equals(eventId, StringComparison.OrdinalIgnoreCase));
			if (!found)
				return CommandResult.Fail($"未知事件: {eventId}，可用: {string.Join(", ", Roguelike.EventPool.All.Select(e => e.Id))}");
		}

		// 设置覆写
		gm.RoomTypeOverride = roomType;
		gm.EventIdOverride = eventId;

		// 战斗类型 → 切到战斗场景；非战斗 → 切到地图
		var isCombat = roomType is Roguelike.RoomType.Monster or Roguelike.RoomType.Elite or Roguelike.RoomType.Boss;
		if (isCombat)
		{
			// 在地图外（如已在地图）也需要切场景让 CombatManager 重新读取
			var cm = CombatManager.Instance;
			if (cm != null && !cm.State.IsGameOver)
			{
				gm.FightOverride = null; // 清掉可能残留的战斗覆写
				return CommandResult.Ok("__ROOM__Combat");
			}
			return CommandResult.Ok("__ROOM__Combat");
		}

		// 非战斗类型 → 切到地图
		var cm2 = CombatManager.Instance;
		if (cm2 != null && !cm2.State.IsGameOver)
			return CommandResult.Ok("__ROOM__Map");

		// 已在地图 → 直接触发
		var mapUI = CommandSceneLookup.FindMapUI();
		if (mapUI != null)
		{
			mapUI.RefreshRoomChoices();   // 触发消费 RoomTypeOverride，直接显示指定房间
			return CommandResult.Ok($"当前层已覆盖为: {roomType}");
		}

		return CommandResult.Fail("无法找到地图界面。");
	}
}

// ===== /event =====

public class EventCommand : DevConsoleCommand
{
	public override string Name => "event";
	public override string Signature => "/event [id]";
	public override string Description => "用指定事件覆盖当前房间（效果真实，完成后推进层数）。无参数则随机。";

	public override CompletionCandidate[]? GetArgCandidates(string partialArg)
	{
		return Roguelike.EventPool.All
			.Where(e => e.Id.StartsWith(partialArg, StringComparison.OrdinalIgnoreCase))
			.Select(e => new CompletionCandidate(e.Id, e.Id, e.Title))
			.ToArray();
	}

	public override CommandResult Execute(string[] args)
	{
		var gm = GameManager.Instance;
		string? eventId = args.Length > 0 ? args[0] : null;

		// 验证事件 ID 有效性（非空时）
		if (eventId != null)
		{
			var found = EventPool.All.Any(e => e.Id.Equals(eventId, StringComparison.OrdinalIgnoreCase));
			if (!found)
				return CommandResult.Fail($"未知事件: {eventId}，可用: {string.Join(", ", EventPool.All.Select(e => e.Id))}");
		}

		// 情况 1：已在战斗中 → 设 RoomTypeOverride + 直接切场景
		var cm = CombatManager.Instance;
		if (cm != null && !cm.State.IsGameOver)
		{
			gm!.RoomTypeOverride = Roguelike.RoomType.Event;
			gm.EventIdOverride = eventId;
			return CommandResult.Ok($"__EVENT__{(eventId ?? "随机")}");
		}

		// 情况 2：在地图中 → 直接覆盖当前房间
		var mapUI = CommandSceneLookup.FindMapUI();
		if (mapUI != null)
		{
			mapUI.DevShowEvent(eventId);
			return CommandResult.Ok($"当前房间已覆盖为事件：{(eventId ?? "随机")}");
		}

		return CommandResult.Fail("当前不在冒险中。请先开始一局游戏。");
	}
}
