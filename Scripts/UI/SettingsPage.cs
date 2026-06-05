using Godot;
using OdysseyCards.Core;
using OdysseyCards.Infrastructure;
using OdysseyCards.Localization;

namespace OdysseyCards.UI;

public partial class SettingsPage : Control
{
    private const float MobileTouchHitPadding = 20f;

    private OptionButton _languageOptionButton;
    private OptionButton _resolutionOptionButton;
    private OptionButton _windowModeOptionButton;
    private Button _backButton;
    private Label _titleLabel;
    private Label _languageLabel;
    private Label _resolutionLabel;
    private Label _windowModeLabel;
    private HBoxContainer _resolutionRow;
    private HBoxContainer _windowModeRow;
    private CheckBox _devModeToggle;
    private Button _consoleButton;
    private Label _emoteIdleTimeLabel;
    private HSlider _emoteIdleTimeSlider;
    private Label _emoteIdleTimeValueLabel;
    private Label _emoteVarMinLabel;
    private HSlider _emoteVarMinSlider;
    private Label _emoteVarMinValueLabel;
    private Label _emoteVarMaxLabel;
    private HSlider _emoteVarMaxSlider;
    private Label _emoteVarMaxValueLabel;

    public override void _Ready()
    {
        SetupUI();
        LoadLanguages();
        LoadResolutions();
        LoadWindowModes();
        ConnectSignals();
        UpdateCurrentLanguage();

        if (MobileInputHelper.IsMobile)
        {
            MouseFilter = MouseFilterEnum.Stop;
            ApplyMobileLayout();
        }
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
        _resolutionRow = resolutionRow;

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
        _windowModeRow = windowModeRow;

        // === 开发者模式行 ===
        _devModeToggle = new CheckBox
        {
            Name = "DevModeToggle",
            Text = Localization.Localization.T("ui.settings.dev_mode", "开发者模式"),
            ButtonPressed = DevConsole.IsDevMode,
        };
        _devModeToggle.AddThemeFontSizeOverride("font_size", 20);

        _consoleButton = new Button
        {
            Name = "ConsoleButton",
            Text = Localization.Localization.T("ui.settings.open_console", "打开控制台"),
            CustomMinimumSize = new Vector2(0, 44),
            Visible = DevConsole.IsDevMode,
        };
        _consoleButton.AddThemeFontSizeOverride("font_size", 16);

        // === 表情空闲时间行 ===
        var gm = GameManager.Instance;
        float currentIdleTime = gm?.EmoteIdleTimeSeconds ?? 5.0f;

        _emoteIdleTimeLabel = new Label
        {
            Name = "EmoteIdleTimeLabel",
            Text = Localization.Localization.T("ui.settings.emote_idle_time", "表情空闲时间"),
        };
        _emoteIdleTimeLabel.AddThemeFontSizeOverride("font_size", 20);
        _emoteIdleTimeLabel.CustomMinimumSize = new Vector2(150, 0);

        _emoteIdleTimeSlider = new HSlider
        {
            Name = "EmoteIdleTimeSlider",
            MinValue = 3.0,
            MaxValue = 15.0,
            Step = 0.5,
            Value = currentIdleTime,
            CustomMinimumSize = new Vector2(160, 0),
        };

        _emoteIdleTimeValueLabel = new Label
        {
            Name = "EmoteIdleTimeValueLabel",
            Text = $"{currentIdleTime:F1}s",
            CustomMinimumSize = new Vector2(50, 0),
        };
        _emoteIdleTimeValueLabel.AddThemeFontSizeOverride("font_size", 16);

        var emoteIdleRow = new HBoxContainer
        {
            Name = "EmoteIdleRow",
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        emoteIdleRow.AddThemeConstantOverride("separation", 12);
        emoteIdleRow.AddChild(_emoteIdleTimeLabel);
        emoteIdleRow.AddChild(_emoteIdleTimeSlider);
        emoteIdleRow.AddChild(_emoteIdleTimeValueLabel);

        // === 随机最小倍率行 ===
        float currentVarMin = gm?.EmoteIdleVariationMin ?? 0.7f;

        _emoteVarMinLabel = new Label
        {
            Text = Localization.Localization.T("ui.settings.emote_variation_min", "随机最小倍率"),
        };
        _emoteVarMinLabel.AddThemeFontSizeOverride("font_size", 20);
        _emoteVarMinLabel.CustomMinimumSize = new Vector2(150, 0);

        _emoteVarMinSlider = new HSlider
        {
            MinValue = 0.1,
            MaxValue = 3.0,
            Step = 0.1,
            Value = currentVarMin,
            CustomMinimumSize = new Vector2(160, 0),
        };

        _emoteVarMinValueLabel = new Label
        {
            Text = $"×{currentVarMin:F1}",
            CustomMinimumSize = new Vector2(50, 0),
        };
        _emoteVarMinValueLabel.AddThemeFontSizeOverride("font_size", 16);

        var varMinRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        varMinRow.AddThemeConstantOverride("separation", 12);
        varMinRow.AddChild(_emoteVarMinLabel);
        varMinRow.AddChild(_emoteVarMinSlider);
        varMinRow.AddChild(_emoteVarMinValueLabel);

        // === 随机最大倍率行 ===
        float currentVarMax = gm?.EmoteIdleVariationMax ?? 1.3f;

        _emoteVarMaxLabel = new Label
        {
            Text = Localization.Localization.T("ui.settings.emote_variation_max", "随机最大倍率"),
        };
        _emoteVarMaxLabel.AddThemeFontSizeOverride("font_size", 20);
        _emoteVarMaxLabel.CustomMinimumSize = new Vector2(150, 0);

        _emoteVarMaxSlider = new HSlider
        {
            MinValue = 0.1,
            MaxValue = 3.0,
            Step = 0.1,
            Value = currentVarMax,
            CustomMinimumSize = new Vector2(160, 0),
        };

        _emoteVarMaxValueLabel = new Label
        {
            Text = $"×{currentVarMax:F1}",
            CustomMinimumSize = new Vector2(50, 0),
        };
        _emoteVarMaxValueLabel.AddThemeFontSizeOverride("font_size", 16);

        var varMaxRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        varMaxRow.AddThemeConstantOverride("separation", 12);
        varMaxRow.AddChild(_emoteVarMaxLabel);
        varMaxRow.AddChild(_emoteVarMaxSlider);
        varMaxRow.AddChild(_emoteVarMaxValueLabel);

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
        container.AddChild(_devModeToggle);
        container.AddChild(_consoleButton);
        container.AddChild(emoteIdleRow);
        container.AddChild(varMinRow);
        container.AddChild(varMaxRow);
        container.AddChild(_backButton);

        AddChild(container);
    }

    public override void _Input(InputEvent @event)
    {
        if (SceneLifecycleGuard.ShouldSkip(this)) return;
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

        Rect2 rect = control.GetGlobalRect().Grow(MobileTouchHitPadding);
        return rect.HasPoint(touchPos);
    }

    private void ApplyMobileLayout()
    {
        _languageOptionButton.CustomMinimumSize = new Vector2(260, 56);
        _resolutionOptionButton.CustomMinimumSize = new Vector2(260, 56);
        _windowModeOptionButton.CustomMinimumSize = new Vector2(260, 56);
        _backButton.CustomMinimumSize = new Vector2(220, 56);
        _backButton.AddThemeFontSizeOverride("font_size", 22);
        _consoleButton.CustomMinimumSize = new Vector2(220, 56);

        // 移动端隐藏无意义的桌面分辨率/窗口模式选项
        _resolutionRow.Visible = false;
        _windowModeRow.Visible = false;
    }

    private static void CycleOptionButton(OptionButton optionButton, System.Action<long> onSelected)
    {
        if (optionButton.ItemCount <= 0)
            return;

        int nextIndex = (optionButton.Selected + 1) % optionButton.ItemCount;
        optionButton.Selected = nextIndex;
        onSelected(nextIndex);
    }

    private void LoadLanguages()
    {
        _languageOptionButton.Clear();

        var languages = Localization.Localization.AvailableLanguages;
        var languageNames = new System.Collections.Generic.Dictionary<string, string>
        {
            { "en", Localization.Localization.T("language.name_en", "English") },
            { "zh", Localization.Localization.T("language.name_zh", "中文") }
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
        _devModeToggle.Toggled += OnDevModeToggled;
        _consoleButton.Pressed += OnConsolePressed;
        _emoteIdleTimeSlider.ValueChanged += OnEmoteIdleTimeChanged;
        _emoteVarMinSlider.ValueChanged += OnEmoteVarMinChanged;
        _emoteVarMaxSlider.ValueChanged += OnEmoteVarMaxChanged;
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
        _emoteIdleTimeLabel.Text = Localization.Localization.T("ui.settings.emote_idle_time", "Emote Idle Time");
        _emoteVarMinLabel.Text = Localization.Localization.T("ui.settings.emote_variation_min", "Variation Min");
        _emoteVarMaxLabel.Text = Localization.Localization.T("ui.settings.emote_variation_max", "Variation Max");
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

    private void OnDevModeToggled(bool toggledOn)
    {
        DevConsole.IsDevMode = toggledOn;
        _consoleButton.Visible = toggledOn;
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
        {
            gm.EmoteIdleTimeSeconds = v;
        }
    }

    private void OnEmoteVarMinChanged(double value)
    {
        float v = (float)value;
        var gm = GameManager.Instance;
        float varMax = gm?.EmoteIdleVariationMax ?? 1.3f;
        // Clamp: min ≤ max
        v = Mathf.Clamp(v, 0.1f, varMax);
        if (Mathf.Abs(v - (float)value) > 0.001f)
        {
            _emoteVarMinSlider.Value = v;
        }
        _emoteVarMinValueLabel.Text = $"×{v:F1}";
        if (gm != null) gm.EmoteIdleVariationMin = v;
    }

    private void OnEmoteVarMaxChanged(double value)
    {
        float v = (float)value;
        var gm = GameManager.Instance;
        float varMin = gm?.EmoteIdleVariationMin ?? 0.7f;
        // Clamp: max ≥ min
        v = Mathf.Clamp(v, varMin, 3.0f);
        if (Mathf.Abs(v - (float)value) > 0.001f)
        {
            _emoteVarMaxSlider.Value = v;
        }
        _emoteVarMaxValueLabel.Text = $"×{v:F1}";
        if (gm != null) gm.EmoteIdleVariationMax = v;
    }

    public override void _ExitTree()
    {
        Core.GameManager.Instance.LanguageChanged -= OnLanguageChanged;

        _languageOptionButton.ItemSelected -= OnLanguageSelected;
        _resolutionOptionButton.ItemSelected -= OnResolutionSelected;
        _windowModeOptionButton.ItemSelected -= OnWindowModeSelected;
        _backButton.Pressed -= OnBackPressed;
        _devModeToggle.Toggled -= OnDevModeToggled;
        _consoleButton.Pressed -= OnConsolePressed;
        _emoteIdleTimeSlider.ValueChanged -= OnEmoteIdleTimeChanged;
        _emoteVarMinSlider.ValueChanged -= OnEmoteVarMinChanged;
        _emoteVarMaxSlider.ValueChanged -= OnEmoteVarMaxChanged;
    }
}
