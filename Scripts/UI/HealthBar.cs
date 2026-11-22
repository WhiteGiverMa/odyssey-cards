using Godot;
using OdysseyCards.Character;

namespace OdysseyCards.UI;

/// <summary>
/// 生命值条组件。
/// 显示指挥官的当前生命值和最大生命值。
/// 星途底座：深空圆角槽 + 垂直渐变填充（健康绿 / 低血绯红警告）。
/// </summary>
public partial class HealthBar : ProgressBar
{
	private ICommander _target;
	public ICommander Target => _target;

	private Label _healthLabel;
	private UIScaler? _fontScaleSource;

	/// <summary>生命数字基础字号（实际字号 = 基础字号 × 界面字体倍率，并按血条高度封顶）。</summary>
	private const int HealthLabelBaseFontSize = 11;

	// 共享样式资源（渐变填充只读共享，避免每条生命条重复构建纹理）
	private static StyleBoxFlat _bgStyle;
	private static StyleBoxTexture _fillStyleHealthy;
	private static StyleBoxTexture _fillStyleCritical;

	public override void _Ready()
	{
		ShowPercentage = false; // 禁止 ProgressBar 自带百分比，避免与 HealthLabel 文字重叠
		ApplyStarlightStyle();

		_healthLabel = GetNodeOrNull<Label>("HealthLabel");

		// 如果场景中没有预设 HealthLabel 子节点，程序化创建一个
		if (_healthLabel == null)
		{
			_healthLabel = new Label
			{
				Name = "HealthLabel",
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				AnchorRight = 1.0f,
				AnchorBottom = 1.0f,
				OffsetLeft = 0,
				OffsetTop = 0,
				OffsetRight = 0,
				OffsetBottom = 0,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			_healthLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
			AddChild(_healthLabel);
		}

		ApplyFontScale();

		_fontScaleSource = UIScaler.Instance;
		if (_fontScaleSource != null)
			_fontScaleSource.OnFontScaleChanged += ApplyFontScale;
	}

	public override void _ExitTree()
	{
		if (_fontScaleSource != null)
			_fontScaleSource.OnFontScaleChanged -= ApplyFontScale;
		_fontScaleSource = null;
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized && GodotObject.IsInstanceValid(_healthLabel))
			ApplyFontScale();
	}

	/// <summary>生命数字跟随界面字体倍率，但不超过血条自身高度的可读上限（敌人 8px 细条不溢出）。</summary>
	public void ApplyFontScale()
	{
		if (_healthLabel == null || !GodotObject.IsInstanceValid(_healthLabel))
			return;
		int maxForHeight = Mathf.Max(HealthLabelBaseFontSize, Mathf.RoundToInt(Size.Y * 0.9f));
		_healthLabel.AddThemeFontSizeOverride("font_size",
			Mathf.Min(UIScaler.ApplyFontScale(HealthLabelBaseFontSize), maxForHeight));
	}

	/// <summary>
	/// 应用星途底座样式——深空圆角槽 + 垂直渐变填充。
	/// 渐变填充为 StyleBoxTexture（拉伸到填充宽度），顶亮底暗营造厚度感。
	/// </summary>
	private void ApplyStarlightStyle()
	{
		_bgStyle ??= new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.06f, 0.12f, 0.95f),
			BorderColor = new Color(0.24f, 0.21f, 0.38f),
			BorderWidthLeft = 1,
			BorderWidthRight = 1,
			BorderWidthTop = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomLeft = 2,
			CornerRadiusBottomRight = 2,
		};
		_fillStyleHealthy ??= CreateGradientFill(new Color(0.48f, 0.9f, 0.55f), new Color(0.18f, 0.56f, 0.26f));
		_fillStyleCritical ??= CreateGradientFill(new Color(1f, 0.58f, 0.5f), new Color(0.72f, 0.2f, 0.22f));

		AddThemeStyleboxOverride("background", _bgStyle);
		AddThemeStyleboxOverride("fill", _fillStyleHealthy);
	}

	/// <summary>创建垂直渐变填充样式（顶亮 → 底暗；StyleBoxTexture 无圆角，微圆角由背景槽承担）。</summary>
	private static StyleBoxTexture CreateGradientFill(Color top, Color bottom)
	{
		var gradient = new Gradient();
		gradient.SetColor(0, top);
		gradient.SetColor(1, bottom);
		var texture = new GradientTexture2D
		{
			Gradient = gradient,
			Fill = GradientTexture2D.FillEnum.Linear,
			FillFrom = Vector2.Zero,
			FillTo = new Vector2(0, 1),
			Width = 8,
			Height = 16,
		};
		return new StyleBoxTexture { Texture = texture };
	}

	public void UpdateHealth(int current, int max)
	{
		MaxValue = max;
		Value = current;

		// 低血量警告：填充切绯红渐变 + 文字暖金
		bool critical = max > 0 && (float)current / max < 0.3f;
		if (_fillStyleHealthy != null && _fillStyleCritical != null)
		{
			AddThemeStyleboxOverride("fill", critical ? _fillStyleCritical : _fillStyleHealthy);
		}
		if (_healthLabel != null)
		{
			_healthLabel.AddThemeColorOverride("font_color", critical ? new Color(1f, 0.85f, 0.55f) : Colors.White);
		}

		// 程序化创建时可能还没有进入场景树，_Ready 尚未执行，兜底创建标签
		if (_healthLabel == null)
		{
			ShowPercentage = false; // 禁止 ProgressBar 自带百分比，避免与 HealthLabel 文字重叠
			_healthLabel = new Label
			{
				Name = "HealthLabel",
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				AnchorRight = 1.0f,
				AnchorBottom = 1.0f,
				OffsetLeft = 0,
				OffsetTop = 0,
				OffsetRight = 0,
				OffsetBottom = 0,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			_healthLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
			AddChild(_healthLabel);
			ApplyFontScale();
		}

		_healthLabel.Text = $"{current}/{max}";
	}

	public void SetTarget(ICommander target)
	{
		_target = target;
		if (_target != null)
		{
			UpdateHealth(_target.CurrentHealth, _target.MaxHealth);
		}
	}
}
