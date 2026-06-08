using Godot;
using OdysseyCards.Core;
using OdysseyCards.Relic;
using OdysseyCards.Roguelike;
using OdysseyCards.Localization;
using OdysseyCards.Infrastructure;
using System;
using System.Collections.Generic;

namespace OdysseyCards.UI;

/// <summary>
/// 路线选择地图 UI — 移动优先响应式布局。
/// 全屏自适应，可滚动房间卡片列表（大触控目标），
/// 支持 MobileInputRouter 触控分区 + MobileDialogHost 弹窗 + SceneLifecycleGuard tombstone。
/// 桌面鼠标操作通过 Button.Pressed 条件兼容。
/// </summary>
public partial class MapUI : Control
{
    // ===== UI 控件 =====

    private Label _titleLabel = null!;
    private Label _progressLabel = null!;
    private Label _layerLabel = null!;
    private ScrollContainer _scrollContainer = null!;
    private VBoxContainer _choicesContainer = null!;
    private Button _quitButton = null!;

    // ===== 状态 =====

    private GameRunState _runState = null!;

    /// <summary>当前层可选房间列表（用于键盘导航索引映射）。</summary>
    private System.Collections.Generic.IReadOnlyList<RoomDefinition> _currentRoomChoices
        = Array.Empty<RoomDefinition>();

    // ===== 键盘导航 =====

    /// <summary>键盘焦点房间索引（-1 表示无焦点）。</summary>
    private int _focusedRoomIndex = -1;

    /// <summary>热键回调引用（用于注销时精确移除）。</summary>
    private Action? _leftAction, _rightAction, _upAction, _downAction;
    private Action? _acceptAction, _cancelAction;
    private Action? _infoScreenAction;

    // ===== 综合信息界面 =====
    private InfoScreen? _infoScreen;

    // ===== MobileInputRouter zone 令牌（_ExitTree 时统一释放） =====

    private IDisposable? _quitZoneToken;
    private readonly List<IDisposable> _roomZoneTokens = new();

    // ===== 房间图标映射 =====

    private static string GetRoomIcon(RoomType type) => type switch
    {
        RoomType.Monster => Localization.Localization.T("ui.map.room_battle", "[战斗]"),
        RoomType.Elite => Localization.Localization.T("ui.map.room_elite", "[精英]"),
        RoomType.Boss => Localization.Localization.T("ui.map.room_boss", "[BOSS]"),
        RoomType.Treasure => Localization.Localization.T("ui.map.room_reward", "[奖励]"),
        RoomType.Shop => Localization.Localization.T("ui.map.room_shop", "[商店]"),
        RoomType.RestSite => Localization.Localization.T("ui.map.room_rest", "[休息]"),
        RoomType.Event => Localization.Localization.T("ui.map.room_event", "[事件]"),
        _ => "[?]"
    };

    /// <summary>
    /// 根据房间类型生成敌人预览文本。
    /// </summary>
    private static string GetEnemyPreview(RoomType type) => type switch
    {
        RoomType.Monster => Localization.Localization.T("ui.map.enemy_preview_monster", "预计遭遇：普通怪物"),
        RoomType.Elite => Localization.Localization.T("ui.map.enemy_preview_elite", "预计遭遇：精英 ×2"),
        RoomType.Boss => Localization.Localization.T("ui.map.enemy_preview_boss", "预计遭遇：位面守护者"),
        _ => ""
    };

    // ===== Godot 生命周期 =====

    public override void _EnterTree()
    {
        base._EnterTree();
        RegisterHotkeyBindings();
    }

	public override void _Ready()
	{
		if (SceneLifecycleGuard.ShouldSkip(this)) return;

		GD.Print("[MapUI] _Ready — 初始化地图界面");

		var gm = GameManager.Instance;
		_runState = gm?.RunState;
		if (_runState == null)
		{
			GD.PrintErr("[MapUI] RunState 为 null！回退创建新冒险...");
			gm?.StartNewRun();
			_runState = gm?.RunState;
			if (_runState == null)
			{
				// ShowErrorAndQuit 内部用 MobileDialogHost，延迟执行
				CallDeferred(nameof(ShowInitError));
				return;
			}
		}

		// 延迟到下一帧构建 UI。若在 _Ready 同步构建，viewport 可能尚未确定最终尺寸，
		// 导致容器布局塌陷到 (0,0)，所有按钮挤在左上角。
		CallDeferred(nameof(DeferredSetupUI));

		GameManager.Instance.LanguageChanged += OnLanguageChanged;
	}

