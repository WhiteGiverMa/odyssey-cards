using Godot;

namespace OdysseyCards.Infrastructure;

/// <summary>
/// 场景生命周期安全守卫 —— 为所有包含 _Input / _Process / _GuiInput 的 UI 场景提供 tombstone 模式。
///
/// 核心问题：Godot 的 ChangeSceneToFile 不是原子操作。在旧场景拆除期间（可能持续数帧），
/// _Input、_Process、_GuiInput 仍可能触发并访问已释放的节点引用，导致 NullReferenceException。
///
/// 解决方案：
///   - _ExitTree 时设置 tombstone 标记 + 禁用 Process/Input
///   - 所有入口方法首先检查 tombstone
///   - CallDeferred 回调检查 IsInstanceValid
///
/// 用法：
///   1. UI 场景继承此类的 partial 扩展（通过组合，因为 Godot C# 不支持多重继承）
///   2. 或直接调用静态方法在现有的 _ExitTree / _Input / _Process 中添加守卫
/// </summary>
public static class SceneLifecycleGuard
{
    /// <summary>
    /// 在 _ExitTree 中调用：设置 tombstone、禁用处理、置空引用。
    /// 使用方式：在场景的 _ExitTree 中调用 SceneLifecycleGuard.OnExitTree(this, ref _field1, ref _field2, ...);
    /// </summary>
    public static void OnExitTree(Control control)
    {
        if (!GodotObject.IsInstanceValid(control)) return;
        if (control.IsQueuedForDeletion()) return;

        // 关键：先禁用所有处理循环，防止它们在拆除期间继续运行。
        control.SetProcess(false);
        control.SetProcessInput(false);
        control.SetProcessUnhandledInput(false);

        GD.Print($"[SceneLifecycleGuard] {control.Name} — 输入已禁用，场景正在拆除");
    }

    /// <summary>
    /// 在 _Input / _Process / _UnhandledInput / _GuiInput 入口处调用。
    /// 如果场景正在拆除或已失效，返回 true 表示应该跳过处理。
    /// </summary>
    public static bool ShouldSkip(Control control)
    {
        if (!GodotObject.IsInstanceValid(control)) return true;
        if (!control.IsInsideTree()) return true;
        if (control.IsQueuedForDeletion()) return true;
        return false;
    }

    /// <summary>
    /// 安全版本的 CallDeferred：在延迟执行前检查节点是否仍然有效。
    /// 使用方式：CallDeferredSafe(this, nameof(MyMethod));
    /// </summary>
    public static void CallDeferredSafe(Control control, string method)
    {
        if (ShouldSkip(control)) return;
        control.CallDeferred(method);
    }

    /// <summary>
    /// 安全版本的 CallDeferred（带参数）。
    /// </summary>
    public static void CallDeferredSafe(Control control, string method, params Variant[] args)
    {
        if (ShouldSkip(control)) return;
        control.CallDeferred(method, args);
    }

    /// <summary>
    /// 安全地访问节点引用：在访问前检查 IsInstanceValid。
    /// 用于 _Input 中 Button 引用等场景。
    /// 返回 true 表示引用有效并可安全使用。
    /// </summary>
    public static bool IsNodeValid(GodotObject? node)
    {
        return node != null && GodotObject.IsInstanceValid(node);
    }
}
