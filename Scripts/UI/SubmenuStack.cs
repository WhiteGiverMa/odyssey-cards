using System.Collections.Generic;
using Godot;

namespace OdysseyCards.UI;

/// <summary>
/// 轻量级子菜单栈 — 管理模态子页面的 Push/Pop 导航。
///
/// 对齐 STS2 NSubmenuStack 模式：Push 时隐藏旧页面，Pop 时恢复。
/// 用于暂停菜单的「设置」子页面等场景。
///
/// 用法：
///   _submenuStack.Push(settingsPage);
///   _submenuStack.Pop();  // 通常在子页面的返回按钮中调用
/// </summary>
public partial class SubmenuStack : Control
{
	private readonly Stack<Control> _stack = new();

	/// <summary>栈中是否有子页面。</summary>
	public bool HasSubmenus => _stack.Count > 0;

	/// <summary>当前子页面数量。</summary>
	public int Count => _stack.Count;

	public override void _Ready()
	{
		// 填满父容器
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Ignore; // 空白时透传，有子页面时由子页面拦截
	}

	/// <summary>
	/// 推入一个子页面到栈顶。隐藏旧页面，显示新页面。
	/// </summary>
	public void Push(Control screen)
	{
		if (_stack.Count > 0)
		{
			var prev = _stack.Peek();
			prev.Visible = false;
			prev.MouseFilter = MouseFilterEnum.Ignore;
		}

		_stack.Push(screen);
		screen.Visible = true;
		screen.MouseFilter = MouseFilterEnum.Stop;
		MouseFilter = MouseFilterEnum.Stop; // 有子页面时拦截输入
		AddChild(screen);
	}

	/// <summary>
	/// 弹出栈顶子页面。恢复前一页面（如有）。
	/// </summary>
	public void Pop()
	{
		if (_stack.Count == 0)
			return;

		var current = _stack.Pop();
		current.Visible = false;
		RemoveChild(current);

		if (_stack.Count > 0)
		{
			var prev = _stack.Peek();
			prev.Visible = true;
			prev.MouseFilter = MouseFilterEnum.Stop;
		}
		else
		{
			MouseFilter = MouseFilterEnum.Ignore; // 栈空时透传
		}
	}

	/// <summary>
	/// 清空栈并移除所有子页面。
	/// </summary>
	public void Clear()
	{
		while (_stack.Count > 0)
		{
			var screen = _stack.Pop();
			screen.Visible = false;
			RemoveChild(screen);
		}
		MouseFilter = MouseFilterEnum.Ignore;
	}
}