	/// <summary>
	/// 延迟 UI 构建入口——viewport 尺寸就绪后再建布局。
	/// </summary>
	private void DeferredSetupUI()
	{
		if (SceneLifecycleGuard.ShouldSkip(this)) return;
		SetupUI();
		RefreshRoomChoices();
	}

	/// <summary>
	/// 延迟显示初始化错误（当 RunState 为 null 时）。
	/// </summary>
	private void ShowInitError()
	{
		if (SceneLifecycleGuard.ShouldSkip(this)) return;
		ShowErrorAndQuit(Localization.Localization.T("ui.map.init_error", "无法初始化冒险数据"));
	}

    public override void _Input(InputEvent @event)
    {
        if (SceneLifecycleGuard.ShouldSkip(this)) return;
        // 所有交互通过 TapZone（移动端）或 Button.Pressed（桌面端）处理
    }

    public override void _ExitTree()
    {
        SceneLifecycleGuard.OnExitTree(this);

        // 注销键盘热键绑定
        UnregisterHotkeyBindings();

        // 释放所有触控分区令牌
        _quitZoneToken?.Dispose();
        _quitZoneToken = null;
        foreach (var token in _roomZoneTokens)
            token.Dispose();
        _roomZoneTokens.Clear();

        if (GameManager.Instance != null)
            GameManager.Instance.LanguageChanged -= OnLanguageChanged;
    }

    // ===== UI 构建 =====

