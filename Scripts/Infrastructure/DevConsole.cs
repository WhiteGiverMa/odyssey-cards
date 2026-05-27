using Godot;
using OdysseyCards.Combat;
using System;

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
        int arg = parts.Length >= 2 && int.TryParse(parts[1], out var v) ? v : 1;

        try
        {
            Execute(action, arg);
        }
        catch (Exception e)
        {
            WriteLine($"[color=#ff4444]命令执行异常: {e.Message}[/color]");
        }
    }

    /// <summary>
    /// 根据 action 执行对应的游戏操作。
    /// </summary>
    private void Execute(string action, int arg)
    {
        var cm = CombatManager.Instance;
        if (cm == null && action != "help" && action != "clear")
        {
            WriteLine("[color=#ffaa44]未在战斗中，此命令需要 CombatManager[/color]");
            return;
        }

        switch (action)
        {
            // ===== 伤害 =====
            case "damage":
            case "dmg":
                cm!.EnemyHero.TakeDamage(arg, null);
                WriteLine($"[color=#ff6644]对敌方英雄造成 {arg} 点伤害（剩余 {cm.EnemyHero.CurrentHealth}）[/color]");
                cm.CheckVictoryOrDefeat(); // ← 注意：CheckVictoryOrDefeat 是 private，需要改为 internal
                break;

            // ===== 抽牌 =====
            case "draw":
            case "d":
                cm!.PlayerHero.DrawCards(arg);
                WriteLine($"[color=#44aaff]抽 {arg} 张牌（手牌 {cm.PlayerHero.Hand.Count}）[/color]");
                break;

            // ===== 法力 =====
            case "mana":
            case "m":
                cm!.PlayerHero.GainMana(arg);
                WriteLine($"[color=#44ddff]获得 {arg} 点法力（当前 {cm.PlayerHero.CurrentMana}）[/color]");
                break;

            // ===== 治疗 =====
            case "heal":
            case "h":
                cm!.PlayerHero.Heal(arg);
                WriteLine($"[color=#44ff44]恢复 {arg} 点生命值（当前 {cm.PlayerHero.CurrentHealth}）[/color]");
                break;

            // ===== 护甲 =====
            case "armor":
            case "a":
                cm!.PlayerHero.GainArmor(arg);
                WriteLine($"[color=#aaaaff]获得 {arg} 点护甲（当前 {cm.PlayerHero.CurrentArmor}）[/color]");
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
                WriteLine("  /damage N  — 对敌方英雄造成 N 点伤害");
                WriteLine("  /draw N    — 抽 N 张牌");
                WriteLine("  /mana N    — 获得 N 点法力");
                WriteLine("  /heal N    — 恢复 N 点生命值");
                WriteLine("  /armor N   — 获得 N 点护甲");
                WriteLine("  /end       — 强制结束回合");
                WriteLine("  /refresh   — 刷新 UI");
                WriteLine("  /clear     — 清空输出");
                WriteLine("  /help      — 显示帮助[/color]");
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
}
