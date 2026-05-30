using Godot;
using OdysseyCards.Character;
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

        Localization.Localization.OnLanguageChanged += OnLanguageChanged;
        GameManager.Instance.LanguageChanged += OnLanguageChanged;
        UpdateLabels();
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
