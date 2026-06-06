using System;
using System.Collections.Generic;
using Godot;

namespace OdysseyCards.Infrastructure;

/// <summary>
/// 热键管理器 — Autoload 单例。
///
/// 职责：将逻辑动作（OdysseyInput StringName）分发给注册的回调函数。
/// 使用栈式绑定：最后注册的回调优先执行（类似 STS2 NHotkeyManager）。
///
/// 三层架构中的第二层（逻辑→行为）：
///   InputManager._UnhandledKeyInput → HotkeyManager.DispatchAction → 已注册回调
///
/// 核心设计：
///   - Dictionary&lt;StringName, List&lt;Action&gt;&gt; 按下/释放绑定
///   - Push/Remove 模式：组件 _EnterTree 时注册，_ExitTree 时注销
///   - AddBlockingScreen：模态层打开时为所有动作注册空回调，阻止下层输入
///   - 自动守卫：DevConsole 可见时、LineEdit/TextEdit 聚焦时不触发
///   - 通过 CallDeferred 调用回调，避免在输入处理中修改场景树
///
/// 用法示例：
///   // 注册
///   HotkeyManager.Instance.PushPressedBinding(OdysseyInput.EndTurn, OnEndTurn);
///   // 注销（通常在 _ExitTree 或 Dispose 中）
///   HotkeyManager.Instance.RemovePressedBinding(OdysseyInput.EndTurn, OnEndTurn);
///   // 阻塞
///   HotkeyManager.Instance.AddBlockingScreen(this);
/// </summary>
public partial class HotkeyManager : Node
{
    // ===== 单例 =====

    public static HotkeyManager Instance { get; private set; } = null!;

    // ===== 状态 =====

    /// <summary>按下回调绑定（动作名 → 回调列表，Last 优先）。</summary>
    private readonly Dictionary<StringName, List<Action>> _pressedBindings = new();

    /// <summary>释放回调绑定（动作名 → 回调列表，Last 优先）。</summary>
    private readonly Dictionary<StringName, List<Action>> _releasedBindings = new();

    /// <summary>阻塞屏幕集合（Node → 空回调引用），用于 RemoveBlockingScreen 时移除。</summary>
    private readonly Dictionary<Node, Action> _blockingScreens = new();

    /// <summary>键盘焦点变化事件 — UI 层订阅以显示焦点指示器。</summary>
    public event Action<bool>? KeyboardFocusChanged;

    /// <summary>上次检测到键盘操作的时间（用于决定是否显示焦点指示器）。</summary>
    public ulong LastKeyboardActivityMsec { get; private set; }

    /// <summary>鼠标移动后隐藏焦点指示器的超时（毫秒）。</summary>
    private const ulong KeyboardFocusTimeoutMsec = 3000;

    // ===== Godot 生命周期 =====

    public override void _Ready()
    {
        Instance = this;
    }

    /// <summary>
    /// 由 InputManager 调用 — 将动作名分发给已注册的回调。
    /// 这是键盘→游戏逻辑的核心入口点。
    /// </summary>
    /// <param name="actionName">OdysseyInput 中定义的动作名</param>
    /// <param name="pressed">true=按下, false=释放</param>
    public void DispatchAction(StringName actionName, bool pressed)
    {
        // 守卫：窗口未聚焦
        if (!WindowHasFocus()) return;

        // 守卫：DevConsole 可见
        var devConsole = GetNodeOrNull<DevConsole>("/root/DevConsole");
        if (devConsole?.IsVisible == true) return;

        // 守卫：LineEdit 或 TextEdit 正在编辑
        var focusOwner = GetViewport()?.GuiGetFocusOwner();
        if (focusOwner is LineEdit lineEdit && lineEdit.HasFocus()) return;
        if (focusOwner is TextEdit textEdit && textEdit.HasFocus()) return;

        // 记录键盘活动时间（用于焦点指示器超时）
        LastKeyboardActivityMsec = Time.GetTicksMsec();

        if (pressed)
        {
            if (_pressedBindings.TryGetValue(actionName, out var bindings) && bindings.Count > 0)
            {
                var callback = bindings[^1]; // Last 优先（栈行为）
                Callable.From(callback).CallDeferred();
                KeyboardFocusChanged?.Invoke(true);
            }
        }
        else
        {
            if (_releasedBindings.TryGetValue(actionName, out var bindings) && bindings.Count > 0)
            {
                var callback = bindings[^1];
                Callable.From(callback).CallDeferred();
            }
        }
    }

