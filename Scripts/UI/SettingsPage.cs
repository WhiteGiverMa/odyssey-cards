using Godot;
using OdysseyCards.Localization;

namespace OdysseyCards.UI;

public partial class SettingsPage : Control
{
    private OptionButton _languageOptionButton;
    private OptionButton _resolutionOptionButton;
    private OptionButton _windowModeOptionButton;
    private Button _backButton;
    private Label _titleLabel;
    private Label _languageLabel;
    private Label _resolutionLabel;
    private Label _windowModeLabel;

    public override void _Ready()
    {
        SetupUI();
        LoadLanguages();
        LoadResolutions();
        LoadWindowModes();
        ConnectSignals();
        UpdateCurrentLanguage();
    }

    private void SetupUI()
    {
        // === 标题 ===
        _titleLabel = new Label
        {
            Name = "TitleLabel",
            Text = Localization.Localization.T("ui.settings.title", "Settings"),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", 36);
        _titleLabel.CustomMinimumSize = new Vector2(0, 50);

        // === 语言行 ===
        _languageLabel = new Label
        {
            Name = "LanguageLabel",
            Text = Localization.Localization.T("ui.settings.language", "Language"),
        };
        _languageLabel.AddThemeFontSizeOverride("font_size", 20);
        _languageLabel.CustomMinimumSize = new Vector2(150, 0);

        _languageOptionButton = new OptionButton
        {
            Name = "LanguageOptionButton",
            CustomMinimumSize = new Vector2(200, 0),
        };

        var languageRow = new HBoxContainer
        {
            Name = "LanguageRow",
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        languageRow.AddThemeConstantOverride("separation", 20);
        languageRow.AddChild(_languageLabel);
        languageRow.AddChild(_languageOptionButton);

        // === 分辨率行 ===
        _resolutionLabel = new Label
        {
            Name = "ResolutionLabel",
            Text = Localization.Localization.T("ui.settings.resolution", "Resolution"),
        };
        _resolutionLabel.AddThemeFontSizeOverride("font_size", 20);
        _resolutionLabel.CustomMinimumSize = new Vector2(150, 0);

        _resolutionOptionButton = new OptionButton
        {
            Name = "ResolutionOptionButton",
            CustomMinimumSize = new Vector2(200, 0),
        };

        var resolutionRow = new HBoxContainer
        {
            Name = "ResolutionRow",
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        resolutionRow.AddThemeConstantOverride("separation", 20);
        resolutionRow.AddChild(_resolutionLabel);
        resolutionRow.AddChild(_resolutionOptionButton);

        // === 窗口模式行 ===
        _windowModeLabel = new Label
        {
            Name = "WindowModeLabel",
            Text = Localization.Localization.T("ui.settings.window_mode", "Window Mode"),
        };
        _windowModeLabel.AddThemeFontSizeOverride("font_size", 20);
        _windowModeLabel.CustomMinimumSize = new Vector2(150, 0);

        _windowModeOptionButton = new OptionButton
        {
            Name = "WindowModeOptionButton",
            CustomMinimumSize = new Vector2(200, 0),
        };

        var windowModeRow = new HBoxContainer
        {
            Name = "WindowModeRow",
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        windowModeRow.AddThemeConstantOverride("separation", 20);
        windowModeRow.AddChild(_windowModeLabel);
        windowModeRow.AddChild(_windowModeOptionButton);

        // === 返回按钮 ===
        _backButton = new Button
        {
            Name = "BackButton",
            Text = Localization.Localization.T("ui.settings.back", "Back"),
            CustomMinimumSize = new Vector2(140, 44),
        };
        _backButton.AddThemeFontSizeOverride("font_size", 18);

        // === 根容器：填满全屏 + 垂直居中 + 水平居中 ===
        var container = new VBoxContainer
        {
            Name = "SettingsContainer",
            AnchorLeft = 0,
            AnchorTop = 0,
            AnchorRight = 1,
            AnchorBottom = 1,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        container.AddThemeConstantOverride("separation", 24);

        container.AddChild(_titleLabel);
        container.AddChild(languageRow);
        container.AddChild(resolutionRow);
        container.AddChild(windowModeRow);
        container.AddChild(_backButton);

        AddChild(container);
    }

    private void LoadLanguages()
    {
        _languageOptionButton.Clear();

        var languages = Localization.Localization.AvailableLanguages;
        var languageNames = new System.Collections.Generic.Dictionary<string, string>
        {
            { "en", "English" },
            { "zh", "中文" }
        };

        int selectedIndex = 0;

        for (int i = 0; i < languages.Count; i++)
        {
            string lang = languages[i];
            string displayName = languageNames.TryGetValue(lang, out string name) ? name : lang;
            _languageOptionButton.AddItem(displayName);
            _languageOptionButton.SetItemMetadata(i, lang);

            if (lang == Localization.Localization.CurrentLanguage)
            {
                selectedIndex = i;
            }
        }

        _languageOptionButton.Selected = selectedIndex;
    }

    private void LoadResolutions()
    {
        _resolutionOptionButton.Clear();

        var resolutions = UIScaler.Instance.GetSupportedResolutions();
        int currentFilteredIndex = UIScaler.Instance.GetCurrentResolutionFilteredIndex();

        for (int i = 0; i < resolutions.Count; i++)
        {
            _resolutionOptionButton.AddItem(resolutions[i].Label);
            _resolutionOptionButton.SetItemMetadata(i, $"{resolutions[i].Width},{resolutions[i].Height}");
        }

        _resolutionOptionButton.Selected = Mathf.Clamp(currentFilteredIndex, 0, resolutions.Count - 1);
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

    private void ConnectSignals()
    {
        _languageOptionButton.ItemSelected += OnLanguageSelected;
        _resolutionOptionButton.ItemSelected += OnResolutionSelected;
        _windowModeOptionButton.ItemSelected += OnWindowModeSelected;
        _backButton.Pressed += OnBackPressed;
        Core.GameManager.Instance.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageSelected(long index)
    {
        var langVariant = _languageOptionButton.GetItemMetadata((int)index);
        string lang = langVariant.AsString();
        if (!string.IsNullOrEmpty(lang))
        {
            Core.GameManager.Instance.SetLanguage(lang);
        }
    }

    private void OnResolutionSelected(long index)
    {
        var meta = _resolutionOptionButton.GetItemMetadata((int)index).AsString();
        var parts = meta.Split(',');
        if (parts.Length == 2 && int.TryParse(parts[0], out int width) && int.TryParse(parts[1], out int height))
        {
            // 仅在窗口模式下切换分辨率有效
            if (UIScaler.Instance.GetCurrentWindowModeIndex() == 0)
            {
                UIScaler.Instance.SetWindowResolution(width, height);
            }
        }
    }

    private void OnWindowModeSelected(long index)
    {
        var modeVariant = _windowModeOptionButton.GetItemMetadata((int)index);
        int modeIndex = modeVariant.AsInt32();
        UIScaler.Instance.SetWindowModeIndex(modeIndex);

        // 切换模式后刷新分辨率选项的选中状态
        LoadResolutions();
    }

    private void OnLanguageChanged(string newLanguage)
    {
        UpdateLabels();
        LoadWindowModes();
    }

    private void UpdateCurrentLanguage()
    {
        for (int i = 0; i < _languageOptionButton.ItemCount; i++)
        {
            var langVariant = _languageOptionButton.GetItemMetadata(i);
            string lang = langVariant.AsString();
            if (lang == Localization.Localization.CurrentLanguage)
            {
                _languageOptionButton.Selected = i;
                break;
            }
        }
    }

    private void UpdateLabels()
    {
        _titleLabel.Text = Localization.Localization.T("ui.settings.title", "Settings");
        _languageLabel.Text = Localization.Localization.T("ui.settings.language", "Language");
        _resolutionLabel.Text = Localization.Localization.T("ui.settings.resolution", "Resolution");
        _windowModeLabel.Text = Localization.Localization.T("ui.settings.window_mode", "Window Mode");
        _backButton.Text = Localization.Localization.T("ui.settings.back", "Back");
    }

    private void OnBackPressed()
    {
        var parent = GetParent();
        if (parent is Core.MainMenu mainMenu)
        {
            mainMenu.ShowMainMenu();
        }

        QueueFree();
    }

    public override void _ExitTree()
    {
        Core.GameManager.Instance.LanguageChanged -= OnLanguageChanged;

        _languageOptionButton.ItemSelected -= OnLanguageSelected;
        _resolutionOptionButton.ItemSelected -= OnResolutionSelected;
        _windowModeOptionButton.ItemSelected -= OnWindowModeSelected;
        _backButton.Pressed -= OnBackPressed;
    }
}
