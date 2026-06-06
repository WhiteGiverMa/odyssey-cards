using Godot;
using OdysseyCards.Combat;
using OdysseyCards.Core;
using OdysseyCards.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OdysseyCards.Infrastructure;

/// <summary>
/// 开发者控制台 — Autoload 单例。
/// 按反引号键 (`) 呼出/隐藏。支持文本命令和 AI 远程调用。
/// </summary>
/// <remarks>
/// 移动端入口：设置页 → 开发者模式开关 → 控制台按钮 → 调用 Toggle()。
/// 开发模式状态通过 IsDevMode 全局共享，PauseMenu 和 SettingsPage 共用。
/// </remarks>
/// <remarks>
/// AI 调用方式（godot-mcp）：
///   game_call_method(nodePath="/root/DevConsole", method="DevCommand", args=["/damage 10"])
///
/// 命令列表：
///   /damage N              对敌方英雄造成 N 点伤害。加 -c 进入点击模式。
///   /damage_enemy N         同上（显式）。
///   /damage_self N          对己方英雄造成 N 点伤害。
///   /damage_eslot X N       对敌方槽位 X(0-4) 随从造成 N 点伤害。
///   /damage_pslot X N       对己方槽位 X(0-4) 随从造成 N 点伤害。
///   /damage_all N           对所有敌方随从造成 N 点伤害。
///   /draw N                 抽 N 张牌。别名 /d。
///   /mana N                 获得 N 点法力。别名 /m。
///   /heal N                 恢复 N 点生命值。别名 /h。
///   /armor N                获得 N 点护甲。别名 /a。
///   /end                    强制结束回合。
///   /refresh                刷新 UI。别名 /r。
///   /clear                  清空控制台输出。别名 /cls。
///   /token &lt;card_id&gt;       将指定 ID 的卡牌加入手牌。别名 /t。
///   /play &lt;card_id&gt;        从手牌打出领域/无目标法术。别名 /p。
///   /summon_player &lt;id&gt; &lt;slot&gt;  在己方槽位召唤随从（QA）。别名 /sp。
///   /unlock_all             解锁全部卡牌加入收藏。
///   /intent_debug            显示当前敌方意图目标（QA）。
///   /qa_tombstone            验证墓碑伤害结算（QA）。
///   /qa_bait_tactics         验证诱饵战术双阵营触发（QA）。
///   /qa_new_cards            验证近期新卡核心规则（QA）。
///   /fight &lt;enemy&gt;          直接与指定敌人战斗，跳过地图。
///   /addrelic &lt;relic_id&gt;    直接获得指定藏品。别名 /ar。
///   /help                    显示帮助。别名 /?。
/// </remarks>
public partial class DevConsole : Node
{
    // ===== UI 组件 =====

    private CanvasLayer _canvasLayer = null!;
    private Panel _panel = null!;
    private RichTextLabel _output = null!;
    private LineEdit _input = null!;
    private RichTextLabel _completionLabel = null!;
    private bool _visible;

    /// <summary>控制台当前是否可见（供 InputManager/HotkeyManager 检查）。</summary>
    public bool IsVisible => _visible;

    // ===== 补全状态 =====

    private readonly List<CompletionCandidate> _completionCandidates = new();
    private int _selectedCompletionIndex = -1;

    // ===== 命令引擎 =====

    private readonly DevConsoleEngine _engine = new();
    private int _historyIndex;

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
        _input.TextChanged += OnTextChanged;
        vbox.AddChild(_input);

        // 补全提示 Label（输入栏上方）
        _completionLabel = new RichTextLabel
        {
            Name = "Completion",
            BbcodeEnabled = true,
            FitContent = true,
            ScrollFollowing = true,
        };
        vbox.AddChild(_completionLabel);

        // 注册命令
        RegisterAllCommands();

        // 加载历史
        var historyPath = ProjectSettings.GlobalizePath("user://console_history.log");
        _engine.LoadHistory(historyPath);