    /// <summary>
    /// 构建全屏响应式布局。
    /// 顶部：标题+进度+层提示 → 中部：可滚动房间卡片列表 → 底部：放弃按钮。
    /// 所有控件使用锚点/容器布局，不硬编码 Position/Size。
    /// </summary>
    private void SetupUI()
    {
        // 背景 — 全屏深色
        var bg = new ColorRect
        {
            Name = "Background",
            Color = new Color(0.08f, 0.08f, 0.12f, 1),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

		// 根容器 — 全屏 VBox
		// 注意：不能用 SetAnchorsAndOffsetsPreset（会覆盖 VBoxContainer 的 LayoutMode=Container，
		// 导致子控件无法正确布局）。改为手动设 anchors。
		var root = new VBoxContainer
		{
			Name = "RootContainer",
			AnchorLeft = 0,
			AnchorTop = 0,
			AnchorRight = 1,
			AnchorBottom = 1,
		};
		root.AddThemeConstantOverride("separation", 0);
		AddChild(root);

        // ===== 顶部区域 =====
        var topSection = new VBoxContainer
        {
            Name = "TopSection",
        };
        topSection.AddThemeConstantOverride("separation", 6);
        root.AddChild(topSection);

        // 顶部留白
        topSection.AddChild(CreateSpacer(20));

        // 标题 — 位面名称
        _titleLabel = new Label
        {
            Name = "TitleLabel",
            Text = _runState?.CurrentPlane?.PlaneName ?? Localization.Localization.T("ui.map.title_fallback", "路线选择"),
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", 36);
        _titleLabel.AddThemeColorOverride("font_color", new Color(1, 0.9f, 0.5f, 1));
        topSection.AddChild(_titleLabel);

        // 进度
        _progressLabel = new Label
        {
            Name = "ProgressLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _progressLabel.AddThemeFontSizeOverride("font_size", 20);
        _progressLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.8f, 1));
        topSection.AddChild(_progressLabel);

        // 层选择提示
        _layerLabel = new Label
        {
            Name = "LayerLabel",
            Text = Localization.Localization.T("ui.map.select_room", "选择下一个房间："),
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _layerLabel.AddThemeFontSizeOverride("font_size", 22);
        _layerLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 1));
        topSection.AddChild(_layerLabel);

        // 顶部与中部间距
        topSection.AddChild(CreateSpacer(16));

        // ===== 中部区域 — 可滚动房间卡片列表（左右留 10% 边距 ≈ 80% 宽度） =====
        var centerMargin = new MarginContainer
        {
            Name = "CenterMargin",
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        centerMargin.AddThemeConstantOverride("margin_left", 40);
        centerMargin.AddThemeConstantOverride("margin_right", 40);
        root.AddChild(centerMargin);

        _scrollContainer = new ScrollContainer
        {
            Name = "ScrollContainer",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        centerMargin.AddChild(_scrollContainer);

        _choicesContainer = new VBoxContainer
        {
            Name = "ChoicesContainer",
        };
        _choicesContainer.AddThemeConstantOverride("separation", 16);
        _scrollContainer.AddChild(_choicesContainer);

        // ===== 底部区域 — 放弃按钮 =====
        var bottomSection = new MarginContainer
        {
            Name = "BottomSection",
        };
        bottomSection.AddThemeConstantOverride("margin_left", 24);
        bottomSection.AddThemeConstantOverride("margin_right", 24);
        bottomSection.AddThemeConstantOverride("margin_bottom", 16);
        root.AddChild(bottomSection);

        _quitButton = new Button
        {
            Name = "QuitButton",
            Text = Localization.Localization.T("ui.map.abandon", "放弃冒险（返回主菜单）"),
            CustomMinimumSize = new Vector2(0, 64),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _quitButton.AddThemeFontSizeOverride("font_size", 18);
        _quitButton.AddThemeColorOverride("font_color", new Color(0.9f, 0.3f, 0.3f, 1));

        // 移动端：TapZone；桌面端：Button.Pressed
        RegisterQuitInteraction();
        bottomSection.AddChild(_quitButton);
    }

    /// <summary>
    /// 注册放弃按钮的交互（移动端 TapZone / 桌面端 Pressed）。
    /// </summary>
    private void RegisterQuitInteraction()
    {
        if (MobileInputRouter.IsMobile)
        {
            var router = MobileInputRouter.Instance;
            if (router != null)
            {
                _quitZoneToken = router.RegisterTapZone(
                    _quitButton,
                    _quitButton.GetGlobalRect(),
                    priority: 400,
                    onTap: ExecuteQuit);
            }
        }
        else
        {
            _quitButton.Pressed += OnQuitPressed;
        }
    }

    /// <summary>
    /// 执行退出（TapZone 和 Button.Pressed 的公共入口）。
    /// </summary>
    private void ExecuteQuit()
    {
        CallDeferred(nameof(OnQuitPressed));
    }

    /// <summary>
    /// 创建垂直间距控件。
    /// </summary>
    private static Control CreateSpacer(int height)
    {
        return new Control
        {
            CustomMinimumSize = new Vector2(0, height),
        };
    }

    // ===== 刷新房间选择 =====

    /// <summary>
    /// 根据 RunState 刷新当前层的可选房间卡片。
    /// 每层显示 1-2 个房间卡片。
    /// </summary>
    private void RefreshRoomChoices()
    {
        // 释放旧的房间 zone 令牌
        foreach (var token in _roomZoneTokens)
            token.Dispose();
        _roomZoneTokens.Clear();

        // 清除旧卡片
        foreach (var child in _choicesContainer.GetChildren())
        {
            child.QueueFree();
        }

        if (_runState == null)
        {
            ShowErrorAndQuit(Localization.Localization.T("ui.map.error_no_runstate", "运行状态丢失"));
            return;
        }

        // 检查位面是否完成
        if (_runState.IsPlaneComplete || _runState.IsRunComplete)
        {
            if (_runState.IsRunComplete && !_runState.IsRunFailed)
            {
                ShowRunComplete();
            }
            return;
        }

        // 更新进度
        int current = _runState.CurrentLayerIndex + 1;
        int total = _runState.TotalLayers;
        _progressLabel.Text = Localization.Localization.T("ui.map.progress_format", "进度：第 {current}/{total} 层")
            .Replace("{current}", current.ToString())
            .Replace("{total}", total.ToString());

        // 获取当前层可选房间
        var choices = _runState.GetCurrentLayerChoices();
        _currentRoomChoices = choices;

        if (choices.Count == 0)
        {
            ShowErrorAndQuit(Localization.Localization.T("ui.map.error_no_choices", "当前层没有可选房间"));
            return;
        }

        // 单选项提示
        if (choices.Count == 1)
        {
            _layerLabel.Text = Localization.Localization.T("ui.map.one_path", "前方只有一条路：");
        }
        else
        {
            _layerLabel.Text = Localization.Localization.T("ui.map.multi_choices", "选择下一个房间（{count} 个可选）：")
                .Replace("{count}", choices.Count.ToString());
        }

        // 创建房间卡片
        _focusedRoomIndex = -1;
        foreach (var room in choices)
        {
            var card = CreateRoomCard(room);
            _choicesContainer.AddChild(card);
            RegisterRoomInteraction(card, room);
        }
    }

    /// <summary>
    /// 为指定房间创建卡片按钮（含图标、名称、描述、敌人预览，最小触控高度 64px）。
    /// 按钮文本为多行富文本，SizeFlags 填满 ScrollContainer 宽度。
    /// 不在此方法中连接 Pressed/TapZone——由调用方 RegisterRoomInteraction 处理。
    /// </summary>
    private static Button CreateRoomCard(RoomDefinition room)
    {
        string enemyPreview = GetEnemyPreview(room.Type);
        string text = string.IsNullOrEmpty(enemyPreview)
            ? $"{GetRoomIcon(room.Type)}  {room.DisplayName}\n{room.Description}"
            : $"{GetRoomIcon(room.Type)}  {room.DisplayName}\n{room.Description}\n{enemyPreview}";

        var btn = new Button
        {
            CustomMinimumSize = new Vector2(0, 80),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Text = text,
        };
        btn.AddThemeFontSizeOverride("font_size", 18);
        btn.AddThemeColorOverride("font_color", new Color(1, 1, 1, 1));
        btn.AddThemeColorOverride("font_hover_color", new Color(1, 0.8f, 0.3f, 1));
        return btn;
    }

    /// <summary>
    /// 注册房间卡片的交互。
    /// 移动端：TapZone（priority 400）；桌面端：Button.Pressed。
    /// </summary>
    private void RegisterRoomInteraction(Button card, RoomDefinition room)
    {
        if (MobileInputRouter.IsMobile)
        {
            var router = MobileInputRouter.Instance;
            if (router != null)
            {
                var token = router.RegisterTapZone(
                    card,
                    card.GetGlobalRect(),
                    priority: 400,
                    onTap: () => HandleRoomSelected(room));
                _roomZoneTokens.Add(token);
            }
        }
        else
        {
            card.Pressed += () => HandleRoomSelected(room);
        }
    }

    // ===== 键盘导航 — HotkeyManager 注册/注销 =====

    /// <summary>
    /// 注册所有键盘热键绑定到 HotkeyManager。
    /// 方向键导航房间列表，Enter 确认选择，Escape 返回。
    /// </summary>
    private void RegisterHotkeyBindings()
    {
        var hm = HotkeyManager.Instance;
        if (hm == null) return;

        // 方向键 — 房间导航
        _leftAction = () => NavigateRoomFocus(-1);
        _rightAction = () => NavigateRoomFocus(1);
        _upAction = () => NavigateRoomFocus(-1);
        _downAction = () => NavigateRoomFocus(1);
        hm.PushPressedBinding(OdysseyInput.Left, _leftAction);
        hm.PushPressedBinding(OdysseyInput.Right, _rightAction);
        hm.PushPressedBinding(OdysseyInput.Up, _upAction);
        hm.PushPressedBinding(OdysseyInput.Down, _downAction);

		// 确认 / 取消
		_acceptAction = AcceptFocusedRoom;
		_cancelAction = HandleKeyboardCancel;
		hm.PushPressedBinding(OdysseyInput.Accept, _acceptAction);
		hm.PushPressedBinding(OdysseyInput.Cancel, _cancelAction);

		// 综合信息界面
		_infoScreenAction = ToggleInfoScreen;
		hm.PushPressedBinding(OdysseyInput.InfoScreen, _infoScreenAction);

        // 键盘焦点超时事件 — 超时后清除焦点指示器
        hm.KeyboardFocusChanged += OnKeyboardFocusChanged;
    }

    /// <summary>
    /// 注销所有键盘热键绑定。
    /// </summary>
    private void UnregisterHotkeyBindings()
    {
        var hm = HotkeyManager.Instance;
        if (hm == null) return;

        hm.KeyboardFocusChanged -= OnKeyboardFocusChanged;

        if (_leftAction != null) { hm.RemovePressedBinding(OdysseyInput.Left, _leftAction); _leftAction = null; }
        if (_rightAction != null) { hm.RemovePressedBinding(OdysseyInput.Right, _rightAction); _rightAction = null; }
        if (_upAction != null) { hm.RemovePressedBinding(OdysseyInput.Up, _upAction); _upAction = null; }
        if (_downAction != null) { hm.RemovePressedBinding(OdysseyInput.Down, _downAction); _downAction = null; }
		if (_acceptAction != null) { hm.RemovePressedBinding(OdysseyInput.Accept, _acceptAction); _acceptAction = null; }
		if (_cancelAction != null) { hm.RemovePressedBinding(OdysseyInput.Cancel, _cancelAction); _cancelAction = null; }
		if (_infoScreenAction != null) { hm.RemovePressedBinding(OdysseyInput.InfoScreen, _infoScreenAction); _infoScreenAction = null; }
	}

    // ===== 键盘导航 — 业务方法 =====

    /// <summary>
    /// 方向键导航：在房间列表中按方向移动焦点。
    /// direction: -1 上/左, +1 下/右。
    /// </summary>
    private void NavigateRoomFocus(int direction)
    {
        if (SceneLifecycleGuard.ShouldSkip(this)) return;
        int count = _currentRoomChoices.Count;
        if (count <= 0) return;

        if (_focusedRoomIndex < 0)
        {
            _focusedRoomIndex = (direction > 0) ? 0 : count - 1;
        }
        else
        {
            _focusedRoomIndex += direction;
            if (_focusedRoomIndex >= count) _focusedRoomIndex = 0;
            if (_focusedRoomIndex < 0) _focusedRoomIndex = count - 1;
        }

        UpdateRoomFocus();
    }

    /// <summary>
    /// 刷新房间焦点高亮。仅当 HotkeyManager 记录到近期键盘活动时显示。
    /// </summary>
    private void UpdateRoomFocus()
    {
        var children = _choicesContainer.GetChildren();
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is Button btn)
            {
                bool shouldHighlight = (i == _focusedRoomIndex)
                    && HotkeyManager.Instance.LastKeyboardActivityMsec > 0;
                btn.SelfModulate = shouldHighlight
                    ? new Color(1.2f, 1.2f, 0.85f, 1)
                    : Colors.White;
            }
        }
    }

    /// <summary>
    /// Enter 键：确认选择当前键盘焦点的房间。
    /// </summary>
    private void AcceptFocusedRoom()
    {
        if (SceneLifecycleGuard.ShouldSkip(this)) return;
        if (_focusedRoomIndex < 0 || _focusedRoomIndex >= _currentRoomChoices.Count) return;

        var room = _currentRoomChoices[_focusedRoomIndex];
        HandleRoomSelected(room);
    }

    /// <summary>
    /// Escape 键：返回（放弃冒险）。
    /// </summary>
    private void HandleKeyboardCancel()
    {
        if (SceneLifecycleGuard.ShouldSkip(this)) return;
        OnQuitPressed();
    }

    /// <summary>
    /// HotkeyManager 键盘焦点超时事件：超时后清除房间焦点指示器。
    /// </summary>
    private void OnKeyboardFocusChanged(bool active)
    {
        if (!active)
        {
            _focusedRoomIndex = -1;
            ClearRoomFocus();
        }
    }

    /// <summary>
    /// 清除所有房间按钮的焦点高亮。
    /// </summary>
    private void ClearRoomFocus()
    {
        foreach (var child in _choicesContainer.GetChildren())
        {
            if (child is Button btn)
                btn.SelfModulate = Colors.White;
        }
    }

    // ===== 房间选择处理（逻辑保持不变） =====

    /// <summary>
    /// 玩家点击选择了一个房间。
    /// </summary>
    private void HandleRoomSelected(RoomDefinition room)
    {
        GD.Print($"[MapUI] 选择了房间：{room.DisplayName} ({room.Type})");

        _runState.SelectRoom(room);

        switch (room.Type)
        {
            case RoomType.Monster:
            case RoomType.Elite:
            case RoomType.Boss:
                EnterCombatRoom(room);
                break;

            case RoomType.Treasure:
                ShowTreasureRoom(room);
                break;

            case RoomType.Event:
            case RoomType.Shop:
            case RoomType.RestSite:
            default:
                ShowPlaceholderRoom(room);
                break;
        }
    }

    /// <summary>
    /// 进入战斗房间——切换到 Combat 场景。
    /// </summary>
    private void EnterCombatRoom(RoomDefinition room)
    {
        GD.Print($"[MapUI] 进入战斗房间：{room.DisplayName}");

        var gm = GameManager.Instance;
        if (gm != null)
        {
            var (savedHP, savedMaxHP) = gm.GetPlayerHealth();
            var player = gm.CurrentPlayer;
            if (player != null)
            {
                player.InitializeHealth(savedMaxHP, savedHP);
                GD.Print($"[MapUI] 已恢复玩家生命值：{savedHP}/{savedMaxHP}");
            }
        }

        GetTree().ChangeSceneToFile("res://Scenes/Combat.tscn");
    }

	/// <summary>
	/// 显示奖励房间——随机获得一个正面藏品。
	/// </summary>
	private void ShowTreasureRoom(RoomDefinition room)
	{
		GD.Print($"[MapUI] 进入奖励房间：{room.DisplayName}");

		// 可用正面藏品池
		var pool = new AbstractRelic[]
		{
			new GoodDreamPillowRelic(),
			new SmallFanRelic(),
		};

		var random = new Random();
		var relic = pool[random.Next(pool.Length)];

		GameManager.Instance?.Relics.AddRelic(relic);
		GD.Print($"[MapUI] 获得正面藏品：{relic.Name}（{relic.Id}）");

		// 直接用 MobileDialogHost 弹窗（不经 RewardPopup 中间层，避免嵌套 Control 尺寸问题）
		var (dialog, content, buttonRow) = MobileDialogHost.CreateDialog(
			this,
			$"{GetRoomIcon(room.Type)} {room.DisplayName}",
			width: 450);

		var nameLabel = new Label
		{
			Text = relic.Name,
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		nameLabel.AddThemeFontSizeOverride("font_size", 28);
		nameLabel.AddThemeColorOverride("font_color", new Color(1, 0.85f, 0.3f, 1));
		content.AddChild(nameLabel);

		var descLabel = new Label
		{
			Text = relic.Description,
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		descLabel.AddThemeFontSizeOverride("font_size", 16);
		descLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.9f, 1));
		content.AddChild(descLabel);

		var btn = MobileDialogHost.CreateDialogButton(
			Localization.Localization.T("ui.map.continue_button", "继续"));
		btn.Pressed += () =>
		{
			MobileDialogHost.CloseDialog(dialog, this);
			CompleteRoomAndAdvance();
		};
		buttonRow.AddChild(btn);
	}

    /// <summary>
    /// 显示占位符房间——使用 MobileDialogHost 弹窗。
    /// </summary>
    private void ShowPlaceholderRoom(RoomDefinition room)
    {
        GD.Print($"[MapUI] 进入占位符房间：{room.DisplayName} ({room.Type})");

        var (dialog, content, buttonRow) = MobileDialogHost.CreateDialog(
            this,
            $"{GetRoomIcon(room.Type)} {room.DisplayName}",
            width: 400);

        var label = new Label
        {
            Text = Localization.Localization.T("ui.map.placeholder_format", "[占位符] {name} 房间尚未实现。\n\n{desc}\n\n点击「继续」推进冒险。")
                .Replace("{name}", room.DisplayName)
                .Replace("{desc}", room.Description),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("font_size", 16);
        content.AddChild(label);

        var btn = MobileDialogHost.CreateDialogButton(
            Localization.Localization.T("ui.map.continue_button", "继续"));
        btn.Pressed += () =>
        {
            MobileDialogHost.CloseDialog(dialog, this);
            CompleteRoomAndAdvance();
        };
        buttonRow.AddChild(btn);
    }

    /// <summary>
    /// 完成当前房间并推进到下一层。
    /// </summary>
    private void CompleteRoomAndAdvance()
    {
        _runState.CompleteRoom();

        if (_runState.IsRunComplete && !_runState.IsRunFailed)
        {
            ShowRunComplete();
        }
        else
        {
            RefreshRoomChoices();
        }
    }

    /// <summary>
    /// 显示冒险完成画面——更新背景标签 + 弹出胜利对话框。
    /// </summary>
    private void ShowRunComplete()
    {
        // 清除所有选择卡片
        foreach (var token in _roomZoneTokens)
            token.Dispose();
        _roomZoneTokens.Clear();

        foreach (var child in _choicesContainer.GetChildren())
        {
            child.QueueFree();
        }

        _layerLabel.Text = "";
        _progressLabel.Text = Localization.Localization.T("ui.map.adventure_complete", "冒险完成！");
        _titleLabel.Text = Localization.Localization.T("ui.map.victory_title", "★ 胜利 ★");
        _quitButton.Visible = false;

        // 弹出胜利对话框
        var (dialog, content, buttonRow) = MobileDialogHost.CreateDialog(
            this,
            Localization.Localization.T("ui.map.victory_title", "★ 胜利 ★"),
            width: 400);

        var victoryLabel = new Label
        {
            Text = Localization.Localization.T("ui.map.victory_desc", "你击败了守护者！\n第一位面冒险完成！"),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        victoryLabel.AddThemeFontSizeOverride("font_size", 24);
        victoryLabel.AddThemeColorOverride("font_color", new Color(1, 0.85f, 0.3f, 1));
        content.AddChild(victoryLabel);

        var returnBtn = MobileDialogHost.CreateDialogButton(
            Localization.Localization.T("ui.combat.back_to_menu", "返回主菜单"));
        returnBtn.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/Main.tscn");
        buttonRow.AddChild(returnBtn);
    }

    /// <summary>
    /// 显示错误信息并返回主菜单——使用 MobileDialogHost 弹窗。
    /// </summary>
    private void ShowErrorAndQuit(string message)
    {
        GD.PrintErr($"[MapUI] 错误：{message}");

        var (dialog, content, buttonRow) = MobileDialogHost.CreateDialog(
            this,
            Localization.Localization.T("ui.map.error_title", "错误"),
            width: 400);

        var label = new Label
        {
            Text = Localization.Localization.T("ui.map.error_format", "发生错误：{message}")
                .Replace("{message}", message),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("font_size", 16);
        content.AddChild(label);

        var btn = MobileDialogHost.CreateDangerButton(
            Localization.Localization.T("ui.combat.back_to_menu", "返回主菜单"));
        btn.Pressed += () =>
        {
            MobileDialogHost.CloseDialog(dialog, this);
            GetTree().ChangeSceneToFile("res://Scenes/Main.tscn");
        };
        buttonRow.AddChild(btn);
    }

    // ===== 语言切换 =====

	// ===== 综合信息界面 =====

	private void ToggleInfoScreen()
	{
		if (_infoScreen != null)
			HideInfoScreen();
		else
			ShowInfoScreen();
	}

	private void ShowInfoScreen()
	{
		if (_infoScreen != null) return;

		GD.Print("[MapUI] 综合信息界面 — 显示");

		_infoScreen = new InfoScreen();
		_infoScreen.OnClosed += HideInfoScreen;
		AddChild(_infoScreen);
		_infoScreen.Open();
	}

	private void HideInfoScreen()
	{
		if (_infoScreen == null) return;

		_infoScreen.OnClosed -= HideInfoScreen;
		_infoScreen.QueueFree();
		_infoScreen = null;
	}

	private void OnLanguageChanged(string newLanguage)
    {
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        _titleLabel.Text = _runState?.CurrentPlane?.PlaneName
            ?? Localization.Localization.T("ui.map.title_fallback", "路线选择");
        _quitButton.Text = Localization.Localization.T("ui.map.abandon", "放弃冒险（返回主菜单）");

        if (_runState != null)
        {
            RefreshRoomChoices();
        }
    }

    // ===== 退出 =====

    private void OnQuitPressed()
    {
        GD.Print("[MapUI] 玩家放弃冒险");
        _runState?.Reset();
        GetTree().ChangeSceneToFile("res://Scenes/Main.tscn");
    }
}
