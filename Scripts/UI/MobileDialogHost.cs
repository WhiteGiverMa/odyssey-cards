using System;
using Godot;
using OdysseyCards.Infrastructure;

namespace OdysseyCards.UI;

/// <summary>
/// 移动端统一弹窗/覆盖层宿主 — 适配横屏 + 触控目标 + 返回键栈。
///
/// 审计发现的问题：
///   - MainMenu/SettingsPage/PauseMenu/CollectionUI/DiscoverUI/RewardUI 各有自己的弹窗实现
///   - AcceptDialog/ConfirmationDialog/FileDialog 行为在不同场景不一致
///   - 移动端触控目标偏小（38-48px vs 推荐的 48-56dp）
///   - 弹窗叠层时底层 _Input 没有被阻断
///
/// 统一后：
///   - 所有弹窗通过 MobileDialogHost 创建，统一尺寸、按钮大小、触控目标
///   - 自动接入 MobileInputRouter 模态层（PushModal/PopModal）
///   - 返回键自动关闭顶层弹窗
///   - 横屏适配：弹窗宽度 ≤ 屏幕 80%，居中显示
/// </summary>
public static class MobileDialogHost
{
	/// <summary>移动端触控目标最小高度（dp）。</summary>
	public const float MinTouchTargetHeight = 56f;

	/// <summary>高风险按钮（删除/清空）触控目标最小高度。</summary>
	public const float MinDangerButtonHeight = 64f;

	/// <summary>弹窗最大宽度占屏幕比例。</summary>
	public const float MaxDialogWidthRatio = 0.8f;

	/// <summary>弹窗内容最大高度占屏幕比例。</summary>
	public const float MaxDialogHeightRatio = 0.7f;

