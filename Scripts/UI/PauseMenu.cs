using System;
using Godot;
using OdysseyCards.Core;
using OdysseyCards.Infrastructure;
using OdysseyCards.Localization;

namespace OdysseyCards.UI;

/// <summary>
/// 战斗中暂停界面——全屏覆盖层，包含继续/设置/保存退出/快速SL四个选项。
/// 设置子页面内嵌语言切换（OptionButton + GameManager.SetLanguage()），
/// 复用 SettingsPage 的程序化 UI 模式。
/// 纯代码创建，无 .tscn 依赖。
/// </summary>
public partial class PauseMenu : Control
{
    private const float MobileTouchHitPadding = 20f;

    // ===== 主菜单控件 =====

    private VBoxContainer _mainContainer = null!;
    private Label _titleLabel = null!;
    private Button _continueBtn = null!;
    private Button _settingsBtn = null!;
    private Button _saveExitBtn = null!;
    private Button _quickSlBtn = null!;

    // ===== 设置子页面控件 =====

    private VBoxContainer _settingsContainer = null!;
    private Label _settingsTitleLabel = null!;
    private Label _languageLabel = null!;
    private OptionButton _languageOptionButton = null!;
    private Label _resolutionLabel = null!;
    private OptionButton _resolutionOptionButton = null!;
    private Label _windowModeLabel = null!;
    private OptionButton _windowModeOptionButton = null!;
    private Button _settingsBackBtn = null!;

    // 开发者模式
    private CheckBox _devModeToggle = null!;
    private Button _consoleButton = null!;

    // 表情空闲时间
    private Label _emoteIdleTimeLabel = null!;
    private HSlider _emoteIdleTimeSlider = null!;
    private Label _emoteIdleTimeValueLabel = null!;
    private Label _emoteVarMinLabel = null!;
    private HSlider _emoteVarMinSlider = null!;
    private Label _emoteVarMinValueLabel = null!;
    private Label _emoteVarMaxLabel = null!;
    private HSlider _emoteVarMaxSlider = null!;
    private Label _emoteVarMaxValueLabel = null!;

    // ===== 事件 =====

    /// <summary>「继续」按钮点击——关闭暂停菜单。</summary>
    public event Action? OnContinue;

    /// <summary>「保存并退出」按钮点击——保存进度并返回主菜单。</summary>
    public event Action? OnSaveAndExit;

    /// <summary>「快速SL」按钮点击——重启当前战斗。</summary>
    public event Action? OnQuickSL;

    // ===== Godot 生命周期 =====

    public override void _Ready()
    {
        Name = "PauseMenu";

        // 暂停场景树时 PauseMenu 仍需处理输入（按钮、ESC等）
        ProcessMode = ProcessModeEnum.Always;

        // 全屏覆盖，阻止下层鼠标事件。
        // 关键：仅设 Anchor 不足够——程序化创建的 Control 默认 OffsetRight/OffsetBottom 非零，
        // 必须显式置零才能让控件真正填满父容器。
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        // 半透明暗色背景——填满 PauseMenu
        var bg = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.6f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        // 根容器——填满全屏 + 居中子元素
        var root = new CenterContainer();
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(root);

        BuildMainMenu();
        root.AddChild(_mainContainer);

        BuildSettings();
        _settingsContainer.Visible = false;
        root.AddChild(_settingsContainer);

        // 订阅语言变更
        GameManager.Instance.LanguageChanged += OnLanguageChanged;
    }

    public override void _ExitTree()
    {
        GameManager.Instance.LanguageChanged -= OnLanguageChanged;
    }

    // ===== 主菜单构建 =====

    private void BuildMainMenu()
    {
        _mainContainer = new VBoxContainer
        {
            Name = "PauseMainContainer",
            Alignment = BoxContainer.AlignmentMode.Center,
            CustomMinimumSize = new Vector2(320, 300),
        };
        _mainContainer.AddThemeConstantOverride("separation", 16);

        _titleLabel = new Label
        {
            Text = T("ui.pause.title", "暂停"),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", 36);
        _titleLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.6f));
        _titleLabel.CustomMinimumSize = new Vector2(0, 56);
        _mainContainer.AddChild(_titleLabel);