        WriteLine("[color=#66ff66][DevConsole] 按 ` 键呼出/隐藏。输入 /help 查看命令[/color]");
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.Quoteleft) // 反引号键
            {
                Toggle();
                GetViewport().SetInputAsHandled();
            }
            else if (_visible && _input.HasFocus() && keyEvent.Keycode == Key.Tab && TryAcceptSelectedCompletion())
            {
                GetViewport().SetInputAsHandled();
            }
            else if (_visible && _input.HasFocus() && keyEvent.Keycode == Key.Up)
            {
                if (_completionCandidates.Count > 0 && TryMoveCompletionSelection(-1))
                {
                    // 有补全候选：切换选中项
                }
                else
                {
                    // 无补全候选：走历史记录
                    NavigateHistory(-1);
                }
                GetViewport().SetInputAsHandled();
            }
            else if (_visible && _input.HasFocus() && keyEvent.Keycode == Key.Down)
            {
                if (_completionCandidates.Count > 0 && TryMoveCompletionSelection(1))
                {
                    // 有补全候选：切换选中项
                }
                else
                {
                    NavigateHistory(1);
                }
                GetViewport().SetInputAsHandled();
            }
            else if (_visible && keyEvent.Keycode == Key.Escape)
            {
                Hide();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    // ===== 开发者模式（全局共享状态） =====

    /// <summary>开发者模式是否启用。SettingsPage 和 PauseMenu 共用此状态。</summary>
    public static bool IsDevMode { get; set; }

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
        _completionLabel.Visible = true;
        _input.GrabFocus();
        _input.Clear();
        ResetCompletionState();
        OnTextChanged("");
    }

    private void Hide()
    {
        _visible = false;
        _panel.Visible = false;
        _completionLabel.Visible = false;
        _input.ReleaseFocus();
        ResetCompletionState();
    }

    // ===== 命令处理 =====

    private void OnCommandSubmitted(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        _input.Clear();
        ResetCompletionState();
        ExecuteCommand(text);
        _input.GrabFocus();
    }

    /// <summary>
    /// 执行开发者命令。供 UI 输入和 AI 远程调用共用。
    /// </summary>
    /// <param name="cmd">命令字符串，格式: /&lt;action&gt; [参数]</param>
    public void DevCommand(string cmd)
    {
        ExecuteCommand(cmd);
    }

    /// <summary>
    /// 统一的命令执行入口：处理特殊命令后委托给引擎。
    /// </summary>
    private void ExecuteCommand(string cmd)
    {
        cmd = cmd.Trim();
        WriteLine($"[color=#aaaaaa]> {cmd}[/color]");

        if (string.IsNullOrWhiteSpace(cmd))
            return;

        // 预派发：点击伤害模式
        if (TryHandleClickDamageFromInput(cmd))
            return;

        try
        {
            var result = _engine.Execute(cmd);

            if (!result.Success)
            {
                WriteLine($"[color=#ff6644]{result.Message}[/color]");
                return;
            }

            // 特殊标记处理
            var msg = result.Message;
            if (msg == "__CLEAR__")
            {
                _output.Clear();
            }
            else if (msg.StartsWith("__FIGHT__"))
            {
                WriteLine($"[color=#66ff66]即将与 {msg[9..]} 战斗…[/color]");
                GetTree().ChangeSceneToFile("res://Scenes/Combat.tscn");
            }
            else
            {
                WriteLine($"[color=#66ff66]{msg}[/color]");
            }
        }
        catch (Exception e)
        {
            WriteLine($"[color=#ff4444]命令执行异常: {e.Message}[/color]");
        }

        _engine.SaveHistory(ProjectSettings.GlobalizePath("user://console_history.log"));
    }

    /// <summary>
    /// 处理 /damage -c N 命令：进入点击伤害模式。
    /// </summary>
    private bool TryHandleClickDamageFromInput(string cmd)
    {
        if (!cmd.StartsWith("/")) return false;
        var parts = cmd[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return false;
        var action = parts[0].ToLowerInvariant();
        if (action is not "damage" and not "dmg") return false;
        if (parts[1] != "-c") return false;
        if (!int.TryParse(parts[2], out int dmg)) return false;

        EnterClickDamageMode(dmg);
        return true;
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

    // ===== 点击伤害模式 =====

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

    // ===== 命令注册 =====

    private void RegisterAllCommands()
    {
        _engine.Register(new Commands.DamageCommand());
        _engine.Register(new Commands.DamageEnemyCommand());
        _engine.Register(new Commands.DamageSelfCommand());
        _engine.Register(new Commands.DamageESlotCommand());
        _engine.Register(new Commands.DamagePSlotCommand());
        _engine.Register(new Commands.DamageAllCommand());
        _engine.Register(new Commands.DrawCommand());
        _engine.Register(new Commands.ManaCommand());
        _engine.Register(new Commands.HealCommand());
        _engine.Register(new Commands.ArmorCommand());
        _engine.Register(new Commands.EndCommand());
        _engine.Register(new Commands.FightCommand());
        _engine.Register(new Commands.RefreshCommand());
        _engine.Register(new Commands.IntentDebugCommand());
        _engine.Register(new Commands.TokenCommand());
        _engine.Register(new Commands.PlayCommand());
        _engine.Register(new Commands.SummonPlayerCommand());
        _engine.Register(new Commands.QaTombstoneCommand());
        _engine.Register(new Commands.QaBaitTacticsCommand());
        _engine.Register(new Commands.QaNewCardsCommand());
        _engine.Register(new Commands.AddRelicCommand());
        _engine.Register(new Commands.ClearCommand());
        _engine.Register(new Commands.UnlockAllCommand());
        _engine.Register(new Commands.TagsCommand());
        _engine.Register(new Commands.EmoteCommand());
        _engine.Register(new Commands.HelpCommand());

        Commands.HelpCommand.AllCommands = _engine.Commands;
    }

    // ===== 历史导航 =====

    private void NavigateHistory(int direction)
    {
        var historyCount = _engine.History.Count;
        if (historyCount == 0) return;

        _historyIndex = Math.Clamp(_historyIndex + direction, 0, historyCount - 1);
        _input.Text = _engine.History[_historyIndex];
        _input.CaretColumn = _input.Text.Length;
    }

    /// <summary>
    /// 输入框文本变化时更新补全提示。
    /// </summary>
    private void OnTextChanged(string text)
    {
        RefreshCompletionHint(text);
    }

    /// <summary>
    /// 根据当前输入刷新补全候选与渲染。
    /// </summary>
    private void RefreshCompletionHint(string input)
    {
        _completionCandidates.Clear();
        _completionCandidates.AddRange(_engine.GetCompletions(input));
        EnsureValidCompletionSelection();

        if (_completionCandidates.Count == 0)
        {
            _completionLabel.Text = string.IsNullOrEmpty(input) || !input.StartsWith("/")
                ? ""
                : $"[color=#ff6644]未知命令或参数: {input}[/color]";
        }
        else
        {
            _completionLabel.Text = RenderCompletionCandidates(GetCompletionHeader(input));
        }
    }

    private static string GetCompletionHeader(string input)
    {
        if (string.IsNullOrEmpty(input) || !input.StartsWith("/"))
            return "补全候选";

        var content = input[1..];
        var spaceIdx = content.IndexOf(' ');
        if (spaceIdx < 0)
            return "匹配命令（Tab 补全，↑↓ 切换）";

        return "可用参数（Tab 补全，↑↓ 切换）";
    }

    private string RenderCompletionCandidates(string header)
    {
        var lines = new List<string>
        {
            $"[color=#aaaaaa]{header}[/color]"
        };

        for (int i = 0; i < _completionCandidates.Count; i++)
        {
            var candidate = _completionCandidates[i];
            var isSelected = i == _selectedCompletionIndex;
            var prefix = isSelected ? "[color=#ffdd66]▶[/color]" : "  ";
            var primary = isSelected
                ? $"[color=#ffffff][b]{candidate.PrimaryText}[/b][/color]"
                : $"[color=#66ff66]{candidate.PrimaryText}[/color]";
            var secondary = string.IsNullOrEmpty(candidate.SecondaryText)
                ? ""
                : $" [color=#888888]— {candidate.SecondaryText}[/color]";
            lines.Add($"{prefix} {primary}{secondary}");
        }

        return string.Join("\n", lines);
    }

    private void EnsureValidCompletionSelection()
    {
        if (_completionCandidates.Count == 0)
        {
            _selectedCompletionIndex = -1;
            return;
        }

        if (_selectedCompletionIndex < 0 || _selectedCompletionIndex >= _completionCandidates.Count)
            _selectedCompletionIndex = 0;
    }

    private bool TryMoveCompletionSelection(int direction)
    {
        if (_completionCandidates.Count == 0)
            return false;

        _selectedCompletionIndex = (_selectedCompletionIndex + direction + _completionCandidates.Count) % _completionCandidates.Count;
        _completionLabel.Text = RenderCompletionCandidates(GetCompletionHeader(_input.Text));
        return true;
    }

    private bool TryAcceptSelectedCompletion()
    {
        if (_selectedCompletionIndex < 0 || _selectedCompletionIndex >= _completionCandidates.Count)
            return false;

        var insertText = _completionCandidates[_selectedCompletionIndex].InsertText;
        _input.Text = insertText;
        _input.CaretColumn = insertText.Length;
        RefreshCompletionHint(insertText);
        return true;
    }

    private void ResetCompletionState()
    {
        _completionCandidates.Clear();
        _selectedCompletionIndex = -1;
    }
}
