using Godot;
using OdysseyCards.Core;
using OdysseyCards.Roguelike;
using OdysseyCards.Localization;
using System;

namespace OdysseyCards.UI;

/// <summary>
/// 路线选择地图 UI。
/// 显示当前位面的房间选择列表，支持 1-2 个可选房间的按钮式分叉选择。
/// 程序化 UI——所有控件纯代码创建，不依赖 .tscn 模板。
/// </summary>
public partial class MapUI : Control
{
    // ===== UI 控件 =====

    private Label _titleLabel = null!;
    private Label _progressLabel = null!;
    private Label _layerLabel = null!;
    private VBoxContainer _choicesContainer = null!;
    private Button _quitButton = null!;

    // ===== 状态 =====

    private GameRunState _runState = null!;

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

    // ===== Godot 生命周期 =====

    public override void _Ready()
    {
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

    // ===== UI 构建 =====

    /// <summary>
    /// 构建所有 UI 控件。
    /// </summary>
    private void SetupUI()
    {
        // 背景
        var bg = new ColorRect
        {
            Name = "Background",
            Color = new Color(0.08f, 0.08f, 0.12f, 1),
            LayoutMode = 1,
            AnchorsPreset = (int)LayoutPreset.FullRect,
        };
        AddChild(bg);

        // 主容器
        var mainContainer = new VBoxContainer
        {
            Name = "MainContainer",
            LayoutMode = 1,
        };
        mainContainer.SetAnchorsPreset(LayoutPreset.Center);
        mainContainer.Position = new Vector2(-250, -300);
        mainContainer.Size = new Vector2(500, 600);
        AddChild(mainContainer);

        // 标题
        _titleLabel = new Label
        {
            Name = "TitleLabel",
            Text = _runState?.CurrentPlane?.PlaneName ?? Localization.Localization.T("ui.map.title_fallback", "路线选择"),
            HorizontalAlignment = HorizontalAlignment.Center,
            LayoutMode = 2,
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", 36);
        _titleLabel.AddThemeColorOverride("font_color", new Color(1, 0.9f, 0.5f, 1));
        mainContainer.AddChild(_titleLabel);

        // 间距
        mainContainer.AddChild(CreateSpacer(20));

        // 层进度
        _progressLabel = new Label
        {
            Name = "ProgressLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
            LayoutMode = 2,
        };
        _progressLabel.AddThemeFontSizeOverride("font_size", 20);
        _progressLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.8f, 1));
        mainContainer.AddChild(_progressLabel);

        // 间距
        mainContainer.AddChild(CreateSpacer(40));

        // 当前层标签
        _layerLabel = new Label
        {
            Name = "LayerLabel",
            Text = Localization.Localization.T("ui.map.select_room", "选择下一个房间："),
            HorizontalAlignment = HorizontalAlignment.Center,
            LayoutMode = 2,
        };
        _layerLabel.AddThemeFontSizeOverride("font_size", 22);
        _layerLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 1));
        mainContainer.AddChild(_layerLabel);

        mainContainer.AddChild(CreateSpacer(20));

        // 房间选择容器
        _choicesContainer = new VBoxContainer
        {
            Name = "ChoicesContainer",
            LayoutMode = 2,
        };
        _choicesContainer.AddThemeConstantOverride("separation", 16);
        mainContainer.AddChild(_choicesContainer);

        // 弹性间距
        var spacer = new Control { LayoutMode = 2, SizeFlagsVertical = Control.SizeFlags.Expand };
        mainContainer.AddChild(spacer);

        // 放弃冒险按钮
        _quitButton = new Button
        {
            Name = "QuitButton",
            Text = Localization.Localization.T("ui.map.abandon", "放弃冒险（返回主菜单）"),
            LayoutMode = 2,
        };
        _quitButton.AddThemeFontSizeOverride("font_size", 16);
        _quitButton.AddThemeColorOverride("font_color", new Color(0.8f, 0.3f, 0.3f, 1));
        _quitButton.Pressed += OnQuitPressed;
        mainContainer.AddChild(_quitButton);
    }

    /// <summary>
    /// 创建垂直间距控件。
    /// </summary>
    private static Control CreateSpacer(int height)
    {
        return new Control
        {
            LayoutMode = 2,
            CustomMinimumSize = new Vector2(0, height),
        };
    }

    // ===== 刷新房间选择 =====

    /// <summary>
    /// 根据 RunState 刷新当前层的可选房间按钮。
    /// 每层显示 1-2 个房间按钮，供玩家选择。
    /// </summary>
    private void RefreshRoomChoices()
    {
        // 清除旧按钮
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
        _progressLabel.Text = Localization.Localization.T("ui.map.progress_format", "进度：第 {current}/{total} 层").Replace("{current}", current.ToString()).Replace("{total}", total.ToString());

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
            _layerLabel.Text = Localization.Localization.T("ui.map.multi_choices", "选择下一个房间（{count} 个可选）：").Replace("{count}", choices.Count.ToString());
        }

        // 创建房间按钮
        foreach (var room in choices)
        {
            var btn = CreateRoomButton(room);
            _choicesContainer.AddChild(btn);
        }
    }

    /// <summary>
    /// 为指定房间创建选择按钮。
    /// </summary>
    private Button CreateRoomButton(RoomDefinition room)
    {
        var btn = new Button
        {
            LayoutMode = 2,
            CustomMinimumSize = new Vector2(400, 70),
            Text = $"{GetRoomIcon(room.Type)}  {room.DisplayName}\n{room.Description}",
        };
        btn.AddThemeFontSizeOverride("font_size", 18);
        btn.AddThemeColorOverride("font_color", new Color(1, 1, 1, 1));
        btn.AddThemeColorOverride("font_hover_color", new Color(1, 0.8f, 0.3f, 1));
        btn.Pressed += () => OnRoomSelected(room);
        return btn;
    }

    // ===== 房间选择处理 =====

    /// <summary>
    /// 玩家点击选择了一个房间。
    /// 根据房间类型决定后续操作：战斗房间进入 Combat 场景，非战斗房间显示占位符。
    /// </summary>
    private void OnRoomSelected(RoomDefinition room)
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

        // 从 GameManager 恢复跨战斗保存的生命值
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
    /// 显示占位符房间——简单提示对话框。
    /// </summary>
    private void ShowPlaceholderRoom(RoomDefinition room)
    {
        GD.Print($"[MapUI] 进入占位符房间：{room.DisplayName} ({room.Type})");

        var popup = new AcceptDialog
        {
            Title = $"{GetRoomIcon(room.Type)} {room.DisplayName}",
            OkButtonText = Localization.Localization.T("ui.map.continue_button", "继续"),
            Exclusive = true,
            Size = new Vector2I(400, 200),
        };

        var label = new Label
        {
            Text = Localization.Localization.T("ui.map.placeholder_format", "[占位符] {name} 房间尚未实现。\n\n{desc}\n\n点击「继续」推进冒险。").Replace("{name}", room.DisplayName).Replace("{desc}", room.Description),
            HorizontalAlignment = HorizontalAlignment.Center,
            LayoutMode = 2,
        };
        label.AddThemeFontSizeOverride("font_size", 16);
        popup.AddChild(label);

        popup.Confirmed += () =>
        {
            popup.QueueFree();
            CompleteRoomAndAdvance();
        };

        AddChild(popup);
        popup.PopupCentered();
    }

    /// <summary>
    /// 完成当前房间并推进到下一层。
    /// </summary>
    private void CompleteRoomAndAdvance()
    {
        _runState.CompleteRoom();

        if (_runState.IsRunComplete && !_runState.IsRunFailed)
        {
            // Boss 击败 → 显示胜利画面
            ShowRunComplete();
        }
        else
        {
            RefreshRoomChoices();
        }
    }

    /// <summary>
    /// 显示冒险完成画面。
    /// </summary>
    private void ShowRunComplete()
    {
        // 清除所有选择按钮
        foreach (var child in _choicesContainer.GetChildren())
        {
            child.QueueFree();
        }

        _layerLabel.Text = "";
        _progressLabel.Text = Localization.Localization.T("ui.map.adventure_complete", "冒险完成！");
        _titleLabel.Text = Localization.Localization.T("ui.map.victory_title", "★ 胜利 ★");

        var victoryLabel = new Label
        {
            Text = Localization.Localization.T("ui.map.victory_desc", "你击败了守护者！\n第一位面冒险完成！"),
            HorizontalAlignment = HorizontalAlignment.Center,
            LayoutMode = 2,
        };
        victoryLabel.AddThemeFontSizeOverride("font_size", 24);
        victoryLabel.AddThemeColorOverride("font_color", new Color(1, 0.85f, 0.3f, 1));
        _choicesContainer.AddChild(victoryLabel);

        _choicesContainer.AddChild(CreateSpacer(20));

        var returnBtn = new Button
        {
            Text = Localization.Localization.T("ui.combat.back_to_menu", "返回主菜单"),
            LayoutMode = 2,
            CustomMinimumSize = new Vector2(250, 50),
        };
        returnBtn.AddThemeFontSizeOverride("font_size", 20);
        returnBtn.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/Main.tscn");
        _choicesContainer.AddChild(returnBtn);

        _quitButton.Visible = false;
    }

    /// <summary>
    /// 显示错误信息并返回主菜单。
    /// </summary>
    private void ShowErrorAndQuit(string message)
    {
        GD.PrintErr($"[MapUI] 错误：{message}");

        var popup = new AcceptDialog
        {
            Title = Localization.Localization.T("ui.map.error_title", "错误"),
            OkButtonText = Localization.Localization.T("ui.combat.back_to_menu", "返回主菜单"),
            Exclusive = true,
            Size = new Vector2I(400, 150),
        };

        var label = new Label
        {
            Text = Localization.Localization.T("ui.map.error_format", "发生错误：{message}").Replace("{message}", message),
            HorizontalAlignment = HorizontalAlignment.Center,
            LayoutMode = 2,
        };
        popup.AddChild(label);

        popup.Confirmed += () => GetTree().ChangeSceneToFile("res://Scenes/Main.tscn");
        AddChild(popup);
        popup.PopupCentered();
    }

    // ===== 语言切换 =====

    private void OnLanguageChanged(string newLanguage)
    {
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        _titleLabel.Text = _runState?.CurrentPlane?.PlaneName ?? Localization.Localization.T("ui.map.title_fallback", "路线选择");
        _quitButton.Text = Localization.Localization.T("ui.map.abandon", "放弃冒险（返回主菜单）");

        if (_runState != null)
        {
            RefreshRoomChoices();
        }
    }

    public override void _ExitTree()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LanguageChanged -= OnLanguageChanged;
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
/// 程序化 UI，显示三张可选卡牌供玩家选择。
/// </summary>
internal partial class RewardPopup : Control
{
    /// <summary>
    /// 奖励选择完成回调。
    /// </summary>
    public event Action? OnRewardCompleted;

