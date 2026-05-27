using Godot;
using OdysseyCards.Character;
using OdysseyCards.Localization;
using OdysseyCards.UI;

namespace OdysseyCards.Core;

public partial class MainMenu : Control
{
    private Button _startButton;
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

        _startButton.Pressed += OnStartPressed;
        _settingsButton.Pressed += OnSettingsPressed;

        Localization.Localization.OnLanguageChanged += OnLanguageChanged;
        UpdateLabels();
    }

    private void OnStartPressed()
    {
        GD.Print("[MainMenu] OnStartPressed called");
        GD.Print($"[MainMenu] GameManager.Instance is null: {GameManager.Instance == null}");

        // 开始新的冒险运行（创建玩家 + 初始化 RunState + 生成位面）
        GameManager.Instance?.StartNewRun();
        GetTree().ChangeSceneToFile("res://Scenes/Map.tscn");
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
        _settingsButton.Text = Localization.Localization.T("ui.menu.settings", "Settings");
    }

    public override void _ExitTree()
    {
        Localization.Localization.OnLanguageChanged -= OnLanguageChanged;
    }
}