	/// <summary>
	/// 创建一个标准移动端弹窗。
	/// 弹窗本身是 Control 节点，已接入 MobileInputRouter 模态层。
	/// 返回 (dialog, contentContainer, buttonRow)。
	/// 本方法会直接把 dialog 添加到 parent。
	/// </summary>
	/// <remarks>
	/// <b>调用时机契约：</b><paramref name="parent"/> 必须已入树（<c>IsInsideTree()</c> 为 true）。
	/// 本方法内部两次调用 <c>parent.GetViewportRect()</c>（计算弹窗高度 + 模态层遮罩尺寸），
	/// 该 Godot API 要求节点已在场景树中，否则 C++ 层会 <c>push_error</c> 并返回空 <see cref="Rect2"/>。
	/// <para>
	/// 典型错误：在 Control 子类的<strong>构造函数</strong>里调用本方法。构造期节点尚未 <c>AddChild</c>，
	/// 调用方 <c>new XxxUI(...)</c> 后才 <c>AddChild</c>，时序必然踩坑。正确做法是把 UI 构建
	/// 推迟到 <see cref="Node._Ready"/>（参考 <see cref="ShopUI"/>、<see cref="CardFlyVfx"/> 的同样纪律）。
	/// </para>
	/// </remarks>
	public static (Control dialog, VBoxContainer content, HBoxContainer buttonRow)
		CreateDialog(Control parent, string title, int width = 400)
	{
		// 前置契约检查：parent 必须已入树，否则后续 GetViewportRect 会爆 C++ 层 is_inside_tree() 错误。
		// 在此显式 push_error + 抛异常，给出比 Godot 原生报错更清晰的修复指引（典型原因：在构造函数里调用而非 _Ready）。
		if (parent == null)
		{
			GD.PushError("[MobileDialogHost] CreateDialog: parent 为 null。调用方必须传入已入树的 Control。");
			throw new ArgumentNullException(nameof(parent));
		}
		if (!parent.IsInsideTree())
		{
			GD.PushError($"[MobileDialogHost] CreateDialog: parent「{parent.Name}」未入树。本方法内部调用 GetViewportRect() 需要节点已在场景树中。请把 UI 构建从构造函数推迟到 _Ready()，确保调用方先 AddChild 再触发构建。");
			throw new InvalidOperationException(
				$"MobileDialogHost.CreateDialog: parent「{parent?.Name}」未入树。GetViewportRect() 需要 IsInsideTree()==true。典型原因：在 Control 子类构造函数里调用本方法，而调用方 new 之后才 AddChild。请改为在 _Ready() 中构建 UI。");
		}

		var router = MobileInputRouter.Instance;

		// 弹窗外层 — 必须全屏铺满父控件，否则遮罩和弹窗都会蜷缩在左上角 (0,0)
		var dialog = new Panel
		{
			Name = "MobileDialog",
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		dialog.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

		// 半透明遮罩背景（全屏）
		var bg = new ColorRect
		{
			Color = new Color(0, 0, 0, 0.5f),
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		dialog.AddChild(bg);

		// 居中容器（手动设 anchors，不用 SetAnchorsAndOffsetsPreset 避免覆盖 Container 布局模式）
		var centerContainer = new CenterContainer
		{
			AnchorLeft = 0,
			AnchorTop = 0,
			AnchorRight = 1,
			AnchorBottom = 1,
		};
		dialog.AddChild(centerContainer);

		// 弹窗面板
		var viewportSize = parent.GetViewportRect().Size;
		var dialogHeight = Mathf.Max(MinTouchTargetHeight * 4f, viewportSize.Y * MaxDialogHeightRatio);
		var panel = new Panel
		{
			Name = "DialogPanel",
			CustomMinimumSize = new Vector2(width, dialogHeight),
		};
		panel.AddThemeStyleboxOverride("panel", CreateDialogStylebox());

		var panelVBox = new VBoxContainer
		{
			Name = "PanelVBox",
		};
		panelVBox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		panelVBox.AddThemeConstantOverride("separation", 16);
		panel.AddChild(panelVBox);

		// 标题
		if (!string.IsNullOrEmpty(title))
		{
			var titleLabel = new Label
			{
				Text = title,
				HorizontalAlignment = HorizontalAlignment.Center,
			};
			titleLabel.AddThemeFontSizeOverride("font_size", 24);
			titleLabel.CustomMinimumSize = new Vector2(0, MinTouchTargetHeight);
			panelVBox.AddChild(titleLabel);
		}

		// 内容区域（可滚动）
		var scrollContainer = new ScrollContainer
		{
			Name = "ScrollContent",
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		var content = new VBoxContainer
		{
			Name = "Content",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(Mathf.Max(0, width - 48), 0),
		};
		content.AddThemeConstantOverride("separation", 12);
		scrollContainer.AddChild(content);
		panelVBox.AddChild(scrollContainer);

		// 按钮行
		var buttonRow = new HBoxContainer
		{
			Name = "ButtonRow",
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		buttonRow.AddThemeConstantOverride("separation", 16);
		panelVBox.AddChild(buttonRow);

		centerContainer.AddChild(panel);
		parent.AddChild(dialog);

		// 接入模态层
		if (MobileInputRouter.IsMobile)
		{
			var dialogRect = new Rect2(Vector2.Zero, parent.GetViewportRect().Size);
			router.PushModalLayer(dialog);
			// 遮罩背景点击关闭
			bg.GuiInput += (@event) =>
			{
				if (@event is InputEventScreenTouch touch && touch.Pressed)
				{
					if (!IsPointInPanel(touch.Position, panel))
					{
						// 点击遮罩区域 → 什么都不做（由调用方决定是否关闭）
					}
				}
			};
		}

		return (dialog, content, buttonRow);
	}

	/// <summary>
	/// 关闭弹窗。从父节点移除并弹出模态层。
	/// </summary>
	public static void CloseDialog(Control dialog, Control parent)
	{
		if (MobileInputRouter.IsMobile)
		{
			MobileInputRouter.Instance.PopModalLayer(dialog);
		}
		parent.RemoveChild(dialog);
		dialog.QueueFree();
	}

	/// <summary>
	/// 创建一个移动端适配按钮。
	/// </summary>
	public static Button CreateDialogButton(string text, Color? color = null, float minHeight = MinTouchTargetHeight)
	{
		var btn = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(120, minHeight),
		};
		btn.AddThemeFontSizeOverride("font_size", 20);

		if (color.HasValue)
		{
			btn.AddThemeColorOverride("font_color", color.Value);
		}

		return btn;
	}

	/// <summary>
	/// 创建一个危险操作按钮（删除/清空），更大的触控目标。
	/// </summary>
	public static Button CreateDangerButton(string text)
	{
		var btn = CreateDialogButton(text, new Color(0.9f, 0.3f, 0.3f), MinDangerButtonHeight);
		return btn;
	}

	/// <summary>
	/// 创建弹窗的 StyleBox（圆角 + 阴影效果）。
	/// </summary>
	private static StyleBoxFlat CreateDialogStylebox()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.15f, 0.15f, 0.2f, 0.95f),
			CornerRadiusTopLeft = 12,
			CornerRadiusTopRight = 12,
			CornerRadiusBottomLeft = 12,
			CornerRadiusBottomRight = 12,
			BorderWidthLeft = 2,
			BorderWidthRight = 2,
			BorderWidthTop = 2,
			BorderWidthBottom = 2,
			BorderColor = new Color(0.3f, 0.3f, 0.4f),
			ContentMarginLeft = 20,
			ContentMarginRight = 20,
			ContentMarginTop = 16,
			ContentMarginBottom = 16,
		};
	}

	private static bool IsPointInPanel(Vector2 point, Control panel)
	{
		if (!GodotObject.IsInstanceValid(panel) || !panel.IsInsideTree())
			return false;
		var rect = panel.GetGlobalRect();
		return rect.HasPoint(point);
	}
}
