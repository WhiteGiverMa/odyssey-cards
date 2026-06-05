using Godot;
using OdysseyCards.AI;
using OdysseyCards.Combat;
using OdysseyCards.Core;
using OdysseyCards.UI;
using System;
using System.Linq;

namespace OdysseyCards.Infrastructure.Commands;

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