    private EventSelector _eventSelector = null!;
    private AcceptDialog _dialog = null!;
    private VBoxContainer _cardContainer = null!;
    private System.Collections.Generic.List<CardData> _choices = null!;

    public override void _Ready()
    {
        _eventSelector = new EventSelector();
    }

    /// <summary>
    /// 显示奖励选择界面。
    /// </summary>
    public void ShowRewards()
    {
        _choices = _eventSelector.GenerateRewardChoices(3);

        _dialog = new AcceptDialog
        {
            Title = Localization.Localization.T("ui.map.reward_title", "选择一张奖励卡牌"),
            Exclusive = true,
            Size = new Vector2I(600, 400),
        };
        _dialog.GetOkButton().Visible = false; // 隐藏默认确认按钮

        _cardContainer = new VBoxContainer
        {
            LayoutMode = 2,
        };
        _cardContainer.AddThemeConstantOverride("separation", 12);

        var headerLabel = new Label
        {
            Text = Localization.Localization.T("ui.map.reward_prompt", "选择一张卡牌加入你的牌堆："),
            HorizontalAlignment = HorizontalAlignment.Center,
            LayoutMode = 2,
        };
        headerLabel.AddThemeFontSizeOverride("font_size", 18);
        _cardContainer.AddChild(headerLabel);

        foreach (var card in _choices)
        {
            var btn = CreateCardButton(card);
            _cardContainer.AddChild(btn);
        }

        _dialog.AddChild(_cardContainer);
        AddChild(_dialog);
        _dialog.PopupCentered();
    }

