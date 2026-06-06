using System;
using System.Collections.Generic;
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

    // ===== 子菜单栈（共享 SettingsPage）=====

    private SubmenuStack _submenuStack = null!;

    // ===== 键盘导航 =====

    /// <summary>当前键盘焦点所在的按钮索引（-1 = 无焦点）。</summary>
    private int _focusedButtonIndex = -1;

    /// <summary>当前受键盘高亮的按钮引用（用于清除视觉）。</summary>
    private Button? _keyboardFocusedButton;

    /// <summary>HotkeyManager 回调引用（用于注销）。</summary>
    private Action? _upAction;
    private Action? _downAction;
    private Action? _acceptAction;
    private Action? _cancelAction;

    /// <summary>当前页面的可聚焦按钮列表。</summary>
    private List<Button> _mainMenuButtonList = null!;

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

        // 子菜单栈（位于主菜单之上，用于 Push SettingsPage 等子页面）
        _submenuStack = new SubmenuStack { Name = "SubmenuStack" };
        AddChild(_submenuStack);

        // 订阅语言变更
        GameManager.Instance.LanguageChanged += OnLanguageChanged;

        // 注册键盘热键绑定
        RegisterHotkeyBindings();
    }

    public override void _ExitTree()
    {
        GameManager.Instance.LanguageChanged -= OnLanguageChanged;
        UnregisterHotkeyBindings();
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

        // 构建主菜单按钮导航列表（用于键盘 Up/Down）
        _mainMenuButtonList = new List<Button>
        {
            _continueBtn, _settingsBtn, _saveExitBtn, _quickSlBtn,
        };
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

    // ===== 页面切换 =====

    private void ShowSettings()
    {
        _mainContainer.Visible = false;
        ClearKeyboardFocusVisual();

        var settingsPage = new SettingsPage();
        settingsPage.OnBack = () =>
        {
            _submenuStack.Pop();
            _mainContainer.Visible = true;
        };

        _submenuStack.Push(settingsPage);
    }

    private void HideSettings()
    {
        if (_submenuStack.HasSubmenus)
            _submenuStack.Pop();
        _mainContainer.Visible = true;
    }

    // ===== 键盘导航 — HotkeyManager 绑定 =====

    /// <summary>
    /// 注册键盘热键绑定到 HotkeyManager。
    /// Up/Down 循环切换焦点按钮，Enter 激活，Escape 关闭暂停菜单。
    /// 与 _Input 中的 ESC 处理并行工作（_Input 先触发，HotkeyManager 延迟一帧回调）。
    /// </summary>
    private void RegisterHotkeyBindings()
    {
        var hm = HotkeyManager.Instance;
        if (hm == null) return;

        _upAction = CycleButtonFocusUp;
        _downAction = CycleButtonFocusDown;
        _acceptAction = ActivateFocusedButton;
        _cancelAction = HotkeyCancelPause;

        hm.PushPressedBinding(OdysseyInput.Up, _upAction);
        hm.PushPressedBinding(OdysseyInput.Down, _downAction);
        hm.PushPressedBinding(OdysseyInput.Accept, _acceptAction);
        hm.PushPressedBinding(OdysseyInput.Cancel, _cancelAction);

        // 监听键盘焦点超时事件——超时后清除焦点指示器
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

        if (_upAction != null) { hm.RemovePressedBinding(OdysseyInput.Up, _upAction); _upAction = null; }
        if (_downAction != null) { hm.RemovePressedBinding(OdysseyInput.Down, _downAction); _downAction = null; }
        if (_acceptAction != null) { hm.RemovePressedBinding(OdysseyInput.Accept, _acceptAction); _acceptAction = null; }
        if (_cancelAction != null) { hm.RemovePressedBinding(OdysseyInput.Cancel, _cancelAction); _cancelAction = null; }
    }

    /// <summary>
    /// 获取当前可见页面的按钮列表。
    /// </summary>
    private List<Button> GetCurrentButtonList()
    {
        return _mainMenuButtonList;
    }

    /// <summary>
    /// Up 键：将焦点移动到上一个按钮（循环）。
    /// </summary>
    private void CycleButtonFocusUp()
    {
        var buttons = GetCurrentButtonList();
        if (buttons.Count == 0) return;

        if (_focusedButtonIndex < 0 || _focusedButtonIndex >= buttons.Count)
            _focusedButtonIndex = buttons.Count - 1;
        else
            _focusedButtonIndex = (_focusedButtonIndex - 1 + buttons.Count) % buttons.Count;

        ApplyKeyboardFocusVisual();
    }

    /// <summary>
    /// Down 键：将焦点移动到下一个按钮（循环）。
    /// </summary>
    private void CycleButtonFocusDown()
    {
        var buttons = GetCurrentButtonList();
        if (buttons.Count == 0) return;

        if (_focusedButtonIndex < 0 || _focusedButtonIndex >= buttons.Count)
            _focusedButtonIndex = 0;
        else
            _focusedButtonIndex = (_focusedButtonIndex + 1) % buttons.Count;

        ApplyKeyboardFocusVisual();
    }

    /// <summary>
    /// Enter 键：激活当前焦点所在的按钮。
    /// </summary>
    private void ActivateFocusedButton()
    {
        var buttons = GetCurrentButtonList();
        if (buttons.Count == 0) return;

        if (_focusedButtonIndex < 0 || _focusedButtonIndex >= buttons.Count)
            _focusedButtonIndex = 0;

        var btn = buttons[_focusedButtonIndex];
        if (btn == null || !GodotObject.IsInstanceValid(btn)) return;

        // 通过 EmitSignal 触发按钮的 Pressed 信号，复用已有的事件处理逻辑
        btn.EmitSignal(BaseButton.SignalName.Pressed);
    }

    /// <summary>
    /// Escape 键（HotkeyManager 回调）：关闭设置子页面或退出暂停。
    /// 与 _Input 中的 ESC 处理并行——_Input 先触发，此回调作为延迟冗余保证。
    /// </summary>
    private void HotkeyCancelPause()
    {
        if (!IsInsideTree()) return;

        if (_submenuStack.HasSubmenus)
        {
            HideSettings();
            // 切换到主菜单后重置焦点
            _focusedButtonIndex = -1;
            ClearKeyboardFocusVisual();
        }
        else
        {
            OnContinue?.Invoke();
        }
    }

    /// <summary>
    /// HotkeyManager 键盘焦点超时回调。
    /// 键盘闲置 3 秒后自动清除焦点指示器。
    /// </summary>
    private void OnKeyboardFocusChanged(bool active)
    {
        if (!active)
        {
            _focusedButtonIndex = -1;
            ClearKeyboardFocusVisual();
        }
    }

    /// <summary>
    /// 给当前键盘焦点的按钮施加蓝色调 SelfModulate 指示器。
    /// 清除之前焦点的视觉。
    /// 仅当 HotkeyManager 记录到近期键盘活动时才显示。
    /// </summary>
    private void ApplyKeyboardFocusVisual()
    {
        bool shouldShowFocus = HotkeyManager.Instance.LastKeyboardActivityMsec > 0;

        // 先清除旧焦点视觉
        ClearKeyboardFocusVisual();

        if (!shouldShowFocus) return;

        var buttons = GetCurrentButtonList();
        if (_focusedButtonIndex < 0 || _focusedButtonIndex >= buttons.Count) return;

        var btn = buttons[_focusedButtonIndex];
        if (btn == null || !GodotObject.IsInstanceValid(btn)) return;

        // 蓝色调指示器（通过 SelfModulate 叠加）
        btn.SelfModulate = new Color(0.72f, 0.85f, 1f, 1f);
        _keyboardFocusedButton = btn;
    }

    /// <summary>
    /// 清除当前按钮的键盘焦点视觉。
    /// </summary>
    private void ClearKeyboardFocusVisual()
    {
        if (_keyboardFocusedButton != null)
        {
            if (GodotObject.IsInstanceValid(_keyboardFocusedButton))
                _keyboardFocusedButton.SelfModulate = Colors.White;
            _keyboardFocusedButton = null;
        }
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
            // 设置子页面由 SettingsPage 自身处理输入，PauseMenu 只处理主菜单按钮
            if (_submenuStack.HasSubmenus)
            {
                GetViewport().SetInputAsHandled();
                return;
            }

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

            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
        {
            if (_submenuStack.HasSubmenus)
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
