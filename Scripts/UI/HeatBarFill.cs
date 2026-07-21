#nullable enable
using Godot;

namespace OdysseyCards.UI;

/// <summary>
/// 热力条填充——自绘圆角槽 + 火焰渐变填充 + 过热红光脉动。
/// 由 HeatBar 驱动（SetHeat 0..3，1.0 = 100%）。
/// 质感：主色随温度迁移（青金→暖橙→绯红）+ 顶部高光带 + 底部暗带。
/// </summary>
public partial class HeatBarFill : Control
{
	/// <summary>热力计量程上限（与 HeatSystem 的 300% 对应）。</summary>
	private const float MaxHeat = 3f;

	private float _heat;
	private float _time;

	// 复用 StyleBoxFlat 绘制圆角矩形，避免手写圆角
	private static readonly StyleBoxFlat _boxStyle = new();

	public HeatBarFill()
	{
		CustomMinimumSize = new Vector2(100, 12);
		MouseFilter = MouseFilterEnum.Ignore;
	}

	/// <summary>设置当前热力值（0..3）。</summary>
	public void SetHeat(float heat)
	{
		_heat = heat;
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		_time += (float)delta;
		// 仅过热时需要持续重绘（红光脉动）
		if (_heat >= 1.2f)
		{
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		if (Size.X < 8f || Size.Y < 6f)
		{
			return;
		}

		float radius = Size.Y / 2f;
		var fullRect = new Rect2(Vector2.Zero, Size);

		// 槽底 + 槽描边
		DrawBox(fullRect, radius, new Color(0.07f, 0.06f, 0.13f, 0.9f));
		DrawRoundedBorder(fullRect, radius, new Color(0.35f, 0.3f, 0.5f, 0.6f));

		float pct = Mathf.Clamp(_heat / MaxHeat, 0f, 1f);
		if (pct <= 0.001f)
		{
			return;
		}

		// 填充宽度至少保留一个圆头
		float fillW = Mathf.Max(Size.X * pct, Size.Y);
		var fillRect = new Rect2(Vector2.Zero, new Vector2(fillW, Size.Y));

		// 温度主色（信息色：低温青金 / 正常暖橙 / 过热绯红）
		Color main = _heat < 0.4f
			? new Color(0.3f, 0.8f, 1.0f)
			: _heat < 1.2f
				? new Color(1.0f, 0.7f, 0.2f)
				: new Color(1.0f, 0.25f, 0.25f);

		// 过热时红光向白色脉动（能量即将溢出的警告感）
		if (_heat >= 1.2f)
		{
			float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 6f);
			main = main.Lerp(Colors.White, 0.18f * pulse);
		}

		// 火焰质感三层：主色底 + 顶部高光带 + 底部暗带
		DrawBox(fillRect, radius, main);
		var topBand = new Rect2(fillRect.Position, new Vector2(fillW, Size.Y * 0.42f));
		DrawBox(topBand, radius, new Color(1f, 1f, 1f, 0.22f));
		var bottomBand = new Rect2(fillRect.Position + new Vector2(0, Size.Y * 0.62f), new Vector2(fillW, Size.Y * 0.38f));
		DrawBox(bottomBand, radius, new Color(0f, 0f, 0f, 0.18f));
	}

	/// <summary>用共享 StyleBoxFlat 画一个圆角纯色矩形。</summary>
	private void DrawBox(Rect2 rect, float radius, Color color)
	{
		_boxStyle.BgColor = color;
		_boxStyle.CornerRadiusTopLeft = (int)radius;
		_boxStyle.CornerRadiusTopRight = (int)radius;
		_boxStyle.CornerRadiusBottomLeft = (int)radius;
		_boxStyle.CornerRadiusBottomRight = (int)radius;
		_boxStyle.BorderWidthLeft = 0;
		_boxStyle.BorderWidthRight = 0;
		_boxStyle.BorderWidthTop = 0;
		_boxStyle.BorderWidthBottom = 0;
		DrawStyleBox(_boxStyle, rect);
	}

	/// <summary>圆角描边（四段圆弧 + 四段直线近似）。</summary>
	private void DrawRoundedBorder(Rect2 rect, float radius, Color color)
	{
		// 简化：四角圆弧 + 四边直线
		DrawLine(rect.Position + new Vector2(radius, 0), rect.Position + new Vector2(rect.Size.X - radius, 0), color, 1f, true);
		DrawLine(rect.Position + new Vector2(radius, rect.Size.Y), rect.Position + new Vector2(rect.Size.X - radius, rect.Size.Y), color, 1f, true);
		DrawLine(rect.Position + new Vector2(0, radius), rect.Position + new Vector2(0, rect.Size.Y - radius), color, 1f, true);
		DrawLine(rect.Position + new Vector2(rect.Size.X, radius), rect.Position + new Vector2(rect.Size.X, rect.Size.Y - radius), color, 1f, true);

		DrawArc(rect.Position + new Vector2(radius, radius), radius, Mathf.Pi, Mathf.Pi * 1.5f, 8, color, 1f, true);
		DrawArc(rect.Position + new Vector2(rect.Size.X - radius, radius), radius, Mathf.Pi * 1.5f, Mathf.Tau, 8, color, 1f, true);
		DrawArc(rect.Position + new Vector2(rect.Size.X - radius, rect.Size.Y - radius), radius, 0, Mathf.Pi * 0.5f, 8, color, 1f, true);
		DrawArc(rect.Position + new Vector2(radius, rect.Size.Y - radius), radius, Mathf.Pi * 0.5f, Mathf.Pi, 8, color, 1f, true);
	}
}
