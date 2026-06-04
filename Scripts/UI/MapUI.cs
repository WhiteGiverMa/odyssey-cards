using Godot;
using OdysseyCards.Core;
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
                ShowErrorAndQuit(Localization.Localization.T("ui.map.init_error", "无法初始化冒险数据"));
                return;
            }
        }

        SetupUI();
        RefreshRoomChoices();

        GameManager.Instance.LanguageChanged += OnLanguageChanged;
    }

    public override void _Input(InputEvent @event)
    {
        if (SceneLifecycleGuard.ShouldSkip(this)) return;
        // 所有交互通过 TapZone（移动端）或 Button.Pressed（桌面端）处理
    }

    public override void _ExitTree()
    {
        SceneLifecycleGuard.OnExitTree(this);

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
        var root = new VBoxContainer
        {
            Name = "RootContainer",
        };
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
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
                    onTap: () => CallDeferred(nameof(HandleRoomSelectedDeferred), Variant.From(room)));
                _roomZoneTokens.Add(token);
            }
        }
        else
        {
            card.Pressed += () => HandleRoomSelected(room);
        }
    }

    /// <summary>
    /// Deferred 版房间选择（供 TapZone 回调使用，避免在触摸栈内执行场景切换）。
    /// </summary>
    private void HandleRoomSelectedDeferred(RoomDefinition room)
    {
        if (SceneLifecycleGuard.ShouldSkip(this)) return;
        HandleRoomSelected(room);
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
    /// 显示奖励房间——使用 EventSelector 提供 3 选 1 卡牌奖励。
    /// </summary>
    private void ShowTreasureRoom(RoomDefinition room)
    {
        GD.Print($"[MapUI] 进入奖励房间：{room.DisplayName}");

        var rewardUI = new RewardPopup();
        rewardUI.OnRewardCompleted += () =>
        {
            CompleteRoomAndAdvance();
        };
        AddChild(rewardUI);
        rewardUI.ShowRewards();
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

/// <summary>
/// 奖励选择弹窗——战后 3 选 1 卡牌奖励。
/// 使用 MobileDialogHost 统一弹窗样式，替换原来的 AcceptDialog。
/// </summary>
internal partial class RewardPopup : Control
{
    /// <summary>
    /// 奖励选择完成回调。
    /// </summary>
    public event Action? OnRewardCompleted;

    private EventSelector _eventSelector = null!;
    private Control _dialog = null!;
    private VBoxContainer _content = null!;
    private HBoxContainer _buttonRow = null!;
    private List<(CardData Card, int CopyCount)> _choices = null!;

    public override void _Ready()
    {
        _eventSelector = new EventSelector();
    }

    /// <summary>
    /// 显示奖励选择界面（MobileDialogHost 弹窗）。
    /// </summary>
    public void ShowRewards()
    {
        _choices = _eventSelector.GenerateRewardBundles(3);

        (_dialog, _content, _buttonRow) = MobileDialogHost.CreateDialog(
            this,
            Localization.Localization.T("ui.map.reward_title", "选择一张奖励卡牌"),
            width: 600);

        var headerLabel = new Label
        {
            Text = Localization.Localization.T("ui.map.reward_prompt", "选择一张卡牌加入你的牌堆："),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        headerLabel.AddThemeFontSizeOverride("font_size", 18);
        _content.AddChild(headerLabel);

        foreach (var (card, _) in _choices)
        {
            _content.AddChild(CreateCardButton(card));
        }
    }

    /// <summary>
    /// 为一张奖励卡牌创建选择按钮（移动端触控高度 60px）。
    /// </summary>
    private Button CreateCardButton(CardData card)
    {
        string cardType = card.Type == CardType.Minion
            ? Localization.Localization.T("ui.map.card_type_minion", "随从")
            : Localization.Localization.T("ui.map.card_type_spell", "法术");
        string keywords = card.Keywords?.Count > 0
            ? $" [{string.Join(", ", card.Keywords)}]"
            : "";

        string btnText = card.Type == CardType.Minion
            ? Localization.Localization.T("ui.map.reward_minion_format", "{name} [{type}] {atk}/{hp}{keywords}\n{desc}")
                .Replace("{name}", card.GetLocalizedName())
                .Replace("{type}", cardType)
                .Replace("{atk}", card.Attack.ToString())
                .Replace("{hp}", card.Health.ToString())
                .Replace("{keywords}", keywords)
                .Replace("{desc}", card.GetLocalizedDescription())
            : Localization.Localization.T("ui.map.reward_card_format", "{name} [{type}] 费用{cost}\n{desc}")
                .Replace("{name}", card.GetLocalizedName())
                .Replace("{type}", cardType)
                .Replace("{cost}", card.Cost.ToString())
                .Replace("{desc}", card.GetLocalizedDescription());

        var btn = MobileDialogHost.CreateDialogButton(btnText, minHeight: 60);
        btn.Pressed += () => OnCardSelected(card, btn);
        return btn;
    }

    /// <summary>
    /// 玩家选择了一张卡牌。
    /// </summary>
    private void OnCardSelected(CardData chosen, Button clickedBtn)
    {
        // 禁用所有按钮防止重复选择
        foreach (var child in _content.GetChildren())
        {
            if (child is Button b) b.Disabled = true;
        }

        GameManager.Instance?.AddCardToDeckInCombat(chosen);
        GameManager.Instance?.SaveToDisk();

        GD.Print($"[RewardPopup] 选择了奖励：{chosen.CardName}");

        // 延迟关闭弹窗
        var timer = new Timer { WaitTime = 1.0f, OneShot = true };
        timer.Timeout += () =>
        {
            MobileDialogHost.CloseDialog(_dialog, this);
            QueueFree();
            OnRewardCompleted?.Invoke();
        };
        AddChild(timer);
        timer.Start();
    }
}