    /// <summary>
    /// 为一张奖励卡牌创建选择按钮。
    /// </summary>
    private Button CreateCardButton(CardData card)
    {
        string cardType = card.Type == CardType.Minion ? Localization.Localization.T("ui.map.card_type_minion", "随从") : Localization.Localization.T("ui.map.card_type_spell", "法术");
        string keywords = card.Keywords?.Count > 0
            ? $" [{string.Join(", ", card.Keywords)}]"
            : "";

        string btnText = card.Type == CardType.Minion
            ? Localization.Localization.T("ui.map.reward_minion_format", "{name} [{type}] {atk}/{hp}{keywords}\n{desc}")
                .Replace("{name}", card.GetLocalizedName()).Replace("{type}", cardType).Replace("{atk}", card.Attack.ToString()).Replace("{hp}", card.Health.ToString()).Replace("{keywords}", keywords).Replace("{desc}", card.GetLocalizedDescription())
            : Localization.Localization.T("ui.map.reward_card_format", "{name} [{type}] 费用{cost}\n{desc}")
                .Replace("{name}", card.GetLocalizedName()).Replace("{type}", cardType).Replace("{cost}", card.Cost.ToString()).Replace("{desc}", card.GetLocalizedDescription());

        var btn = new Button
        {
            LayoutMode = 2,
            CustomMinimumSize = new Vector2(500, 60),
            Text = btnText,
        };
        btn.AddThemeFontSizeOverride("font_size", 16);
        btn.Pressed += () => OnCardSelected(card, btn);
        return btn;
    }

    /// <summary>
    /// 玩家选择了一张卡牌。
    /// </summary>
    private void OnCardSelected(CardData chosen, Button clickedBtn)
    {
        // 禁用所有按钮防止重复选择
        foreach (var child in _cardContainer.GetChildren())
        {
            if (child is Button b) b.Disabled = true;
        }

        // 应用奖励
        var player = GameManager.Instance?.CurrentPlayer;
        _eventSelector.ApplyReward(chosen, player!);

        // 同时通过 GameManager 添加到持久化牌堆
        GameManager.Instance?.AddCardToDeck(chosen);

        GD.Print($"[RewardPopup] 选择了奖励：{chosen.CardName}");

        // 延迟关闭弹窗
        var timer = new Timer { WaitTime = 1.0f, OneShot = true };
        timer.Timeout += () =>
        {
            _dialog.QueueFree();
            QueueFree();
            OnRewardCompleted?.Invoke();
        };
        AddChild(timer);
        timer.Start();
    }
}