        _continueBtn = CreateMenuButton("ui.pause.continue_game", "继续", () => OnContinue?.Invoke());
        _mainContainer.AddChild(_continueBtn);

        _settingsBtn = CreateMenuButton("ui.pause.settings", "设置", ShowSettings);
        _mainContainer.AddChild(_settingsBtn);

        _saveExitBtn = CreateMenuButton("ui.pause.save_and_exit", "保存并退出", () => OnSaveAndExit?.Invoke());
        _mainContainer.AddChild(_saveExitBtn);

        _quickSlBtn = CreateMenuButton("ui.pause.quick_sl", "快速SL（重打这场战斗）", () => OnQuickSL?.Invoke());
        _mainContainer.AddChild(_quickSlBtn);
    }

    /// <summary>
    /// 创建统一样式的主菜单按钮。
    /// </summary>
    private Button CreateMenuButton(string key, string defaultText, Action onPressed)
    {
        var btn = new Button
        {
            Text = T(key, defaultText),
            CustomMinimumSize = new Vector2(280, 46),
        };
        btn.AddThemeFontSizeOverride("font_size", 18);
        btn.Pressed += onPressed;
        return btn;
    }

    // ===== 设置子页面构建 =====

    private void BuildSettings()
    {
        _settingsContainer = new VBoxContainer
        {
            Name = "PauseSettingsContainer",
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        _settingsContainer.AddThemeConstantOverride("separation", 20);

        // 标题
        _settingsTitleLabel = new Label
        {
            Text = T("ui.settings.title", "设置"),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _settingsTitleLabel.AddThemeFontSizeOverride("font_size", 36);
        _settingsTitleLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.6f));
        _settingsTitleLabel.CustomMinimumSize = new Vector2(0, 50);
        _settingsContainer.AddChild(_settingsTitleLabel);

        // 语言行
        _languageLabel = new Label
        {
            Text = T("ui.settings.language", "语言"),
        };
        _languageLabel.AddThemeFontSizeOverride("font_size", 20);
        _languageLabel.CustomMinimumSize = new Vector2(150, 0);

        _languageOptionButton = new OptionButton
        {
            CustomMinimumSize = new Vector2(200, 0),
        };
        LoadLanguages();

        var languageRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        languageRow.AddThemeConstantOverride("separation", 20);
        languageRow.AddChild(_languageLabel);
        languageRow.AddChild(_languageOptionButton);
        _settingsContainer.AddChild(languageRow);

        // 分辨率行
        _resolutionLabel = new Label
        {
            Text = T("ui.settings.resolution", "分辨率"),
        };
        _resolutionLabel.AddThemeFontSizeOverride("font_size", 20);
        _resolutionLabel.CustomMinimumSize = new Vector2(150, 0);

        _resolutionOptionButton = new OptionButton
        {
            CustomMinimumSize = new Vector2(200, 0),
        };
        LoadResolutions();

        var resolutionRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        resolutionRow.AddThemeConstantOverride("separation", 20);
        resolutionRow.AddChild(_resolutionLabel);
        resolutionRow.AddChild(_resolutionOptionButton);
        _settingsContainer.AddChild(resolutionRow);

        // 窗口模式行
        _windowModeLabel = new Label
        {
            Text = T("ui.settings.window_mode", "窗口模式"),
        };
        _windowModeLabel.AddThemeFontSizeOverride("font_size", 20);
        _windowModeLabel.CustomMinimumSize = new Vector2(150, 0);

        _windowModeOptionButton = new OptionButton
        {
            CustomMinimumSize = new Vector2(200, 0),
        };
        LoadWindowModes();

        var windowModeRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        windowModeRow.AddThemeConstantOverride("separation", 20);
        windowModeRow.AddChild(_windowModeLabel);
        windowModeRow.AddChild(_windowModeOptionButton);
        _settingsContainer.AddChild(windowModeRow);

        // 开发者模式行（移动端/桌面端均可用）
        _devModeToggle = new CheckBox
        {
            Text = T("ui.settings.dev_mode", "开发者模式"),
        };
        _devModeToggle.AddThemeFontSizeOverride("font_size", 20);
        _devModeToggle.Toggled += OnDevModeToggled;
        _settingsContainer.AddChild(_devModeToggle);

        // 控制台按钮（开发者模式启用时可见）
        _consoleButton = new Button
        {
            Text = T("ui.settings.open_console", "打开控制台"),
            CustomMinimumSize = new Vector2(200, 44),
            Visible = false,
        };
        _consoleButton.AddThemeFontSizeOverride("font_size", 16);
        _consoleButton.Pressed += OnConsolePressed;
        _settingsContainer.AddChild(_consoleButton);

        // 表情空闲时间行
        float currentIdleTime = GameManager.Instance.EmoteIdleTimeSeconds;

        _emoteIdleTimeLabel = new Label
        {
            Text = T("ui.settings.emote_idle_time", "表情空闲时间"),
        };
        _emoteIdleTimeLabel.AddThemeFontSizeOverride("font_size", 20);
        _emoteIdleTimeLabel.CustomMinimumSize = new Vector2(150, 0);

        _emoteIdleTimeSlider = new HSlider
        {
            MinValue = 3.0,
            MaxValue = 15.0,
            Step = 0.5,
            Value = currentIdleTime,
            CustomMinimumSize = new Vector2(160, 0),
        };

        _emoteIdleTimeValueLabel = new Label
        {
            Text = $"{currentIdleTime:F1}s",
            CustomMinimumSize = new Vector2(50, 0),
        };
        _emoteIdleTimeValueLabel.AddThemeFontSizeOverride("font_size", 16);

        var emoteIdleRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        emoteIdleRow.AddThemeConstantOverride("separation", 12);
        emoteIdleRow.AddChild(_emoteIdleTimeLabel);
        emoteIdleRow.AddChild(_emoteIdleTimeSlider);
        emoteIdleRow.AddChild(_emoteIdleTimeValueLabel);
        _settingsContainer.AddChild(emoteIdleRow);

        // 随机最小倍率行
        float currentVarMin = GameManager.Instance.EmoteIdleVariationMin;

        _emoteVarMinLabel = new Label
        {
            Text = T("ui.settings.emote_variation_min", "随机最小倍率"),
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

        var varMinRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        varMinRow.AddThemeConstantOverride("separation", 12);
        varMinRow.AddChild(_emoteVarMinLabel);
        varMinRow.AddChild(_emoteVarMinSlider);
        varMinRow.AddChild(_emoteVarMinValueLabel);
        _settingsContainer.AddChild(varMinRow);

        // 随机最大倍率行
        float currentVarMax = GameManager.Instance.EmoteIdleVariationMax;

        _emoteVarMaxLabel = new Label
        {
            Text = T("ui.settings.emote_variation_max", "随机最大倍率"),
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

        var varMaxRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        varMaxRow.AddThemeConstantOverride("separation", 12);
        varMaxRow.AddChild(_emoteVarMaxLabel);
        varMaxRow.AddChild(_emoteVarMaxSlider);
        varMaxRow.AddChild(_emoteVarMaxValueLabel);
        _settingsContainer.AddChild(varMaxRow);

        // 返回按钮
        _settingsBackBtn = new Button
        {
            Text = T("ui.settings.back", "返回"),
            CustomMinimumSize = new Vector2(140, 44),
        };
        _settingsBackBtn.AddThemeFontSizeOverride("font_size", 18);
        _settingsBackBtn.Pressed += HideSettings;
        _settingsContainer.AddChild(_settingsBackBtn);

        // 连接信号
        _languageOptionButton.ItemSelected += OnLanguageSelected;
        _resolutionOptionButton.ItemSelected += OnResolutionSelected;
        _windowModeOptionButton.ItemSelected += OnWindowModeSelected;
        _emoteIdleTimeSlider.ValueChanged += OnEmoteIdleTimeChanged;
        _emoteVarMinSlider.ValueChanged += OnEmoteVarMinChanged;
        _emoteVarMaxSlider.ValueChanged += OnEmoteVarMaxChanged;
    }

    // ===== 设置页面——数据加载 =====

    private void LoadLanguages()
    {
        _languageOptionButton.Clear();

        var languages = OdysseyCards.Localization.Localization.AvailableLanguages;
        var languageNames = new System.Collections.Generic.Dictionary<string, string>
        {
            { "en", T("language.name_en", "English") },
            { "zh", T("language.name_zh", "中文") }
        };

        int selectedIndex = 0;
        for (int i = 0; i < languages.Count; i++)
        {
            string lang = languages[i];
            string displayName = languageNames.TryGetValue(lang, out string? name) ? name : lang;
            _languageOptionButton.AddItem(displayName);
            _languageOptionButton.SetItemMetadata(i, lang);

            if (lang == OdysseyCards.Localization.Localization.CurrentLanguage)
                selectedIndex = i;
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

        _windowModeOptionButton.AddItem(T("ui.settings.windowed", "窗口"));
        _windowModeOptionButton.SetItemMetadata(0, 0);

        _windowModeOptionButton.AddItem(T("ui.settings.borderless", "无边框全屏"));
        _windowModeOptionButton.SetItemMetadata(1, 1);

        _windowModeOptionButton.AddItem(T("ui.settings.fullscreen", "全屏"));
        _windowModeOptionButton.SetItemMetadata(2, 2);

        _windowModeOptionButton.Selected = UIScaler.Instance.GetCurrentWindowModeIndex();
    }

    // ===== 设置页面——事件处理 =====

    private void OnLanguageSelected(long index)
    {
        var langVariant = _languageOptionButton.GetItemMetadata((int)index);
        string lang = langVariant.AsString();
        if (!string.IsNullOrEmpty(lang))
        {
            GameManager.Instance.SetLanguage(lang);
        }
    }

    private void OnResolutionSelected(long index)
    {
        var meta = _resolutionOptionButton.GetItemMetadata((int)index).AsString();
        var parts = meta.Split(',');
        if (parts.Length == 2 && int.TryParse(parts[0], out int width) && int.TryParse(parts[1], out int height))
        {
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
        LoadResolutions();
    }

    private void OnDevModeToggled(bool toggledOn)
    {
        DevConsole.IsDevMode = toggledOn;
        _consoleButton.Visible = DevConsole.IsDevMode;
    }

    private void OnConsolePressed()
    {
        // 关闭暂停菜单
        OnContinue?.Invoke();

        // 延迟一帧打开控制台，确保暂停菜单完全关闭
        CallDeferred(nameof(OpenDevConsole));
    }

    private void OnEmoteIdleTimeChanged(double value)
    {
        float v = (float)value;
        _emoteIdleTimeValueLabel.Text = $"{v:F1}s";
        GameManager.Instance.EmoteIdleTimeSeconds = v;
    }

    private void OnEmoteVarMinChanged(double value)
    {
        float v = (float)value;
        float varMax = GameManager.Instance.EmoteIdleVariationMax;
        v = Mathf.Clamp(v, 0.1f, varMax);
        if (Mathf.Abs(v - (float)value) > 0.001f) _emoteVarMinSlider.Value = v;
        _emoteVarMinValueLabel.Text = $"×{v:F1}";
        GameManager.Instance.EmoteIdleVariationMin = v;
    }

    private void OnEmoteVarMaxChanged(double value)
    {
        float v = (float)value;
        float varMin = GameManager.Instance.EmoteIdleVariationMin;
        v = Mathf.Clamp(v, varMin, 3.0f);
        if (Mathf.Abs(v - (float)value) > 0.001f) _emoteVarMaxSlider.Value = v;
        _emoteVarMaxValueLabel.Text = $"×{v:F1}";
        GameManager.Instance.EmoteIdleVariationMax = v;
    }

    private void OpenDevConsole()
    {
        GetNodeOrNull<DevConsole>("/root/DevConsole")?.Toggle();
    }

    // ===== 页面切换 =====

    private void ShowSettings()
    {
        _mainContainer.Visible = false;
        _settingsContainer.Visible = true;
        LoadLanguages();
        LoadResolutions();
        LoadWindowModes();
    }

    private void HideSettings()
    {
        _settingsContainer.Visible = false;
        _mainContainer.Visible = true;
    }

    // ===== 语言变更 =====

    private void OnLanguageChanged(string lang)
    {
        UpdateLabels();
    }

    private void UpdateLabels()
    {
        _titleLabel.Text = T("ui.pause.title", "暂停");
        _continueBtn.Text = T("ui.pause.continue_game", "继续");
        _settingsBtn.Text = T("ui.pause.settings", "设置");
        _saveExitBtn.Text = T("ui.pause.save_and_exit", "保存并退出");
        _quickSlBtn.Text = T("ui.pause.quick_sl", "快速SL（重打这场战斗）");

        _settingsTitleLabel.Text = T("ui.settings.title", "设置");
        _languageLabel.Text = T("ui.settings.language", "语言");
        _resolutionLabel.Text = T("ui.settings.resolution", "分辨率");
        _windowModeLabel.Text = T("ui.settings.window_mode", "窗口模式");
        _settingsBackBtn.Text = T("ui.settings.back", "返回");
        _devModeToggle.Text = T("ui.settings.dev_mode", "开发者模式");
        _consoleButton.Text = T("ui.settings.open_console", "打开控制台");
        _emoteIdleTimeLabel.Text = T("ui.settings.emote_idle_time", "表情空闲时间");
        _emoteVarMinLabel.Text = T("ui.settings.emote_variation_min", "随机最小倍率");
        _emoteVarMaxLabel.Text = T("ui.settings.emote_variation_max", "随机最大倍率");

        LoadWindowModes();
    }

    // ===== ESC 键处理 =====

    /// <summary>
    /// ESC 键在暂停覆盖层显示时自动关闭。
    /// 设置页可见时先返回到主暂停菜单，主菜单时触发 OnContinue 关闭整个暂停。
    /// </summary>
    public override void _Input(InputEvent @event)
    {
        if (SceneLifecycleGuard.ShouldSkip(this)) return;
        if (!IsInsideTree()) return;

        if (MobileInputHelper.IsMobile && @event is InputEventScreenTouch touch && touch.Pressed)
        {
            if (_settingsContainer.Visible)
            {
                if (HitTestControl(_settingsBackBtn, touch.Position))
                {
                    HideSettings();
                }
                else if (HitTestControl(_languageOptionButton, touch.Position))
                {
                    CycleOptionButton(_languageOptionButton, OnLanguageSelected);
                }
                else if (HitTestControl(_resolutionOptionButton, touch.Position))
                {
                    CycleOptionButton(_resolutionOptionButton, OnResolutionSelected);
                }
                else if (HitTestControl(_windowModeOptionButton, touch.Position))
                {
                    CycleOptionButton(_windowModeOptionButton, OnWindowModeSelected);
                }
                else if (HitTestControl(_devModeToggle, touch.Position))
                {
                    _devModeToggle.ButtonPressed = !_devModeToggle.ButtonPressed;
                    OnDevModeToggled(_devModeToggle.ButtonPressed);
                }
                else if (_consoleButton.Visible && HitTestControl(_consoleButton, touch.Position))
                {
                    OnConsolePressed();
                }
            }
            else
            {
                if (HitTestControl(_continueBtn, touch.Position))
                {
                    OnContinue?.Invoke();
                }
                else if (HitTestControl(_settingsBtn, touch.Position))
                {
                    ShowSettings();
                }
                else if (HitTestControl(_saveExitBtn, touch.Position))
                {
                    OnSaveAndExit?.Invoke();
                }
                else if (HitTestControl(_quickSlBtn, touch.Position))
                {
                    OnQuickSL?.Invoke();
                }
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
        {
            if (_settingsContainer.Visible)
            {
                HideSettings();
            }
            else
            {
                OnContinue?.Invoke();
            }
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

    private static void CycleOptionButton(OptionButton optionButton, System.Action<long> onSelected)
    {
        if (optionButton.ItemCount <= 0)
            return;

        int nextIndex = (optionButton.Selected + 1) % optionButton.ItemCount;
        optionButton.Selected = nextIndex;
        onSelected(nextIndex);
    }

    // ===== 便捷方法 =====

    private static string T(string key, string defaultText) =>
        OdysseyCards.Localization.Localization.T(key, defaultText);
}
