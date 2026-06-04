using Godot;

namespace OdysseyCards.UI;

/// <summary>
/// 意图悬浮详情面板——悬停/长按意图图标时弹出，显示意图标题和完整描述（含伤害计算链）。
/// 不依赖任何游戏逻辑类，只接受原始字符串。自管理单例模式（静态 Show/HideCurrent）。
/// </summary>
public partial class IntentTooltip : Panel
{
	// ===== 静态管理 =====
	private static IntentTooltip? _current;

	// ===== 内部结构 =====
	private Label _titleLabel = null!;
	private Label _descLabel = null!;
	private Tween? _hideTween;

	// ===== 公共属性 =====
	/// <summary>拥有此 Tooltip 的意图图标引用，用于外部跟踪。</summary>
	public Control? OwnerIcon { get; set; }

	// ===== 静态工厂方法 =====

	/// <summary>
	/// 显示 IntentTooltip。自动隐藏旧的（如果存在），在指定 parent 下创建新实例。
	/// </summary>
	/// <param name="parent">父节点（Control / CanvasLayer）</param>
	/// <param name="position">意图图标的全局位置（屏幕坐标）</param>
	/// <param name="title">标题文本（可为 null/empty）</param>
	/// <param name="description">完整描述文本</param>
	/// <param name="isDebuff">是否为负面意图</param>
	/// <param name="accentColor">强调色</param>
	public static IntentTooltip Show(
		Control parent,
		Vector2 position,
		string title,
		string description,
		bool isDebuff,
		Color accentColor)
	{
		HideCurrent();

		var tooltip = new IntentTooltip(title, description, isDebuff, accentColor);
		parent.AddChild(tooltip);
		_current = tooltip;
		tooltip.ShowAt(position);
		return tooltip;
	}

	/// <summary>隐藏并销毁当前显示的 IntentTooltip。</summary>
	public static void HideCurrent()
	{
		if (_current is not null)
		{
			_current.HideTooltip();
			_current = null;
		}
	}

	// ===== 强调色映射 =====

	/// <summary>
	/// 根据意图类型 ID 返回对应的强调色。
	/// 可在外部使用，也可在此类内部用到 isDebuff 时将颜色覆写。
	/// </summary>
	/// <param name="intentTypeId">意图类型 ID（0=Attack, 1=MultiAttack, 2=Defend, ...）</param>
	public static Color GetAccentColor(int intentTypeId)
	{
		return intentTypeId switch
		{
			0 => new Color(1f, 0.15f, 0.15f),    // Attack - Red
			1 => new Color(1f, 0.2f, 0.1f),      // MultiAttack - Darker red
			2 => new Color(0.3f, 0.5f, 1f),      // Defend - Blue
			3 => new Color(0.2f, 0.9f, 0.3f),    // Buff - Green
			4 => new Color(0.7f, 0.2f, 1f),      // Debuff - Purple
			5 => new Color(0.3f, 0.9f, 0.4f),    // Heal - Green
			6 => new Color(1f, 0.85f, 0.1f),     // Summon - Yellow
			7 => new Color(0.5f, 0.7f, 1f),      // Sleep - Light blue
			8 => new Color(1f, 0.9f, 0.2f),      // Stun - Yellow
			9 => new Color(0.5f, 0.5f, 0.5f),    // Escape - Gray
			10 => new Color(0.2f, 0.8f, 0.9f),   // StatusCard - Cyan
			11 => new Color(0.4f, 0.4f, 0.4f),   // Unknown - Gray
			_ => new Color(0.6f, 0.6f, 0.6f),     // Default - Gray
		};
	}

	// ===== 构造函数 =====

