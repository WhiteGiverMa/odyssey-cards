using Godot;
using OdysseyCards.Character;
using OdysseyCards.Infrastructure;
using OdysseyCards.Localization;
using OdysseyCards.UI;

namespace OdysseyCards.Core;

public partial class MainMenu : Control
{
    private Button _startButton;
    private Button _collectionButton;
    private Button _settingsButton;
    private Label _titleLabel;
    private Control _mainMenuContainer;

    public override void _Ready()
    {
        Localization.Localization.Initialize();
        PlaceholderAssetGenerator.GenerateAllPlaceholders();

        _mainMenuContainer = GetNode<Control>("MainMenuContainer");
        _startButton = GetNode<Button>("MainMenuContainer/ButtonContainer/StartButton");
        _settingsButton = GetNode<Button>("MainMenuContainer/ButtonContainer/SettingsButton");
        _titleLabel = GetNode<Label>("MainMenuContainer/TitleLabel");

        // 在 VBoxContainer 中动态插入「我的收藏」按钮
        var btnContainer = GetNode<VBoxContainer>("MainMenuContainer/ButtonContainer");
        _collectionButton = new Button
        {
            Name = "CollectionButton",
            LayoutMode = 2,
        };
        _collectionButton.AddThemeColorOverride("font_color", new Color(1, 1, 1, 1));
        _collectionButton.AddThemeFontSizeOverride("font_size", 24);
        // 插入在 Settings 按钮之前
        int settingsIdx = _settingsButton.GetIndex();
        btnContainer.AddChild(_collectionButton);
        btnContainer.MoveChild(_collectionButton, settingsIdx);

        _startButton.Pressed += OnStartPressed;
        _collectionButton.Pressed += OnCollectionPressed;
        _settingsButton.Pressed += OnSettingsPressed;

        // 移动端触控兼容：确保鼠标过滤正确
        if (MobileInputHelper.IsMobile)
        {
            MouseFilter = MouseFilterEnum.Stop;
        }

        Localization.Localization.OnLanguageChanged += OnLanguageChanged;
        GameManager.Instance.LanguageChanged += OnLanguageChanged;
        UpdateLabels();
    }

    /// <summary>
    /// 移动端触控兼容：手动将触控事件路由到按钮。
    /// Godot 4.6 C# Android 上标准 Button 可能不响应触控，
    /// 因此在顶层 Control._GuiInput 中做命中检测并直接触发按钮回调。
    /// </summary>
    public override void _GuiInput(InputEvent @event)
    {
        if (!MobileInputHelper.IsMobile) return;

        if (@event is InputEventScreenTouch touch && touch.Pressed)
        {
            if (HitTestButton(_startButton, touch.Position))
                OnStartPressed();
            else if (HitTestButton(_collectionButton, touch.Position))
                OnCollectionPressed();
            else if (HitTestButton(_settingsButton, touch.Position))
                OnSettingsPressed();
        }
    }

    /// <summary>检测触控坐标是否在按钮矩形内。</summary>
    private static bool HitTestButton(Button button, Vector2 touchPos)
    {
        if (!button.IsInsideTree() || !button.Visible) return false;
        return button.GetGlobalRect().HasPoint(touchPos);
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
