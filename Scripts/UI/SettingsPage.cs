using Godot;
using OdysseyCards.Core;
using OdysseyCards.Infrastructure;
using System;
using System.Collections.Generic;

namespace OdysseyCards.UI;

public partial class SettingsPage : Control
{
	private const float MobileTouchHitPadding = 20f;

	// ===== Tab 切换 =====

	private Button _generalTabBtn = null!;
	private Button _keybindTabBtn = null!;
	private VBoxContainer _generalContainer = null!;
	private VBoxContainer _keybindContainer = null!;

	// ===== 常规设置控件（全部保留）=====

	private OptionButton _languageOptionButton = null!;
	private OptionButton _resolutionOptionButton = null!;
	private OptionButton _windowModeOptionButton = null!;
	private Button _backButton = null!;
	private Label _titleLabel = null!;
	private Label _languageLabel = null!;
	private Label _resolutionLabel = null!;
	private Label _windowModeLabel = null!;
	private HBoxContainer _resolutionRow = null!;
	private HBoxContainer _windowModeRow = null!;
	private Label _visualStyleLabel = null!;
	private CheckBox _intentIconFloatingToggle = null!;
	private CheckBox _intentValueFloatingToggle = null!;
	private CheckBox _devModeToggle = null!;
	private Button _consoleButton = null!;
	private Label _emoteIdleTimeLabel = null!;
	private HSlider _emoteIdleTimeSlider = null!;
	private Label _emoteIdleTimeValueLabel = null!;
	private Label _emoteVarMinLabel = null!;
	private HSlider _emoteVarMinSlider = null!;
	private Label _emoteVarMinValueLabel = null!;
	private Label _emoteVarMaxLabel = null!;
	private HSlider _emoteVarMaxSlider = null!;
	private Label _emoteVarMaxValueLabel = null!;

	// ===== 键位设置控件 =====

	/// <summary>子菜单栈返回回调（Set by SubmenuStack）。为 null 时走默认 QueueFree 路径。</summary>
	public Action OnBack { get; set; }

	private bool _isListeningForKey;
	private StringName _listeningAction;
	private Button _listeningButton = null!;
	private VBoxContainer _keybindListContainer = null!;
	private OptionButton _profileSelector = null!;
	private Button _newProfileBtn = null!;
	private Button _deleteProfileBtn = null!;
	private Button _resetDefaultsBtn = null!;
	private ScrollContainer _keybindScroll = null!;

	/// <summary>可重绑定的动作列表，按显示顺序排列。</summary>
	private static readonly List<StringName> RebindableActions = new()
	{
        // 导航
        OdysseyInput.Up, OdysseyInput.Down, OdysseyInput.Left, OdysseyInput.Right,
        // 动作
        OdysseyInput.Accept, OdysseyInput.Cancel, OdysseyInput.Select,
        // 战斗 — 手牌
        OdysseyInput.SelectCard1, OdysseyInput.SelectCard2, OdysseyInput.SelectCard3,
		OdysseyInput.SelectCard4, OdysseyInput.SelectCard5, OdysseyInput.SelectCard6,
		OdysseyInput.SelectCard7, OdysseyInput.SelectCard8, OdysseyInput.SelectCard9,
		OdysseyInput.SelectCard10,
		// 战斗 — 命令
		OdysseyInput.EndTurn, OdysseyInput.Pause,
		OdysseyInput.ViewDeck, OdysseyInput.ViewDiscard, OdysseyInput.TabTarget,
		// 战斗 — 其他
		OdysseyInput.InfoScreen,
		// 场景导航
		OdysseyInput.PageUp, OdysseyInput.PageDown, OdysseyInput.Skip,
	};

	/// <summary>动作名 → 中文显示名。</summary>
	private static readonly Dictionary<StringName, string> ActionDisplayNames = new()
	{
		[OdysseyInput.Up] = "向上",
		[OdysseyInput.Down] = "向下",
		[OdysseyInput.Left] = "向左",
		[OdysseyInput.Right] = "向右",
		[OdysseyInput.Accept] = "确认",
		[OdysseyInput.Cancel] = "取消",
		[OdysseyInput.Select] = "选择/查看",
		[OdysseyInput.SelectCard1] = "手牌 1",
		[OdysseyInput.SelectCard2] = "手牌 2",
		[OdysseyInput.SelectCard3] = "手牌 3",
		[OdysseyInput.SelectCard4] = "手牌 4",
		[OdysseyInput.SelectCard5] = "手牌 5",
		[OdysseyInput.SelectCard6] = "手牌 6",
		[OdysseyInput.SelectCard7] = "手牌 7",
		[OdysseyInput.SelectCard8] = "手牌 8",
		[OdysseyInput.SelectCard9] = "手牌 9",
		[OdysseyInput.SelectCard10] = "手牌 10",
		[OdysseyInput.EndTurn] = "结束回合",
		[OdysseyInput.Pause] = "暂停",
		[OdysseyInput.ViewDeck] = "查看牌库",
		[OdysseyInput.ViewDiscard] = "查看弃牌堆",
		[OdysseyInput.TabTarget] = "切换目标",
		[OdysseyInput.PageUp] = "上一页",
		[OdysseyInput.PageDown] = "下一页",
		[OdysseyInput.Skip] = "跳过",
		[OdysseyInput.InfoScreen] = "综合信息",
	};

