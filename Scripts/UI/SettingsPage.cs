using Godot;
using OdysseyCards.Core;
using OdysseyCards.Infrastructure;
using System;
using System.Collections.Generic;
using Loc = OdysseyCards.Localization.Localization;

namespace OdysseyCards.UI;

public partial class SettingsPage : Control
{
	private const float MobileTouchHitPadding = 20f;

	// ===== Tab 切换 =====

	private Button _displayTabBtn = null!;
	private Button _gameTabBtn = null!;
	private Button _keybindTabBtn = null!;
	private ScrollContainer _displayScroll = null!;
	private ScrollContainer _gameScroll = null!;
	private ScrollContainer _keybindPageScroll = null!;
	private VBoxContainer _displayContainer = null!;
	private VBoxContainer _gameContainer = null!;
	private VBoxContainer _keybindContainer = null!;
	private string _activeTab = "display";

	// ===== 常规设置控件（全部保留）=====

	private OptionButton _languageOptionButton = null!;
	private OptionButton _resolutionOptionButton = null!;
	private OptionButton _windowModeOptionButton = null!;
	private Button _backButton = null!;
	private Label _titleLabel = null!;
	private Label _languageLabel = null!;
	private Label _resolutionLabel = null!;
	private Label _windowModeLabel = null!;
	private Label _cardDescriptionAlignmentLabel = null!;
	private HBoxContainer _resolutionRow = null!;
	private HBoxContainer _windowModeRow = null!;
	private HBoxContainer _cardDescriptionAlignmentRow = null!;
	private OptionButton _cardDescriptionAlignmentOptionButton = null!;
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
	private HFlowContainer _keybindListContainer = null!;
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
		// 填满父容器（SubmenuStack 或 MainMenu），防止布局塌陷到 (0,0)
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		SetupUI();
		LoadLanguages();
		LoadResolutions();
		LoadWindowModes();
		ConnectSignals();
		UpdateCurrentLanguage();
		SwitchToTab("display");

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
		_cardDescriptionAlignmentOptionButton.ItemSelected -= OnCardDescriptionAlignmentSelected;
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

		_displayTabBtn = CreateTabButton(Loc.T("ui.settings.tab_display", "显示"), true);
		_gameTabBtn = CreateTabButton(Loc.T("ui.settings.tab_game", "游戏"), false);
		_keybindTabBtn = CreateTabButton(Loc.T("ui.settings.tab_keybinds", "键位"), false);
		_displayTabBtn.Pressed += () => SwitchToTab("display");
		_gameTabBtn.Pressed += () => SwitchToTab("game");
		_keybindTabBtn.Pressed += () => SwitchToTab("keybind");

		tabRow.AddChild(_displayTabBtn);
		tabRow.AddChild(_gameTabBtn);
		tabRow.AddChild(_keybindTabBtn);

		// === 显示设置（可滚动）===
		_displayScroll = CreateTabScroll();
		_displayContainer = CreateTabContentContainer();
		SetupDisplayUI();
		_displayScroll.AddChild(_displayContainer);

		// === 游戏设置（可滚动）===
		_gameScroll = CreateTabScroll();
		_gameScroll.Visible = false;
		_gameContainer = CreateTabContentContainer();
		SetupGameUI();
		_gameScroll.AddChild(_gameContainer);

		// === 键位设置（可滚动）===
		_keybindPageScroll = CreateTabScroll();
		_keybindPageScroll.Visible = false;
		_keybindContainer = CreateTabContentContainer();
		SetupKeybindUI();
		_keybindPageScroll.AddChild(_keybindContainer);

		// === 返回按钮 ===
		_backButton = new Button
		{
			Name = "BackButton",
			Text = Loc.T("ui.settings.back", "Back"),
			CustomMinimumSize = new Vector2(140, 44),
		};
		_backButton.AddThemeFontSizeOverride("font_size", 18);

		// === 根容器（全屏锚定，不设 Alignment 避免塌陷）===
		var root = new VBoxContainer { Name = "SettingsRoot" };
		root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		root.AddThemeConstantOverride("separation", 10);

