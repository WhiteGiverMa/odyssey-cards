using Godot;
using OdysseyCards.Character;
using OdysseyCards.Infrastructure;
using OdysseyCards.Localization;
using OdysseyCards.UI;

namespace OdysseyCards.Core;

public partial class MainMenu : Control
{
    private const string TouchDebugPrefix = "[TOUCHDBG-mainmenu]";
    private const float MobileTouchHitPadding = 24f;

    private Button _startButton;
    private Button _collectionButton;
    private Button _settingsButton;
    private Label _titleLabel;
    private Control _mainMenuContainer;
    private VBoxContainer _buttonContainer;
    private Label? _touchDebugLabel;
    private readonly System.Collections.Generic.Queue<string> _touchDebugLines = new();

    public override void _Ready()
    {
        Localization.Localization.Initialize();

        _mainMenuContainer = GetNode<Control>("MainMenuContainer");
        _startButton = GetNode<Button>("MainMenuContainer/ButtonContainer/StartButton");
        _settingsButton = GetNode<Button>("MainMenuContainer/ButtonContainer/SettingsButton");
        _titleLabel = GetNode<Label>("MainMenuContainer/TitleLabel");
        _buttonContainer = GetNode<VBoxContainer>("MainMenuContainer/ButtonContainer");

        // 占位资源只在编辑器开发期生成。放在按钮引用初始化之后，避免异常导致主菜单失活。
        try
        {
            PlaceholderAssetGenerator.GenerateAllPlaceholders();
        }
        catch (System.Exception ex)
        {
            GD.PushWarning($"[MainMenu] PlaceholderAssetGenerator skipped: {ex.Message}");
        }

        // 在 VBoxContainer 中动态插入「我的收藏」按钮
        _collectionButton = new Button
        {
            Name = "CollectionButton",
            LayoutMode = 2,
        };
        _collectionButton.AddThemeColorOverride("font_color", new Color(1, 1, 1, 1));
        _collectionButton.AddThemeFontSizeOverride("font_size", 24);
        // 插入在 Settings 按钮之前
        int settingsIdx = _settingsButton.GetIndex();
        _buttonContainer.AddChild(_collectionButton);
        _buttonContainer.MoveChild(_collectionButton, settingsIdx);

        _startButton.Pressed += OnStartPressed;
        _collectionButton.Pressed += OnCollectionPressed;
        _settingsButton.Pressed += OnSettingsPressed;

        // 移动端触控兼容：确保顶层控件能收到输入，并创建屏幕调试标签。
        if (MobileInputHelper.IsMobile)
        {
            MouseFilter = MouseFilterEnum.Stop;
            ApplyMobileButtonLayout();
            CreateTouchDebugLabel();
            CallDeferred(nameof(LogButtonRects));
        }

        Localization.Localization.OnLanguageChanged += OnLanguageChanged;
        GameManager.Instance.LanguageChanged += OnLanguageChanged;
        UpdateLabels();
    }

    /// <summary>
    /// 移动端触控兼容：在顶层 _Input 中直接捕获触控并做 hit-test。
    /// 这样即使 Godot 4.6 Android 上标准 Button 不响应触控，也能确认事件链是否到达根节点。
    /// </summary>
    public override void _Input(InputEvent @event)
    {
        if (!MobileInputHelper.IsMobile) return;

        if (_startButton == null || _collectionButton == null || _settingsButton == null
            || !GodotObject.IsInstanceValid(_startButton)
            || !GodotObject.IsInstanceValid(_collectionButton)
            || !GodotObject.IsInstanceValid(_settingsButton))
        {
            LogTouchDebug("buttons not ready yet");
            return;
        }

        if (@event is InputEventScreenTouch touch && touch.Pressed)
        {
            LogTouchDebug($"touch down pos={touch.Position} start={MobileInputHelper.TouchStartPosition}");

            if (HitTestButton(_startButton, touch.Position))
            {
                LogTouchDebug("hit StartButton -> OnStartPressed");
                OnStartPressed();
            }
            else if (HitTestButton(_collectionButton, touch.Position))
            {
                LogTouchDebug("hit CollectionButton -> OnCollectionPressed");
                OnCollectionPressed();
            }
            else if (HitTestButton(_settingsButton, touch.Position))
            {
                LogTouchDebug("hit SettingsButton -> OnSettingsPressed");
                OnSettingsPressed();
            }
            else
            {
                LogTouchDebug("touch miss all buttons");
            }

            GetViewport().SetInputAsHandled();
        }

        if (@event is InputEventMouseButton mouseBtn && mouseBtn.Pressed)
        {
            LogTouchDebug($"mouse btn={mouseBtn.ButtonIndex} pos={mouseBtn.Position}");
        }
    }

    /// <summary>检测触控坐标是否在按钮矩形内。</summary>
    private static bool HitTestButton(Button button, Vector2 touchPos)
    {
        if (button == null) return false;
        if (!GodotObject.IsInstanceValid(button)) return false;
        if (!button.IsInsideTree() || !button.Visible) return false;

        Rect2 rect = button.GetGlobalRect();
        if (MobileInputHelper.IsMobile)
        {
            rect = rect.GrowIndividual(MobileTouchHitPadding, MobileTouchHitPadding, MobileTouchHitPadding, MobileTouchHitPadding);
        }

        return rect.HasPoint(touchPos);
    }