	/// <summary>
	/// 创建 IntentTooltip 实例——初始化外观和子控件。
	/// </summary>
	/// <param name="title">标题文本</param>
	/// <param name="description">完整描述文本</param>
	/// <param name="isDebuff">是否为负面意图（追加红色标记）</param>
	/// <param name="accentColor">强调色（用于标题和边框）</param>
	public IntentTooltip(string title, string description, bool isDebuff, Color accentColor)
	{
		// ---- Panel 基础设置 ----
		CustomMinimumSize = new Vector2(180, 40);
		MouseFilter = MouseFilterEnum.Ignore;
		Visible = false;

		// ---- Panel 样式（StyleBoxFlat） ----
		var styleBox = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.08f, 0.12f, 0.92f),
			BorderColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.6f),
			BorderWidthLeft = 2,
			BorderWidthRight = 2,
			BorderWidthTop = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
			ContentMarginLeft = 10,
			ContentMarginTop = 6,
			ContentMarginRight = 10,
			ContentMarginBottom = 6,
		};
		AddThemeStyleboxOverride("panel", styleBox);

		// ---- 内容容器 ----
		var vbox = new VBoxContainer();

		// ---- 标题标签 ----
		_titleLabel = new Label();
		if (!string.IsNullOrEmpty(title))
		{
			_titleLabel.Text = title;
		}
		_titleLabel.AddThemeFontSizeOverride("font_size", 14);
		// 颜色逻辑：debuff 叠加红色，否则使用 accentColor
		Color titleColor = isDebuff
			? new Color(1f, 0.3f, 0.3f)
			: accentColor;
		_titleLabel.AddThemeColorOverride("font_color", titleColor);
		// 隐藏空标题标签
		_titleLabel.Visible = !string.IsNullOrEmpty(title);
		vbox.AddChild(_titleLabel);

		// ---- 描述标签 ----
		_descLabel = new Label
		{
			Text = description,
			AutowrapMode = TextServer.AutowrapMode.Word,
			CustomMinimumSize = new Vector2(250, 0),
		};
		_descLabel.AddThemeFontSizeOverride("font_size", 12);
		_descLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f));
		vbox.AddChild(_descLabel);

		AddChild(vbox);
	}

	// ===== 显示 / 定位 =====

	/// <summary>
	/// 在指定位置显示 Tooltip，智能避让屏幕边缘。
	/// </summary>
	/// <param name="screenPosition">意图图标的全局位置</param>
	/// <param name="ownerIcon">意图图标引用（可选，用于计算偏移）</param>
	public void ShowAt(Vector2 screenPosition, Control? ownerIcon = null)
	{
		if (ownerIcon is not null)
		{
			OwnerIcon = ownerIcon;
		}

		// 强制更新布局以获取 Size
		// （新加入场景树的 Control 可能尚未布局）
		if (GetTree() is not null)
		{
			// 等待一帧让布局生效后再定位
			CallDeferred(nameof(PositionAfterLayout), screenPosition, ownerIcon);
		}
		else
		{
			PositionAt(screenPosition, ownerIcon);
			PlayShowAnimation();
		}
	}

	/// <summary>布局就绪后的定位回调（Deferred）。</summary>
	private void PositionAfterLayout(Vector2 screenPosition, Control? ownerIcon)
	{
		PositionAt(screenPosition, ownerIcon);
		PlayShowAnimation();
	}

	/// <summary>执行智能定位逻辑。</summary>
	private void PositionAt(Vector2 screenPosition, Control? ownerIcon)
	{
		Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
		float viewportWidth = viewportSize.X;
		float viewportHeight = viewportSize.Y;

		Vector2 mySize = Size;
		float iconWidth = ownerIcon?.Size.X ?? 0f;
		const float margin = 8f;

		float posX;
		float posY = screenPosition.Y - mySize.Y * 0.5f;

		// 水平方向：根据相对屏幕位置决定左/右放置
		if (screenPosition.X < viewportWidth * 0.55f)
		{
			// 放在图标右侧
			posX = screenPosition.X + iconWidth + margin;
		}
		else
		{
			// 放在图标左侧
			posX = screenPosition.X - mySize.X - margin;
		}

		// 边界修正
		const float edgeMargin = 16f;

		// 下边界
		if (posY + mySize.Y > viewportHeight - edgeMargin)
			posY = viewportHeight - mySize.Y - edgeMargin;

		// 上边界
		if (posY < edgeMargin)
			posY = edgeMargin;

		// 左边界
		if (posX < margin)
			posX = margin;

		// 右边界
		if (posX + mySize.X > viewportWidth - margin)
			posX = viewportWidth - mySize.X - margin;

		Position = new Vector2(posX, posY);
	}

	/// <summary>播放淡入动画。</summary>
	private void PlayShowAnimation()
	{
		Modulate = new Color(1, 1, 1, 0);
		Visible = true;

		_hideTween?.Kill();
		var tween = CreateTween();
		tween.TweenProperty(this, "modulate", Colors.White, 0.15f)
		     .SetEase(Tween.EaseType.Out)
		     .SetTrans(Tween.TransitionType.Cubic);
	}

	// ===== 隐藏 / 销毁 =====

	/// <summary>隐藏 Tooltip——淡出后自动销毁。</summary>
	public void HideTooltip()
	{
		if (!IsInstanceValid(this)) return;

		// 如果从未显示，直接销毁
		if (!Visible)
		{
			QueueFree();
			return;
		}

		// 播放淡出动画
		_hideTween?.Kill();
		var tween = CreateTween();
		tween.TweenProperty(this, "modulate", new Color(1, 1, 1, 0), 0.1f)
		     .SetEase(Tween.EaseType.In)
		     .SetTrans(Tween.TransitionType.Cubic);
		tween.Finished += QueueFree;
		_hideTween = tween;
	}

	// ===== 生命周期 =====

	public override void _ExitTree()
	{
		// 如果当前实例是被静态管理器持有的，清除引用
		if (_current == this)
		{
			_current = null;
		}
		_hideTween?.Kill();
		base._ExitTree();
	}
}