		root.AddChild(_titleLabel);
		root.AddChild(tabRow);
		root.AddChild(_displayScroll);
		root.AddChild(_gameScroll);
		root.AddChild(_keybindPageScroll);
		root.AddChild(_backButton);

		AddChild(root);
	}

	// ===== Tab 滚动容器 / 内容容器工厂 =====

	private static ScrollContainer CreateTabScroll()
	{
		var scroll = new ScrollContainer
		{
			Name = "TabScroll",
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Stop,
		};
		return scroll;
	}

	private static VBoxContainer CreateTabContentContainer()
	{
		var vbox = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Begin,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		vbox.AddThemeConstantOverride("separation", 12);
		return vbox;
	}

	/// <summary>创建分组标题行：分隔线 + 居中标签。</summary>
	private static HBoxContainer CreateSectionHeader(string key, string fallback)
	{
		var row = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		var leftSep = new HSeparator { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		var rightSep = new HSeparator { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		var label = new Label
		{
			Text = Loc.T(key, fallback),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		label.AddThemeFontSizeOverride("font_size", 16);
		label.AddThemeColorOverride("font_color", new Color(0.75f, 0.85f, 1f));
		label.CustomMinimumSize = new Vector2(80, 0);

		row.AddChild(leftSep);
		row.AddChild(label);
		row.AddChild(rightSep);
		return row;
	}

	// ===== 显示设置 UI =====

	private void SetupDisplayUI()
	{
		_displayContainer.AddChild(CreateSectionHeader("ui.settings.section_basic", "基础"));

		// 语言行
		_languageLabel = CreateSettingLabel("ui.settings.language", "Language");
		_languageOptionButton = new OptionButton { CustomMinimumSize = new Vector2(200, 0) };
		_displayContainer.AddChild(CreateSettingRow(_languageLabel, _languageOptionButton));

		// 分辨率行
		_resolutionLabel = CreateSettingLabel("ui.settings.resolution", "Resolution");
		_resolutionOptionButton = new OptionButton { CustomMinimumSize = new Vector2(200, 0) };
		var resolutionRow = CreateSettingRow(_resolutionLabel, _resolutionOptionButton);
		_resolutionRow = resolutionRow;
		_displayContainer.AddChild(resolutionRow);

		// 窗口模式行
		_windowModeLabel = CreateSettingLabel("ui.settings.window_mode", "Window Mode");
		_windowModeOptionButton = new OptionButton { CustomMinimumSize = new Vector2(200, 0) };
		var windowModeRow = CreateSettingRow(_windowModeLabel, _windowModeOptionButton);
		_windowModeRow = windowModeRow;
		_displayContainer.AddChild(windowModeRow);

		_displayContainer.AddChild(CreateSectionHeader("ui.settings.section_visual", "卡牌视觉"));

		// 卡牌描述对齐
		_cardDescriptionAlignmentLabel = CreateSettingLabel("ui.settings.card_description_alignment", "Card Description Alignment");
		_cardDescriptionAlignmentOptionButton = new OptionButton { CustomMinimumSize = new Vector2(200, 0) };
		_cardDescriptionAlignmentRow = CreateSettingRow(_cardDescriptionAlignmentLabel, _cardDescriptionAlignmentOptionButton);
		LoadCardDescriptionAlignmentOptions();
		_displayContainer.AddChild(_cardDescriptionAlignmentRow);

		// 意图视觉效果
		bool iconFloating = UIScaler.Instance?.IntentIconFloatingEnabled ?? true;
		bool valueFloating = UIScaler.Instance?.IntentValueFloatingEnabled ?? true;
		_intentIconFloatingToggle = new CheckBox
		{
			Text = Loc.T("ui.settings.intent_icon_floating", "意图图标整体浮动"),
			ButtonPressed = iconFloating,
		};
		_intentIconFloatingToggle.AddThemeFontSizeOverride("font_size", 18);
		_displayContainer.AddChild(CreateCenteredRow(_intentIconFloatingToggle));

		_intentValueFloatingToggle = new CheckBox
		{
			Text = Loc.T("ui.settings.intent_value_floating", "伤害数字随图标浮动"),
			ButtonPressed = valueFloating,
			Disabled = !iconFloating,
		};
		_intentValueFloatingToggle.AddThemeFontSizeOverride("font_size", 18);
		_displayContainer.AddChild(CreateCenteredRow(_intentValueFloatingToggle));
	}

	// ===== 游戏设置 UI =====

	private void SetupGameUI()
	{
		_gameContainer.AddChild(CreateSectionHeader("ui.settings.section_emote", "表情"));

		// 表情空闲时间
		var gm = GameManager.Instance;
		float currentIdleTime = gm?.EmoteIdleTimeSeconds ?? 5.0f;
		_emoteIdleTimeLabel = CreateSettingLabel("ui.settings.emote_idle_time", "Emote Idle Time");
		_emoteIdleTimeSlider = new HSlider
		{
			MinValue = 3.0, MaxValue = 15.0, Step = 0.5,
			Value = currentIdleTime, CustomMinimumSize = new Vector2(160, 0),
		};
		_emoteIdleTimeValueLabel = new Label { Text = $"{currentIdleTime:F1}s", CustomMinimumSize = new Vector2(50, 0) };
		_emoteIdleTimeValueLabel.AddThemeFontSizeOverride("font_size", 16);
		_gameContainer.AddChild(CreateSliderRow(_emoteIdleTimeLabel, _emoteIdleTimeSlider, _emoteIdleTimeValueLabel));

		// 随机最小/最大倍率
		float currentVarMin = gm?.EmoteIdleVariationMin ?? 0.7f;
		_emoteVarMinLabel = CreateSettingLabel("ui.settings.emote_variation_min", "Variation Min");
		_emoteVarMinSlider = new HSlider
		{
			MinValue = 0.1, MaxValue = 3.0, Step = 0.1,
			Value = currentVarMin, CustomMinimumSize = new Vector2(160, 0),
		};
		_emoteVarMinValueLabel = new Label { Text = $"×{currentVarMin:F1}", CustomMinimumSize = new Vector2(50, 0) };
		_emoteVarMinValueLabel.AddThemeFontSizeOverride("font_size", 16);
		_gameContainer.AddChild(CreateSliderRow(_emoteVarMinLabel, _emoteVarMinSlider, _emoteVarMinValueLabel));

		float currentVarMax = gm?.EmoteIdleVariationMax ?? 1.3f;
		_emoteVarMaxLabel = CreateSettingLabel("ui.settings.emote_variation_max", "Variation Max");
		_emoteVarMaxSlider = new HSlider
		{
			MinValue = 0.1, MaxValue = 3.0, Step = 0.1,
			Value = currentVarMax, CustomMinimumSize = new Vector2(160, 0),
		};
		_emoteVarMaxValueLabel = new Label { Text = $"×{currentVarMax:F1}", CustomMinimumSize = new Vector2(50, 0) };
		_emoteVarMaxValueLabel.AddThemeFontSizeOverride("font_size", 16);
		_gameContainer.AddChild(CreateSliderRow(_emoteVarMaxLabel, _emoteVarMaxSlider, _emoteVarMaxValueLabel));

		_gameContainer.AddChild(CreateSectionHeader("ui.settings.section_developer", "开发者"));

		// 开发者模式
		bool devMode = UIScaler.Instance?.DevModeEnabled ?? false;
		DevConsole.IsDevMode = devMode; // 启动时从持久化恢复
		_devModeToggle = new CheckBox
		{
			Text = Loc.T("ui.settings.dev_mode", "开发者模式"),
			ButtonPressed = devMode,
		};
		_devModeToggle.AddThemeFontSizeOverride("font_size", 20);
		_gameContainer.AddChild(CreateCenteredRow(_devModeToggle));

		_consoleButton = new Button
		{
			Text = Loc.T("ui.settings.open_console", "打开控制台"),
			CustomMinimumSize = new Vector2(0, 44),
			Visible = devMode,
		};
		_consoleButton.AddThemeFontSizeOverride("font_size", 16);
		_gameContainer.AddChild(CreateCenteredRow(_consoleButton));
	}

	// ===== 键位设置 UI =====

	private void SetupKeybindUI()
	{
		// 配置选择器行
		var profileLabel = new Label
		{
			Text = Loc.T("ui.settings.keybind_profile", "键位配置"),
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
			Text = Loc.T("ui.settings.keybind_hint", "点击键位按钮后按下新按键即可重新绑定"),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		hintLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
		hintLabel.AddThemeFontSizeOverride("font_size", 14);
		_keybindContainer.AddChild(hintLabel);

		// 可滚动键位网格（流式布局，响应窗口宽度自动换行）
		_keybindScroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(0, 280),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		_keybindListContainer = new HFlowContainer
		{
			Name = "KeybindFlow",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		_keybindListContainer.AddThemeConstantOverride("h_separation", 14);
		_keybindListContainer.AddThemeConstantOverride("v_separation", 8);
		_keybindScroll.AddChild(_keybindListContainer);
		_keybindContainer.AddChild(_keybindScroll);

		// 重置按钮
		_resetDefaultsBtn = new Button
		{
			Text = Loc.T("ui.settings.reset_defaults", "重置为默认键位"),
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

	/// <summary>重建键位网格，反映当前映射。</summary>
	private void RefreshKeybindList()
	{
		foreach (var child in _keybindListContainer.GetChildren())
			child.QueueFree();

		var im = InputManager.Instance;
		if (im == null)
			return;

		foreach (var action in RebindableActions)
		{
			var key = im.GetKey(action);
			var displayName = ActionDisplayNames.TryGetValue(action, out var name) ? name : action.ToString();
			AddKeybindFlowItem(action, displayName, key);
		}
	}

	/// <summary>向键位流式布局添加一项：标签 + 按钮的 HBoxContainer。</summary>
	private void AddKeybindFlowItem(StringName action, string displayName, Key currentKey)
	{
		var row = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			CustomMinimumSize = new Vector2(180, 36),
		};
		row.AddThemeConstantOverride("separation", 8);

		var label = new Label
		{
			Text = displayName,
			CustomMinimumSize = new Vector2(80, 0),
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
		};
		label.AddThemeFontSizeOverride("font_size", 18);

		var keyBtn = new Button
		{
			Text = KeyToDisplay(currentKey),
			CustomMinimumSize = new Vector2(90, 32),
		};
		keyBtn.AddThemeFontSizeOverride("font_size", 16);
		keyBtn.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.6f));

		var capturedAction = action;
		keyBtn.Pressed += () => StartListening(capturedAction, keyBtn);

		row.AddChild(label);
		row.AddChild(keyBtn);
		_keybindListContainer.AddChild(row);
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
		bool isDisplay = tab == "display";
		bool isGame = tab == "game";
		bool isKeybind = tab == "keybind";
		_activeTab = tab;

		_displayScroll.Visible = isDisplay;
		_gameScroll.Visible = isGame;
		_keybindPageScroll.Visible = isKeybind;

		_displayScroll.MouseFilter = isDisplay ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
		_gameScroll.MouseFilter = isGame ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
		_keybindPageScroll.MouseFilter = isKeybind ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;

		UpdateTabStyle(_displayTabBtn, isDisplay);
		UpdateTabStyle(_gameTabBtn, isGame);
		UpdateTabStyle(_keybindTabBtn, isKeybind);

		StopListening();

		if (isKeybind)
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
			Text = Loc.T("ui.settings.title", "Settings"),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		label.AddThemeFontSizeOverride("font_size", 36);
		label.CustomMinimumSize = new Vector2(0, 50);
		return label;
	}

	private static Label CreateSettingLabel(string key, string fallback)
	{
		var label = new Label { Text = Loc.T(key, fallback) };
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

	/// <summary>将单个控件包装在居中的 HBoxContainer 中（用于 CheckBox/Button 等无需配对标签的控件）。</summary>
	private static HBoxContainer CreateCenteredRow(Control control)
	{
		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
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
		_cardDescriptionAlignmentOptionButton.ItemSelected += OnCardDescriptionAlignmentSelected;
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

	private void OnCardDescriptionAlignmentSelected(long index)
	{
		bool centered = _cardDescriptionAlignmentOptionButton.GetItemMetadata((int)index).AsBool();
		UIScaler.Instance?.SetCardDescriptionCentered(centered);
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
		UIScaler.Instance?.SetDevMode(on); // 持久化
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
			{ "en", Loc.T("language.name_en", "English") },
			{ "zh", Loc.T("language.name_zh", "中文") }
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
		_windowModeOptionButton.AddItem(Loc.T("ui.settings.windowed", "Windowed"));
		_windowModeOptionButton.SetItemMetadata(0, 0);
		_windowModeOptionButton.AddItem(Loc.T("ui.settings.borderless", "Borderless Fullscreen"));
		_windowModeOptionButton.SetItemMetadata(1, 1);
		_windowModeOptionButton.AddItem(Loc.T("ui.settings.fullscreen", "Fullscreen"));
		_windowModeOptionButton.SetItemMetadata(2, 2);
		_windowModeOptionButton.Selected = UIScaler.Instance.GetCurrentWindowModeIndex();
	}

	private void OnLanguageChanged(string newLanguage)
	{
		UpdateLabels();
		LoadWindowModes();
		LoadCardDescriptionAlignmentOptions();
	}

	private void UpdateLabels()
	{
		_titleLabel.Text = Loc.T("ui.settings.title", "Settings");
		_displayTabBtn.Text = Loc.T("ui.settings.tab_display", "显示");
		_gameTabBtn.Text = Loc.T("ui.settings.tab_game", "游戏");
		_keybindTabBtn.Text = Loc.T("ui.settings.tab_keybinds", "键位");
		_languageLabel.Text = Loc.T("ui.settings.language", "Language");
		_resolutionLabel.Text = Loc.T("ui.settings.resolution", "Resolution");
		_windowModeLabel.Text = Loc.T("ui.settings.window_mode", "Window Mode");
		_cardDescriptionAlignmentLabel.Text = Loc.T("ui.settings.card_description_alignment", "Card Description Alignment");
		_intentIconFloatingToggle.Text = Loc.T("ui.settings.intent_icon_floating", "意图图标整体浮动");
		_intentValueFloatingToggle.Text = Loc.T("ui.settings.intent_value_floating", "伤害数字随图标浮动");
		_emoteIdleTimeLabel.Text = Loc.T("ui.settings.emote_idle_time", "Emote Idle Time");
		_emoteVarMinLabel.Text = Loc.T("ui.settings.emote_variation_min", "Variation Min");
		_emoteVarMaxLabel.Text = Loc.T("ui.settings.emote_variation_max", "Variation Max");
		_backButton.Text = Loc.T("ui.settings.back", "Back");
		_devModeToggle.Text = Loc.T("ui.settings.dev_mode", "开发者模式");
		_consoleButton.Text = Loc.T("ui.settings.open_console", "打开控制台");
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

	private void LoadCardDescriptionAlignmentOptions()
	{
		_cardDescriptionAlignmentOptionButton.Clear();
		_cardDescriptionAlignmentOptionButton.AddItem(Loc.T("ui.settings.card_description_left", "Left Align"));
		_cardDescriptionAlignmentOptionButton.SetItemMetadata(0, false);
		_cardDescriptionAlignmentOptionButton.AddItem(Loc.T("ui.settings.card_description_center", "Centered"));
		_cardDescriptionAlignmentOptionButton.SetItemMetadata(1, true);

		bool centered = UIScaler.Instance?.CardDescriptionCentered ?? false;
		_cardDescriptionAlignmentOptionButton.Selected = centered ? 1 : 0;
	}

	// ===== 键盘 / 系统返回 =====

	public override void _Input(InputEvent @event)
	{
		if (SceneLifecycleGuard.ShouldSkip(this))
			return;

		// ESC / Android 返回键 → 返回上一级（键位监听中由 _UnhandledKeyInput 接管）
		if (@event is InputEventKey key && key.Pressed && !_isListeningForKey)
		{
			if (key.Keycode == Key.Escape || key.Keycode == Key.Back)
			{
				OnBackPressed();
				GetViewport().SetInputAsHandled();
				return;
			}
		}

		// 移动端触摸
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
		if (IsDisplayTabActive() && HitTestControl(_languageOptionButton, touch.Position))
		{
			CycleOptionButton(_languageOptionButton, OnLanguageSelected);
			GetViewport().SetInputAsHandled();
			return;
		}
		if (IsDisplayTabActive() && HitTestControl(_resolutionOptionButton, touch.Position))
		{
			CycleOptionButton(_resolutionOptionButton, OnResolutionSelected);
			GetViewport().SetInputAsHandled();
			return;
		}
		if (IsDisplayTabActive() && HitTestControl(_windowModeOptionButton, touch.Position))
		{
			CycleOptionButton(_windowModeOptionButton, OnWindowModeSelected);
			GetViewport().SetInputAsHandled();
			return;
		}
		if (IsDisplayTabActive() && HitTestControl(_cardDescriptionAlignmentOptionButton, touch.Position))
		{
			CycleOptionButton(_cardDescriptionAlignmentOptionButton, OnCardDescriptionAlignmentSelected);
			GetViewport().SetInputAsHandled();
			return;
		}
		if (IsGameTabActive() && _devModeToggle.Visible && HitTestControl(_devModeToggle, touch.Position))
		{
			_devModeToggle.ButtonPressed = !_devModeToggle.ButtonPressed;
			OnDevModeToggled(_devModeToggle.ButtonPressed);
			GetViewport().SetInputAsHandled();
			return;
		}
		if (IsDisplayTabActive() && HitTestControl(_intentIconFloatingToggle, touch.Position))
		{
			_intentIconFloatingToggle.ButtonPressed = !_intentIconFloatingToggle.ButtonPressed;
			OnIntentIconFloatingToggled(_intentIconFloatingToggle.ButtonPressed);
			GetViewport().SetInputAsHandled();
			return;
		}
		if (IsDisplayTabActive() && !_intentValueFloatingToggle.Disabled && HitTestControl(_intentValueFloatingToggle, touch.Position))
		{
			_intentValueFloatingToggle.ButtonPressed = !_intentValueFloatingToggle.ButtonPressed;
			OnIntentValueFloatingToggled(_intentValueFloatingToggle.ButtonPressed);
			GetViewport().SetInputAsHandled();
			return;
		}
		if (IsGameTabActive() && _consoleButton.Visible && HitTestControl(_consoleButton, touch.Position))
		{
			OnConsolePressed();
			GetViewport().SetInputAsHandled();
		}
	}
	private bool IsDisplayTabActive() => _activeTab == "display" && _displayScroll.IsVisibleInTree();

	private bool IsGameTabActive() => _activeTab == "game" && _gameScroll.IsVisibleInTree();

	/// <summary>
	/// Android 系统返回手势/按钮 → 返回上一级。
	/// 在 Godot 中处理此通知可阻止默认行为（退出应用）。
	/// </summary>
	public override void _Notification(int what)
	{
		if (what == NotificationWMGoBackRequest)
		{
			OnBackPressed();
		}
	}

	private static bool HitTestControl(Control control, Vector2 touchPos)
	{
		if (control == null || !control.IsInsideTree() || !control.IsVisibleInTree())
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
		_cardDescriptionAlignmentOptionButton.CustomMinimumSize = new Vector2(260, 56);
		_intentIconFloatingToggle.CustomMinimumSize = new Vector2(260, 56);
		_intentValueFloatingToggle.CustomMinimumSize = new Vector2(260, 56);
		_backButton.CustomMinimumSize = new Vector2(220, 56);
		_backButton.AddThemeFontSizeOverride("font_size", 22);
		_consoleButton.CustomMinimumSize = new Vector2(220, 56);
		_resolutionRow.Visible = false;
		_windowModeRow.Visible = false;
	}
}
