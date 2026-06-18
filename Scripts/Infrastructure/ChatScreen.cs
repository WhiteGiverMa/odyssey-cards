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
///   game_call_method(nodePath="/root/ChatScreen", method="DevCommand", args=["/damage 10"])
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
public partial class ChatScreen : Node
{
	// ===== UI 组件 =====

	private CanvasLayer _canvasLayer = null!;
	private Panel _panel = null!;
	private RichTextLabel _output = null!;
	private LineEdit _input = null!;
	private Button _sendButton = null!;
	private RichTextLabel _completionLabel = null!;
	private Timer _completionDebounceTimer = null!;
	private bool _visible;
	private string _pendingCompletionInput = "";
	private string _lastCompletionLabelText = "";

	/// <summary>控制台当前是否可见（供 InputManager/HotkeyManager 检查）。</summary>
	public bool IsVisible => _visible;

	// ===== 补全状态 =====

	private readonly List<CompletionCandidate> _completionCandidates = new();
	private int _selectedCompletionIndex = -1;

	// ===== 命令引擎 =====

	private readonly ChatScreenEngine _engine = new();
	private int _historyIndex;

	// ===== 生命周期 =====

	public override void _Ready()
	{
		// 从持久化设置恢复开发者模式状态（不必等 SettingsPage 被打开）
		IsDevMode = UIScaler.Instance?.DevModeEnabled ?? false;

		// 创建 UI 层级（置于最顶层）
		_canvasLayer = new CanvasLayer { Layer = 128, Name = "ChatScreenLayer" };
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
			BorderWidthLeft = 2,
			BorderWidthRight = 2,
			BorderWidthTop = 2,
			BorderWidthBottom = 2,
			BorderColor = new Color(0.3f, 0.6f, 0.3f),
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4,
		};
		_panel.AddThemeStyleboxOverride("panel", panelStyle);
		_canvasLayer.AddChild(_panel);

