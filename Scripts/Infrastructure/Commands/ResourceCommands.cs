using OdysseyCards.Combat;

namespace OdysseyCards.Infrastructure.Commands;

// ===== /draw /mana /heal /armor =====

public class DrawCommand : DevConsoleCommand
{
    public override string Name => "draw";
    public override string[] Aliases => ["d"];
    public override string Signature => "/draw N";
    public override string Description => "抽 N 张牌。";
    public override CommandResult Execute(string[] args)
    {
        var cm = CombatManager.Instance;
        if (cm == null) return CommandResult.Fail("未在战斗中");
        int n = args.Length > 0 && int.TryParse(args[0], out var v) ? v : 1;
        cm.PlayerHero.DrawCards(n);
        return CommandResult.Ok($"抽 {n} 张牌（手牌 {cm.PlayerHero.Hand.Count}）");
    }
}

public class ManaCommand : DevConsoleCommand
{
    public override string Name => "mana";
    public override string[] Aliases => ["m"];
    public override string Signature => "/mana N";
    public override string Description => "获得 N 点法力。";
    public override CommandResult Execute(string[] args)
    {
        var cm = CombatManager.Instance;
        if (cm == null) return CommandResult.Fail("未在战斗中");
        int n = args.Length > 0 && int.TryParse(args[0], out var v) ? v : 1;
        cm.PlayerHero.GainMana(n);
        return CommandResult.Ok($"获得 {n} 点法力（当前 {cm.PlayerHero.CurrentMana}）");
    }
}

public class HealCommand : DevConsoleCommand
{
    public override string Name => "heal";
    public override string[] Aliases => ["h"];
    public override string Signature => "/heal N";
    public override string Description => "恢复 N 点生命值。";
    public override CommandResult Execute(string[] args)
    {
        var cm = CombatManager.Instance;
        if (cm == null) return CommandResult.Fail("未在战斗中");
        int n = args.Length > 0 && int.TryParse(args[0], out var v) ? v : 1;
        cm.PlayerHero.Heal(n);
        return CommandResult.Ok($"恢复 {n} 点生命值（当前 {cm.PlayerHero.CurrentHealth}）");
    }
}

public class ArmorCommand : DevConsoleCommand
{
    public override string Name => "armor";
    public override string[] Aliases => ["a"];
    public override string Signature => "/armor N";
    public override string Description => "获得 N 点护甲。";
    public override CommandResult Execute(string[] args)
    {
        var cm = CombatManager.Instance;
        if (cm == null) return CommandResult.Fail("未在战斗中");
        int n = args.Length > 0 && int.TryParse(args[0], out var v) ? v : 1;
        cm.PlayerHero.GainArmor(n);
        return CommandResult.Ok($"获得 {n} 点护甲（当前 {cm.PlayerHero.CurrentArmor}）");
    }
}
