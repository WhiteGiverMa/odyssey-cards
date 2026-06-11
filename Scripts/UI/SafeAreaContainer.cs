using Godot;
using OdysseyCards.Infrastructure;

namespace OdysseyCards.UI;

/// <summary>
/// 安全区域容器 — 自动适配 Android 刘海屏、圆角、手势导航栏。
///
/// 审计发现的问题：
///   - CombatUI.BuildLayout() 使用硬编码 24/24/12/24 安全区偏移
///   - 没有使用 Godot 的 DisplayServer.GetDisplaySafeArea() API
///   - export_presets 中 edge_to_edge=false 导致系统栏占用布局空间
///
/// Day1 方案：
///   - 所有场景根节点包裹在 SafeAreaContainer 内
///   - 自动查询系统安全区并设置内容边距
///   - 移动端 edge_to_edge=true 时可正确处理刘海区域
/// </summary>
public partial class SafeAreaContainer : MarginContainer
{
	/// <summary>桌面端默认边距（安全区不可用时的回退）。</summary>
	private static readonly int[] DesktopMargins = { 0, 0, 0, 0 };

	/// <summary>移动端默认边距（安全区不可用时的回退，最小 24px）。</summary>
	private static readonly int[] MobileMargins = { 24, 24, 12, 24 }; // top, left, bottom, right

	public override void _Ready()
	{
		Name = "SafeAreaContainer";

		if (MobileInputRouter.IsMobile)
		{
			ApplyMobileSafeArea();
		}
		else
		{
			ApplyDesktopMargins();
		}

		// 窗口大小变化时重新计算
		GetTree().Root.SizeChanged += OnSizeChanged;
	}

	public override void _ExitTree()
	{
		if (IsInsideTree())
		{
			GetTree().Root.SizeChanged -= OnSizeChanged;
		}
	}

	private void OnSizeChanged()
	{
		if (MobileInputRouter.IsMobile)
		{
			ApplyMobileSafeArea();
		}
	}

	/// <summary>
	/// 查询系统安全区并应用为内容边距。
	/// </summary>
	private void ApplyMobileSafeArea()
	{
		var safeArea = DisplayServer.GetDisplaySafeArea();
		var screenSize = DisplayServer.ScreenGetSize();

		if (safeArea.Size.X <= 0 || safeArea.Size.Y <= 0)
		{
			// 安全区不可用 — 使用默认移动端边距
			GD.Print("[SafeAreaContainer] 安全区不可用，使用默认边距");
			AddThemeConstantOverride("margin_top", MobileMargins[0]);
			AddThemeConstantOverride("margin_left", MobileMargins[1]);
			AddThemeConstantOverride("margin_bottom", MobileMargins[2]);
			AddThemeConstantOverride("margin_right", MobileMargins[3]);
			return;
		}

		// 计算各方向需要的内边距
		int topMargin = safeArea.Position.Y;
		int leftMargin = safeArea.Position.X;
		int bottomMargin = screenSize.Y - safeArea.End.Y;
		int rightMargin = screenSize.X - safeArea.End.X;

		// 最小保障（防止计算错误导致内容紧贴边缘）
		topMargin = Mathf.Max(topMargin, 12);
		leftMargin = Mathf.Max(leftMargin, 12);
		bottomMargin = Mathf.Max(bottomMargin, 12);
		rightMargin = Mathf.Max(rightMargin, 12);

		AddThemeConstantOverride("margin_top", topMargin);
		AddThemeConstantOverride("margin_left", leftMargin);
		AddThemeConstantOverride("margin_bottom", bottomMargin);
		AddThemeConstantOverride("margin_right", rightMargin);

		GD.Print($"[SafeAreaContainer] 安全区: T={topMargin} L={leftMargin} B={bottomMargin} R={rightMargin}");
	}

	private void ApplyDesktopMargins()
	{
		AddThemeConstantOverride("margin_top", DesktopMargins[0]);
		AddThemeConstantOverride("margin_left", DesktopMargins[1]);
		AddThemeConstantOverride("margin_bottom", DesktopMargins[2]);
		AddThemeConstantOverride("margin_right", DesktopMargins[3]);
	}
}