    private void ApplyMobileButtonLayout()
    {
        // 移动端增大主菜单按钮的视觉尺寸与触控目标，减少手指遮挡导致的 consistently miss。
        _buttonContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        _buttonContainer.OffsetLeft = -180;
        _buttonContainer.OffsetTop = -110;
        _buttonContainer.OffsetRight = 180;
        _buttonContainer.OffsetBottom = 110;
        _buttonContainer.AddThemeConstantOverride("separation", 28);

        ConfigureMobileMenuButton(_startButton);
        ConfigureMobileMenuButton(_collectionButton);
        ConfigureMobileMenuButton(_settingsButton);
    }

    private static void ConfigureMobileMenuButton(Button button)
    {
        button.CustomMinimumSize = new Vector2(360, 64);
        button.AddThemeFontSizeOverride("font_size", 28);
    }

    private void CreateTouchDebugLabel()
    {
        _touchDebugLabel = new Label
        {
            Name = "TouchDebugLabel",
            Visible = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _touchDebugLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        _touchDebugLabel.Position = new Vector2(12, 12);
        _touchDebugLabel.Size = new Vector2(900, 220);
        _touchDebugLabel.AddThemeFontSizeOverride("font_size", 16);
        _touchDebugLabel.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.6f));
        AddChild(_touchDebugLabel);
        LogTouchDebug("mobile touch debug ready");
    }

    private void LogButtonRects()
    {
        if (!MobileInputHelper.IsMobile)
            return;

        LogTouchDebug($"start rect={_startButton.GetGlobalRect()}");
        LogTouchDebug($"collection rect={_collectionButton.GetGlobalRect()}");
        LogTouchDebug($"settings rect={_settingsButton.GetGlobalRect()}");
    }

    private void LogTouchDebug(string message)
    {
        string line = $"{TouchDebugPrefix} {message}";
        GD.Print(line);

        if (_touchDebugLabel == null)
            return;

        while (_touchDebugLines.Count >= 7)
            _touchDebugLines.Dequeue();

        _touchDebugLines.Enqueue(message);
        _touchDebugLabel.Text = string.Join("\n", _touchDebugLines);
    }

    private void OnStartPressed()
    {
        GD.Print("[MainMenu] OnStartPressed called");

        // 检查牌组是否满足要求
        var gm = GameManager.Instance;
        var deck = gm?.ActiveDeck;

        if (deck == null)
        {
            ShowDeckNotReadyDialog(
                Localization.Localization.T("ui.menu.deck_too_few_desc",
                    "当前牌组不满足最小卡牌数要求（至少 10 张）。\n请先前往收藏界面构筑牌组。"));
            return;
        }

        if (!deck.MeetsMinimum())
        {
            ShowDeckNotReadyDialog(
                Localization.Localization.T("ui.menu.deck_too_few_desc",
                    "当前牌组不满足最小卡牌数要求（至少 10 张）。\n请先前往收藏界面构筑牌组。"));
            return;
        }

        if (deck.IsOverLimit())
        {
            ShowDeckNotReadyDialog(
                Localization.Localization.T("ui.menu.deck_too_many_desc",
                    "当前牌组超过 20 张上限。\n请先前往收藏界面调整牌组。"));
            return;
        }

        // 开始新的冒险运行（创建玩家 + 初始化 RunState + 生成位面）
        gm?.StartNewRun();
        GetTree().ChangeSceneToFile("res://Scenes/Map.tscn");
    }

    private void ShowDeckNotReadyDialog(string description)
    {
        var dialog = new AcceptDialog
        {
            Title = Localization.Localization.T("ui.menu.deck_not_ready_title", "牌组未就绪"),
            OkButtonText = Localization.Localization.T("ui.menu.go_to_collection", "前往收藏"),
            Exclusive = true,
            Size = new Vector2I(400, 180),
        };
        dialog.GetOkButton().Text = Localization.Localization.T("ui.menu.go_to_collection", "前往收藏");

        var label = new Label
        {
            Text = description,
            HorizontalAlignment = HorizontalAlignment.Center,
            LayoutMode = 2,
        };
        label.AddThemeFontSizeOverride("font_size", 14);
        dialog.AddChild(label);

        dialog.Confirmed += () =>
        {
            GetTree().ChangeSceneToFile("res://Scenes/Collection.tscn");
        };

        AddChild(dialog);
        dialog.PopupCentered();
    }

    private void OnCollectionPressed()
    {
        GD.Print("[MainMenu] OnCollectionPressed → 收藏界面");
        GetTree().ChangeSceneToFile("res://Scenes/Collection.tscn");
    }

    private void OnSettingsPressed()
    {
        SettingsPage settingsPage = new();
        settingsPage.Name = "SettingsPage";
        settingsPage.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(settingsPage);

        _mainMenuContainer.Visible = false;
    }

    public void ShowMainMenu()
    {
        _mainMenuContainer.Visible = true;
    }

    private void OnLanguageChanged(string newLanguage)
    {
        UpdateLabels();
    }

    private void UpdateLabels()
    {
        _titleLabel.Text = Localization.Localization.T("ui.menu.title", "Odyssey Cards");
        _startButton.Text = Localization.Localization.T("ui.menu.start_game", "Start Game");
        _collectionButton.Text = Localization.Localization.T("ui.menu.collection", "我的收藏");
        _settingsButton.Text = Localization.Localization.T("ui.menu.settings", "Settings");
    }

    public override void _ExitTree()
    {
        Localization.Localization.OnLanguageChanged -= OnLanguageChanged;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LanguageChanged -= OnLanguageChanged;
        }
    }
}