		// VBox 布局
		var vbox = new VBoxContainer
		{
			AnchorLeft = 0,
			AnchorTop = 0,
			AnchorRight = 1,
			AnchorBottom = 1,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		_panel.AddChild(vbox);

		// 标题栏
		var titleBar = new HBoxContainer();
		var title = new Label
		{
			Text = "  信息发送界面",
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

		// 输入栏：Minecraft 式聊天/命令共用输入框
		var inputRow = new HBoxContainer
		{
			CustomMinimumSize = new Vector2(0, 34),
		};
		inputRow.AddThemeConstantOverride("separation", 8);

		_input = new LineEdit
		{
			Name = "Input",
			PlaceholderText = "输入消息，或输入 /help、fight 等命令",
			CustomMinimumSize = new Vector2(0, 32),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		_input.TextChanged += OnTextChanged;
		inputRow.AddChild(_input);

		_sendButton = new Button
		{
			Name = "SendButton",
			Text = "发送",
			CustomMinimumSize = new Vector2(86, 32),
		};
		_sendButton.Pressed += SubmitCurrentInput;
		inputRow.AddChild(_sendButton);
		vbox.AddChild(inputRow);

		// 补全提示 Label（输入栏上方）
		_completionLabel = new RichTextLabel
		{
			Name = "Completion",
			BbcodeEnabled = true,
			FitContent = true,
			ScrollFollowing = true,
		};
		vbox.AddChild(_completionLabel);

		// IME 组合/提交可能在同一帧触发多次 TextChanged；补全延迟一小段时间合并刷新，避免输入信号里同步扫表和重排富文本。
		_completionDebounceTimer = new Timer
		{
			Name = "CompletionDebounceTimer",
			OneShot = true,
			WaitTime = 0.03,
			Autostart = false,
		};
		_completionDebounceTimer.Timeout += FlushPendingCompletionHint;
		AddChild(_completionDebounceTimer);

		// 注册命令
		RegisterAllCommands();

		// 加载历史
		var historyPath = ProjectSettings.GlobalizePath("user://console_history.log");
		_engine.LoadHistory(historyPath);

		WriteLine("[color=#66ff66][Message] 按 ` 键呼出/隐藏。像 MC 一样输入消息，或输入 /help 查看命令[/color]");
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			if (keyEvent.Keycode == Key.Quoteleft) // 反引号键
			{
				if (!IsDevMode) return; // 非开发者模式下禁止快捷键呼出
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
			else if (_visible && _input.HasFocus() && (keyEvent.Keycode == Key.Enter || keyEvent.Keycode == Key.KpEnter))
			{
				// 不使用 LineEdit.TextSubmitted 信号：Godot 4 在 emit 该信号后会内部
				// 释放焦点并推进"编辑完成"状态机，导致后续 GrabFocus 与引擎内部状态
				// 竞争——has_focus()=true（边框高亮）但 caret/输入失效（状态分叉）。
				// 在 _Input 中拦截 Enter 并 SetInputAsHandled，阻止引擎触发
				// TextSubmitted，LineEdit 焦点自然维持。参考 STS2 NChatScreen 做法。
				OnCommandSubmitted(_input.Text);
				GetViewport().SetInputAsHandled();
			}
			else if (_visible && keyEvent.Keycode == Key.Escape)
			{
				Hide();
				GetViewport().SetInputAsHandled();
			}
		}
		else if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
		{
			// 点击控制台面板外的空白处时取消输入框焦点。
			// 不调用 SetInputAsHandled：点击需要继续传播到游戏画面（如战斗棋盘）。
			if (_visible && _input.HasFocus() && !_panel.GetGlobalRect().HasPoint(mouseEvent.GlobalPosition))
				_input.ReleaseFocus();
		}
	}

	// ===== 开发者模式（全局共享状态） =====

	/// <summary>开发者模式是否启用。SettingsPage 和 PauseMenu 共用此状态。</summary>
	public static bool IsDevMode { get; set; }

	// ===== 可见性 =====

	public void Toggle()
	{
		if (_visible)
			Hide();
		else
			Show();
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
		if (string.IsNullOrWhiteSpace(text))
			return;
		_input.Clear();
		ResetCompletionState();
		ExecuteConsoleInput(text);
		// Enter 路径无需焦点恢复：_Input 拦截了 Enter 并 SetInputAsHandled，
		// Godot 未触发 TextSubmitted，LineEdit 焦点自然维持。
		// SendButton 路径的焦点恢复在 SubmitCurrentInput 中处理。
	}

	private void SubmitCurrentInput()
	{
		OnCommandSubmitted(_input.Text);
		// SendButton 点击会抢占焦点到按钮。延迟到帧末恢复输入框焦点。
		// 此处不经过 TextSubmitted 信号，无引擎内部状态竞争，CallDeferred 即可。
		if (_visible && GodotObject.IsInstanceValid(_input))
			CallDeferred(nameof(RegrabInputFocusAfterButton));
	}

	/// <summary>
	/// SendButton 点击后延迟重新聚焦输入框。
	/// </summary>
	private void RegrabInputFocusAfterButton()
	{
		if (!_visible)
			return;
		if (!GodotObject.IsInstanceValid(_input))
			return;
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
	/// 统一的输入提交入口：有斜杠或命令名匹配则执行命令，否则作为玩家消息发送。
	/// </summary>
	private void ExecuteConsoleInput(string input)
	{
		input = input.Trim();
		if (string.IsNullOrWhiteSpace(input))
			return;

		if (input.StartsWith("/"))
		{
			ExecuteCommand(input);
			return;
		}

		var firstToken = input.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
		if (TryFindCommandByToken(firstToken, out _))
		{
			ExecuteCommand($"/{input}");
			return;
		}

		SendPlayerMessage(input);
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
			else if (msg.StartsWith("__ROOM__"))
			{
				var target = msg[8..]; // 格式: __ROOM__<scene>
				if (target == "Combat")
				{
					WriteLine("[color=#66ff66]跳转到战斗房间…[/color]");
					GetTree().ChangeSceneToFile("res://Scenes/Combat.tscn");
				}
				else
				{
					WriteLine($"[color=#66ff66]跳转到{target}房间…[/color]");
					GetTree().ChangeSceneToFile("res://Scenes/Map.tscn");
				}
			}
			else if (msg.StartsWith("__EVENT__"))
			{
				WriteLine($"[color=#66ff66]即将跳转到事件：{msg[9..]}…[/color]");
				GetTree().ChangeSceneToFile("res://Scenes/Map.tscn");
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

	private void SendPlayerMessage(string text)
	{
		text = text.Trim();
		WriteLine($"[color=#aaaaaa]> {EscapeBbcode(text)}[/color]");

		var cm = CombatManager.Instance;
		if (cm == null)
		{
			WriteLine("[color=#ff6644]当前不在战斗中，无法发送消息[/color]");
			return;
		}

		cm.SendPlayerEmote(text);
		WriteLine($"[color=#88ccff]已发送消息：「{EscapeBbcode(text)}」[/color]");
		_engine.History.Enqueue(text);
		_engine.SaveHistory(ProjectSettings.GlobalizePath("user://console_history.log"));
	}

	/// <summary>
	/// 处理 /damage -c N 命令：进入点击伤害模式。
	/// </summary>
	private bool TryHandleClickDamageFromInput(string cmd)
	{
		if (!cmd.StartsWith("/"))
			return false;
		var parts = cmd[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length < 3)
			return false;
		var action = parts[0].ToLowerInvariant();
		if (action is not "damage" and not "dmg")
			return false;
		if (parts[1] != "-c")
			return false;
		if (!int.TryParse(parts[2], out int dmg))
			return false;

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
		if (cm == null)
		{ Show(); return; }

		var combatUI = cm.GetNodeOrNull<CombatUI>("CanvasLayer/CombatUI");
		if (combatUI == null)
		{ Show(); return; }

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
		_engine.Register(new Commands.PurifyCommand());
		_engine.Register(new Commands.EndCommand());
		_engine.Register(new Commands.FightCommand());
		_engine.Register(new Commands.RefreshCommand());
		_engine.Register(new Commands.SkipCommand());
		_engine.Register(new Commands.RoomCommand());
		_engine.Register(new Commands.EventCommand());
		_engine.Register(new Commands.IntentDebugCommand());
		_engine.Register(new Commands.TokenCommand());
		_engine.Register(new Commands.PlayCommand());
		_engine.Register(new Commands.DiscardCommand());
		_engine.Register(new Commands.SummonPlayerCommand());
		_engine.Register(new Commands.SummonEnemyCommand());
		_engine.Register(new Commands.QaTombstoneCommand());
		_engine.Register(new Commands.QaBaitTacticsCommand());
		_engine.Register(new Commands.QaNewCardsCommand());
		_engine.Register(new Commands.AddRelicCommand());
		_engine.Register(new Commands.ClearCommand());
		_engine.Register(new Commands.UnlockAllCommand());
		_engine.Register(new Commands.TagsCommand());
		_engine.Register(new Commands.EmoteCommand());
		_engine.Register(new Commands.ThemePreviewCommand());
		_engine.Register(new Commands.HelpCommand());

		Commands.HelpCommand.AllCommands = _engine.Commands;
	}

	// ===== 历史导航 =====

	private void NavigateHistory(int direction)
	{
		var historyCount = _engine.History.Count;
		if (historyCount == 0)
			return;

		_historyIndex = Math.Clamp(_historyIndex + direction, 0, historyCount - 1);
		_input.Text = _engine.History[_historyIndex];
		_input.CaretColumn = _input.Text.Length;
	}

	/// <summary>
	/// 输入框文本变化时更新补全提示。
	/// </summary>
	private void OnTextChanged(string text)
	{
		_pendingCompletionInput = text;
		_completionDebounceTimer.Start();
	}

	private void FlushPendingCompletionHint()
	{
		RefreshCompletionHint(_pendingCompletionInput);
	}

	/// <summary>
	/// 根据当前输入刷新补全候选与渲染。
	/// </summary>
	private void RefreshCompletionHint(string input)
	{
		_completionCandidates.Clear();
		_completionCandidates.AddRange(GetCompletions(input));
		EnsureValidCompletionSelection();

		string labelText;
		if (_completionCandidates.Count == 0)
		{
			labelText = string.IsNullOrEmpty(input)
				? ""
				: $"[color=#888888]Enter 发送消息；输入 / 或命令名前缀可执行命令[/color]";
		}
		else
		{
			labelText = RenderCompletionCandidates(GetCompletionHeader(input));
		}

		SetCompletionLabelText(labelText);
	}

	private void SetCompletionLabelText(string text)
	{
		if (_lastCompletionLabelText == text)
			return;

		_completionLabel.Text = text;
		_lastCompletionLabelText = text;
	}

	private static string GetCompletionHeader(string input)
	{
		if (string.IsNullOrEmpty(input) || !input.StartsWith("/"))
			return "匹配消息/命令（Tab 补全，↑↓ 切换）";

		var content = input[1..];
		var spaceIdx = content.IndexOf(' ');
		if (spaceIdx < 0)
			return "匹配命令（Tab 补全，↑↓ 切换）";

		return "可用参数（Tab 补全，↑↓ 切换）";
	}

	private List<CompletionCandidate> GetCompletions(string input)
	{
		if (string.IsNullOrWhiteSpace(input))
			return [];

		if (input.StartsWith("/"))
			return _engine.GetCompletions(input);

		var candidates = new List<CompletionCandidate>();
		if (IsSlashlessCommandPrefix(input))
			candidates.AddRange(GetSlashlessCommandCompletions(input));
		candidates.AddRange(GetEmoteCompletions(input));
		return candidates
			.GroupBy(candidate => candidate.InsertText, StringComparer.OrdinalIgnoreCase)
			.Select(group => group.First())
			.Take(8)
			.ToList();
	}

	private static bool IsSlashlessCommandPrefix(string input)
	{
		var token = input.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? input;
		if (string.IsNullOrEmpty(token))
			return false;

		return token.All(ch => ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '_' or '-');
	}

	private IEnumerable<CompletionCandidate> GetSlashlessCommandCompletions(string input)
	{
		var token = input.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? input;
		if (token.Contains(' '))
			yield break;

		var unique = new Dictionary<string, ChatScreenCommand>(StringComparer.OrdinalIgnoreCase);
		foreach (var cmd in _engine.Commands)
			if (!unique.ContainsKey(cmd.Name))
				unique[cmd.Name] = cmd;

		foreach (var cmd in unique.Values.OrderBy(cmd => cmd.Name))
		{
			if (!cmd.Name.StartsWith(token, StringComparison.OrdinalIgnoreCase) &&
				!cmd.Aliases.Any(alias => alias.StartsWith(token, StringComparison.OrdinalIgnoreCase)))
			{
				continue;
			}

			var signatureTail = cmd.Signature.Contains(' ')
				? cmd.Signature[(cmd.Signature.IndexOf(' ') + 1)..]
				: "";
			yield return new CompletionCandidate(
				$"{cmd.Name} ",
				$"{cmd.Name} {signatureTail}".TrimEnd(),
				$"命令 — {cmd.Description}");
		}
	}

	private static IEnumerable<CompletionCandidate> GetEmoteCompletions(string input)
	{
		var gm = GameManager.Instance;
		if (gm == null)
			yield break;

		foreach (var entry in gm.GetActiveEmoteEntries())
		{
			string text = entry.Text.Trim();
			if (string.IsNullOrWhiteSpace(text))
				continue;
			if (!text.StartsWith(input, StringComparison.OrdinalIgnoreCase))
				continue;

			string tag = entry.IsOfficialCollection ? "官方收藏集" : "预设表情";
			yield return new CompletionCandidate(text, text, tag);
		}
	}

	private bool TryFindCommandByToken(string token, out ChatScreenCommand command)
	{
		foreach (var cmd in _engine.Commands)
		{
			if (string.Equals(cmd.Name, token, StringComparison.OrdinalIgnoreCase) ||
				cmd.Aliases.Any(alias => string.Equals(alias, token, StringComparison.OrdinalIgnoreCase)))
			{
				command = cmd;
				return true;
			}
		}

		command = null!;
		return false;
	}

	private static string EscapeBbcode(string text)
	{
		return text.Replace("[", "[lb]").Replace("]", "[rb]");
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
		SetCompletionLabelText(RenderCompletionCandidates(GetCompletionHeader(_input.Text)));
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
		_pendingCompletionInput = "";
		SetCompletionLabelText("");
	}
}