	/// <summary>物理按键 → 显示字符串。</summary>
	private static string KeyToDisplay(Key key) => key switch
	{
		Key.None => "(未绑定)",
		Key.Enter => "Enter",
		Key.Escape => "Esc",
		Key.Space => "Space",
		Key.Tab => "Tab",
		Key.Up => "↑",
		Key.Down => "↓",
		Key.Left => "←",
		Key.Right => "→",
		Key.Pageup => "PageUp",
		Key.Pagedown => "PageDown",
		Key.Backspace => "Backspace",
		Key.Shift => "Shift",
		Key.Ctrl => "Ctrl",
		Key.Alt => "Alt",
		Key.Capslock => "CapsLock",
		Key.Key0 => "0",
		Key.Key1 => "1",
		Key.Key2 => "2",
		Key.Key3 => "3",
		Key.Key4 => "4",
		Key.Key5 => "5",
		Key.Key6 => "6",
		Key.Key7 => "7",
		Key.Key8 => "8",
		Key.Key9 => "9",
		Key.A => "A",
		Key.B => "B",
		Key.C => "C",
		Key.D => "D",
		Key.E => "E",
		Key.F => "F",
		Key.G => "G",
		Key.H => "H",
		Key.I => "I",
		Key.J => "J",
		Key.K => "K",
		Key.L => "L",
		Key.M => "M",
		Key.N => "N",
		Key.O => "O",
		Key.P => "P",
		Key.Q => "Q",
		Key.R => "R",
		Key.S => "S",
		Key.T => "T",
		Key.U => "U",
		Key.V => "V",
		Key.W => "W",
		Key.X => "X",
		Key.Y => "Y",
		Key.Z => "Z",
		_ => key.ToString(),
	};

	// ===== 生命周期 =====

	public override void _Ready()
	{
		SetupUI();
		LoadLanguages();
		LoadResolutions();
		LoadWindowModes();
		ConnectSignals();
		UpdateCurrentLanguage();
		SwitchToTab("general");

		if (MobileInputHelper.IsMobile)
		{
			MouseFilter = MouseFilterEnum.Stop;
			ApplyMobileLayout();
		}
	}

	public override void _ExitTree()
	{
		StopListening();

		Core.GameManager.Instance.LanguageChanged -= OnLanguageChanged;

		_languageOptionButton.ItemSelected -= OnLanguageSelected;
		_resolutionOptionButton.ItemSelected -= OnResolutionSelected;
		_windowModeOptionButton.ItemSelected -= OnWindowModeSelected;
		_intentIconFloatingToggle.Toggled -= OnIntentIconFloatingToggled;
		_intentValueFloatingToggle.Toggled -= OnIntentValueFloatingToggled;
		_backButton.Pressed -= OnBackPressed;
		_devModeToggle.Toggled -= OnDevModeToggled;
		_consoleButton.Pressed -= OnConsolePressed;
		_emoteIdleTimeSlider.ValueChanged -= OnEmoteIdleTimeChanged;
		_emoteVarMinSlider.ValueChanged -= OnEmoteVarMinChanged;
		_emoteVarMaxSlider.ValueChanged -= OnEmoteVarMaxChanged;
		_profileSelector.ItemSelected -= OnProfileSelected;
		_newProfileBtn.Pressed -= OnNewProfile;
		_deleteProfileBtn.Pressed -= OnDeleteProfile;
		_resetDefaultsBtn.Pressed -= OnResetDefaults;
	}

	/// <summary>
	/// 捕获键位重绑定的按键。仅在「正在监听」状态下有效。
	/// </summary>
	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (!_isListeningForKey)
			return;
		if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
			return;

		// ESC 取消监听
		if (keyEvent.Keycode == Key.Escape)
		{
			StopListening();
			GetViewport()?.SetInputAsHandled();
			return;
		}

		// 记录新键位
		var im = InputManager.Instance;
		if (im != null)
		{
			im.SetKey(_listeningAction, keyEvent.Keycode);
			im.SaveProfiles();
		}