    public override void _Process(double delta)
    {
        // 超时后隐藏焦点指示器
        if (LastKeyboardActivityMsec > 0
            && Time.GetTicksMsec() - LastKeyboardActivityMsec > KeyboardFocusTimeoutMsec)
        {
            LastKeyboardActivityMsec = 0;
            KeyboardFocusChanged?.Invoke(false);
        }
    }

    // ===== 按下绑定 =====

    /// <summary>注册按下热键回调（栈顶优先执行）。</summary>
    public void PushPressedBinding(StringName actionName, Action callback)
    {
        if (!_pressedBindings.ContainsKey(actionName))
            _pressedBindings[actionName] = new List<Action>();

        if (!_pressedBindings[actionName].Contains(callback))
            _pressedBindings[actionName].Add(callback);
    }

    /// <summary>注销按下热键回调。</summary>
    public void RemovePressedBinding(StringName actionName, Action callback)
    {
        if (_pressedBindings.TryGetValue(actionName, out var list))
        {
            list.Remove(callback);
            if (list.Count == 0)
                _pressedBindings.Remove(actionName);
        }
    }

    // ===== 释放绑定 =====

    /// <summary>注册释放热键回调（栈顶优先执行）。</summary>
    public void PushReleasedBinding(StringName actionName, Action callback)
    {
        if (!_releasedBindings.ContainsKey(actionName))
            _releasedBindings[actionName] = new List<Action>();

        if (!_releasedBindings[actionName].Contains(callback))
            _releasedBindings[actionName].Add(callback);
    }

    /// <summary>注销释放热键回调。</summary>
    public void RemoveReleasedBinding(StringName actionName, Action callback)
    {
        if (_releasedBindings.TryGetValue(actionName, out var list))
        {
            list.Remove(callback);
            if (list.Count == 0)
                _releasedBindings.Remove(actionName);
        }
    }

    // ===== 阻塞屏幕 =====

    /// <summary>
    /// 为模态层注册输入阻塞。
    /// 为 OdysseyInput.AllInputs 中的所有动作注册空回调，
    /// 有效阻止这些动作触发任何下层注册的回调。
    /// </summary>
    public void AddBlockingScreen(Node screen)
    {
        if (_blockingScreens.ContainsKey(screen)) return;

        // 为每个可阻塞动作创建空回调
        foreach (var actionName in OdysseyInput.AllInputs)
        {
            void BlockingCallback() { }
            PushPressedBinding(actionName, BlockingCallback);
        }

        _blockingScreens[screen] = null!;
    }

    /// <summary>
    /// 移除模态层的输入阻塞。
    /// 清除该屏幕注册的所有空回调。
    /// </summary>
    public void RemoveBlockingScreen(Node screen)
    {
        if (!_blockingScreens.ContainsKey(screen)) return;

        // 清除所有按下绑定中的空回调
        foreach (var actionName in OdysseyInput.AllInputs)
        {
            if (_pressedBindings.TryGetValue(actionName, out var list))
            {
                _pressedBindings.Remove(actionName);
            }
        }

        _blockingScreens.Remove(screen);
    }

    // ===== 工具方法 =====

    /// <summary>检查游戏窗口是否有焦点。</summary>
    private static bool WindowHasFocus()
    {
        var window = ((SceneTree)Engine.GetMainLoop()).Root;
        return window != null && window.HasFocus();
    }

    /// <summary>通知焦点指示器系统：键盘活动已发生。</summary>
    public void NotifyKeyboardActivity()
    {
        LastKeyboardActivityMsec = Time.GetTicksMsec();
        KeyboardFocusChanged?.Invoke(true);
    }
}
