using Godot;

namespace OdysseyCards.UI;

/// <summary>
/// 浮动表情文本——VCD 字幕风格，带暗色半透明背景条，从指定位置缓缓上浮并淡出。
/// 参考 FloatingDamageNumber 的自包含 VFX 模式。
/// </summary>
public partial class FloatingEmote : Control
{
	// ===== 常量 =====
	private const float FloatDistance = 60f;          // 上浮距离（像素）
	private const float Duration = 4.0f;              // 动画时长
	private const int FontSize = 20;                  // 文字大小
	private const float BgAlpha = 0.65f;              // 背景透明度
	private const int BgPaddingH = 20;                // 背景水平内边距
	private const int BgPaddingV = 8;                 // 背景垂直内边距
	private const int CornerRadius = 4;               // 背景圆角
	private const float CharWidthEstimate = 0.7f;     // 每字符宽度估算系数

	private static readonly Color _textColor = new(0.95f, 0.95f, 0.95f);
	private static readonly Color _bgColor = new(0.05f, 0.05f, 0.08f, BgAlpha);

	// ===== 静态工厂 =====

	/// <summary>
	/// 在指定屏幕位置显示一段表情文本，动画结束后自动清理。
	/// </summary>
	/// <param name="text">表情文本</param>
	/// <param name="screenPosition">生成位置（屏幕坐标，文本水平居中于此点）</param>
	/// <param name="parent">父节点（应为 CanvasLayer 下的 Control）</param>
	public static void Show(string text, Vector2 screenPosition, Node parent)
	{
		if (string.IsNullOrEmpty(text))
			return;
		var emote = new FloatingEmote();
		emote.Initialize(text, screenPosition);
		parent.AddChild(emote);
	}

	// ===== 初始化 =====

	private void Initialize(string text, Vector2 screenPosition)
	{
		MouseFilter = MouseFilterEnum.Ignore;

		// 估算文本尺寸（中文字符按 FontSize*0.7 宽估算）
		float textW = text.Length * FontSize * CharWidthEstimate;
		float textH = FontSize + 4;
		float bgW = textW + BgPaddingH * 2;
		float bgH = textH + BgPaddingV * 2;

		// 自身尺寸
		Size = new Vector2(bgW, bgH);
		CustomMinimumSize = Size;

		// 位置：水平居中于屏幕坐标，起始在指定点上方偏移
		Position = new Vector2(screenPosition.X - bgW / 2, screenPosition.Y - bgH - 8);

		// 暗色圆角背景条
		var bgPanel = new Panel
		{
			MouseFilter = MouseFilterEnum.Ignore,
			Size = Size,
		};
		var style = new StyleBoxFlat
		{
			BgColor = _bgColor,
			CornerRadiusTopLeft = CornerRadius,
			CornerRadiusTopRight = CornerRadius,
			CornerRadiusBottomLeft = CornerRadius,
			CornerRadiusBottomRight = CornerRadius,
		};
		bgPanel.AddThemeStyleboxOverride("panel", style);
		AddChild(bgPanel);

		// 文本标签（居中于背景）
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Size = new Vector2(bgW, bgH),
			Position = Vector2.Zero,
		};
		label.AddThemeColorOverride("font_color", _textColor);
		label.AddThemeFontSizeOverride("font_size", FontSize);
		// 细描边增强可读性
		label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.4f));
		label.AddThemeConstantOverride("outline_size", 1);
		AddChild(label);

		// 开始动画
		Animate();
	}

	// ===== 动画 =====

	private void Animate()
	{
		var tween = CreateTween();
		tween.SetParallel(true);

		// 向上缓缓浮动
		tween.TweenProperty(this, "position:y", Position.Y - FloatDistance, Duration)
			 .SetEase(Tween.EaseType.Out)
			 .SetTrans(Tween.TransitionType.Sine);

		// 透明度 1.0 → 0.0（前 30% 完全可见，之后淡出）
		tween.TweenProperty(this, "modulate:a", 0.0f, Duration * 0.7f)
			 .SetDelay(Duration * 0.3f)
			 .SetEase(Tween.EaseType.In)
			 .SetTrans(Tween.TransitionType.Sine);

		// 初始微缩放弹入
		Scale = new Vector2(0.85f, 0.85f);
		var scaleTween = CreateTween();
		scaleTween.TweenProperty(this, "scale", Vector2.One, Duration * 0.3f)
				  .SetEase(Tween.EaseType.Out)
				  .SetTrans(Tween.TransitionType.Back);

		// 动画结束后自动销毁
		tween.Finished += QueueFree;
	}
}
