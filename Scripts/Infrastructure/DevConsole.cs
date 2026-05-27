using Godot;
using OdysseyCards.Combat;
using OdysseyCards.UI;
using System;
using System.Linq;

namespace OdysseyCards.Infrastructure;

/// <summary>
/// 开发者控制台 — Autoload 单例。
/// 按反引号键 (`) 呼出/隐藏。支持文本命令和 AI 远程调用。
/// 命令格式: /&lt;action&gt; [参数]
/// </summary>
public partial class DevConsole : Node
{
    // ===== UI 组件 =====

    private CanvasLayer _canvasLayer = null!;
    private Panel _panel = null!;
    private RichTextLabel _output = null!;
    private LineEdit _input = null!;
    private bool _visible;

    // ===== 生命周期 =====

    public override void _Ready()
    {
        // 创建 UI 层级（置于最顶层）
        _canvasLayer = new CanvasLayer { Layer = 128, Name = "DevConsoleLayer" };
        AddChild(_canvasLayer);

        // 半透明背景面板
        _panel = new Panel
        {
            Name = "ConsolePanel",
            Size = new Vector2(800, 400),
            Position = new Vector2(240, 60),
            Visible = false,
        };
        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.05f, 0.1f, 0.92f),
            BorderWidthLeft = 2, BorderWidthRight = 2,
            BorderWidthTop = 2, BorderWidthBottom = 2,
            BorderColor = new Color(0.3f, 0.6f, 0.3f),
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
        };
        _panel.AddThemeStyleboxOverride("panel", panelStyle);
        _canvasLayer.AddChild(_panel);

        // VBox 布局
        var vbox = new VBoxContainer
        {
            AnchorLeft = 0, AnchorTop = 0, AnchorRight = 1, AnchorBottom = 1,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _panel.AddChild(vbox);

        // 标题栏
        var titleBar = new HBoxContainer();
        var title = new Label
        {
            Text = "  DevConsole",
            CustomMinimumSize = new Vector2(0, 28),
        };
        title.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));
        title.AddThemeFontSizeOverride("font_size", 14);
        titleBar.AddChild(title);
        vbox.AddChild(titleBar);

        // 输出区域
        _output = new RichTextLabel
        {
            Name = "Output",
            BbcodeEnabled = true,
            ScrollFollowing = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 300),
        };
        vbox.AddChild(_output);

        // 输入栏
        _input = new LineEdit
        {
            Name = "Input",
            PlaceholderText = "输入命令，如 /damage 10, /draw 5, /help ...",
            CustomMinimumSize = new Vector2(0, 32),
        };
        _input.TextSubmitted += OnCommandSubmitted;
        vbox.AddChild(_input);

        WriteLine("[color=#66ff66][DevConsole] 按 ` 键呼出/隐藏。输入 /help 查看命令[/color]");
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.Quoteleft) // 反引号键
            {
                Toggle();
            }
            else if (_visible && keyEvent.Keycode == Key.Escape)
            {
                Hide();
            }
        }
    }

    // ===== 可见性 =====

    public void Toggle()
    {
        if (_visible) Hide();
        else Show();
    }

    private void Show()
    {
        _visible = true;
        _panel.Visible = true;
        _input.GrabFocus();
        _input.Clear();
    }

    private void Hide()
    {
        _visible = false;
        _panel.Visible = false;
        _input.ReleaseFocus();
    }

    // ===== 命令处理 =====

    private void OnCommandSubmitted(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        _input.Clear();
        DevCommand(text);
        _input.GrabFocus();
    }

    /// <summary>
    /// 执行开发者命令。供 UI 输入和 AI 远程调用共用。
    /// </summary>
    /// <param name="cmd">命令字符串，格式: /&lt;action&gt; [参数]</param>
    public void DevCommand(string cmd)
    {
        cmd = cmd.Trim();
        WriteLine($"[color=#aaaaaa]> {cmd}[/color]");

        if (!cmd.StartsWith("/"))
        {
            WriteLine("[color=#ff6644]命令需以 / 开头，输入 /help 查看帮助[/color]");
            return;
        }

        var parts = cmd[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        var action = parts[0].ToLowerInvariant();

        try
        {
            Execute(action, parts);
        }
        catch (Exception e)
        {
            WriteLine($"[color=#ff4444]命令执行异常: {e.Message}[/color]");
        }
    }

    /// <summary>
    /// 根据 action 执行对应的游戏操作。
    /// </summary>
    private void Execute(string action, string[] parts)
    {
        var cm = CombatManager.Instance;
        if (cm == null && action != "help" && action != "clear")
        {
            WriteLine("[color=#ffaa44]未在战斗中，此命令需要 CombatManager[/color]");
            return;
        }

        int Arg(int i = 1) => parts.Length > i && int.TryParse(parts[i], out var v) ? v : 1;

        switch (action)
        {
            // ===== 伤害 =====
            case "damage":
            case "dmg":
                if (TryHandleClickDamage(parts, cm!)) return;
                cm!.EnemyHero.TakeDamage(Arg(), null);
                WriteLine($"[color=#ff6644]对敌方英雄造成 {Arg()} 点伤害（剩余 {cm.EnemyHero.CurrentHealth}）[/color]");
                cm.CheckVictoryOrDefeat();
                RefreshCombatUI(cm);
                break;

            // ===== 伤害敌方随从 =====
            case "damage_eslot":
            case "des":
                if (parts.Length < 3)
                { WriteLine("[color=#ffaa44]用法: /damage_eslot <槽位0-4> <伤害值>[/color]"); break; }
                if (!int.TryParse(parts[1], out var eslot) || eslot < 0 || eslot > 4)
                { WriteLine("[color=#ffaa44]槽位需为 0-4[/color]"); break; }
                int edmg = int.Parse(parts[2]);
                var em = cm!.Board.GetMinionAt(eslot, isPlayerSide: false);
                if (em == null || em.IsDead)
                { WriteLine($"[color=#ffaa44]敌方槽位 {eslot} 无有效随从[/color]"); break; }
                em.TakeDamage(edmg, null);
                WriteLine($"[color=#ff6644]对敌方槽位{eslot} {em.CardName} 造成 {edmg} 点伤害（剩余 {em.CurrentHealth}）[/color]");
                if (em.IsDead) { cm.Board.RemoveMinion(em); cm.TriggerDeathrattle(em); }
                cm.CheckDeaths();
                cm.CheckVictoryOrDefeat();
                RefreshCombatUI(cm);
                break;

            // ===== 伤害己方随从 =====
            case "damage_pslot":
            case "dps":
                if (parts.Length < 3)
                { WriteLine("[color=#ffaa44]用法: /damage_pslot <槽位0-4> <伤害值>[/color]"); break; }
                if (!int.TryParse(parts[1], out var pslot) || pslot < 0 || pslot > 4)
                { WriteLine("[color=#ffaa44]槽位需为 0-4[/color]"); break; }
                int pdmg = int.Parse(parts[2]);
                var pm = cm!.Board.GetMinionAt(pslot, isPlayerSide: true);
                if (pm == null || pm.IsDead)
                { WriteLine($"[color=#ffaa44]己方槽位 {pslot} 无有效随从[/color]"); break; }
                pm.TakeDamage(pdmg, null);
                WriteLine($"[color=#ff6644]对己方槽位{pslot} {pm.CardName} 造成 {pdmg} 点伤害（剩余 {pm.CurrentHealth}）[/color]");
                if (pm.IsDead) { cm.Board.RemoveMinion(pm); cm.TriggerDeathrattle(pm); }
                cm.CheckDeaths();
                cm.CheckVictoryOrDefeat();
                RefreshCombatUI(cm);
                break;

            // ===== 伤害己方英雄 =====
            case "damage_self":
            case "dself":
                cm!.PlayerHero.TakeDamage(Arg(), null);
                WriteLine($"[color=#ff6644]对己方英雄造成 {Arg()} 点伤害（剩余 {cm.PlayerHero.CurrentHealth}）[/color]");
                cm.CheckVictoryOrDefeat();
                RefreshCombatUI(cm);
                break;

            // ===== 伤害敌方英雄（显式） =====
            case "damage_enemy":
            case "denemy":
                cm!.EnemyHero.TakeDamage(Arg(), null);
                WriteLine($"[color=#ff6644]对敌方英雄造成 {Arg()} 点伤害（剩余 {cm.EnemyHero.CurrentHealth}）[/color]");
                cm.CheckVictoryOrDefeat();
                RefreshCombatUI(cm);
                break;

            // ===== 伤害全部敌方随从 =====
            case "damage_all":
            case "dall":
                var enemies = cm!.Board.GetEnemyMinions().Where(m => !m.IsDead).ToList();
                foreach (var e in enemies) { e.TakeDamage(Arg(), null);
                    if (e.IsDead) { cm.Board.RemoveMinion(e); cm.TriggerDeathrattle(e); } }
                WriteLine($"[color=#ff6644]对所有敌方随从造成 {Arg()} 点伤害（命中 {enemies.Count} 个目标）[/color]");
                cm.CheckDeaths();
                cm.CheckVictoryOrDefeat();
                RefreshCombatUI(cm);
                break;

            // ===== 抽牌 =====
            case "draw":
            case "d":
                cm!.PlayerHero.DrawCards(Arg());
                WriteLine($"[color=#44aaff]抽 {Arg()} 张牌（手牌 {cm.PlayerHero.Hand.Count}）[/color]");
                break;

            // ===== 法力 =====
            case "mana":
            case "m":
                cm!.PlayerHero.GainMana(Arg());
                WriteLine($"[color=#44ddff]获得 {Arg()} 点法力（当前 {cm.PlayerHero.CurrentMana}）[/color]");
                break;

            // ===== 治疗 =====
            case "heal":
            case "h":
                cm!.PlayerHero.Heal(Arg());
                WriteLine($"[color=#44ff44]恢复 {Arg()} 点生命值（当前 {cm.PlayerHero.CurrentHealth}）[/color]");
                break;

            // ===== 护甲 =====
            case "armor":
            case "a":
                cm!.PlayerHero.GainArmor(Arg());
                WriteLine($"[color=#aaaaff]获得 {Arg()} 点护甲（当前 {cm.PlayerHero.CurrentArmor}）[/color]");
                break;

            // ===== 强制结束回合 =====
            case "end":
            case "endturn":
                cm!.EndPlayerTurn();
                WriteLine("[color=#ffaa44]强制结束玩家回合[/color]");
                break;

            // ===== 刷新 UI =====
            case "refresh":
            case "r":
                // 通过场景树查找 CombatUI 并刷新
                var combatUINode = cm!.GetNodeOrNull<Control>("../CanvasLayer/CombatUI");
                if (combatUINode != null)
                {
                    var refreshMethod = combatUINode.GetType().GetMethod("RefreshAll");
                    refreshMethod?.Invoke(combatUINode, null);
                }
                WriteLine("[color=#aaaaaa]UI 已刷新[/color]");
                break;

            // ===== 帮助 =====
            case "help":
            case "?":
                WriteLine("[color=#66ff66]=== 开发者命令 ===");
                WriteLine("  /damage N       — 对敌方英雄造成 N 点伤害");
                WriteLine("  /damage_enemy N — 同上（显式）");
                WriteLine("  /damage_self N  — 对己方英雄造成 N 点伤害");
                WriteLine("  /damage_eslot X N — 对敌方槽位 X(0-4) 随从造成 N 点伤害");
                WriteLine("  /damage_pslot X N — 对己方槽位 X(0-4) 随从造成 N 点伤害");
                WriteLine("  /damage_all N   — 对所有敌方随从造成 N 点伤害");
                WriteLine("  /draw N         — 抽 N 张牌");
                WriteLine("  /mana N         — 获得 N 点法力");
                WriteLine("  /heal N         — 恢复 N 点生命值");
                WriteLine("  /armor N        — 获得 N 点护甲");
                WriteLine("  /end            — 强制结束回合");
                WriteLine("  /refresh        — 刷新 UI");
                WriteLine("  /clear          — 清空输出");
                WriteLine("  /help           — 显示帮助[/color]");
                break;

            // ===== 清空输出 =====
            case "clear":
            case "cls":
                _output.Clear();
                break;

            default:
                WriteLine($"[color=#ff6644]未知命令: /{action}，输入 /help 查看帮助[/color]");
                break;
        }
    }

    // ===== 输出 =====

    private void WriteLine(string text)
    {
        _output.AppendText($"{text}\n");
    }

    /// <summary>
    /// 通过场景树查找 CombatUI 并调用 RefreshAll，确保 UI 反映最新状态。
    /// </summary>
    private static void RefreshCombatUI(CombatManager cm)
    {
        var ui = cm.GetNodeOrNull<Control>("CanvasLayer/CombatUI");
        if (ui != null)
        {
            var m = ui.GetType().GetMethod("RefreshAll");
            m?.Invoke(ui, null);
        }
    }

    /// <summary>
    /// 进入点击伤害模式：隐藏控制台，通过 CombatUI 进入交互模式。
    /// </summary>
    private void EnterClickDamageMode(int damageAmount)
    {
        Hide();

        var cm = CombatManager.Instance;
        if (cm == null) { Show(); return; }

        var combatUI = cm.GetNodeOrNull<CombatUI>("CanvasLayer/CombatUI");
        if (combatUI == null) { Show(); return; }

        combatUI.EnterDevDamageMode(damageAmount);
        combatUI.OnDevDamageModeCompleted += () =>
        {
            WriteLine("[color=#66ff66]伤害选择完成[/color]");
            Show();
        };
    }

    /// <summary>
    /// 处理 /damage -c N 命令：进入点击伤害模式。
    /// </summary>
    private bool TryHandleClickDamage(string[] parts, CombatManager cm)
    {
        // /damage -c N → 交互式点击伤害
        if (parts.Length >= 3 && parts[1] == "-c" && int.TryParse(parts[2], out int dmg))
        {
            EnterClickDamageMode(dmg);
            return true;
        }
        return false;
    }
}