		RefreshKeybindList();
		StopListening();
		GetViewport()?.SetInputAsHandled();
	}

	// ===== UI 构建 =====

	private void SetupUI()
	{
		// === 标题 ===
		_titleLabel = CreateTitleLabel();

		// === Tab 按钮行 ===
		var tabRow = new HBoxContainer
		{
			Name = "TabRow",
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		tabRow.AddThemeConstantOverride("separation", 12);

		_generalTabBtn = CreateTabButton("常规", true);
		_keybindTabBtn = CreateTabButton("键位", false);
		_generalTabBtn.Pressed += () => SwitchToTab("general");
		_keybindTabBtn.Pressed += () => SwitchToTab("keybind");

		tabRow.AddChild(_generalTabBtn);
		tabRow.AddChild(_keybindTabBtn);

		// === 常规设置容器 ===
		_generalContainer = new VBoxContainer
		{
			Name = "GeneralContainer",
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		_generalContainer.AddThemeConstantOverride("separation", 20);
		SetupGeneralUI();

		// === 键位设置容器 ===
		_keybindContainer = new VBoxContainer
		{
			Name = "KeybindContainer",
			Visible = false,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		_keybindContainer.AddThemeConstantOverride("separation", 16);
		SetupKeybindUI();

		// === 返回按钮 ===
		_backButton = new Button
		{
			Name = "BackButton",
			Text = Localization.Localization.T("ui.settings.back", "Back"),
			CustomMinimumSize = new Vector2(140, 44),
		};
		_backButton.AddThemeFontSizeOverride("font_size", 18);

		// === 根容器 ===
		var root = new VBoxContainer
		{
			Name = "SettingsRoot",
			AnchorLeft = 0,
			AnchorTop = 0,
			AnchorRight = 1,
			AnchorBottom = 1,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		root.AddThemeConstantOverride("separation", 20);

		root.AddChild(_titleLabel);
		root.AddChild(tabRow);
		root.AddChild(_generalContainer);
		root.AddChild(_keybindContainer);
		root.AddChild(_backButton);

		AddChild(root);
	}

	// ===== 常规设置 UI（保留原有逻辑）=====

	private void SetupGeneralUI()
	{
		// 语言行
		_languageLabel = CreateSettingLabel("ui.settings.language", "Language");
		_languageOptionButton = new OptionButton { CustomMinimumSize = new Vector2(200, 0) };
		var languageRow = CreateSettingRow(_languageLabel, _languageOptionButton);

		// 分辨率行
		_resolutionLabel = CreateSettingLabel("ui.settings.resolution", "Resolution");
		_resolutionOptionButton = new OptionButton { CustomMinimumSize = new Vector2(200, 0) };
		var resolutionRow = CreateSettingRow(_resolutionLabel, _resolutionOptionButton);
		_resolutionRow = resolutionRow;

		// 窗口模式行
		_windowModeLabel = CreateSettingLabel("ui.settings.window_mode", "Window Mode");
		_windowModeOptionButton = new OptionButton { CustomMinimumSize = new Vector2(200, 0) };
		var windowModeRow = CreateSettingRow(_windowModeLabel, _windowModeOptionButton);
		_windowModeRow = windowModeRow;

		// 自定义视觉风格
		_visualStyleLabel = new Label
		{
			Text = Localization.Localization.T("ui.settings.visual_style", "自定义视觉风格"),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		_visualStyleLabel.AddThemeFontSizeOverride("font_size", 18);
		_visualStyleLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.85f, 1f));

		bool iconFloating = UIScaler.Instance?.IntentIconFloatingEnabled ?? true;
		bool valueFloating = UIScaler.Instance?.IntentValueFloatingEnabled ?? true;
		_intentIconFloatingToggle = new CheckBox
		{
			Text = Localization.Localization.T("ui.settings.intent_icon_floating", "意图图标整体浮动"),
			ButtonPressed = iconFloating,
		};
		_intentIconFloatingToggle.AddThemeFontSizeOverride("font_size", 18);

		_intentValueFloatingToggle = new CheckBox
		{
			Text = Localization.Localization.T("ui.settings.intent_value_floating", "伤害数字随图标浮动"),
			ButtonPressed = valueFloating,
			Disabled = !iconFloating,
		};
		_intentValueFloatingToggle.AddThemeFontSizeOverride("font_size", 18);

		// 开发者模式
		_devModeToggle = new CheckBox
		{
			Text = Localization.Localization.T("ui.settings.dev_mode", "开发者模式"),
			ButtonPressed = DevConsole.IsDevMode,
		};
		_devModeToggle.AddThemeFontSizeOverride("font_size", 20);

		_consoleButton = new Button
		{
			Text = Localization.Localization.T("ui.settings.open_console", "打开控制台"),
			CustomMinimumSize = new Vector2(0, 44),
			Visible = DevConsole.IsDevMode,
		};
		_consoleButton.AddThemeFontSizeOverride("font_size", 16);

		// 表情空闲时间行
		var gm = GameManager.Instance;
		float currentIdleTime = gm?.EmoteIdleTimeSeconds ?? 5.0f;
		_emoteIdleTimeLabel = CreateSettingLabel("ui.settings.emote_idle_time", "Emote Idle Time");

		_emoteIdleTimeSlider = new HSlider
		{
			MinValue = 3.0,
			MaxValue = 15.0,
			Step = 0.5,
			Value = currentIdleTime,
			CustomMinimumSize = new Vector2(160, 0),
		};
		_emoteIdleTimeValueLabel = new Label { Text = $"{currentIdleTime:F1}s", CustomMinimumSize = new Vector2(50, 0) };
		_emoteIdleTimeValueLabel.AddThemeFontSizeOverride("font_size", 16);
		var emoteIdleRow = CreateSliderRow(_emoteIdleTimeLabel, _emoteIdleTimeSlider, _emoteIdleTimeValueLabel);

		// 随机最小倍率行
		float currentVarMin = gm?.EmoteIdleVariationMin ?? 0.7f;
		_emoteVarMinLabel = CreateSettingLabel("ui.settings.emote_variation_min", "Variation Min");
		_emoteVarMinSlider = new HSlider
		{
			MinValue = 0.1,
			MaxValue = 3.0,
			Step = 0.1,
			Value = currentVarMin,
			CustomMinimumSize = new Vector2(160, 0),
		};
		_emoteVarMinValueLabel = new Label { Text = $"×{currentVarMin:F1}", CustomMinimumSize = new Vector2(50, 0) };
		_emoteVarMinValueLabel.AddThemeFontSizeOverride("font_size", 16);
		var varMinRow = CreateSliderRow(_emoteVarMinLabel, _emoteVarMinSlider, _emoteVarMinValueLabel);

		// 随机最大倍率行
		float currentVarMax = gm?.EmoteIdleVariationMax ?? 1.3f;
		_emoteVarMaxLabel = CreateSettingLabel("ui.settings.emote_variation_max", "Variation Max");
		_emoteVarMaxSlider = new HSlider
		{
			MinValue = 0.1,
			MaxValue = 3.0,
			Step = 0.1,
			Value = currentVarMax,
			CustomMinimumSize = new Vector2(160, 0),
		};
		_emoteVarMaxValueLabel = new Label { Text = $"×{currentVarMax:F1}", CustomMinimumSize = new Vector2(50, 0) };
		_emoteVarMaxValueLabel.AddThemeFontSizeOverride("font_size", 16);
		var varMaxRow = CreateSliderRow(_emoteVarMaxLabel, _emoteVarMaxSlider, _emoteVarMaxValueLabel);

		_generalContainer.AddChild(languageRow);
		_generalContainer.AddChild(resolutionRow);
		_generalContainer.AddChild(windowModeRow);
		_generalContainer.AddChild(_visualStyleLabel);
		_generalContainer.AddChild(_intentIconFloatingToggle);
		_generalContainer.AddChild(_intentValueFloatingToggle);
		_generalContainer.AddChild(_devModeToggle);
		_generalContainer.AddChild(_consoleButton);
		_generalContainer.AddChild(emoteIdleRow);
		_generalContainer.AddChild(varMinRow);
		_generalContainer.AddChild(varMaxRow);
	}

	// ===== 键位设置 UI =====

	private void SetupKeybindUI()
	{
		// 配置选择器行
		var profileLabel = new Label
		{
			Text = Localization.Localization.T("ui.settings.keybind_profile", "键位配置"),
		};
		profileLabel.AddThemeFontSizeOverride("font_size", 18);

		_profileSelector = new OptionButton { CustomMinimumSize = new Vector2(160, 0) };
		_newProfileBtn = new Button { Text = "+", CustomMinimumSize = new Vector2(36, 0) };
		_deleteProfileBtn = new Button { Text = "−", CustomMinimumSize = new Vector2(36, 0) };
		_newProfileBtn.AddThemeFontSizeOverride("font_size", 18);
		_deleteProfileBtn.AddThemeFontSizeOverride("font_size", 18);

		var profileRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		profileRow.AddThemeConstantOverride("separation", 8);
		profileRow.AddChild(profileLabel);
		profileRow.AddChild(_profileSelector);
		profileRow.AddChild(_newProfileBtn);
		profileRow.AddChild(_deleteProfileBtn);

		_keybindContainer.AddChild(profileRow);

		// 提示标签
		var hintLabel = new Label
		{
			Text = Localization.Localization.T("ui.settings.keybind_hint", "点击键位按钮后按下新按键即可重新绑定"),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		hintLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
		hintLabel.AddThemeFontSizeOverride("font_size", 14);
		_keybindContainer.AddChild(hintLabel);

		// 可滚动键位列表
		_keybindScroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(0, 280),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		_keybindListContainer = new VBoxContainer { Name = "KeybindList" };
		_keybindListContainer.AddThemeConstantOverride("separation", 4);
		_keybindScroll.AddChild(_keybindListContainer);
		_keybindContainer.AddChild(_keybindScroll);

		// 重置按钮
		_resetDefaultsBtn = new Button
		{
			Text = Localization.Localization.T("ui.settings.reset_defaults", "重置为默认键位"),
			CustomMinimumSize = new Vector2(200, 36),
		};
		_resetDefaultsBtn.AddThemeFontSizeOverride("font_size", 16);
		_keybindContainer.AddChild(_resetDefaultsBtn);

		// 初始填充
		RefreshProfileList();
		RefreshKeybindList();
	}

	/// <summary>从 InputManager 加载所有配置名称填充下拉框。</summary>
	private void RefreshProfileList()
	{
		_profileSelector.Clear();
		var im = InputManager.Instance;
		if (im == null)
			return;

		int activeIdx = 0;
		int i = 0;
		foreach (var name in im.GetProfileNames())
		{
			_profileSelector.AddItem(name);
			if (name == im.ActiveProfileName)
				activeIdx = i;
			i++;
		}
		_profileSelector.Selected = activeIdx;
	}

	/// <summary>重建键位列表，反映当前映射。</summary>
	private void RefreshKeybindList()
	{
		// 清除旧行
		foreach (var child in _keybindListContainer.GetChildren())
			child.QueueFree();

		var im = InputManager.Instance;
		if (im == null)
			return;

		foreach (var action in RebindableActions)
		{
			var key = im.GetKey(action);
			var displayName = ActionDisplayNames.TryGetValue(action, out var name) ? name : action.ToString();
			var row = CreateKeybindRow(action, displayName, key);
			_keybindListContainer.AddChild(row);
		}
	}

	/// <summary>创建单个键位行：标签 + 按钮。</summary>
	private HBoxContainer CreateKeybindRow(StringName action, string displayName, Key currentKey)
	{
		var row = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			CustomMinimumSize = new Vector2(0, 32),
		};
		row.AddThemeConstantOverride("separation", 12);

		var label = new Label
		{
			Text = displayName,
			CustomMinimumSize = new Vector2(120, 0),
			HorizontalAlignment = HorizontalAlignment.Right,
		};
		label.AddThemeFontSizeOverride("font_size", 16);

		var keyBtn = new Button
		{
			Text = KeyToDisplay(currentKey),
			CustomMinimumSize = new Vector2(100, 30),
		};
		keyBtn.AddThemeFontSizeOverride("font_size", 14);
		keyBtn.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.6f));

		// 点击进入「监听」模式
		var capturedAction = action; // 闭包捕获
		keyBtn.Pressed += () => StartListening(capturedAction, keyBtn);

		row.AddChild(label);
		row.AddChild(keyBtn);
		return row;
	}

	// ===== 监听模式 =====

	/// <summary>进入监听模式：按钮文字变 "..."，开始捕获下一个按键。</summary>
	private void StartListening(StringName action, Button button)
	{
		// 如果已经在监听另一个，先取消
		StopListening();

		_listeningAction = action;
		_listeningButton = button;
		_isListeningForKey = true;
		button.Text = "...";
		button.AddThemeColorOverride("font_color", new Color(1f, 0.5f, 0.2f)); // 橙色提示
	}

	/// <summary>退出监听模式，恢复按钮文字。</summary>
	private void StopListening()
	{
		if (_isListeningForKey)
		{
			var im = InputManager.Instance;
			var key = im != null ? im.GetKey(_listeningAction) : Key.None;
			_listeningButton.Text = KeyToDisplay(key);
			_listeningButton.RemoveThemeColorOverride("font_color");
		}

		_isListeningForKey = false;
		_listeningAction = default;
		_listeningButton = null!;
	}

	// ===== Tab 切换 =====

	private void SwitchToTab(string tab)
	{
		bool isGeneral = tab == "general";
		_generalContainer.Visible = isGeneral;
		_keybindContainer.Visible = !isGeneral;

		// 更新 Tab 按钮样式
		UpdateTabStyle(_generalTabBtn, isGeneral);
		UpdateTabStyle(_keybindTabBtn, !isGeneral);

		// 切换标签页时停止监听
		StopListening();

		// 切到键位页时刷新列表
		if (!isGeneral)
		{
			RefreshProfileList();
			RefreshKeybindList();
		}
	}

	private Button CreateTabButton(string text, bool active)
	{
		var btn = new Button { Text = text, CustomMinimumSize = new Vector2(100, 36) };
		btn.AddThemeFontSizeOverride("font_size", 18);
		UpdateTabStyle(btn, active);
		return btn;
	}

	private static void UpdateTabStyle(Button btn, bool active)
	{
		if (active)
		{
			btn.AddThemeColorOverride("font_color", Colors.White);
			btn.AddThemeColorOverride("font_hover_color", Colors.White);
			btn.AddThemeStyleboxOverride("normal", CreateTabStylebox(new Color(0.3f, 0.55f, 0.3f)));
			btn.AddThemeStyleboxOverride("hover", CreateTabStylebox(new Color(0.35f, 0.6f, 0.35f)));
		}
		else
		{
			btn.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
			btn.AddThemeColorOverride("font_hover_color", new Color(0.7f, 0.7f, 0.7f));
			btn.AddThemeStyleboxOverride("normal", CreateTabStylebox(new Color(0.15f, 0.15f, 0.15f)));
			btn.AddThemeStyleboxOverride("hover", CreateTabStylebox(new Color(0.22f, 0.22f, 0.22f)));
		}
	}

	private static StyleBoxFlat CreateTabStylebox(Color bgColor)
	{
		return new StyleBoxFlat
		{
			BgColor = bgColor,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 0,
			CornerRadiusBottomRight = 0,
			BorderWidthBottom = 0,
		};
	}

	// ===== 辅助 UI 方法 =====

	private static Label CreateTitleLabel()
	{
		var label = new Label
		{
			Text = Localization.Localization.T("ui.settings.title", "Settings"),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		label.AddThemeFontSizeOverride("font_size", 36);
		label.CustomMinimumSize = new Vector2(0, 50);
		return label;
	}

	private static Label CreateSettingLabel(string key, string fallback)
	{
		var label = new Label { Text = Localization.Localization.T(key, fallback) };
		label.AddThemeFontSizeOverride("font_size", 20);
		label.CustomMinimumSize = new Vector2(150, 0);
		return label;
	}

	private static HBoxContainer CreateSettingRow(Label label, Control control)
	{
		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("separation", 20);
		row.AddChild(label);
		row.AddChild(control);
		return row;
	}

	private static HBoxContainer CreateSliderRow(Label label, HSlider slider, Label valueLabel)
	{
		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("separation", 12);
		row.AddChild(label);
		row.AddChild(slider);
		row.AddChild(valueLabel);
		return row;
	}

	// ===== 信号连接 =====

	private void ConnectSignals()
	{
		_languageOptionButton.ItemSelected += OnLanguageSelected;
		_resolutionOptionButton.ItemSelected += OnResolutionSelected;
		_windowModeOptionButton.ItemSelected += OnWindowModeSelected;
		_intentIconFloatingToggle.Toggled += OnIntentIconFloatingToggled;
		_intentValueFloatingToggle.Toggled += OnIntentValueFloatingToggled;
		_backButton.Pressed += OnBackPressed;
		_devModeToggle.Toggled += OnDevModeToggled;
		_consoleButton.Pressed += OnConsolePressed;
		_emoteIdleTimeSlider.ValueChanged += OnEmoteIdleTimeChanged;
		_emoteVarMinSlider.ValueChanged += OnEmoteVarMinChanged;
		_emoteVarMaxSlider.ValueChanged += OnEmoteVarMaxChanged;
		_profileSelector.ItemSelected += OnProfileSelected;
		_newProfileBtn.Pressed += OnNewProfile;
		_deleteProfileBtn.Pressed += OnDeleteProfile;
		_resetDefaultsBtn.Pressed += OnResetDefaults;
		Core.GameManager.Instance.LanguageChanged += OnLanguageChanged;
	}

	// ===== 事件处理 =====

	private void OnLanguageSelected(long index)
	{
		var langVariant = _languageOptionButton.GetItemMetadata((int)index);
		string lang = langVariant.AsString();
		if (!string.IsNullOrEmpty(lang))
			Core.GameManager.Instance.SetLanguage(lang);
	}

	private void OnResolutionSelected(long index)
	{
		var meta = _resolutionOptionButton.GetItemMetadata((int)index).AsString();
		var parts = meta.Split(',');
		if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h))
		{
			if (UIScaler.Instance.GetCurrentWindowModeIndex() == 0)
				UIScaler.Instance.SetWindowResolution(w, h);
		}
	}

	private void OnWindowModeSelected(long index)
	{
		var modeVariant = _windowModeOptionButton.GetItemMetadata((int)index);
		int modeIndex = modeVariant.AsInt32();
		UIScaler.Instance.SetWindowModeIndex(modeIndex);
		LoadResolutions();
	}

	private void OnIntentIconFloatingToggled(bool on)
	{
		_intentValueFloatingToggle.Disabled = !on;
		UIScaler.Instance?.SetIntentVisualFloating(on, _intentValueFloatingToggle.ButtonPressed);
	}

	private void OnIntentValueFloatingToggled(bool on)
	{
		UIScaler.Instance?.SetIntentVisualFloating(_intentIconFloatingToggle.ButtonPressed, on);
	}

	private void OnBackPressed()
	{
		if (OnBack != null)
		{
			OnBack();
			return;
		}

		if (GetParent() is Core.MainMenu mainMenu)
			mainMenu.ShowMainMenu();
		QueueFree();
	}

	private void OnDevModeToggled(bool on)
	{
		DevConsole.IsDevMode = on;
		_consoleButton.Visible = on;
	}

	private void OnConsolePressed()
	{
		GetNodeOrNull<DevConsole>("/root/DevConsole")?.Toggle();
	}

	private void OnEmoteIdleTimeChanged(double value)
	{
		float v = (float)value;
		_emoteIdleTimeValueLabel.Text = $"{v:F1}s";
		var gm = GameManager.Instance;
		if (gm != null)
			gm.EmoteIdleTimeSeconds = v;
	}

	private void OnEmoteVarMinChanged(double value)
	{
		float v = (float)value;
		var gm = GameManager.Instance;
		float varMax = gm?.EmoteIdleVariationMax ?? 1.3f;
		v = Mathf.Clamp(v, 0.1f, varMax);
		if (Mathf.Abs(v - (float)value) > 0.001f)
			_emoteVarMinSlider.Value = v;
		_emoteVarMinValueLabel.Text = $"×{v:F1}";
		if (gm != null)
			gm.EmoteIdleVariationMin = v;
	}

	private void OnEmoteVarMaxChanged(double value)
	{
		float v = (float)value;
		var gm = GameManager.Instance;
		float varMin = gm?.EmoteIdleVariationMin ?? 0.7f;
		v = Mathf.Clamp(v, varMin, 3.0f);
		if (Mathf.Abs(v - (float)value) > 0.001f)
			_emoteVarMaxSlider.Value = v;
		_emoteVarMaxValueLabel.Text = $"×{v:F1}";
		if (gm != null)
			gm.EmoteIdleVariationMax = v;
	}

	private void OnProfileSelected(long index)
	{
		var im = InputManager.Instance;
		if (im == null || index < 0 || index >= _profileSelector.ItemCount)
			return;

		string profileName = _profileSelector.GetItemText((int)index);
		im.SwitchProfile(profileName);
		im.SaveProfiles();
		RefreshKeybindList();
	}

	private void OnNewProfile()
	{
		var im = InputManager.Instance;
		if (im == null)
			return;

		// 自动命名：配置 N
		int n = 1;
		string name;
		var existing = new HashSet<string>(im.GetProfileNames());
		do
		{ name = $"配置 {n}"; n++; } while (existing.Contains(name));

		im.DuplicateProfile(name);
		im.SaveProfiles();
		RefreshProfileList();
		// 切换到新配置
		for (int i = 0; i < _profileSelector.ItemCount; i++)
		{
			if (_profileSelector.GetItemText(i) == name)
			{
				_profileSelector.Selected = i;
				break;
			}
		}
	}

	private void OnDeleteProfile()
	{
		var im = InputManager.Instance;
		if (im == null)
			return;

		int idx = _profileSelector.Selected;
		if (idx < 0 || idx >= _profileSelector.ItemCount)
			return;
		string name = _profileSelector.GetItemText(idx);

		im.DeleteProfile(name);
		im.SaveProfiles();
		RefreshProfileList();
		RefreshKeybindList();
	}

	private void OnResetDefaults()
	{
		var im = InputManager.Instance;
		if (im == null)
			return;

		im.ResetToDefaults();
		im.SaveProfiles();
		RefreshKeybindList();
	}

	// ===== 语言 / 分辨率 / 窗口 =====

	private void LoadLanguages()
	{
		_languageOptionButton.Clear();
		var languages = Localization.Localization.AvailableLanguages;
		var languageNames = new Dictionary<string, string>
		{
			{ "en", Localization.Localization.T("language.name_en", "English") },
			{ "zh", Localization.Localization.T("language.name_zh", "中文") }
		};

		int selectedIndex = 0;
		for (int i = 0; i < languages.Count; i++)
		{
			string lang = languages[i];
			string displayName = languageNames.TryGetValue(lang, out string n) ? n : lang;
			_languageOptionButton.AddItem(displayName);
			_languageOptionButton.SetItemMetadata(i, lang);
			if (lang == Localization.Localization.CurrentLanguage)
				selectedIndex = i;
		}
		_languageOptionButton.Selected = selectedIndex;
	}

	private void LoadResolutions()
	{
		_resolutionOptionButton.Clear();
		var resolutions = UIScaler.Instance.GetSupportedResolutions();
		int idx = UIScaler.Instance.GetCurrentResolutionFilteredIndex();

		for (int i = 0; i < resolutions.Count; i++)
		{
			_resolutionOptionButton.AddItem(resolutions[i].Label);
			_resolutionOptionButton.SetItemMetadata(i, $"{resolutions[i].Width},{resolutions[i].Height}");
		}
		_resolutionOptionButton.Selected = Mathf.Clamp(idx, 0, resolutions.Count - 1);
	}

	private void LoadWindowModes()
	{
		_windowModeOptionButton.Clear();
		_windowModeOptionButton.AddItem(Localization.Localization.T("ui.settings.windowed", "Windowed"));
		_windowModeOptionButton.SetItemMetadata(0, 0);
		_windowModeOptionButton.AddItem(Localization.Localization.T("ui.settings.borderless", "Borderless Fullscreen"));
		_windowModeOptionButton.SetItemMetadata(1, 1);
		_windowModeOptionButton.AddItem(Localization.Localization.T("ui.settings.fullscreen", "Fullscreen"));
		_windowModeOptionButton.SetItemMetadata(2, 2);
		_windowModeOptionButton.Selected = UIScaler.Instance.GetCurrentWindowModeIndex();
	}

	private void OnLanguageChanged(string newLanguage)
	{
		UpdateLabels();
		LoadWindowModes();
	}

	private void UpdateLabels()
	{
		_titleLabel.Text = Localization.Localization.T("ui.settings.title", "Settings");
		_languageLabel.Text = Localization.Localization.T("ui.settings.language", "Language");
		_resolutionLabel.Text = Localization.Localization.T("ui.settings.resolution", "Resolution");
		_windowModeLabel.Text = Localization.Localization.T("ui.settings.window_mode", "Window Mode");
		_visualStyleLabel.Text = Localization.Localization.T("ui.settings.visual_style", "自定义视觉风格");
		_intentIconFloatingToggle.Text = Localization.Localization.T("ui.settings.intent_icon_floating", "意图图标整体浮动");
		_intentValueFloatingToggle.Text = Localization.Localization.T("ui.settings.intent_value_floating", "伤害数字随图标浮动");
		_emoteIdleTimeLabel.Text = Localization.Localization.T("ui.settings.emote_idle_time", "Emote Idle Time");
		_emoteVarMinLabel.Text = Localization.Localization.T("ui.settings.emote_variation_min", "Variation Min");
		_emoteVarMaxLabel.Text = Localization.Localization.T("ui.settings.emote_variation_max", "Variation Max");
		_backButton.Text = Localization.Localization.T("ui.settings.back", "Back");
	}

	private void UpdateCurrentLanguage()
	{
		for (int i = 0; i < _languageOptionButton.ItemCount; i++)
		{
			var langVariant = _languageOptionButton.GetItemMetadata(i);
			if (langVariant.AsString() == Localization.Localization.CurrentLanguage)
			{
				_languageOptionButton.Selected = i;
				break;
			}
		}
	}

	// ===== 移动端 =====

	public override void _Input(InputEvent @event)
	{
		if (SceneLifecycleGuard.ShouldSkip(this))
			return;
		if (!MobileInputHelper.IsMobile)
			return;
		if (@event is not InputEventScreenTouch touch || !touch.Pressed)
			return;

		if (HitTestControl(_backButton, touch.Position))
		{
			OnBackPressed();
			GetViewport().SetInputAsHandled();
			return;
		}
		if (HitTestControl(_languageOptionButton, touch.Position))
		{
			CycleOptionButton(_languageOptionButton, OnLanguageSelected);
			GetViewport().SetInputAsHandled();
			return;
		}
		if (HitTestControl(_resolutionOptionButton, touch.Position))
		{
			CycleOptionButton(_resolutionOptionButton, OnResolutionSelected);
			GetViewport().SetInputAsHandled();
			return;
		}
		if (HitTestControl(_windowModeOptionButton, touch.Position))
		{
			CycleOptionButton(_windowModeOptionButton, OnWindowModeSelected);
			GetViewport().SetInputAsHandled();
			return;
		}
		if (_devModeToggle.Visible && HitTestControl(_devModeToggle, touch.Position))
		{
			_devModeToggle.ButtonPressed = !_devModeToggle.ButtonPressed;
			OnDevModeToggled(_devModeToggle.ButtonPressed);
			GetViewport().SetInputAsHandled();
			return;
		}
		if (HitTestControl(_intentIconFloatingToggle, touch.Position))
		{
			_intentIconFloatingToggle.ButtonPressed = !_intentIconFloatingToggle.ButtonPressed;
			OnIntentIconFloatingToggled(_intentIconFloatingToggle.ButtonPressed);
			GetViewport().SetInputAsHandled();
			return;
		}
		if (!_intentValueFloatingToggle.Disabled && HitTestControl(_intentValueFloatingToggle, touch.Position))
		{
			_intentValueFloatingToggle.ButtonPressed = !_intentValueFloatingToggle.ButtonPressed;
			OnIntentValueFloatingToggled(_intentValueFloatingToggle.ButtonPressed);
			GetViewport().SetInputAsHandled();
			return;
		}
		if (_consoleButton.Visible && HitTestControl(_consoleButton, touch.Position))
		{
			OnConsolePressed();
			GetViewport().SetInputAsHandled();
		}
	}

	private static bool HitTestControl(Control control, Vector2 touchPos)
	{
		if (control == null || !control.IsInsideTree() || !control.Visible)
			return false;
		return control.GetGlobalRect().Grow(MobileTouchHitPadding).HasPoint(touchPos);
	}

	private static void CycleOptionButton(OptionButton optionButton, System.Action<long> onSelected)
	{
		if (optionButton.ItemCount <= 0)
			return;
		int nextIdx = (optionButton.Selected + 1) % optionButton.ItemCount;
		optionButton.Selected = nextIdx;
		onSelected(nextIdx);
	}

	private void ApplyMobileLayout()
	{
		_languageOptionButton.CustomMinimumSize = new Vector2(260, 56);
		_resolutionOptionButton.CustomMinimumSize = new Vector2(260, 56);
		_windowModeOptionButton.CustomMinimumSize = new Vector2(260, 56);
		_intentIconFloatingToggle.CustomMinimumSize = new Vector2(260, 56);
		_intentValueFloatingToggle.CustomMinimumSize = new Vector2(260, 56);
		_backButton.CustomMinimumSize = new Vector2(220, 56);
		_backButton.AddThemeFontSizeOverride("font_size", 22);
		_consoleButton.CustomMinimumSize = new Vector2(220, 56);
		_resolutionRow.Visible = false;
		_windowModeRow.Visible = false;
	}
}
